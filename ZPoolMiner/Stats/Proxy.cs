using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ZPoolMiner.Stats
{
    public class ProxyCheck
    {
        public static bool localProxyTest = false;
        public static List<ProxyChecker.Proxy> ProxyList = new();
        public static List<ProxyChecker.Proxy> HTTPSInvalidProxyList = new();
        public static ProxyChecker.Proxy CurrentHttpsProxy = new();
        public static void GetProxy()
        {
            ProxyList = new();
            ProxyList.Clear();
            List<string> proxys = new();

            if (localProxyTest)
            {
                proxys.Add("127.0.0.1");
            }
            else
            {
                //46.17.44.22
                //193.106.150.178
                try
                {
                    var Records = DnsInterop.GetTxtRecords("stratum-proxy.ru");
                    if (Records is object && Records != null && Records.Count() > 0)
                    {
                        foreach (var d in Records)
                        {
                            var _Record = DnsInterop.GetTxtRecords(d).ToArray()[0];
                            var ip = DNStoIP(d)[0];
                            Helpers.ConsolePrint("GetProxy", "From text records proxy IP: " + 
                                ip + " (" + _Record + ")");
                            if (!proxys.Contains(ip)) proxys.Add(ip);
                        }
                    }
                    else
                    {
                        foreach (var _ip in DNStoIP("stratum-proxy.ru"))
                        {
                            Helpers.ConsolePrint("GetProxy", "Proxy IP: " + _ip);
                            if (!proxys.Contains(_ip)) proxys.Add(_ip);
                        }
                    }
                } catch (Exception ex)
                {
                    Helpers.ConsolePrintError("GetProxy", ex.ToString());
                    proxys.Add("46.17.44.22");
                }
                //proxys.Add("31.58.171.225");
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
                ProxyList.Add(proxy);
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
            
            Stats.CurrentProxy = ProxyCheck.ProxyList[0];
            Helpers.ConsolePrintError("GetProxy", "Set to " + Stats.CurrentProxy.Ip + " proxy");

        }
        public static void ProxyRotate()
        {
            //переключение на другой прокси
            var first = ProxyCheck.ProxyList[0];
            ProxyCheck.ProxyList.RemoveAt(0);
            ProxyCheck.ProxyList.Add(first);
            Stats.CurrentProxy = ProxyCheck.ProxyList[0];
            Helpers.ConsolePrintError("ProxyRotate", "Switch to " + Stats.CurrentProxy.Ip + " proxy");
        }
        public static List<string> DNStoIP(string dnsname)
        {
            List<string> addr = new List<string>();
            try
            {
                System.Text.ASCIIEncoding ASCII = new System.Text.ASCIIEncoding();

                IPHostEntry heserver = GetHostEntry(dnsname);
                if (heserver != null)
                {
                    var ipsCount = heserver.AddressList.Count();
                    foreach (IPAddress curAdd in heserver.AddressList)
                    {
                        if (curAdd.AddressFamily.ToString() == ProtocolFamily.InterNetwork.ToString())
                        {
                            addr.Add(curAdd.ToString());
                        }
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

    /// <summary>
    /// Based on https://stackoverflow.com/a/11884174 (Martin Liversage)
    /// </summary>
    class DnsInterop
    {
        private const short DNS_TYPE_TEXT = 0x0010;
        private const int DNS_QUERY_STANDARD = 0x00000000;
        private const int DNS_ERROR_RCODE_NAME_ERROR = 9003;
        private const int DNS_INFO_NO_RECORDS = 9501;


        public static IEnumerable<string> GetTxtRecords(string domain)
        {
            var results = new List<string>();
            var queryResultsSet = IntPtr.Zero;
            DnsRecordTxt dnsRecord;

            try
            {
                // get all text records
                // pointer to results is returned in queryResultsSet
                var dnsStatus = DnsQuery(
                  domain,
                  DNS_TYPE_TEXT,
                  DNS_QUERY_STANDARD,
                  IntPtr.Zero,
                  ref queryResultsSet,
                  IntPtr.Zero
                );

                // return null if no records or DNS lookup failed
                if (dnsStatus == DNS_ERROR_RCODE_NAME_ERROR
                    || dnsStatus == DNS_INFO_NO_RECORDS)
                {
                    return null;
                }

                // throw an exception if other non success code
                if (dnsStatus != 0)
                    throw new Win32Exception(dnsStatus);

                // step through each result
                for (
                    var pointer = queryResultsSet;
                    pointer != IntPtr.Zero;
                    pointer = dnsRecord.pNext)
                {
                    dnsRecord = (DnsRecordTxt)
                        Marshal.PtrToStructure(pointer, typeof(DnsRecordTxt));

                    if (dnsRecord.wType == DNS_TYPE_TEXT)
                    {
                        var builder = new StringBuilder();

                        // pointer to array of pointers
                        // to each string that makes up the record
                        var stringArrayPointer = pointer + Marshal.OffsetOf(
                            typeof(DnsRecordTxt), "pStringArray").ToInt32();

                        // concatenate multiple strings in the case of long records
                        for (var i = 0; i < dnsRecord.dwStringCount; ++i)
                        {
                            var stringPointer = (IntPtr)Marshal.PtrToStructure(
                                stringArrayPointer, typeof(IntPtr));

                            builder.Append(Marshal.PtrToStringUni(stringPointer));
                            stringArrayPointer += IntPtr.Size;
                        }

                        results.Add(builder.ToString());
                    }
                }
            }
            finally
            {
                if (queryResultsSet != IntPtr.Zero)
                {
                    DnsRecordListFree(queryResultsSet,
                        (int)DNS_FREE_TYPE.DnsFreeRecordList);
                }
            }

            return results;
        }


        [DllImport("Dnsapi.dll", EntryPoint = "DnsQuery_W",
            ExactSpelling = true, CharSet = CharSet.Unicode,
            SetLastError = true)]
        static extern int DnsQuery(string lpstrName, short wType, int options,
            IntPtr pExtra, ref IntPtr ppQueryResultsSet, IntPtr pReserved);


        [DllImport("Dnsapi.dll")]
        static extern void DnsRecordListFree(IntPtr pRecordList, int freeType);


        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct DnsRecordTxt
        {
            public IntPtr pNext;
            public string pName;
            public short wType;
            public short wDataLength;
            public int flags;
            public int dwTtl;
            public int dwReserved;
            public int dwStringCount;
            public string pStringArray;
        }


        enum DNS_FREE_TYPE
        {
            DnsFreeFlat = 0,
            DnsFreeRecordList = 1,
            DnsFreeParsedMessageFields = 2
        }
    }
}
