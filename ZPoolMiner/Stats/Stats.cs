using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ZPoolMiner.Configs;
using ZPoolMiner.Miners;
using ZPoolMiner.Switching;
using ZPoolMinerLegacy.Common.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.ExceptionServices;
using WebSocketSharp;
using System.Collections;
using System.Collections.Concurrent;
using System.Text;
using ZPoolMiner.Algorithms;
using ZPoolMinerLegacy.UUID;
using System.Threading;
using System.Globalization;
using SystemTimer = System.Timers.Timer;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Net.Http;
using SocksSharp;
using SocksSharp.Proxy;
using System.Diagnostics;
using ZPoolMiner.Devices.Algorithms;
using ZPoolMiner.Devices;

namespace ZPoolMiner.Stats
{
    public class Stats
    {
        public static double Balance { get; private set; }
        public static string Version = "";

        public static List<Coin> CoinList = new();
        public class Coin
        {
            public string algo { get; set; }
            public string name { get; set; }
            public string symbol { get; set; }
            public double hashrate { get; set; }
            public double estimate { get; set; }
            public double profit { get; set; }
            public double adaptive_profit { get; set; }
            public double adaptive_factor { get; set; }
            public double mbtc_mh_factor { get; set; }
            public int port { get; set; }
            public int ssl_port { get; set; }
            public int _24h_blocks { get; set; }
            public int lastblock { get; set; }
            public int timesincelast { get; set; }
            public int conversion_disabled { get; set; }
            public int only_direct { get; set; }
            public int has_price { get; set; }
            public int is_aux { get; set; }
            public string error { get; set; }
            public bool tempBlock { get; set; }
        }
        public static List<Coin> MinerStatCoinList = new();
        private static int httpsProxyCheck = 0;
        public static string CurrentProxyIP;
        public static int CurrentProxyHTTPSPort = 13150;
        public static int CurrentProxySocks5SPort = 13155;
        public static async Task<string> GetPoolApiAsync(string url, int timeout = 5, bool log = true)
        {
            string responseFromServer = "";
            if (ConfigManager.GeneralConfig.EnableProxy)
            {
                foreach (var proxy in ProxyCheck.HttpsProxyList)
                {
                    if (proxy.Valid)
                    {
                        try
                        {
                            //try direct
                            responseFromServer = await GetPoolApiDataAsync(url, proxy, false, log);
                            if (!string.IsNullOrEmpty(responseFromServer))
                            {
                                if (log)
                                {
                                    Helpers.ConsolePrint("GetPoolApiData", "Received bytes: " +
                                    responseFromServer.Length.ToString() + " directly from " + url);
                                }
                                break;
                            }
                            else
                            {
                                if (log)
                                {
                                    Helpers.ConsolePrintError("GetPoolApiAsync", "Direct connection failure to " + url);
                                }
                            }
                            //proxy
                            responseFromServer = await GetPoolApiDataAsync(url, proxy, true, log);
                            if (!string.IsNullOrEmpty(responseFromServer))
                            {
                                if (log)
                                {
                                    Helpers.ConsolePrint("GetPoolApiData", "Received bytes: " +
                                    responseFromServer.Length.ToString() + " from " + url + " " +
                                    proxy.Ip + ":" + proxy.HTTPSPort);
                                }
                                break;
                            }
                            else
                            {
                                if (log)
                                {
                                    Helpers.ConsolePrintError("GetPoolApiAsync", "Connect fail via proxy: " +
                                    proxy.Ip + ":" + proxy.HTTPSPort.ToString());
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Helpers.ConsolePrintError("GetPoolApiAsync", ex.ToString());
                        }
                    }
                }

                if (string.IsNullOrEmpty(responseFromServer))
                {
                    //Helpers.ConsolePrintError("GetPoolApiAsync", "All proxy unavailable");
                    new Task(() => ProxyCheck.GetHttpsProxy()).Start();
                }
            } else
            {
                try
                {
                    responseFromServer = await GetPoolApiDataAsync(url, null, false, log);
                    if (!string.IsNullOrEmpty(responseFromServer))
                    {
                        if (log)
                        {
                            Helpers.ConsolePrint("GetPoolApiData", "Received bytes: " +
                        responseFromServer.Length.ToString() + " from " + url + " ");
                        }
                    }
                    else
                    {
                        if (log)
                        {
                            Helpers.ConsolePrintError("GetPoolApiData", "Error getting data from " + url);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Helpers.ConsolePrintError("GetPoolApiAsync", ex.ToString());
                }
            }
            return responseFromServer;
        }

        public static async Task<string> GetPoolApiDataAsync(string url, ProxyChecker.Proxy proxy, 
            bool viaProxy, bool log = true)
        {
            var uri = new Uri(url);
            string host = new Uri(url).Host;
            var responseFromServer = "";
            Random r = new Random();
            var id = "[" + r.Next(100, 999).ToString() + "] ";
            var watch = Stopwatch.StartNew();
            try
            {
                //ServicePointManager.ServerCertificateValidationCallback = (s, cert, chain, ssl) => true;
                //ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                var httpClient = new HttpClient();
                if (viaProxy)
                {
                    if (log)
                    {
                        Helpers.ConsolePrint("GetPoolApiData", id + "Try connect to " + url + " via proxy " +
                        proxy.Ip + ":" + proxy.HTTPSPort.ToString());
                    }
                    var _proxy = new WebProxy
                    {
                        Address = new Uri("http://" + proxy.Ip + ":" + proxy.HTTPSPort.ToString())

                    };
                    //proxy.Credentials = new NetworkCredential(); //Used to set Proxy logins.

                    var proxyClientHandler = new HttpClientHandler
                    {
                        Proxy = _proxy
                    };
                    httpClient = new HttpClient(proxyClientHandler);
                } else
                {
                    if (log)
                    {
                        Helpers.ConsolePrint("GetPoolApiData", id + "Try connect to " + url);
                    }
                }
                using (httpClient)
                {
                    bool success = false;
                    new Thread(() =>
                    {
                        for (int i = 0; i < 15 * 10; i++)
                        {
                            if (Form_Main.ProgramClosing) return;
                            Thread.Sleep(100);
                        }
                        if (httpClient is object && httpClient is not null && !success)
                        {
                            httpClient.Dispose();
                        }
                    }).Start();
                    
                    var response = await httpClient.GetAsync(url);
                    if (response.IsSuccessStatusCode)
                    {
                        success = true;
                        var contents = await response.Content.ReadAsStringAsync();
                        if (contents.Length == 0 || (contents[0] != '{' && contents[0] != '['))
                        {
                            if (log)
                            {
                                Helpers.ConsolePrintError("GetPoolApiDataAsync", id + "Error! Not JSON from " + url +
                                "\r\n" + responseFromServer);
                            }
                            responseFromServer = "";
                        }
                        else
                        {
                            responseFromServer = contents;
                            if (viaProxy)
                            {
                                CurrentProxyIP = proxy.Ip;
                                CurrentProxyHTTPSPort = proxy.HTTPSPort;
                                CurrentProxySocks5SPort = proxy.Socks5Port;
                            }
                        }
                    }
                    else
                    {
                        Helpers.ConsolePrintError("GetPoolApiDataAsync", id + response.ReasonPhrase + ", " +
                            response.StatusCode.ToString() + ", " +
                            response.RequestMessage.ToString() + ", " +
                            response.Headers.ToString() + ", ");
                    }
                }
                if (httpClient is object && httpClient != null)
                {
                    httpClient.Dispose();
                }
            }
            catch (Exception ex)
            {
                var t = (int)watch.ElapsedMilliseconds;
                watch.Stop();
                if (viaProxy)
                {
                    Helpers.ConsolePrintError("GetPoolApiDataAsync", id + "Connection error in " + t.ToString() + " ms " + ex.Message + " " +
                    url + " " + proxy.Ip + ":" + proxy.HTTPSPort.ToString());
                } else
                {
                    Helpers.ConsolePrintError("GetPoolApiDataAsync", id + "Connection error in " + t.ToString() + 
                        " ms " + ex.Message + " " + url);
                }
                return "";
            }
            Form_Main.ZPoolAPIError = null;
            return responseFromServer;
        }
        public class AlgoCoin
        {
            public string algo;
            public string coin;
        }

        private static bool _FirstRunGetCoinsAsync;
        private static int delayGetBlock = 0;
        public static async Task<List<Coin>> GetCoinsAsync(string link)
        {
            Helpers.ConsolePrint("Stats", "Trying " + link);
            double correction = 1.0;
            double adaptivecorrection = 1.0;
            List<Coin> coinlist = new List<Coin>();
            List<AlgoCoin> conversion_disabledCoin = new();
            List<AlgoCoin> zeroHashrateCoin = new();
            List<AlgoCoin> noBlockFoundCoin = new();
            delayGetBlock++;
            if (delayGetBlock > 15)
            {
                delayGetBlock = 0;
            }
            try
            {
                try
                {
                    if (File.Exists("configs\\CoinList.json"))
                    {
                        var coinsjson = File.ReadAllText("configs\\CoinList.json");
                        coinlist = JsonConvert.DeserializeObject<List<Coin>>(coinsjson);
                    }
                }
                catch (Exception ex)
                {
                    Helpers.ConsolePrintError("Stats", ex.ToString());
                }

                string ResponseFromAPI = await GetPoolApiAsync(link, 7);
                if (ResponseFromAPI != null)
                {
                    var APIdata = JObject.Parse(ResponseFromAPI);
                    foreach (var coinAPI in APIdata)
                    {
                        var symbol = coinAPI.Key;
                        var coin = coinAPI.Value;

                        string algo = coin.Value<string>("algo").Replace("-", "_");
                        var port = coin.Value<int>("port");
                        var ssl_port = coin.Value<int>("ssl_port");
                        string name = coin.Value<string>("name");
                        var hashrate = coin.Value<double>("hashrate");
                        var estimate = coin.Value<double>("estimate");
                        var mbtc_mh_factor = coin.Value<double>("mbtc_mh_factor");
                        var _24h_blocks = coin.Value<int>("24h_blocks");
                        var lastblock = coin.Value<int>("lastblock");
                        var timesincelast = coin.Value<int>("timesincelast");
                        var conversion_disabled = coin.Value<int>("conversion_disabled");
                        var only_direct = coin.Value<int>("only_direct");
                        var has_price = coin.Value<int>("has_price");
                        var is_aux = coin.Value<int>("is_aux");
                        var error = coin.Value<string>("error");
                        
                        Coin _coin = new();
                        _coin.name = name;
                        _coin.algo = algo;
                        _coin.symbol = symbol;
                        
                        _coin.port = port;
                        _coin.ssl_port = ssl_port;
                        _coin.hashrate = hashrate;
                        _coin.adaptive_factor = 1.0;

                        _coin.estimate = (estimate * 1000000 / mbtc_mh_factor) * correction;//mBTC/GH
                        _coin.profit = _coin.estimate;
                        _coin.mbtc_mh_factor = mbtc_mh_factor;

                        _coin._24h_blocks = _24h_blocks;
                        _coin.lastblock = lastblock;
                        _coin.timesincelast = timesincelast;
                        _coin.conversion_disabled = conversion_disabled;
                        _coin.only_direct = only_direct;
                        _coin.has_price = has_price;
                        _coin.is_aux = is_aux;
                        _coin.error = error;

                        //get from CoinList.json
                        var _c = coinlist.Find(a => (a.symbol.ToLower() == _coin.symbol.ToLower()) &&
                                (a.algo.ToLower() == _coin.algo.ToLower()));
                        if (_c is object && _c != null)
                        {
                            _coin.adaptive_factor = _c.adaptive_factor;
                        }
                        
                        /*
                        var unstableAlgosList = AlgorithmSwitchingManager.unstableAlgosList.Select(s => s.ToString().ToLower()).ToList();
                        if (unstableAlgosList.Contains(algo.ToLower()))
                        {
                            _coin.estimate = _coin.estimate * 0.3;
                        }
                        */
                        //***********
                        
                        /*
                        if (_coin.actual_last24h != 0 && _coin.estimate_current > _coin.actual_last24h * 100)
                        {
                            Helpers.ConsolePrint("Stats", _coin.symbol + " API bug");
                            _coin.estimate = _coin.actual_last24h;
                            _coin.estimate_current = _coin.actual_last24h;
                            _coin.estimate_last24h = _coin.actual_last24h;
                        }
                        */
                        //test
                        /*
                        if (_coin.symbol.ToLower().Equals("btg"))
                        {
                            _coin.estimate_current = 9999999;
                            _coin.tempTTF_Disabled = false;
                        }
                        */
                        
                        //adaptive section
                        _coin.adaptive_profit = _coin.estimate * _coin.adaptive_factor;

                        //not adaptive section
                        double _profits = 0d;
                        int profitsCount = 0;
                        /*
                        if (checkAPIbug(_coin))
                        {
                            _coin.profit = getMin(_coin.estimate_current, _coin.estimate_last24h, _coin.actual_last24h);
                        }
                        else
                        {
                            if (ConfigManager.GeneralConfig.CurrentEstimate)
                            {
                                _profits = _profits + _coin.estimate_current;
                                profitsCount++;
                            }
                            if (ConfigManager.GeneralConfig._24hEstimate)
                            {
                                _profits = _profits + _coin.estimate_last24h;
                                profitsCount++;
                            }
                            if (ConfigManager.GeneralConfig._24hActual)
                            {
                                _profits = _profits + _coin.actual_last24h;
                                profitsCount++;
                            }
                            if (profitsCount != 0)
                            {
                                _profits = _profits / profitsCount;
                            }
                            _coin.profit = _profits;
                        }
                        */

                        //тут ограничени€
                        _coin.tempBlock = false;
                        
                        if (!Stats.coinsBlocked.ContainsKey(_coin.symbol))
                        {
                            _coin.tempBlock = false;
                        }

                        foreach (var c in Stats.coinsBlocked)
                        {
                            if (c.Key.Equals(_coin.symbol) && c.Value.checkTime >= 15)//15 min checking
                            {
                                Helpers.ConsolePrint("Stats", "Actual hashrate is missing from zpool for 15 minutes for " + _coin.algo +
                                    "(" + c.Key + "). Temporary block");
                                _coin.profit = _coin.profit / 100;
                                _coin.adaptive_profit = _coin.adaptive_profit / 100;
                                _coin.tempBlock = true;

                                foreach (var miningDevice in MiningSession._miningDevices)
                                {
                                    if (miningDevice.DeviceCurrentMiningCoin.Equals(c.Key) &&
                                                   !_coin.tempBlock)//блокируем
                                    {
                                        //miningDevice.needSwitch = true;
                                    }
                                }
                            }
                        }
                        /*
                        if (_coin.error.Contains("Please switch to another currency"))
                        //"error": "We are short of this currency (-112.34353488 SCC). Please switch to another currency until we find more SCC blocks.",
                        {
                            Helpers.ConsolePrint("Stats", "ZPool are short of currency " + _coin.algo + " (" + _coin.symbol + "). Temporary block");
                            _coin.profit = _coin.profit / 100;
                            _coin.adaptive_profit = _coin.adaptive_profit / 100;
                            _coin.tempBlock = true;
                        }
                        */
                        if (_coin.conversion_disabled == 1)
                        {
                            //Helpers.ConsolePrint("Stats", _coin.algo + " (" + _coin.symbol + ") conversion_disabled. Temporary block");
                            AlgoCoin c = new();
                            c.algo = _coin.algo;
                            c.coin = _coin.symbol;
                            conversion_disabledCoin.Add(c);
                            _coin.tempBlock = true;
                            _coin.profit = _coin.profit / 100;
                            _coin.adaptive_profit = _coin.adaptive_profit / 100;
                        }

                        if (_coin._24h_blocks == 0 && !_coin.tempBlock)
                        {
                            //Helpers.ConsolePrint("Stats", _coin.algo + " (" + _coin.symbol + ") 24h_blocks. Temporary block");
                            AlgoCoin c = new();
                            c.algo = _coin.algo;
                            c.coin = _coin.symbol;
                            noBlockFoundCoin.Add(c);
                            _coin.tempBlock = true;
                            _coin.profit = _coin.profit / 100;
                            _coin.adaptive_profit = _coin.adaptive_profit / 100;
                        }

                        if (hashrate == 0 && !_coin.tempBlock && _coin._24h_blocks < 10)
                        {
                            //Helpers.ConsolePrint("Stats", _coin.algo + " (" + _coin.symbol + ") zero hashrate. Temporary block");
                            AlgoCoin zhc = new();
                            zhc.algo = _coin.algo;
                            zhc.coin = _coin.symbol;
                            zeroHashrateCoin.Add(zhc);
                            _coin.tempBlock = true;
                            _coin.profit = _coin.profit / 100;
                            _coin.adaptive_profit = _coin.adaptive_profit / 100;
                        }
                        //"only_direct": 1,

                        //**************
                        if (CoinList.Exists(a => a.symbol.ToLower() == _coin.symbol.ToLower()) &&
                            CoinList.Exists(a => a.algo.ToLower() == _coin.algo.ToLower()))
                        {
                            _c = CoinList.Find(a => (a.symbol.ToLower() == _coin.symbol.ToLower()) &&
                            (a.algo.ToLower() == _coin.algo.ToLower()));
                            if (_c is object && _c != null)
                            {
                                CoinList.RemoveAll(a => (a.symbol.ToLower() == _coin.symbol.ToLower()) &&
                                (a.algo.ToLower() == _coin.algo.ToLower()));
                                CoinList.Add(_coin);
                            }
                        }
                        else //coin not exist in CoinList.json
                        {
                            Helpers.ConsolePrint("Stats", _coin.algo + " (" + _coin.symbol + ") added");
                            CoinList.Add(_coin);
                        }
                    }

                    //добавить об€зательную проверку на удаленные монеты

                    //7771
                    if (APIdata.Count > 10)
                    {
                        foreach (var c in Enumerable.Reverse(CoinList).ToList())
                        {
                            bool founded = false;
                            foreach (var item in APIdata)
                            {
                                var coin = item.Value;
                                string symbol = item.Key; 
                                if (c.symbol.Equals(symbol))
                                {
                                    founded = true;
                                    break;
                                }
                            }
                            if (!founded)
                            {
                                CoinList.Remove(c);
                                Helpers.ConsolePrint("Stats", "Missing coin " + c.symbol + "(" + c.algo + "). Delete");
                            }
                        }
                    }
                }

                conversion_disabledCoin.Sort((x, y) => x.algo.CompareTo(y.algo));
                
                string _algo = "";
                string coins = "";
                List<string> _conversion_disabledCoinCoinList = new();
                foreach (var c in conversion_disabledCoin)
                {
                    if (_algo.IsNullOrEmpty())
                    {
                        _algo = c.algo;
                    }
                    if (c.algo.Equals(_algo))
                    {
                        coins = coins + c.coin + ", ";
                    } else
                    {
                        _conversion_disabledCoinCoinList.Add(_algo + "(" + coins.Substring(0, coins.Length - 2) + ")");
                        //Helpers.ConsolePrint("Stats", _algo + " (" + coins.Substring(0, coins.Length - 2) + ") no autotrade. Disabled");
                        coins = "";
                        _algo = c.algo;
                        coins = coins + c.coin + ", ";
                    }
                }
                if (_conversion_disabledCoinCoinList.Count > 0)
                {
                    Helpers.ConsolePrint("Stats", "Conversion disabled coins: " + string.Join(", ", _conversion_disabledCoinCoinList) + 
                        " - Disabled");
                }

                zeroHashrateCoin.Sort((x, y) => x.algo.CompareTo(y.algo));
                _algo = "";
                coins = "";
                List<string> _zeroHashrateCoinList = new();
                foreach (var c in zeroHashrateCoin)
                {
                    if (_algo.IsNullOrEmpty())
                    {
                        _algo = c.algo;
                    }
                    if (c.algo.Equals(_algo))
                    {
                        coins = coins + c.coin + ", ";
                    }
                    else
                    {
                        _zeroHashrateCoinList.Add(_algo + " (" + coins.Substring(0, coins.Length - 2) + ")");
                        //Helpers.ConsolePrint("Stats", _algo + " (" + coins.Substring(0, coins.Length - 2) + ") zero hashrate. Disabled");
                        coins = "";
                        _algo = c.algo;
                        coins = coins + c.coin + ", ";
                    }
                }
                if (_zeroHashrateCoinList.Count > 0)
                {
                    Helpers.ConsolePrint("Stats", "Zero hashrate coins: " + string.Join(", ", _zeroHashrateCoinList));
                }

                noBlockFoundCoin.Sort((x, y) => x.algo.CompareTo(y.algo));
                _algo = "";
                coins = "";
                List<string> _noBlockFoundCoinList = new();
                foreach (var c in noBlockFoundCoin)
                {
                    if (_algo.IsNullOrEmpty())
                    {
                        _algo = c.algo;
                    }
                    if (c.algo.Equals(_algo))
                    {
                        coins = coins + c.coin + ", ";
                    }
                    else
                    {
                        _noBlockFoundCoinList.Add(_algo + " (" + coins.Substring(0, coins.Length - 2) + ")");
                        coins = "";
                        _algo = c.algo;
                        coins = coins + c.coin + ", ";
                    }
                }
                if (_noBlockFoundCoinList.Count > 0)
                {
                    Helpers.ConsolePrint("Stats", "No block found coins: " + string.Join(", ", _noBlockFoundCoinList));
                }

                lock (fileLock)
                {
                    CoinList.Sort((x, y) => x.algo.CompareTo(y.algo));
                    var json = JsonConvert.SerializeObject(CoinList, Formatting.Indented);
                    if (json.Length > 5)
                    {
                        Helpers.WriteAllTextWithBackup("configs\\CoinList.json", json);
                    }
                }
            }
            catch (Exception ex)
            {
                Helpers.ConsolePrintError("GetCoins", ex.ToString());
                /*
                try
                {
                    if (File.Exists("configs\\CoinList.json"))
                    {
                        var coinsjson = File.ReadAllText("configs\\CoinList.json");
                        if (!coinsjson.StartsWith("["))
                        {
                            File.Delete("configs\\CoinList.json");
                            Helpers.ConsolePrintError("Stats", "Coins database corrupted. Restart program");
                            Form_Main.MakeRestart(1);
                        }
                        else
                        {
                            CoinList = JsonConvert.DeserializeObject<List<Coin>>(coinsjson);
                        }
                    }
                }
                catch (Exception ex1)
                {
                    Helpers.ConsolePrintError("Stats", ex1.ToString());
                }
                */
                var json = File.ReadAllText("configs\\CoinList.json");
                CoinList = (List<Coin>)JsonConvert.DeserializeObject(json);
                _FirstRunGetCoinsAsync = true;
            }
            _FirstRunGetCoinsAsync = true;
            return CoinList;
        }

        public static List<string> miningPoolAlgos = new();
        public static List<Coin> GetMostProfitCoins(List<Coin> _coinlist)
        {
            List<Coin> _CoinList = new();
            List<string> progAlgosList = new();
            List<string> poolAlgosList = new();

            try
            {
                foreach (AlgorithmType alg in Enum.GetValues(typeof(AlgorithmType)))
                {
                    string _alg = alg.ToString().ToLower();
                    //_alg = _alg.Replace("xelisv2_pepew", "xelisv2-pepew");
                    //_alg = _alg.Replace("neoscrypt_xaya", "neoscrypt-xaya");
                    Coin mostProfitCoin = new();
                    if ((int)alg >= 1000 && !alg.ToString().Contains("unused"))
                    {
                        progAlgosList.Add(_alg);
                        foreach (var coin in _coinlist)
                        {
                            poolAlgosList.Add(coin.algo.ToLower());
                            if (!miningPoolAlgos.Contains(coin.algo.ToLower()))
                            {
                                miningPoolAlgos.Add(coin.algo.ToLower());
                            }

                            if (coin.algo.ToLower().Equals(_alg))
                            {
                                //foreach (var hashratencoin in ZPoolcoins)
                                {
                                    //Helpers.ConsolePrint(coin.symbol, minerStatcoin.symbol);
                                    /*
                                    if (coin.symbol.Equals(hashratencoin.symbol) && coin.algo.Equals(hashratencoin.algo))
                                    {
                                        Helpers.ConsolePrint(coin.algo.ToLower(), coin.symbol + ": " + 
                                            coin.estimate_current.ToString() + " " +
                                            hashratencoin.symbol + ": " + 
                                            (hashratencoin.estimate_current * coin.mbtc_mh_factor).ToString());
                                    }
                                    */
                                    /*
                                    //по хешрейту
                                    if (coin.hashrate >= defcoin.hashrate && !coin.tempTTF_Disabled &&
                                        !coin.tempDeleted && coin.noautotrade == 0)
                                    {
                                        defcoin = coin;
                                        defcoin.algo = _alg;
                                    }
                                    */

                                    //по монете
                                    //if (coin.hashrate > 0 && coin.estimate_current >= defcoin.estimate_current &&
                                    if (coin.adaptive_profit >= mostProfitCoin.adaptive_profit)
                                    {
                                        mostProfitCoin = coin;
                                    }
                                }
                            }
                        }
                        if (!string.IsNullOrEmpty(mostProfitCoin.symbol) && mostProfitCoin.adaptive_profit > 0)
                        {
                            _CoinList.Add(mostProfitCoin);
                        }
                    }
                }

                List<string> _deletedAlgosList = new();
                foreach (var a in progAlgosList)
                {
                    string _alg = a.ToLower();
                    _alg = _alg.Replace("-", "_");
                    /*
                    if (!poolAlgosList.Contains(_alg))
                    {
                        _deletedAlgosList.Add(_alg);
                        //Helpers.ConsolePrint("Stats", "Deleted? - " + _alg);
                        var itemToDelete = _CoinList.Find(r => r.algo.ToLower() == _alg);
                        if (itemToDelete != null)
                        {
                            itemToDelete.coinTempDeleted = true;
                        }
                    } else
                    {
                        var itemToDelete = _CoinList.Find(r => r.algo.ToLower() == _alg);
                        if (itemToDelete != null) itemToDelete.coinTempDeleted = false;
                    }
                    */
                }

                if (_deletedAlgosList.Count > 0)
                {
                    Helpers.ConsolePrint("Stats", "Missing algos: " + string.Join(", ", _deletedAlgosList));
                }

            } catch (Exception ex)
            {
                Helpers.ConsolePrintError("GetMostProfitCoins", ex.ToString());
            }
            return _CoinList;
        }

        public static string GetTime(int seconds)
        {
            TimeSpan ts = new TimeSpan(0, 0, seconds);
            if (ts.Days > 0)
            {
                return ts.Days + " Day(s)";
            }
            else if (ts.Hours > 0)
            {
                return ts.Hours + " Hour(s)";
            }
            else if (ts.Minutes > 0)
            {
                return ts.Minutes + " Minute(s)";
            }
            else
            {
                return ts.Seconds + " Second(s)";
            }
            return "?";
        }

        public static void ClearBalance()
        {
            Balance = 0;
        }

        public static void GetWalletBalance(object sender, EventArgs e)
        {
            GetWalletBalanceAsync(sender, e);
        }

        public static async Task GetWalletBalanceAsync(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(ConfigManager.GeneralConfig.Wallet))
            {
                Helpers.ConsolePrint("Stats", "Error getting wallet balance. Wallet not entered");
                return;
            }
            try
            {
                string ResponseFromAPI = await GetPoolApiAsync(Links.WalletBalance + ConfigManager.GeneralConfig.Wallet);
                if (!string.IsNullOrEmpty(ResponseFromAPI))
                {
                    dynamic data = JsonConvert.DeserializeObject(ResponseFromAPI);
                    double unpaid = data.@unpaid;
                    double balance = data.balance;
                    Balance = balance;
                } else
                {
                    //Balance = 0;
                }
            }
            catch (Exception ex)
            {
                Helpers.ConsolePrintError("GetWalletBalance", ex.ToString());
            }
            Helpers.ConsolePrint("Stats", "Wallet balance: " + Balance.ToString() + " " +
                ConfigManager.GeneralConfig.PayoutCurrency + 
                " (" + (Balance * ExchangeRateApi.GetPayoutCurrencyExchangeRate()).ToString("F2") + " " +
                ConfigManager.GeneralConfig.DisplayCurrency + ")");
            return;
        }
        private static readonly object fileLock = new object();
        public class AlgoProperty
        {
            public string algo;
            public string coin;
            public string hashrate;
            public double localhashrate;
            public double actualhashrate;
            public double localProfit;
            public double actualProfit;
            public double adaptive_factor;
            public double adaptive_profit;
            public int ticks;
            public List<double> factorsList = new();
        }

        public static ConcurrentDictionary<string, AlgoProperty> algosProperty = new();
        public static List<CoinProperty> coinsMining = new();
        public class CoinProperty
        {
            public string algorithm;
            public string symbol;
            public double hashrate_shared;
        }
        public static async Task GetWalletBalanceExAsync(object sender, EventArgs e)
        {
            Form_Main.adaptiveRunning = false;
            if (string.IsNullOrEmpty(ConfigManager.GeneralConfig.Wallet))
            {
                Helpers.ConsolePrintError("Stats", "Error getting wallet balance. Wallet not entered");
                return;
            }
            try
            {
                string ResponseFromAPI = await GetPoolApiAsync(Links.WalletBalanceEx + ConfigManager.GeneralConfig.Wallet);
                if (!string.IsNullOrEmpty(ResponseFromAPI))
                {
                    coinsMining.Clear();
                    double overallBTC = 0;
                    dynamic data = JsonConvert.DeserializeObject(ResponseFromAPI);
                    double localProfit = 0d;
                    double chart_actualProfit = 0d;
                    double balance = data.balance;
                    Balance = balance;

                    Helpers.ConsolePrint("Stats", "Wallet balance: " + Balance.ToString() + " " +
                        ConfigManager.GeneralConfig.PayoutCurrency +
                        " (" + (Balance * ExchangeRateApi.GetPayoutCurrencyExchangeRate()).ToString("F2") + " " +
                        ConfigManager.GeneralConfig.DisplayCurrency + ")");

                    if (MiningSession._miningDevices is not object) return;
                    if (MiningSession._miningDevices.Count == 0) return;

                    List<string> currentminingAlgos = new();
                    List<string> currentminingAlgosPool = new();
                    foreach (var _coin in CoinList)
                    {
                        if (_coin.adaptive_factor != 0 && _coin.adaptive_factor < 0.95)
                        {
                            _coin.adaptive_factor = _coin.adaptive_factor + 0.00001;
                        }
                        if (_coin.adaptive_factor != 0 && _coin.adaptive_factor > 1.05)
                        {
                            _coin.adaptive_factor = _coin.adaptive_factor - 0.00001;
                        }
                        if (_coin.tempBlock) continue;

                        double actualHashrate = 0d;
                        foreach (var cur in data.SelectToken("miners"))
                        {
                            string _algo = cur.SelectToken("algo");
                            _algo = _algo.Replace("xelisv2-pepew", "xelisv2_pepew");
                            _algo = _algo.Replace("neoscrypt-xaya", "neoscrypt_xaya");
                            string _hashrate = cur.SelectToken("hashrate");
                            string _ID = cur.SelectToken("ID");
                            string _password = cur.SelectToken("password");
                            int _subscribe = cur.SelectToken("subscribe");
                            double _accepted = cur.SelectToken("accepted");
                            string coin = _password.Split(',')[2].Replace("zap=", "");

                            if (_coin.algo.ToLower().Equals(_algo.ToLower()) &&
                                _coin.symbol.ToLower().Equals(coin.ToLower()) &&
                            _ID.Equals(Miner.GetWorkerUID()))
                            {
                                var _algoProperty = new AlgoProperty();
                                double localHashrate = 0d;
                                foreach (var miningDevice in MiningSession._miningDevices)
                                {
                                    if (_coin.algo.ToLower().Equals(((AlgorithmType)miningDevice.Device.AlgorithmID).ToString().ToLower()) &&
                                        _coin.symbol.ToLower().Equals(miningDevice.Device.Coin.ToLower()))
                                    {
                                        localHashrate = localHashrate + miningDevice.Device.MiningHashrate;
                                    }
                                    if (_coin.algo.ToLower().Equals(((AlgorithmType)miningDevice.Device.SecondAlgorithmID).ToString().ToLower()) &&
                                        _coin.symbol.ToLower().Equals(miningDevice.Device.Coin.ToLower()))
                                    {
                                        localHashrate = localHashrate + miningDevice.Device.MiningHashrateSecond;
                                    }
                                    if (!currentminingAlgos.Contains(((AlgorithmType)miningDevice.Device.AlgorithmID).ToString().ToLower()))
                                    {
                                        currentminingAlgos.Add(((AlgorithmType)miningDevice.Device.AlgorithmID).ToString().ToLower());
                                    }
                                    if (!currentminingAlgos.Contains(((AlgorithmType)miningDevice.Device.SecondAlgorithmID).ToString().ToLower()))
                                    {
                                        currentminingAlgos.Add(((AlgorithmType)miningDevice.Device.SecondAlgorithmID).ToString().ToLower());
                                    }
                                }

                                if (!currentminingAlgosPool.Contains(_algo.ToLower()))
                                {
                                    currentminingAlgosPool.Add(_algo.ToLower());
                                }

                                localProfit = (localHashrate * _coin.adaptive_profit / _coin.adaptive_factor);

                                actualHashrate = actualHashrate + _accepted * (1.00);

                                if (actualHashrate > 0)
                                {
                                    CoinProperty cp = new();
                                    if (!coinsMining.Contains(cp))
                                    {
                                        cp.algorithm = _coin.algo;
                                        cp.symbol = _coin.symbol;
                                        cp.hashrate_shared = actualHashrate;
                                        //Helpers.ConsolePrint("******** adding " + cp.symbol, _symbol + "worker hashrate for " + cp.algorithm);
                                        coinsMining.Add(cp);
                                    }
                                }

                                if (algosProperty.ContainsKey(_coin.algo.ToLower()))
                                {
                                    if (MiningSession._miningDevices.Exists(x => (((AlgorithmType)x.Device.AlgorithmID).ToString().ToLower() == _coin.algo.ToLower()) &&
                                    _coin.symbol.ToLower().Equals(x.Device.Coin.ToLower())))
                                    {
                                        _algoProperty.ticks = algosProperty.FirstOrDefault(x => x.Key.ToLower() == _coin.algo.ToLower()).Value.ticks;
                                    }
                                    if (MiningSession._miningDevices.Exists(x => (((AlgorithmType)x.Device.SecondAlgorithmID).ToString().ToLower() == _coin.algo.ToLower()) &&
                                    _coin.symbol.ToLower().Equals(x.Device.Coin.ToLower())))
                                    {
                                        _algoProperty.ticks = algosProperty.FirstOrDefault(x => x.Key.ToLower() == _coin.algo.ToLower()).Value.ticks;
                                    }
                                    _algoProperty.factorsList = algosProperty.FirstOrDefault(x => x.Key.ToLower() == _coin.algo.ToLower()).Value.factorsList;
                                }
                                _algoProperty.localhashrate = localHashrate;
                                _algoProperty.localProfit = localProfit;
                                _algoProperty.actualhashrate = actualHashrate;
                                _algoProperty.algo = _coin.algo.ToLower();
                                _algoProperty.coin = _coin.symbol.ToLower();
                                _algoProperty.hashrate = _hashrate;
                                _algoProperty.adaptive_factor = _coin.adaptive_factor;

                                if (currentminingAlgos.Contains(_coin.algo.ToLower()))
                                {
                                    algosProperty.AddOrUpdate(_coin.algo.ToLower(), _algoProperty, (k, v) => _algoProperty);
                                }
                            }
                        }
                    }

                    foreach (var __algoProperty in algosProperty)
                    {
                        double average = 1.0;
                        var _algoProperty = __algoProperty.Value;
                        var coin = CoinList.FirstOrDefault(x => (x.algo.ToLower() == __algoProperty.Key.ToLower() &&
                        x.symbol.ToLower() == __algoProperty.Value.coin.ToLower()));

                        //пусть на графике присутствуют предыдущие значени€
                        chart_actualProfit = chart_actualProfit + (_algoProperty.actualhashrate * coin.adaptive_profit / coin.adaptive_factor);

                        //а дл€ расчета только текущие
                        if (!currentminingAlgosPool.Contains(_algoProperty.algo.ToLower()))
                        {
                            //Helpers.ConsolePrint("Stats", "Delete current mining algo: " + _algoProperty.algo);
                            algosProperty.TryRemove(_algoProperty.algo, out var r);
                        }

                        if (_algoProperty.localhashrate > 0)
                        {
                            _algoProperty.ticks++;
                            _algoProperty.actualProfit = (_algoProperty.actualhashrate * coin.adaptive_profit / coin.adaptive_factor);
                            /*
                            if (_algoProperty.ticks >= ConfigManager.GeneralConfig.ticksBeforeAdaptiveStart &&//15 
                                _algoProperty.ticks <= ConfigManager.GeneralConfig.ticksBeforeAdaptiveStart +
                                ConfigManager.GeneralConfig.ticksAdaptiveTuning &&
                                alg.adaptive_factor == 0)
                            {
                                Helpers.ConsolePrint("Stats", alg.name + " adaptive tuning in process");
                                Form_Main.adaptiveRunning = true;
                            }
                            */
                            //if (_algoProperty.factorsList.Count < 100 && _algoProperty.adaptive_factor != 0)
                            if (_algoProperty.factorsList.Count < 30)
                            {
                                for (int i = _algoProperty.factorsList.Count; i < 30; i++)
                                {
                                    if (_algoProperty.adaptive_factor != 0)
                                    {
                                        _algoProperty.factorsList.Add(_algoProperty.adaptive_factor);
                                    }
                                    else
                                    {
                                        _algoProperty.factorsList.Add(0.95);
                                    }
                                }
                            }

                            double factorNow = _algoProperty.actualProfit / _algoProperty.localProfit;

                            if (_algoProperty.ticks >= 15)
                            {
                                _algoProperty.factorsList.Add(factorNow);

                                average = _algoProperty.factorsList.Sum() / _algoProperty.factorsList.Count;
                                if (_algoProperty.ticks > 30)
                                {
                                    //Form_Main.adaptiveRunning = false;
                                    if (!double.IsInfinity(average) && !double.IsNaN(average))
                                    {
                                        if (average < 0.2)
                                        {
                                            average = 0.2;
                                        }
                                        if (average > 1.1)
                                        {
                                            average = 1.1;
                                        }
                                        coin.adaptive_factor = average;
                                        CoinList.RemoveAll(a => (a.symbol.ToLower() == coin.symbol.ToLower()) &&
                                                                (a.algo.ToLower() == coin.algo.ToLower()));
                                        CoinList.Add(coin);
                                    }
                                }
                            }
                        }
                        else
                        {
                            //algosProperty.TryRemove(_algoProperty.name, out var r);
                        }
                    }

                    overallBTC = chart_actualProfit;
                    Form_Main.TotalActualProfitability = overallBTC;
                    Form_Main.lastRigProfit.currentProfitAPI = overallBTC * Algorithm.Mult;

                    lock (fileLock)
                    {
                        CoinList.Sort((x, y) => x.algo.CompareTo(y.algo));
                        var json = JsonConvert.SerializeObject(CoinList, Formatting.Indented);
                        if (json.Length > 5)
                        {
                            Helpers.WriteAllTextWithBackup("configs\\CoinList.json", json);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Helpers.ConsolePrintError("GetWalletBalance", ex.ToString());
            }
            return;
        }

        public static bool emptypool = true;
        [HandleProcessCorruptedStateExceptions]
        public static async Task GetAlgosAsync()
        {
            setAlgos(await GetCoinsAsync(Links.Currencies));
        }

        public static async Task LoadCoinListAsync(bool onlyCached = false)
        {
            List<Coin> coinlist = new List<Coin>();
            try
            {
                if (System.IO.File.Exists("configs\\CoinList.json"))
                {
                    var jsonData = File.ReadAllText("configs\\CoinList.json");
                    if (string.IsNullOrEmpty(jsonData.Trim('\0')))
                    {
                        File.Delete("configs\\CoinList.json");
                    }
                    else
                    {
                        Helpers.ConsolePrint("LoadAlgoritmsList", "Loadind previous CoinList");
                        coinlist = JsonConvert.DeserializeObject<List<Coin>>(jsonData);
                        CoinList = coinlist;
                    }
                }
            }
            catch (Exception ex)
            {
                Helpers.ConsolePrintError("LoadAlgoritmsList", ex.ToString());
            }
            setAlgos(coinlist);
            if (onlyCached) return;
        }
        private static double getMin(double a, double b, double c)
        {
            double min = 0;
            if (a != 0)
            {
                min = a;
            }
            else if (b != 0)
            {
                min = b;
            }

            if (min > a && b != 0) min = a;
            if (min > b && b != 0) min = b;
            if (min > c && c != 0) min = c;
            return min;
        }

        private static List<string> delalgos = new();

        private class MostProfitableCoin
        {
            public string coin { get; set; }
            public double currentProfit { get; set; }
        }
        private static void setAlgos(List<Coin> _coinlist)
        {
            var coinlist = GetMostProfitCoins(_coinlist);
            try
            {
                delalgos.Clear();
                if (coinlist != null && coinlist.Count > 0)
                {
                    foreach (AlgorithmType alg in Enum.GetValues(typeof(AlgorithmType)))
                    {
                        if ((int)alg >= 1 && !alg.ToString().Contains("UNUSED"))
                        {
                            delalgos.Add(alg.ToString().ToLower());
                        }
                    }
                }
                Dictionary<AlgorithmType, MostProfitableCoin> data = new();

                foreach (var coin in coinlist)
                {
                    string AlgorithmName = "";
                    string _AlgorithmName = "";
                    foreach (AlgorithmType _algo in Enum.GetValues(typeof(AlgorithmType)))
                    {
                        AlgorithmName = AlgorithmNames.GetName(_algo);
                        _AlgorithmName = AlgorithmNames.GetName(_algo).Replace("_", "-");
                        if (_algo >= 0 && coin.algo.ToUpper().Equals(AlgorithmName.ToUpper()) ||
                            _algo >= 0 && coin.algo.ToUpper().Equals(_AlgorithmName.ToUpper()))
                        {
                            if (!data.ContainsKey(_algo))
                            {
                                MostProfitableCoin _MostProfitableCoin = new();
                                _MostProfitableCoin.coin = coin.symbol;
                                _MostProfitableCoin.currentProfit = coin.adaptive_profit;
                                if (_MostProfitableCoin.currentProfit > 0)
                                {
                                    data.Add(_algo, _MostProfitableCoin);
                                }
                            }

                            if (delalgos.Exists(e => e.Equals(coin.algo.ToLower()))) delalgos.Remove(coin.algo.ToLower());
                            break;
                        }
                    }
                }

                if (data != null)
                {
                    foreach (var algo in data)
                    {
                        var algoKey = algo.Key;

                        if (algoKey.ToString().Contains("UNUSED"))
                        {
                            continue;
                        }

                        var AlgorithmName = AlgorithmNames.GetName(algoKey);
                        //Helpers.ConsolePrint("** " + algoKey.ToString(), algo.Value.coin + " " + algo.Value.currentProfit.ToString());
                    }
                }

                delalgos.Remove("neoscrypt_xaya");
                delalgos.Remove("xelisv2_pepew");
                SetAlgorithmRates(data, 1, false);
            }
            catch (Exception ex)
            {
                Helpers.ConsolePrintError("setAlgos", ex.ToString());
            }
            AlgosProfitData.FinalizeAlgosProfitList();
        }
       
        public static void CheckNewAlgo()
        {
            List<string> algolist = new();
            foreach (AlgorithmType alg in Enum.GetValues(typeof(AlgorithmType)))
            {
                algolist.Add(alg.ToString().ToLower().Replace("_unused", ""));
            }
            foreach (var algo in miningPoolAlgos)
            {
                if (!algolist.Contains(algo.Replace("-", "_")))
                {
                    Helpers.ConsolePrint("Stats", "New algorithm: " + algo);
                }
            }
        }

        public static ConcurrentDictionary<string, CoinBlocked> coinsBlocked = new();
        public class CoinBlocked
        {
            public string coin;
            public int checkTime;
        }
        private static void SetAlgorithmRates(Dictionary<AlgorithmType, MostProfitableCoin> data, int multipl = 1, 
            bool average = false)
        {
            double paying = 0.0d;
            string coin = "Unknown";
            try
            {
                var payingDict = data;
                if (data != null)
                {
                    foreach (var algo in data)
                    {
                        var algoKey = algo.Key;

                        if (algoKey.ToString().Contains("UNUSED"))
                        {
                            continue;
                        }
                        //Helpers.ConsolePrint("SetAlgorithmRates " + algoKey.ToString(), algo.Value.coin + " " + algo.Value.currentProfit.ToString());
                        var AlgorithmName = AlgorithmNames.GetName(algoKey);
                        paying = algo.Value.currentProfit;
                        coin = algo.Value.coin;
                        AlgosProfitData.UpdatePayingForAlgo(algoKey, coin, paying, true);
                    }
                }
                //testing
                //payingDict[AlgorithmType.RandomX] = 12345;
                //NHSmaData.UpdateSmaPaying(payingDict, average);
            }
            catch (Exception e)
            {
                Helpers.ConsolePrint("SetAlgorithmRates", e.ToString());
            }
        }
    }
}



