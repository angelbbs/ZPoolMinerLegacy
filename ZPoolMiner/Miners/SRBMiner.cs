using Newtonsoft.Json;
using ZPoolMiner.Algorithms;
using ZPoolMiner.Configs;
using ZPoolMiner.Devices;
using ZPoolMiner.Forms;
using ZPoolMiner.Miners.Grouping;
using ZPoolMiner.Miners.Parsing;
using ZPoolMinerLegacy.Common.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using ZPoolMiner.Stats;

namespace ZPoolMiner.Miners
{
    public class SRBMiner : Miner
    {
        private readonly int GPUPlatformNumber;
        private int benchmarkTimeWait = 180;
        private int _benchmarkTimeWait = 180;

        private const int TotalDelim = 2;
        private double _power = 0.0d;

        public SRBMiner() : base("SRBMiner")
        {
            CurrentMinerReadStatus = MinerApiReadStatus.GOT_READ;
            GPUPlatformNumber = ComputeDeviceManager.Available.AmdOpenCLPlatformNum;
        }

        public override void Start(string wallet, string ID, string password)
        {
            new Task(() => Form_Main.DelWinDivert()).Start();
            LastCommandLine = GetStartCommand(wallet, ID, password);
            ProcessHandle = _Start();
        }
        private new string GetServer(string algo)
        {
            string ret = "";
            try
            {
                algo = algo.Replace("-", "_");
                var _a = Stats.Stats.CoinList.FirstOrDefault(item => item.algo.ToLower() == algo.ToLower());

                string serverUrl = Form_Main.regionList[ConfigManager.GeneralConfig.ServiceLocation].RegionLocation +
                    "mine.zpool.ca";

                if (_a == null)
                {
                    Helpers.ConsolePrintError("SRBMiner", algo + " Error!");
                    return "";
                }

                if (ConfigManager.GeneralConfig.EnableSSL)
                {
                    ret = "--pool " +
                        Links.CheckDNS(algo + serverUrl).Replace("stratum+tcp://", "stratum+ssl://") +
                        ":" + _a.ssl_port.ToString();
                } else
                {
                    ret = "--pool " +
                        Links.CheckDNS(algo + serverUrl) + ":" + _a.port.ToString();
                }
            }
            catch (Exception ex)
            {
                Helpers.ConsolePrint("GetServer", "Error in " + algo + " " + ex.ToString());
                ret = "error_in_list_of_algos.err:1111";
            }
            
            return ret + " ";
        }
        
        private string GetStartCommand(string wallet, string ID, string password)
        {
            string disablePlatform = "--disable-gpu-nvidia ";
            DeviceType devtype = DeviceType.NVIDIA;
            var sortedMinerPairs = MiningSetup.MiningPairs.OrderBy(pair => pair.Device.IDByBus).ToList();
            foreach (var mPair in sortedMinerPairs)
            {
                devtype = mPair.Device.DeviceType;
            }

            if (devtype == DeviceType.CPU)
            {
                disablePlatform = "--disable-gpu ";
            }
            if (devtype == DeviceType.AMD)
            {
                disablePlatform = "--disable-cpu --disable-gpu-nvidia --disable-gpu-intel ";
            }
            if (devtype == DeviceType.INTEL)
            {
                disablePlatform = "--disable-cpu --disable-gpu-nvidia --disable-gpu-amd ";
            }
            if (devtype == DeviceType.NVIDIA)
            {
                disablePlatform = "--disable-cpu --disable-gpu-intel --disable-gpu-amd ";
            }

            string proxy = "";
            if (ConfigManager.GeneralConfig.EnableProxy)
            {
                proxy = "--proxy 127.0.0.1:" + Socks5Relay.Port;
            }

            var extras = ExtraLaunchParametersParser.ParseForMiningSetup(MiningSetup, devtype);
            try
            {
                var _algo = MiningSetup.CurrentAlgorithmType.ToString().ToLower();
                _algo = _algo.Replace("sha512256d", "sha512_256d_radiant");
                _algo = _algo.Replace("argon2d16000", "argon2d_16000");
                _algo = _algo.Replace("interchained", "yespowerinterchained");

                var mainpool = GetServer(MiningSetup.CurrentAlgorithmType.
                                    ToString().ToLower()).Trim().Replace("--pool ", "");
                var failoverPool = GetServer(MiningSetup.CurrentAlgorithmType.
                                    ToString().ToLower()).Trim().Replace("--pool ", "");

                if (mainpool.Contains(".eu.")) failoverPool = mainpool.Replace(".eu.", ".na.");
                if (mainpool.Contains(".jp.")) failoverPool = mainpool.Replace(".jp.", ".na.");
                if (mainpool.Contains(".sea.")) failoverPool = mainpool.Replace(".sea.", ".na.");
                if (mainpool.Contains(".na.")) failoverPool = mainpool.Replace(".na.", ".eu.");

                if (MiningSetup.CurrentSecondaryAlgorithmType == AlgorithmType.NONE)
                {
                    return " --algorithm " + _algo + " " +
                        disablePlatform + $"--api-enable --api-port {ApiPort} {extras} " +
                        $"--pool {mainpool},{failoverPool} " + 
                        $"--wallet {wallet}.{ID},{wallet}.{ID} --password {password}" + " " +
                        proxy + " " +
                        " --give-up-limit 1 --retry-time 1 --gpu-id " +
                        GetDevicesCommandString().Trim();
                } else
                {
                    /*
                    var _algo2 = MiningSetup.CurrentSecondaryAlgorithmType.ToString().ToLower();
                    _algo2 = _algo2.Replace("sha512256d", "sha512_256d_radiant");

                    var pass1 = password.Split(',')[2].Replace("zap=", "").Split('+')[0];
                    var pass2 = password.Split(',')[2].Replace("zap=", "").Split('+')[1];

                    return " --algorithm " + _algo + " " +
                        GetServer(MiningSetup.CurrentAlgorithmType.ToString().ToLower()) + " " +
                        _wallet + " " + _password.Replace("zap=" + pass1 + "+" + pass2, "zap=" + pass1) + " " +
                        " --algorithm " + _algo2 + " " +
                        GetServer(MiningSetup.CurrentSecondaryAlgorithmType.ToString().ToLower()) + " " +
                        proxy + " " +
                        _wallet + " " + _password.Replace("zap=" + pass1 + "+" + pass2, "zap=" + pass2) + " " +
                        " " + disablePlatform + $"--api-enable --api-port {ApiPort} {extras} " +
                        " --gpu-id " +
                        GetDevicesCommandString().Trim();
                    */
                }

            } catch (Exception ex)
            {
                Helpers.ConsolePrint("GetStartCommand", ex.ToString());
            }
            
            return "unsupported algo";

        }

        protected override string GetDevicesCommandString()
        {
            ad = new ApiData(MiningSetup.CurrentAlgorithmType, MiningSetup.CurrentSecondaryAlgorithmType, MiningSetup.MiningPairs[0]);
            ad.ThirdAlgorithmID = AlgorithmType.NONE;
            
            var deviceStringCommand = " ";
            var ids = MiningSetup.MiningPairs.Select(mPair => mPair.Device.IDByBus.ToString()).ToList();
            ids.Sort();
            deviceStringCommand += string.Join("!", ids);

            return deviceStringCommand;
        }

        private string GetStartBenchmarkCommand(string btcAddress, string worker)
        {
            new Task(() => Form_Main.DelWinDivert()).Start();
            string disablePlatform = "--disable-gpu-nvidia ";
            DeviceType devtype = DeviceType.NVIDIA;
            var sortedMinerPairs = MiningSetup.MiningPairs.OrderBy(pair => pair.Device.IDByBus).ToList();
            foreach (var mPair in sortedMinerPairs)
            {
                devtype = mPair.Device.DeviceType;
            }
            BenchmarkAlgorithm.DeviceType = devtype;

            if (devtype == DeviceType.CPU)
            {
                disablePlatform = "--disable-gpu ";
            }
            if (devtype == DeviceType.AMD)
            {
                disablePlatform = "--disable-cpu --disable-gpu-nvidia --disable-gpu-intel ";
            }
            if (devtype == DeviceType.INTEL)
            {
                disablePlatform = "--disable-cpu --disable-gpu-nvidia --disable-gpu-amd ";
            }
            if (devtype == DeviceType.NVIDIA)
            {
                disablePlatform = "--disable-cpu --disable-gpu-intel --disable-gpu-amd ";
            }

            string proxy = "";
            if (ConfigManager.GeneralConfig.EnableProxy)
            {
                //proxy = "--proxy " + Stats.Stats.CurrentProxyIP + ":" + Stats.Stats.CurrentProxySocks5SPort + " ";
                proxy = "--proxy 127.0.0.1:" + Socks5Relay.Port;
            }

            var extras = ExtraLaunchParametersParser.ParseForMiningSetup(MiningSetup, devtype);

            string serverUrl = Form_Main.regionList[ConfigManager.GeneralConfig.ServiceLocation].RegionLocation +
                "mine.zpool.ca";

            var algo = MiningSetup.CurrentAlgorithmType.ToString().ToLower();

            var _algo = MiningSetup.CurrentAlgorithmType.ToString().ToLower();
            _algo = _algo.Replace("sha512256d", "sha512_256d_radiant");
            _algo = _algo.Replace("argon2d16000", "argon2d_16000");
            _algo = _algo.Replace("interchained", "yespowerinterchained");

            string demoWallet = Globals.DemoUser;
            string failoverPool = "";
            string failoverWallet = "";
            string failoverPassword = "";
            string failoverPool2 = "";
            string failoverWallet2 = "";
            string failoverPassword2 = "";
            switch (MiningSetup.CurrentAlgorithmType)
            {
                case AlgorithmType.VerusHash:
                    failoverPool = "stratum+ssl://pool.hashvault.pro:443";
                    failoverWallet = "RX8dEm1eqgmXmUm4iQ1Vg5LRaxuzophkTJ";
                    break;
                case AlgorithmType.Argon2d16000:
                    failoverPool = "stratum+tcp://argon2d16000.mine.zpool.ca:4241";
                    failoverWallet = Globals.DemoUser;
                    break;
                case AlgorithmType.Flex:
                    failoverPool = "stratum+tcp://flex.mine.zpool.ca:3340";
                    failoverWallet = Globals.DemoUser;
                    break;
                case AlgorithmType.Ghostrider:
                    failoverPool = "stratum+tcp://ghostrider.na.mine.zpool.ca:5354";
                    failoverWallet = Globals.DemoUser;
                    break;
                case AlgorithmType.Mike:
                    failoverPool = "stratum+tcp://mike.mine.na.zpool.ca:5356";
                    failoverWallet = Globals.DemoUser;
                    break;
                case AlgorithmType.Minotaurx:
                    failoverPool = "stratum+tcp://minotaurx.na.mine.zpool.ca:7019";
                    failoverWallet = Globals.DemoUser;
                    break;
                case AlgorithmType.Xelisv2_Pepew:
                    failoverPool = "stratum+tcp://xelisv2-pepew.na.mine.zpool.ca:4833";
                    failoverWallet = Globals.DemoUser;
                    break;
                case AlgorithmType.Yespower:
                    failoverPool = "stratum+tcp://yespower.mine.na.zpool.ca:6234";
                    failoverWallet = Globals.DemoUser;
                    break;
                case AlgorithmType.YespowerEQPAY:
                    failoverPool = "stratum+tcp://yespowereqpay.na.mine.zpool.ca:6249";
                    failoverWallet = Globals.DemoUser;
                    break;
                case AlgorithmType.YespowerLTNCG:
                    failoverPool = "stratum+tcp://yespowerLTNCG.na.mine.zpool.ca:6245";
                    failoverWallet = Globals.DemoUser;
                    break;
                case AlgorithmType.YespowerMGPC:
                    failoverPool = "stratum+tcp://yespowerMGPC.na.mine.zpool.ca:6247";
                    failoverWallet = Globals.DemoUser;
                    break;
                case AlgorithmType.YespowerSUGAR:
                    failoverPool = "stratum+tcp://yespowerSUGAR.na.mine.zpool.ca:6241";
                    failoverWallet = Globals.DemoUser;
                    break;
                case AlgorithmType.YespowerTIDE:
                    failoverPool = "stratum+tcp://yespowerTIDE.na.mine.zpool.ca:6239";
                    failoverWallet = Globals.DemoUser;
                    break;
                case AlgorithmType.YespowerURX:
                    failoverPool = "stratum+tcp://yespowerURX.na.mine.zpool.ca:6236";
                    failoverWallet = Globals.DemoUser;
                    break;
                case AlgorithmType.YespowerADVC:
                    failoverPool = "stratum+tcp://yespowerADVC.na.mine.zpool.ca:6248";
                    failoverWallet = Globals.DemoUser;
                    break;
                case AlgorithmType.YespowerR16:
                    failoverPool = "stratum+tcp://yespowerr16.na.mine.zpool.ca:6534";
                    failoverWallet = Globals.DemoUser;
                    break;
                case AlgorithmType.Interchained:
                    failoverPool = "stratum+tcp://interchained.na.mine.zpool.ca:6250";
                    failoverWallet = Globals.DemoUser;
                    break;
                case AlgorithmType.Yescrypt:
                    failoverPool = "stratum+tcp://yescrypt.mine.na.zpool.ca:6233";
                    failoverWallet = Globals.DemoUser;
                    break;
                case AlgorithmType.YescryptR8:
                    failoverPool = "stratum+tcp://yescryptR8.na.mine.zpool.ca:6323";
                    failoverWallet = Globals.DemoUser;
                    break;
                case AlgorithmType.YescryptR16:
                    failoverPool = "stratum+tcp://yescryptR16.na.mine.zpool.ca:6333";
                    failoverWallet = Globals.DemoUser;
                    break;
                case AlgorithmType.YescryptR32:
                    failoverPool = "stratum+tcp://yescryptR32.na.mine.zpool.ca:6343";
                    failoverWallet = Globals.DemoUser;
                    break;
                case AlgorithmType.Power2b:
                    failoverPool = "stratum+tcp://power2b.mine.na.zpool.ca:6242";
                    failoverWallet = Globals.DemoUser;
                    break;
                case AlgorithmType.RinHash:
                    failoverPool = "stratum+tcp://rinhash.na.mine.zpool.ca:7444";
                    failoverWallet = Globals.DemoUser;
                    break;

                case AlgorithmType.VertHash:
                    failoverPool = "stratum+tcp://verthash.na.mine.zpool.ca:6144";
                    failoverWallet = Globals.DemoUser;
                    break;
                case AlgorithmType.FiroPow:
                    failoverPool = "stratum+tcp://firopow.na.mine.zpool.ca:1326";
                    failoverWallet = Globals.DemoUser;
                    break;
                case AlgorithmType.SccPow:
                    failoverPool = "stratum+tcp://sccpow.na.mine.zpool.ca:1328";
                    failoverWallet = Globals.DemoUser;
                    break;
                case AlgorithmType.HeavyHash:
                    failoverPool = "stratum+tcp://heavyhash.na.mine.zpool.ca:5138";
                    failoverWallet = Globals.DemoUser;
                    break;
                case AlgorithmType.KawPow:
                    failoverPool = "stratum+tcp://rvn.2miners.com:6060";
                    failoverWallet = "bc1qun08kg08wwdsszrymg8z4la5d6ygckg9nxh4pq";
                    break;
                case AlgorithmType.Meraki:
                    failoverPool = "stratum+tcp://meraki.na.mine.zpool.ca:3387";
                    failoverWallet = Globals.DemoUser;
                    break;
                case AlgorithmType.Curve:
                    failoverPool = "stratum+tcp://curve.na.mine.zpool.ca:4633";
                    failoverWallet = Globals.DemoUser;
                    break;
                case AlgorithmType.PhiHash:
                    failoverPool = "stratum+tcp://eu.neuropool.net:10110";
                    failoverWallet = "PmM5d7PbhocvoUgFK3E1tdNBeN1hU2ipXs";//exbitron
                    break;
                case AlgorithmType.MeowPow:
                    failoverPool = "stratum+tcp://meowpow.na.mine.zpool.ca:1327";
                    failoverWallet = Globals.DemoUser;
                    break;
                case AlgorithmType.SHA512256d:
                    failoverPool = "stratum+tcp://sha512256d.na.mine.zpool.ca:3342";
                    failoverWallet = Globals.DemoUser;
                    break;
                default:
                    break;
            }

            switch (MiningSetup.CurrentSecondaryAlgorithmType)
            {
                /*
                case AlgorithmType.HooHash:
                    failoverPool2 = "stratum+tcp://nushypool.com:40012";
                    failoverWallet2 = $",hoosat:qzuy0ydzzmw82ffa8j30m724w4cmwnxk9864meytpkgs0y502pmwzk886446m";
                    break;
                default:
                    break;
                */
            }
            var mainpool = GetServer(MiningSetup.CurrentAlgorithmType.
                ToString().ToLower()).Trim().Replace("--pool ", "");
            if (mainpool.Contains(".na.") ||
                mainpool.Contains(".jp.") ||
                mainpool.Contains(".sea."))
            {
                mainpool = mainpool.Replace(".na.", ".eu.");
                mainpool = mainpool.Replace(".jp.", ".eu.");
                mainpool = mainpool.Replace(".sea.", ".eu.");
            }
            var comma = ",";
            if (MiningSetup.CurrentSecondaryAlgorithmType == AlgorithmType.NONE)
            {
                if (mainpool.Contains("error"))
                {
                    comma = "";
                    mainpool = failoverPool;
                    failoverPool = "";
                    demoWallet = "";
                }

                return " " + disablePlatform + "--algorithm " + _algo + " " +
                    "--pool " + mainpool + comma + failoverPool + " " +
                    $"--wallet {demoWallet}{comma}{failoverWallet} --password c=LTC{failoverPassword}" + " " +
                    proxy + " " +
                    $"--api-enable --api-port {ApiPort} {extras}" + " --give-up-limit 1 --retry-time 1 --gpu-id " +
                    GetDevicesCommandString().Trim();
            }
            else
            {
                var _algo2 = MiningSetup.CurrentSecondaryAlgorithmType.ToString().ToLower();
                _algo2 = _algo2.Replace("sha512256d", "sha512_256d_radiant");

                return " " + disablePlatform + "--algorithm " + _algo + " " +
                     "--pool " + mainpool + comma + failoverPool + " " +
                    //$"--wallet {Globals.DemoUser}{failoverWallet} --password c=LTC{failoverPassword},mc=*" + " " +
                    $"--wallet {Globals.DemoUser}{comma}{failoverWallet} --password c=LTC{failoverPassword}" + " " +
                    "--algorithm " + _algo2 + " " +
                    GetServer(MiningSetup.CurrentSecondaryAlgorithmType.ToString().ToLower()).Trim() + "," + failoverPool2 + " " +
                    //$"--wallet {Globals.DemoUser}{failoverWallet2} --password c=LTC{failoverPassword2},mc=*" + " " +
                    $"--wallet {Globals.DemoUser},{failoverWallet2} --password c=LTC{failoverPassword2}" + " " +
                     proxy + " " +
                    $"--api-enable --api-port {ApiPort} {extras}" + " --give-up-limit 1 --retry-time 1 --gpu-id " +
                    GetDevicesCommandString().Trim();
            }
        }
        protected override void _Stop(MinerStopType willswitch)
        {
            Helpers.ConsolePrint("SRBMINER Stop", "");
            DeviceType devtype = DeviceType.AMD;
            var sortedMinerPairs = MiningSetup.MiningPairs.OrderBy(pair => pair.Device.IDByBus).ToList();
            foreach (var mPair in sortedMinerPairs)
            {
                devtype = mPair.Device.DeviceType;
            }

            Stop_cpu_ccminer_sgminer_nheqminer(willswitch);
            StopDriver();
        }
        private void StopDriver()
        {
            //srbminer driver
            var CMDconfigHandleWD = new Process

            {
                StartInfo =
                {
                    FileName = "sc.exe"
                }
            };

            CMDconfigHandleWD.StartInfo.Arguments = "stop winio";
            CMDconfigHandleWD.StartInfo.UseShellExecute = false;
            CMDconfigHandleWD.StartInfo.CreateNoWindow = true;
            CMDconfigHandleWD.Start();
        }
        protected override int GetMaxCooldownTimeInMilliseconds()
        {
            return 60 * 1000 * 5;  // 5 min
        }

        private ApiData ad;
        public override ApiData GetApiData()
        {
            return ad;
        }

        public override async Task<ApiData> GetSummaryAsync()
        {
            string ResponseFromSRBMiner;
            try
            {
                HttpWebRequest WR = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:" + ApiPort.ToString());
                WR.UserAgent = "GET / HTTP/1.1\r\n\r\n";
                WR.Timeout = 3 * 1000;
                WR.Credentials = CredentialCache.DefaultCredentials;
                WebResponse Response = WR.GetResponse();
                Stream SS = Response.GetResponseStream();
                SS.ReadTimeout = 4 * 1000;
                StreamReader Reader = new StreamReader(SS);
                ResponseFromSRBMiner = await Reader.ReadToEndAsync();

                Reader.Close();
                Response.Close();
                WR.Abort();
                SS.Close();
            }
            catch (Exception ex)
            {
                Helpers.ConsolePrint("SRBMiner API Exception", ex.Message);
                CurrentMinerReadStatus = MinerApiReadStatus.READ_SPEED_ZERO;
                ad.Speed = 0;
                ad.SecondarySpeed = 0;
                ad.ThirdSpeed = 0;
                return ad;
            }

            dynamic resp = JsonConvert.DeserializeObject(ResponseFromSRBMiner);
            //Helpers.ConsolePrint("API ->:", ResponseFromSRBMiner.ToString());

            if (!MiningSetup.CurrentSecondaryAlgorithmType.Equals(AlgorithmType.NONE))
            {
                ad.SecondaryAlgorithmID = MiningSetup.CurrentSecondaryAlgorithmType;
            }

            double totalsMain = 0;
            double totalsSecond = 0;
            double totalsThird = 0;

            try
            {
                DeviceType devtype = DeviceType.AMD;
                var sortedMinerPairs = MiningSetup.MiningPairs.OrderBy(pair => pair.Device.IDByBus).ToList();
                foreach (var mPair in sortedMinerPairs)
                {
                    devtype = mPair.Device.DeviceType;
                }

                if (resp != null)
                {
                    if (MiningSetup.CurrentSecondaryAlgorithmType.Equals(AlgorithmType.NONE) &&
                        devtype != DeviceType.CPU)//single,
                    {
                        foreach (var mPair in sortedMinerPairs)
                        {
                            try
                            {
                                string token = $"algorithms[0].hashrate.gpu.gpu{mPair.Device.IDByBus}";
                                var hash = resp.SelectToken(token);
                                int gpu_hr = (int)Convert.ToDouble(hash, CultureInfo.InvariantCulture.NumberFormat);
                                mPair.Device.MiningHashrate = gpu_hr;
                                _power = mPair.Device.PowerUsage;
                                mPair.Device.AlgorithmID = (int)MiningSetup.CurrentAlgorithmType;
                                mPair.Device.SecondAlgorithmID = (int)MiningSetup.CurrentSecondaryAlgorithmType;
                                mPair.Device.ThirdAlgorithmID = (int)AlgorithmType.NONE;
                            }
                            catch (Exception ex)
                            {
                                Helpers.ConsolePrint("API Exception:", ex.ToString());
                            }
                        }
                        dynamic _tm = resp.algorithms[0].hashrate.gpu.total;
                        if (_tm != null)
                        {
                            totalsMain = resp.algorithms[0].hashrate.gpu.total;
                        }
                    }

                    if (!MiningSetup.CurrentSecondaryAlgorithmType.Equals(AlgorithmType.NONE) &&
                        devtype != DeviceType.CPU)//dual 
                    {
                        foreach (var mPair in sortedMinerPairs)
                        {
                            try
                            {
                                string token0 = $"algorithms[0].hashrate.gpu.gpu{mPair.Device.IDByBus}";
                                var hash0 = resp.SelectToken(token0);
                                int gpu_hr0 = (int)Convert.ToInt32(hash0, CultureInfo.InvariantCulture.NumberFormat);

                                string token1 = $"algorithms[1].hashrate.gpu.gpu{mPair.Device.IDByBus}";
                                var hash1 = resp.SelectToken(token1);
                                int gpu_hr1 = (int)Convert.ToInt32(hash1, CultureInfo.InvariantCulture.NumberFormat);

                                mPair.Device.MiningHashrate = gpu_hr0;
                                mPair.Device.MiningHashrateSecond = gpu_hr1;
                                _power = mPair.Device.PowerUsage;
                                mPair.Device.AlgorithmID = (int)MiningSetup.CurrentAlgorithmType;
                                mPair.Device.SecondAlgorithmID = (int)MiningSetup.CurrentSecondaryAlgorithmType;
                                mPair.Device.ThirdAlgorithmID = (int)AlgorithmType.NONE;
                            }
                            catch (OverflowException ex)
                            {
                                Helpers.ConsolePrint("API OverflowException Exception:", ex.ToString());
                                Restart();
                            }
                            catch (Exception ex)
                            {
                                Helpers.ConsolePrint("API Exception:", ex.ToString());
                            }
                        }
                        try
                        {
                            totalsMain = resp.algorithms[0].hashrate.gpu.total;
                        }
                        catch
                        {
                            //totalsMain = resp.algorithms[0].hashrate.1min;
                        }
                        try
                        {
                            totalsSecond = resp.algorithms[1].hashrate.gpu.total;
                        }
                        catch
                        {
                            totalsSecond = 0;
                        }
                    }

                    if (devtype == DeviceType.CPU)
                    {
                        try
                        {
                            totalsMain = resp.algorithms[0].hashrate.cpu.total;
                        }
                        catch (Exception ex)
                        {
                            totalsMain = 0;
                        }
                        foreach (var mPair in sortedMinerPairs)
                        {
                            mPair.Device.MiningHashrate = totalsMain;
                            _power = mPair.Device.PowerUsage;
                            mPair.Device.AlgorithmID = (int)MiningSetup.CurrentAlgorithmType;
                            mPair.Device.SecondAlgorithmID = (int)MiningSetup.CurrentSecondaryAlgorithmType;
                            mPair.Device.ThirdAlgorithmID = (int)AlgorithmType.NONE;
                        }
                    }


                    ad.Speed = totalsMain;
                    ad.SecondarySpeed = totalsSecond;
                    ad.ThirdSpeed = totalsThird;

                    if (ad.Speed + ad.SecondarySpeed + ad.ThirdSpeed == 0)
                    {
                        CurrentMinerReadStatus = MinerApiReadStatus.READ_SPEED_ZERO;
                    }
                    else
                    {
                        CurrentMinerReadStatus = MinerApiReadStatus.GOT_READ;
                        sortedMinerPairs = MiningSetup.MiningPairs.OrderBy(pair => pair.Device.IDByBus).ToList();
                        foreach (var mPair in sortedMinerPairs)
                        {
                            devtype = mPair.Device.DeviceType;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                //Helpers.ConsolePrint("API error", ex.ToString());
                CurrentMinerReadStatus = MinerApiReadStatus.READ_SPEED_ZERO;
                ad.Speed = 0;
                GC.Collect();
                return ad;
            }
            ad.Speed = totalsMain;
            ad.SecondarySpeed = totalsSecond;
            ad.ThirdSpeed = 0;
            ad.ThirdAlgorithmID = AlgorithmType.NONE;

            if (MiningSetup.CurrentSecondaryAlgorithmType != AlgorithmType.NONE)//dual
            {
                ad.Speed = totalsMain;
                ad.SecondarySpeed = totalsSecond;
            }
            else
            {
                ad.Speed = totalsMain;
                ad.SecondarySpeed = 0;
                ad.SecondaryAlgorithmID = AlgorithmType.NONE;
            }
            GC.Collect();
            Thread.Sleep(1);
            return ad;
        }

        protected override bool IsApiEof(byte third, byte second, byte last)
        {
            return third == 0x7d && second == 0xa && last == 0x7d;
        }

        #region Benchmark

        protected override string BenchmarkCreateCommandLine(Algorithm algorithm, int time)
        {
            benchmarkTimeWait = time;
            _benchmarkTimeWait = time;
            return GetStartBenchmarkCommand(Globals.DemoUser, Miner.GetWorkerName());
        }
        
        protected override void BenchmarkOutputErrorDataReceivedImpl(string outdata)
        {
            CheckOutdata(outdata);
        }
        protected override bool BenchmarkParseLine(string outdata)
        {
            return true;
        }
        #endregion
    }

}
