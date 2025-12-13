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
            if (Socks5Relay.Listener.Server.IsBound) return;
            while (CheckRelayPort(RelayPort))
            {
                RelayPort++;
                Thread.Sleep(100);
            }
            Helpers.ConsolePrint("Socks5Relay", "Start relay 127.0.0.1:" + RelayPort + " -> " + Stats.CurrentProxyIP + ":" + Stats.CurrentProxySocks5SPort.ToString());
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
                                    " Proxy: " + Stats.CurrentProxyIP + ":" + Stats.CurrentProxySocks5SPort.ToString() + " " +
                                    "ThreadsCount: " + ThreadsCount.ToString());
                                /*
                                if (ThreadsCount >= 100)
                                {
                                    Helpers.ConsolePrint("Socks5RelayStart", "Many relay errors. Restart program");
                                    Form_Main.MakeRestart(10);
                                }
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
                var proxy = new TcpClient(Stats.CurrentProxyIP, Stats.CurrentProxySocks5SPort);

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
                    minerStream.Write(message, 0, serverBytes);
                }
                catch (Exception ex)
                {
                    Helpers.ConsolePrintError("ReadFromProxy", ex.ToString());
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
            var message = new byte[BufferSize];
            var _message = new byte[BufferSize];
            int count = 0;

            while (true)
            {
                count++;
                int minerBytes = 0;
                try
                {
                    minerBytes = minerStream.Read(message, 0, BufferSize);

                    /*
                    if (ThreadsCount > 10 && ConfigManager.GeneralConfig.EnableProxy)
                    {
                        if (message[0] == 5 && message[1] == 1 && message[2] == 0 && message[3] == 3)
                        {
                            Helpers.ConsolePrint("ReadFromMiner", 
                                "Try external relay connection to " + ParsePacketPoolPort(message));
                            var _pool = Encoding.Default.GetString(message);
                            _pool = _pool.Replace("eu.mine.zpool.ca",
                                                  "stratum-proxy.ru");
                            _pool = _pool.Replace("na.mine.zpool.ca",
                                                  "stratum-proxy.ru");
                            _message = Encoding.Default.GetBytes(_pool);
                            proxyStream.Write(_message, 0, minerBytes);
                        }
                        else
                        {
                            proxyStream.Write(message, 0, minerBytes);
                        }
                    } else
                    {
                        proxyStream.Write(message, 0, minerBytes);
                    }
                    */
                    proxyStream.Write(message, 0, minerBytes);
                }
                catch (Exception ex)
                {
                    //Helpers.ConsolePrintError("ReadFromMiner", ex.ToString());
                    break;
                }
                if (minerBytes == 0)
                {
                    break;
                }

                if (message[0] == 5 && message[1] == 1 && message[2] == 0 && message[3] == 3)
                {
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
                            if (_pool.Contains("zpool.ca"))
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
                    } catch (Exception ex)
                    {
                        Helpers.ConsolePrintError("ReadFromMiner", ex.ToString());
                    }
                }
                if (message[0] == 5 && message[1] == 1 && message[2] == 0 && message[3] == 1)
                {
                    ParsePacketPort(message, minerStream, proxyStream);
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

        public static RelayConnection AddProxyConnectionProcessID()
        {
            RelayConnection rc = new();
            try
            {
                List<ZPoolMinerLegacy.Stats.Connection> _allConnections = new List<ZPoolMinerLegacy.Stats.Connection>();
                _allConnections.Clear();
                _allConnections.AddRange(ZPoolMinerLegacy.Stats.NetworkInformation.GetTcpV4Connections());
                ZPoolMinerLegacy.Stats.Connection.UpdateProcessList();

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

                //Helpers.ConsolePrint("Socks5Relay", "Miner connected througt proxy to: "
                //  + _pool + ":" + (port1 * 256 + port2).ToString());
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
                ZPoolMinerLegacy.Stats.Connection.UpdateProcessList();

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
