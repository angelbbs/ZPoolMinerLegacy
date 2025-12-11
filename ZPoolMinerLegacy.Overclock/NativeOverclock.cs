using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ZPoolMinerLegacy.Overclock
{
    public class NativeOverclock
    {
        public static bool logging = false;
        public static IntPtr OverclockStart(int _processId, int _CurrentAlgorithmType, int _SecondaryAlgorithmType,
            string _MinerName, string _strPlatform,
            string _w, bool _log, string Arguments)
        {
            return new IntPtr(0);
        }
        public static List<RelayConnection> RelayConnectionList;
        public class RelayConnection
        {
            public int threadId { get; set; }
            public int pID { get; set; }
            public int LocalPort { get; set; }
            public string Pool { get; set; }
            public int PoolPort { get; set; }
        }

        public static void GetMinerData(string _RelayConnectionList)
        {
            try
            {
                RelayConnectionList = JsonConvert.DeserializeObject<List<RelayConnection>>(_RelayConnectionList);
            }
            catch (Exception ex)
            { }
        }
    }
}

