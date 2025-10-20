using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Threading;
using System.Diagnostics;
using System.Net.Sockets;

namespace ZPoolMiner.Stats
{
    public class ProxyChecker
    {
        public class Proxy
        {
            public string Ip { get; set; }
            public int HTTPSPort { get; set; }
            public int Socks5Port { get; set; }
            public bool sslValid { get; set; } = false;
            public bool tcpValid { get; set; } = false;
            public int sslSpeed { get; set; }
            public int tcpSpeed { get; set; }
            public bool allValid { get; set; }
        }

        public static Proxy CheckProxies(string ip, int timeout = 1000)
        {
            Proxy checkedProxy = new();
            var httpsport = 13150;
            var socks5port = 13155;

            using (var socket = new System.Net.Sockets.Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                bool success = false;
                
                socket.ReceiveTimeout = timeout;
                socket.SendTimeout = timeout;
                Proxy pl = new();
                try
                {
                    new Thread(() =>
                    {
                        Thread.Sleep(timeout);
                        if (!success)
                        {
                            socket.Close();
                        }
                    }).Start();

                    var watch = Stopwatch.StartNew();
                    Helpers.ConsolePrint("CheckProxies", "Try connect to https " + ip + ":" + httpsport.ToString());
                    socket.Connect(ip, httpsport);

                    if (socket.Connected)
                    {
                        success = true;
                        checkedProxy.Ip = ip;
                        checkedProxy.HTTPSPort = httpsport;
                        checkedProxy.tcpSpeed = (int)watch.ElapsedMilliseconds;
                        checkedProxy.tcpValid = true;
                        /*
                        object _lock = new object();
                        lock (_lock)
                        {

                        }
                        */
                        watch.Stop();

                        //var responseFromServer = Stats.GetPoolApiDataAsync(url, proxy, true, true);
                        //Helpers.ConsolePrint("CheckProxies", "Proxy " + ip + ":" + httpsport.ToString() + " valid");
                    }
                }
                catch (Exception sex)
                {
                    Helpers.ConsolePrint("CheckProxies", sex.ToString());
                    /*
                    if ((ProxyCheck.HTTPSInvalidProxyList.FindAll(x => x.Ip == ip).Count() == 0))
                    {
                        pl.Ip = ip;
                        pl.Port = port;
                        ProxyCheck.HTTPSInvalidProxyList.Add(pl);
                    }
                    */
                    //return new Proxy();
                }
            }

            using (var socket = new System.Net.Sockets.Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                bool success = false;

                socket.ReceiveTimeout = timeout;
                socket.SendTimeout = timeout;
                Proxy pl = new();
                try
                {
                    new Thread(() =>
                    {
                        Thread.Sleep(timeout);
                        if (!success)
                        {
                            socket.Close();
                        }
                    }).Start();

                    var watch = Stopwatch.StartNew();
                    Helpers.ConsolePrint("CheckProxies", "Try connect to socks5 " + ip + ":" + socks5port.ToString());
                    socket.Connect(ip, socks5port);
                    //{"id": 1, "method": "mining.subscribe", "params": []string{"stratum-ping/1.0.0", "EthereumStratum/1.0.0"}}
                    if (socket.Connected)
                    {
                        success = true;
                        checkedProxy.Ip = ip;
                        checkedProxy.HTTPSPort = socks5port;
                        checkedProxy.sslSpeed = (int)watch.ElapsedMilliseconds;
                        checkedProxy.sslValid = true;
                        /*
                        object _lock = new object();
                        lock (_lock)
                        {

                        }
                        */
                        watch.Stop();
                        //Helpers.ConsolePrint("CheckProxies", "Proxy " + ip + ":" + socks5port.ToString() + " valid");
                    }
                }
                catch (Exception sex)
                {
                    Helpers.ConsolePrint("CheckProxies", sex.ToString());
                    /*
                    if ((ProxyCheck.HTTPSInvalidProxyList.FindAll(x => x.Ip == ip).Count() == 0))
                    {
                        pl.Ip = ip;
                        pl.Port = port;
                        ProxyCheck.HTTPSInvalidProxyList.Add(pl);
                    }
                    */
                    //return new Proxy();
                }
            }

            return checkedProxy;

        }
    }
}
