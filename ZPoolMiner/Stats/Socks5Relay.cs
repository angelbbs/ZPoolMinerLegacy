using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using System.Net;
using System.Net.Security;
using System.Diagnostics;
using System.ComponentModel;
using System.Net.NetworkInformation;
using ZPoolMinerLegacy.Stats;
using ZPoolMiner.Configs;
using ZPoolMinerLegacy.Overclock;
using Newtonsoft.Json;

namespace ZPoolMiner.Stats
{
    public static class Socks5Relay
    {
        public static List<RelayConnection> RelayConnectionList = new();
        public class RelayConnection
        {
            public int threadId { get; set; }
            public int pID { get; set; }
            public int LocalPort { get; set; }
            public string Pool { get; set; }
            public int PoolPort { get; set; }
        }
        public static volatile int RelayPort = 13600;
        public static volatile TcpListener Listener = new TcpListenerEx(IPAddress.Any, RelayPort);
        const int BufferSize = 4096;
        public static int ThreadsCount = 0;

        public static void Socks5RelayStart()
        {
            //return;//отключим пока, т.к. на стороне сервера частая ошибка 95 (например, lolminer не подключается к equihash144)
            if (Socks5Relay.Listener.Server.IsBound) return;
            while (CheckRelayPort(RelayPort))
            {
                RelayPort++;
                Thread.Sleep(100);
            }
            Helpers.ConsolePrint("Socks5Relay", "Start relay 127.0.0.1:" + RelayPort + 
                " -> " + Stats.CurrentProxy.HostName + ":" + Stats.CurrentProxy.Socks5Port.ToString());
            ConfigManager.GeneralConfig.RelayPort = RelayPort;
            try
            {
                Listener.Server.Dispose();
                Listener = new TcpListener(IPAddress.Any, RelayPort);
                Listener.Start();
                new Task(() =>
                {
                    while (true)
                    {
                        try
                        {
                            var minerClient = Listener.AcceptTcpClient();
                            if (minerClient.Connected)
                            {
                                ThreadsCount++;
                                Helpers.ConsolePrint("Socks5Relay", "Miner connected to relay 127.0.0.1:" + RelayPort +
                                    " Proxy: " + Stats.CurrentProxy.HostName + ":" + Stats.CurrentProxy.Socks5Port.ToString() + " " +
                                    "ThreadsCount: " + ThreadsCount.ToString());
                                /*
                                if (ThreadsCount >= 100)
                                {
                                    Helpers.ConsolePrint("Socks5RelayStart", "Many relay errors. Restart program");
                                    Form_Main.MakeRestart(10);
                                }
                                */
                                /*
                                Thread _AcceptConnection = new Thread(() =>
                                {
                                    AcceptConnection(minerClient);
                                });
                                _AcceptConnection.Start();
                                */
                                new Task(() => AcceptConnection(minerClient)).Start();
                            }
                        } catch (Exception ex)
                        {
                            Helpers.ConsolePrintError("Socks5Relay", ex.Message);
                            break;
                        }
                    }
                }).Start();
            } catch (Exception ex)
            {
                Helpers.ConsolePrintError("Socks5Relay", ex.ToString());
            }
        }

        private static void AcceptConnection(TcpClient minerClient)
        {
            try
            {
                var minerStream = minerClient.GetStream();
                var proxy = new TcpClient(Stats.CurrentProxy.HostName, Stats.CurrentProxy.Socks5Port);

                var sock = proxy.Client;
                sock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

                var sockClient = minerClient.Client;
                sockClient.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

                NetworkStream proxyStream = proxy.GetStream();
                
                Thread _ReadFromMiner = new Thread(() =>
                {
                    ReadFromMiner(minerClient, minerStream, proxyStream);
                });
                _ReadFromMiner.Start();
                
                Thread _ReadFromProxy = new Thread(() =>
                {
                    ReadFromProxy(minerClient, proxyStream, minerStream);
                });
                _ReadFromProxy.Start();
                
                //new Task(() => ReadFromMiner(minerClient, minerStream, proxyStream)).Start();
                //new Task(() => ReadFromProxy(minerClient,proxyStream, minerStream)).Start();
            }
            catch (Exception ex)
            {
                Helpers.ConsolePrintError("Socks5Relay", ex.ToString());
                if (minerClient is object && minerClient != null)
                {
                    minerClient.Close();
                    minerClient.Dispose();
                }
                ProxyCheck.ProxyRotate();
            }
        }

        private static void ReadFromProxy(TcpClient minerClient, Stream proxyStream, Stream minerStream)
        {
            var message = new byte[BufferSize];

            while (true)
            {
                int serverBytes = 0;
                try
                {
                    serverBytes = proxyStream.Read(message, 0, BufferSize);
                    /*
                    if (message[0] == 5 && message[1] == 2 && message[2] == 0 && message[3] == 1 & serverBytes < 20)
                    {
                        Helpers.ConsolePrintError("ReadFromProxy", "Declined");
                        proxyStream.Close();
                    }
                    */
                    minerStream.Write(message, 0, serverBytes);
                }
                catch (Exception ex)
                {
                    Helpers.ConsolePrintError("ReadFromProxy", "Disconnect from proxy " +
                                    Stats.CurrentProxy.HostName + ":" + Stats.CurrentProxy.Socks5Port.ToString());
                    break;
                }
                if (serverBytes == 0)
                {
                    break;
                }
            }

            //ThreadsCount--;
            if (minerStream is object && minerStream != null)
            {
                minerStream.Close();
                minerStream.Dispose();
            }
            if (minerClient is object && minerClient != null)
            {
                minerClient.Client.Close();
                minerClient.Client.Dispose();
            }
        }

        private static void ReadFromMiner(TcpClient minerClient, Stream minerStream, Stream proxyStream)
        {
            int threadId = 0;
            int count = 0;

            while (true)
            {
                int minerBytes = 0;
                string targetHost = "";
                ushort targetPort = 0;
                string _targetPort = "";
                bool bug = false;
                var message = new byte[BufferSize];
                try
                {
                    if (!minerStream.CanRead)
                    {
                        Helpers.ConsolePrintError("ReadFromMiner", "Miner stream cannot be read. Exiting loop.");
                        break;
                    }

                    minerBytes = minerStream.Read(message, 0, BufferSize);

                    if (message[0] == 5 && message[1] == 1 && message[2] == 0)
                    {
                        byte atyp = message[3];
                        // Смещение данных начинается сразу после ATYP
                        int dataOffset = 4;

                        try
                        {
                            if (atyp == 0x01) // Стандартный IPv4 (4 байта)
                            {
                                bug = true; // Т.к. на стороне прокси Стандартный IPv4 не всегда работает
                                targetHost = $"{message[dataOffset]}.{message[dataOffset + 1]}.{message[dataOffset + 2]}.{message[dataOffset + 3]}";
                                targetPort = (ushort)(message[dataOffset + 4] << 8 | message[dataOffset + 5]);
                            }
                            else if (atyp == 0x03) // Domain Name или Майнер-баг
                            {
                                int lenOrFirstOctet = message[dataOffset];

                                // ПРОВЕРКА НА ТЕКСТОВЫЙ IP (Майнер-баг):
                                // Длина текста "255.255.255.255" = 15 символов.
                                // Если первый байт > 3 (октет не может быть больше 255, но здесь мы ловим именно строку),
                                // и при этом общая длина пакета совпадает с длиной строки.
                                if (lenOrFirstOctet > 3 && minerBytes >= dataOffset + 1 + lenOrFirstOctet + 2)
                                {
                                    string potentialString = Encoding.ASCII.GetString(message, dataOffset + 1, lenOrFirstOctet);

                                    // Проверяем, похож ли остаток на IP адрес через Split
                                    var parts = potentialString.Split('.');

                                    bool isTextIP = parts.Length == 4 &&
                                                    int.TryParse(parts[0], out _) &&
                                                    int.TryParse(parts[1], out _) &&
                                                    int.TryParse(parts[2], out _) &&
                                                    int.TryParse(parts[3], out _);
                                    //bool isTextIP = text.Any(char.IsLetter);
                                    if (isTextIP)
                                    {
                                        bug = true;
                                        targetHost = potentialString;

                                        // Порт всё равно находится СТРОГО ПОСЛЕ строки
                                        targetPort = (ushort)(message[dataOffset + 1 + lenOrFirstOctet] << 8 |
                                                              message[dataOffset + 1 + lenOrFirstOctet + 1]);
                                        //Helpers.ConsolePrint("Socks5Relay", $"Detected TEXTUAL IP from miner bug: {potentialString}:{targetPort}");
                                    }
                                }

                                // Если это НЕ текстовый IP, обрабатываем как нормальный домен
                                int domainLen = message[dataOffset];
                                targetHost = Encoding.ASCII.GetString(message, dataOffset + 1, domainLen);
                                targetPort = (ushort)(message[dataOffset + 1 + domainLen] << 8 | message[dataOffset + 1 + domainLen + 1]);
                                //Helpers.ConsolePrint("Socks5Relay", $"Detected host from miner: {targetHost}:{targetPort}");
                            }
                            else
                            {
                                //Helpers.ConsolePrintError("ReadFromMiner", $"Unsupported ATYP: {atyp}. Dropping connection.");
                                //break;
                            }
                        }
                        catch (Exception ex)
                        {
                            Helpers.ConsolePrintError("ReadFromMiner-Parse", $"Failed to parse packet: {ex.ToString()}");
                            break;
                        }

                        if (bug)
                        {
                            var newPool = "bug.mine.zpool.ca";//0x11
                            var newMessage = "\x05\x01\x00\x03" +
                                "\x11" + newPool +
                                (char)(targetPort >> 8) + (char)(targetPort & 0xFF);
                            var byt = new byte[newMessage.Length];
                            for (int i = 0; i < newMessage.Length; i++)
                            {
                                byt[i] = Convert.ToByte(newMessage[i]);
                            }

                            message = byt;
                            minerBytes = newMessage.Length;

                            //var connectPacket = BuildConnectCommand(targetHost, targetPort);
                            //message = connectPacket;
                        }
                        Random r = new Random();
                        var _id = r.Next(1, 65534);
                        threadId = AppDomain.GetCurrentThreadId() +
                                Thread.CurrentThread.ManagedThreadId + _id;
                        
                        var pool = ParsePacketPoolPort(message);
                        try
                        {
                            if (pool.Contains(":"))
                            {
                                string _pool = pool.Split(':')[0];
                                int _port = 0;
                                int.TryParse(pool.Split(':')[1], out _port);
                                if (_pool.Contains("zpool.ca") || _pool.Contains("188.165.24.209") || _pool.Contains("198.50.168.213"))
                                {
                                    Helpers.ConsolePrint("Socks5Relay", "Miner connected througt proxy to: " +
                                        _pool + ":" + _port.ToString());
                                }
                                var pc = AddProxyConnectionProcessID();
                                pc.threadId = threadId;
                                if (pc.pID > 0)
                                {
                                    pc.Pool = _pool;
                                    pc.PoolPort = _port;
                                    lock (RelayConnectionList)
                                    {
                                        RelayConnectionList.Add(pc);
                                        try
                                        {
                                            NativeOverclock.GetMinerData(JsonConvert.SerializeObject(RelayConnectionList));
                                        }
                                        catch (Exception ex)
                                        {
                                            Helpers.ConsolePrintError("ReadFromMiner", ex.ToString());
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Helpers.ConsolePrintError("ReadFromMiner", ex.ToString());
                        }
                        
                    }
                    if (message[0] == 5 && message[1] == 1 && message[2] == 0 && message[3] == 1)
                    {
                        ParsePacketPort(message, minerStream, proxyStream);
                    }
                    if (minerBytes == 0)
                    {
                        //Helpers.ConsolePrintError("ReadFromMiner", "minerBytes == 0");
                        //break;
                    }
                    if (bug)
                    {
                        proxyStream.Write(message, 0, message.Length);
                    }
                    else
                    {
                        proxyStream.Write(message, 0, minerBytes);
                    }
                }
                catch (Exception ex)
                {
                    //Helpers.ConsolePrintError("ReadFromMiner", ex.ToString());
                    break;
                }
            }

            ThreadsCount--;
            lock (RelayConnectionList)
            {
                RelayConnectionList.RemoveAll(a => a.threadId == threadId);
                try
                {
                    NativeOverclock.GetMinerData(JsonConvert.SerializeObject(RelayConnectionList));
                }
                catch (Exception ex)
                {
                    Helpers.ConsolePrintError("ReadFromMiner", ex.ToString());
                }
            }
            if (proxyStream is object && proxyStream != null)
            {
                proxyStream.Close();
                proxyStream.Dispose();
            }
            if (minerClient is object && minerClient != null)
            {
                minerClient.Client.Close();
                minerClient.Client.Dispose();
            }
        }
        private static byte[] BuildConnectCommand(string host, int port)
        {
            var data = new List<byte> { 5, 1, 0 }; // VER, CMD=CONNECT(1), RSV

            if (IPAddress.TryParse(host, out IPAddress ip))
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    data.Add(1); // ATYP = IPv4
                    data.AddRange(ip.GetAddressBytes());
                }
                else
                {
                    data.Add(4); // ATYP = IPv6
                    data.AddRange(ip.GetAddressBytes());
                }
            }
            else
            {
                byte[] domainBytes = Encoding.ASCII.GetBytes(host);
                data.Add(3); // ATYP = DOMAINNAME
                data.Add((byte)domainBytes.Length);
                data.AddRange(domainBytes);
            }

            data.Add((byte)(port >> 8));   // DST.PORT MSB
            data.Add((byte)(port & 0xFF)); // DST.PORT LSB
            return data.ToArray();
        }
        public static RelayConnection AddProxyConnectionProcessID()
        {
            RelayConnection rc = new();
            try
            {
                List<ZPoolMinerLegacy.Stats.Connection> _allConnections = new List<ZPoolMinerLegacy.Stats.Connection>();
                _allConnections.Clear();
                _allConnections.AddRange(ZPoolMinerLegacy.Stats.NetworkInformation.GetTcpV4Connections());

                for (int i = 1; i < _allConnections.Count; i++)
                {
                    if (RelayPort == _allConnections[i].RemoteEndPoint.Port)
                    {
                        lock (RelayConnectionList)
                        {
                            int index = RelayConnectionList.FindIndex(a => a.LocalPort == RelayPort);
                            if (index < 0)
                            {
                                rc.LocalPort = _allConnections[i].LocalEndPoint.Port;
                                rc.pID = (int)_allConnections[i].OwningPid;
                                //Helpers.ConsolePrint("CheckRelayPort", 
                                //   "OwningProcess: " + _allConnections[i].OwningProcess + " " +
                                // "OwningPid: " + _allConnections[i].OwningPid.ToString() + " " +
                                //"LocalPort: " + _allConnections[i].LocalEndPoint.Port.ToString());
                            }
                        }
                    }
                }
                ZPoolMinerLegacy.Stats.Connection.UpdateProcessList();
                _allConnections.Clear();
                _allConnections = null;

                return rc;
            }
            catch (Exception e)
            {
                Helpers.ConsolePrintError("CheckRelayPort", e.ToString());
                Thread.Sleep(500);
            }
            finally
            {

            }
            return rc;
        }

        private static string ParsePacketPoolPort(byte[] message)
        {
            string ret = "";
            var pool = new byte[128];
            try
            {
                int length = message[4];
                Array.ConstrainedCopy(message, 5, pool, 0, length);
                var _pool = Encoding.ASCII.GetString(pool).Split((char)0)[0];
                int port1 = BitConverter.ToInt32(new byte[] { message[length + 5], 0, 0, 0 }, 0);
                int port2 = BitConverter.ToInt32(new byte[] { message[length + 6], 0, 0, 0 }, 0);
                //equihash144.eu.mine.zpool.ca
                //188.165.24.209
                //Helpers.ConsolePrint("Socks5Relay", "Miner connected througt proxy to: "
                  //+ _pool + ":" + (port1 * 256 + port2).ToString());
                ret = _pool + ":" + (port1 * 256 + port2).ToString();
            } catch (Exception ex)
            {

            }
            return ret;
        }
        private static void ParsePacketPort(byte[] message, Stream minerStream, Stream proxyStream)
        {
            int pool1 = BitConverter.ToInt32(new byte[] { message[4], 0, 0, 0 }, 0);
            int pool2 = BitConverter.ToInt32(new byte[] { message[5], 0, 0, 0 }, 0);
            int pool3 = BitConverter.ToInt32(new byte[] { message[6], 0, 0, 0 }, 0);
            int pool4 = BitConverter.ToInt32(new byte[] { message[7], 0, 0, 0 }, 0);

            int port1 = BitConverter.ToInt32(new byte[] { message[8], 0, 0, 0 }, 0);
            int port2 = BitConverter.ToInt32(new byte[] { message[9], 0, 0, 0 }, 0);
            /*
            Helpers.ConsolePrint("Socks5Relay", "Miner connected througt proxy to: " +
                pool1.ToString() + "." + pool2.ToString() + "." +
                pool3.ToString() + "." + pool4.ToString() + ":" +
                (port1 * 256 + port2).ToString() + " port");
            */
            if (port1+port2 == 0)
            {
                Helpers.ConsolePrint("Socks5Relay", "Miner connected througt proxy to zero port! Disconnecting");
                try
                {
                    proxyStream.Close();
                    minerStream.Close();
                } catch (Exception ex)
                {

                }
            }
        }
        public static bool CheckRelayPort(int Port)
        {
            try
            {
                List<ZPoolMinerLegacy.Stats.Connection> _allConnections = new List<ZPoolMinerLegacy.Stats.Connection>();
                _allConnections.Clear();
                _allConnections.AddRange(ZPoolMinerLegacy.Stats.NetworkInformation.GetTcpV4Connections());

                for (int i = 1; i < _allConnections.Count; i++)
                {
                    /*
                    Helpers.ConsolePrintError("CheckRelayPort", _allConnections[i].LocalEndPoint.Port.ToString() + " " +
                        _allConnections[i].RemoteEndPoint.Port.ToString() + " " +
                        _allConnections[i].OwningProcess);
                    */
                    if (Port == _allConnections[i].LocalEndPoint.Port ||
                        Port == _allConnections[i].RemoteEndPoint.Port)
                    {
                        var id = _allConnections[i].OwningPid;
                        Helpers.ConsolePrintError("CheckRelayPort", "Relay port in use by " + _allConnections[i].OwningProcess);
                        return true;
                    }
                }
                ZPoolMinerLegacy.Stats.Connection.UpdateProcessList();
                _allConnections.Clear();
                _allConnections = null;

                return false;
            }
            catch (Exception e)
            {
                Helpers.ConsolePrintError("CheckRelayPort", e.ToString());
                Thread.Sleep(500);
            }
            finally
            {

            }
            return false;
        }
    }
   
}
