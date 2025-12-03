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

namespace ZPoolMiner.Stats
{
    public static class Socks5Relay
    {
        public static int Port = 13600;
        public static volatile TcpListener Listener = new TcpListenerEx(IPAddress.Any, Port);
        const int BufferSize = 4096;
        public static int ThreadsCount = 0;

        public static void Socks5RelayStart()
        {
            if (Socks5Relay.Listener.Server.IsBound) return;
            while (CheckRelayPort(Port))
            {
                Port++;
                Thread.Sleep(100);
            }
            Helpers.ConsolePrint("Socks5Relay", "Start relay 127.0.0.1:" + Port + " -> " + Stats.CurrentProxyIP + ":" + Stats.CurrentProxySocks5SPort.ToString());
            ConfigManager.GeneralConfig.RelayPort = Port;
            try
            {
                Listener.Server.Dispose();
                Listener = new TcpListener(IPAddress.Any, Port);
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
                                Helpers.ConsolePrint("Socks5Relay", "Miner connected to relay 127.0.0.1:" + Port +
                                    " Proxy: " + Stats.CurrentProxyIP + ":" + Stats.CurrentProxySocks5SPort.ToString() + " " +
                                    "ThreadsCount: " + ThreadsCount.ToString());
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
                NetworkStream proxyStream = proxy.GetStream();
                new Task(() => ReadFromMiner(minerClient, minerStream, proxyStream)).Start();
                new Task(() => ReadFromProxy(minerClient,proxyStream, minerStream)).Start();
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
                    //Helpers.ConsolePrintError("ReadFromProxy", ex.ToString());
                    break;
                }
                if (serverBytes == 0)
                {
                    break;
                }
            }

            if (minerStream is object && minerStream != null)
            {
                minerStream.Close();
                minerStream.Dispose();
            }
            if (minerClient is object && minerClient != null)
            {
                minerClient.Client.Close();
                minerClient.Client.Dispose();
                ThreadsCount--;
            }
        }

        private static void ReadFromMiner(TcpClient minerClient, Stream minerStream, Stream proxyStream)
        {
            var message = new byte[BufferSize];
            int count = 0;
            while (true)
            {
                count++;
                int minerBytes = 0;
                try
                {
                    minerBytes = minerStream.Read(message, 0, BufferSize);
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
                    //ParsePacketPoolPort(message);
                }
                if (message[0] == 5 && message[1] == 1 && message[2] == 0 && message[3] == 1)
                {
                    ParsePacketPort(message, minerStream, proxyStream);
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

        private static void ParsePacketPoolPort(byte[] message)
        {
            var pool = new byte[128];
            int length = message[4];
            Array.ConstrainedCopy(message, 5, pool, 0, length);
            var _pool = Encoding.ASCII.GetString(pool).Split((char)0)[0]; 
            int port1 = BitConverter.ToInt32(new byte[] { message[length + 5], 0, 0, 0 }, 0);
            int port2 = BitConverter.ToInt32(new byte[] { message[length + 6], 0, 0, 0 }, 0);

            Helpers.ConsolePrint("Socks5Relay", "Miner connected througt proxy to: "
                + _pool + ":" + (port1 * 256 + port2).ToString());
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
                List<Connection> _allConnections = new List<Connection>();
                _allConnections.Clear();
                _allConnections.AddRange(NetworkInformation.GetTcpV4Connections());
                Connection.UpdateProcessList();

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
