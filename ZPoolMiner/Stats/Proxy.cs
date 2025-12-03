using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ZPoolMiner.Stats
{
    public class ProxyCheck
    {
        public static List<ProxyChecker.Proxy> HttpsProxyList = new();
        public static List<ProxyChecker.Proxy> HTTPSInvalidProxyList = new();
        public static ProxyChecker.Proxy CurrentHttpsProxy = new();
        public static void GetProxy()
        {
            bool localProxyTest = false;

            HttpsProxyList = new();
            List<string> proxys = new();

            if (localProxyTest)
            {
                proxys.Add("127.0.0.1");
            }
            else
            {
                foreach (var p in DNStoIP("stratum-proxy.ru"))
                {
                    if (!proxys.Contains(p)) proxys.Add(p);
                }
                proxys.Add("46.17.44.22");
            }

            foreach (var p in proxys)
            {
                ProxyChecker.Proxy proxy = new();
                proxy.Ip = p;
                proxy.HTTPSPort = 13150;
                proxy.Socks5Port = 13155;
                proxy.tcpValid = true;
                proxy.sslValid = true;
                proxy.allValid = true;
                HttpsProxyList.Add(proxy);
            }
            
            /*
            proxy = new();
            proxy.Ip = "31.58.171.225";
            proxy.HTTPSPort = 13150;
            proxy.Socks5Port = 13155;
            proxy.tcpValid = false;
            proxy.sslValid = true;
            proxy.allValid = true;
            //proxy.Speed = 1;
            HttpsProxyList.Add(proxy);
            */
            
            Stats.CurrentProxyIP = ProxyCheck.HttpsProxyList[0].Ip;
            Stats.CurrentProxyHTTPSPort = ProxyCheck.HttpsProxyList[0].HTTPSPort;
            Stats.CurrentProxySocks5SPort = ProxyCheck.HttpsProxyList[0].Socks5Port;
            Helpers.ConsolePrintError("GetProxy", "Set to " + Stats.CurrentProxyIP + " proxy");

        }
        public static void ProxyRotate()
        {
            //переключение на другой прокси
            var first = ProxyCheck.HttpsProxyList[0];
            ProxyCheck.HttpsProxyList.RemoveAt(0);
            ProxyCheck.HttpsProxyList.Add(first);
            Stats.CurrentProxyIP = ProxyCheck.HttpsProxyList[0].Ip;
            Stats.CurrentProxyHTTPSPort = ProxyCheck.HttpsProxyList[0].HTTPSPort;
            Stats.CurrentProxySocks5SPort = ProxyCheck.HttpsProxyList[0].Socks5Port;
            Helpers.ConsolePrintError("ProxyRotate", "Switch to " + Stats.CurrentProxyIP + " proxy");
        }
        public static List<string> DNStoIP(string dnsname)
        {
            List<string> addr = new List<string>();
            try
            {
                System.Text.ASCIIEncoding ASCII = new System.Text.ASCIIEncoding();
                IPHostEntry heserver = GetHostEntry(dnsname);
                foreach (IPAddress curAdd in heserver.AddressList)
                {
                    if (curAdd.AddressFamily.ToString() == ProtocolFamily.InterNetwork.ToString())
                    {
                        addr.Add(curAdd.ToString());
                    }
                }
            }
            catch (Exception e)
            {
                Helpers.ConsolePrint("Proxy", "Exception: " + e.ToString());
            }
            return addr;
        }
        public static IPHostEntry GetHostEntry(string host)
        {
            IPHostEntry ret = null;
            try
            {
                return Dns.GetHostEntry(host);
            }
            catch (Exception ex)
            {
                Helpers.ConsolePrint("Proxy", "GetHostEntry " + host + ": " + ex.ToString());
            }
            return ret;
        }
    }
}
