using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
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
            //Helpers.ConsolePrint("GetHttpsProxy", "Start check https proxy");
            /*
            try
            {
                if (File.Exists("Configs\\HTTPSInvalidProxyList.json"))
                {
                    var json = File.ReadAllText("Configs\\HTTPSInvalidProxyList.json");
                    HTTPSInvalidProxyList = JsonConvert.DeserializeObject<List<ProxyChecker.Proxy>>(json);
                }
            } catch (Exception ex)
            {
                Helpers.ConsolePrintError("GetHttpsProxy", ex.ToString());
            }
            */
            HttpsProxyList = new();
            List<ProxyChecker.Proxy> _HttpsProxyList = new();

            //string link = @"https://cdn.jsdelivr.net/gh/proxifly/free-proxy-list@main/proxies/protocols/socks5/data.json";

            //https https://github.com/claude89757/free_https_proxies
            //string link = @"https://raw.githubusercontent.com/claude89757/free_https_proxies/refs/heads/main/https_proxies.txt";
            //var list1 = GetProxyList(link).Distinct().ToList().FindAll(x => x.Port == 3128);
            //_HttpsProxyList.AddRange(GetProxyList(link).Distinct().ToList().FindAll(x => x.Port == 3128));

            //https https://github.com/casa-ls/proxy-list
            //string link = @"https://raw.githubusercontent.com/casa-ls/proxy-list/refs/heads/main/http";
            //_HttpsProxyList.AddRange(GetProxyList(link).Distinct().ToList());

            /*
            try
            {
                if (File.Exists("Configs\\HTTPSValidProxy.txt"))
                {
                    var text = File.ReadAllText("Configs\\HTTPSValidProxy.txt");
                    ProxyChecker.Proxy proxy = new();
                    string ip = text.Split(':')[0];
                    string _port = text.Split(':')[1];
                    int.TryParse(_port, out int port);
                    proxy.Ip = ip;
                    proxy.Port = port;
                    proxy.Speed = 0;
                    proxy.Valid = true;
                    object _lock = new object();
                    lock (_lock)
                    {
                        HttpsProxyList.Add(proxy);
                    }
                }
            }
            catch (Exception ex)
            {
                Helpers.ConsolePrintError("GetHttpsProxy", ex.ToString());
            }
            */
            /*
            if (_HttpsProxyList.Count > 0)
            {
                int timeout = 800;
                for (int j = 0; j < _HttpsProxyList.Count; j = j + 8)
                //for (int j = 0; j < 256; j = j + 8)
                {
                    if (j + 8 >= _HttpsProxyList.Count) break;
                    List<ProxyChecker.Proxy> tempProxyList = new();
                    for (int i = j; i < 8 + j; i++)
                    {
                        ProxyChecker.Proxy proxy = new();
                        proxy.Ip = _HttpsProxyList[i].Ip;
                        proxy.Port = _HttpsProxyList[i].Port;
                        tempProxyList.Add(proxy);

                        //HttpsProxyList.AddRange(pr);
                        //if (HttpsProxyList.Count > 15) break;
                    }
                    var pr = ProxyChecker.CheckProxies(tempProxyList, timeout);
                    Thread.Sleep(timeout);
                    HttpsProxyList.AddRange(pr);
                }
            }
            */
            ProxyChecker.Proxy proxy = new();
            proxy.Ip = "193.106.150.178";
            proxy.HTTPSPort = 13150;
            proxy.Socks5Port = 13155;
            proxy.tcpValid = true;
            proxy.sslValid = true;
            proxy.allValid = true;
            //proxy.Speed = 0;
            HttpsProxyList.Add(proxy);
            /*
            proxy = new();
            proxy.Ip = "94.231.123.82";
            proxy.HTTPSPort = 13150;
            proxy.Socks5Port = 13155;
            proxy.tcpValid = true;
            proxy.sslValid = true;
            proxy.allValid = true;
            proxy.Speed = 1;
            HttpsProxyList.Add(proxy);
            */
            proxy = new();
            proxy.Ip = "31.58.171.225";
            proxy.HTTPSPort = 13150;
            proxy.Socks5Port = 13155;
            proxy.tcpValid = false;
            proxy.sslValid = true;
            proxy.allValid = true;
            //proxy.Speed = 1;
            HttpsProxyList.Add(proxy);

            /*
            proxy.Ip = "192.168.1.110";
            proxy.HTTPSPort = 13150;
            proxy.Socks5Port = 13155;
            proxy.Valid = true;
            proxy.Speed = 1;
            HttpsProxyList.Add(proxy);
            */
            Stats.CurrentProxyIP = ProxyCheck.HttpsProxyList[0].Ip;
            Stats.CurrentProxyHTTPSPort = ProxyCheck.HttpsProxyList[0].HTTPSPort;
            Stats.CurrentProxySocks5SPort = ProxyCheck.HttpsProxyList[0].Socks5Port;
            Helpers.ConsolePrintError("GetProxy", "Set to " + Stats.CurrentProxyIP + " proxy");
            //HttpsProxyList = HttpsProxyList.OrderBy(s => s.sslSpeed).ToList();
            /*
            foreach (var p in HttpsProxyList)
            {
                Helpers.ConsolePrint("GetHttpsProxy", "Valid https proxy: " + p.Ip + ":" + p.Port.ToString() + " " + p.Speed.ToString() + "ms");
            }
            Helpers.ConsolePrint("GetHttpsProxy", "Valid " + HttpsProxyList.Count.ToString() + " of " + _HttpsProxyList.Count.ToString());
            */
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
    }
}
