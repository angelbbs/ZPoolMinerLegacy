using ZPoolMiner.Algorithms;
using ZPoolMiner.Configs;
using ZPoolMinerLegacy.Common.Enums;
using System;
using System.Collections.Generic;
using System.IO;

namespace ZPoolMiner.Miners
{
    public class ClaymoreNeoscryptMiner : ClaymoreBaseMiner
    {
        public ClaymoreNeoscryptMiner()
            : base("ClaymoreNeoscryptMiner")
        {
            LookForStart = "ns - total speed:";
        }

        
        public override void Start(string wallet, string ID, string password)
        {
            /*
            //# POOL: stratum+tcp://neoscrypt.eu.nicehash.com:3341, WALLET: YourWallet, PSW: x
            try
            {
                var failover = "POOL: " + GetServer("neoscrypt", ConfigManager.GeneralConfig.EnableSSL) + ", " +
                    "WALLET: " + wallet + "." + ID + ", " +
                    "PSW: " + password;
                File.WriteAllText("miners\\claymore_neoscrypt\\pools.txt", failover);
            } catch (Exception ex)
            {

            }
            */
            if (ConfigManager.GeneralConfig.EnableSSL)
                LastCommandLine = " " + GetDevicesCommandString() + " -mport -" + ApiPort + " -pool " +
                GetServer("neoscrypt", false) +
                " -wal " + wallet + "." + ID + " -psw " + password + " -dbg -1 -ftime 10 -retrydelay 5";

            ProcessHandle = _Start();
        }

        // benchmark stuff
        protected override bool BenchmarkParseLine(string outdata)
        {
            return true;
        }
        protected override string BenchmarkCreateCommandLine(Algorithm algorithm, int time)
        {
            BenchmarkAlgorithm.DeviceType = DeviceType.AMD;
            BenchmarkTimeWait = time;
            // demo for benchmark
            return $" {GetDevicesCommandString()} -mport -{ApiPort} -pool " + Links.CheckDNS("stratum+tcp://neoscrypt.eu.mine.zpool.ca") + ":4233 -wal " + Globals.DemoUser + " -psw c=LTC -logfile " + GetLogFileName();
        }

    }
}
