using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZPoolMiner.Configs;

namespace ZPoolMiner.Wallets
{
    [Serializable]
    public static class Wallets
    {
        public class WalletData
        {
            public bool Use;
            public string Coin;
            public double Treshold;
            public string Wallet;
            public string ID;
        }
        public static List<WalletData> WalletDataList = new List<WalletData>();
        public static List<PayoutCoin> CoinsList = new List<PayoutCoin>();
        public class PayoutCoin
        {
            public string Coin;
            public string Name;
            public double Treshold;
        }
        public static void InitCoinsList()
        {
            CoinsList.Clear();
            PayoutCoin payoutCoin = new PayoutCoin();

            payoutCoin = new PayoutCoin();
            payoutCoin.Coin = "BTC";
            payoutCoin.Name = "Bitcoin";
            payoutCoin.Treshold = 0;
            CoinsList.Add(payoutCoin);

            payoutCoin = new PayoutCoin();
            payoutCoin.Coin = "DASH";
            payoutCoin.Name = "Dash";
            payoutCoin.Treshold = 0;
            CoinsList.Add(payoutCoin);

            payoutCoin = new PayoutCoin();
            payoutCoin.Coin = "DGB";
            payoutCoin.Name = "Digibyte";
            payoutCoin.Treshold = 0;
            CoinsList.Add(payoutCoin);

            payoutCoin = new PayoutCoin();
            payoutCoin.Coin = "FLUX";
            payoutCoin.Name = "Flux";
            payoutCoin.Treshold = 0;
            CoinsList.Add(payoutCoin);

            payoutCoin = new PayoutCoin();
            payoutCoin.Coin = "LTC";
            payoutCoin.Name = "Litecoin";
            payoutCoin.Treshold = 0;
            CoinsList.Add(payoutCoin);

            payoutCoin = new PayoutCoin();
            payoutCoin.Coin = "RVN";
            payoutCoin.Name = "Ravencoin";
            payoutCoin.Treshold = 0;
            CoinsList.Add(payoutCoin);

        }
        public static void InitWallets()
        {
            InitCoinsList();
            try
            {
                if (File.Exists("configs\\wallets.json"))
                {
                    string json = File.ReadAllText("configs\\wallets.json");
                    WalletDataList = JsonConvert.DeserializeObject<List<WalletData>>(json);
                    string _coin = "";
                    string _wallet = "";
                    string _worker = "";
                    foreach(var v in Enumerable.Reverse(WalletDataList).ToList())
                    {
                        var _c = CoinsList.Find(c => c.Coin.ToLower() == v.Coin.ToLower());
                        if (_c == null)
                        {
                            WalletDataList.Remove(v);
                        } else
                        {
                            _coin = v.Coin;
                            _wallet = v.Wallet;
                            _worker = v.ID;
                            if (v.Use)
                            {
                                Form_Main._demoMode = false;
                                ConfigManager.GeneralConfig.Wallet = v.Wallet;
                                ConfigManager.GeneralConfig.PayoutCurrency = v.Coin;
                                ConfigManager.GeneralConfig.WorkerName = v.ID;
                            }
                        }
                    }
                    if (string.IsNullOrEmpty(ConfigManager.GeneralConfig.PayoutCurrency))
                    {
                        Form_Main._demoMode = true;
                        //ConfigManager.GeneralConfig.Wallet = _wallet;
                        //ConfigManager.GeneralConfig.PayoutCurrency = _coin;
                        //ConfigManager.GeneralConfig.WorkerName = _worker;
                    }
                    ConfigManager.GeneralConfigFileCommit();
                    WalletDataList.Sort((x, y) => x.Coin.CompareTo(y.Coin));
                    string _json = JsonConvert.SerializeObject(WalletDataList, Formatting.Indented);
                    Helpers.WriteAllTextWithBackup("configs\\wallets.json", _json);
                }
            } catch (Exception ex)
            {
                Helpers.ConsolePrint("InitWallets", ex.ToString());
            }
        }
    }
}
