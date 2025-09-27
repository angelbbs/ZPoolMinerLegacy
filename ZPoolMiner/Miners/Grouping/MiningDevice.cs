using ZPoolMiner.Algorithms;
using ZPoolMiner.Configs;
using ZPoolMiner.Devices;
using ZPoolMinerLegacy.Common.Enums;
using System;
using System.Collections.Generic;
using ZPoolMiner.Switching;
using System.Linq;

namespace ZPoolMiner.Miners.Grouping
{
    public class MiningDevice
    {
        public MiningDevice(ComputeDevice device)
        {
            Device = device;
            foreach (var algo in Device.GetAlgorithmSettings())
            {
                var isAlgoMiningCapable = GroupSetupUtils.IsAlgoMiningCapable(algo);
                var isValidMinerPath = MinerPaths.IsValidMinerPath(algo.MinerBinaryPath);
                if (isAlgoMiningCapable && isValidMinerPath)
                {
                    Algorithms.Add(algo);
                }
            }

            MostProfitableAlgorithmType = AlgorithmType.NONE;
            DeviceMostProfitableCoin = "noNe";
            MostProfitableMinerBaseType = MinerBaseType.NONE;
        }

        public ComputeDevice Device { get; }
        public List<Algorithm> Algorithms = new List<Algorithm>();
        public double diff = 0d;
        public double diffPercent = 0d;
        public double CurrentProfit = 0d;
        public bool needSwitch;
        public string GetMostProfitableString()
        {
            return
                Enum.GetName(typeof(MinerBaseType), MostProfitableMinerBaseType)
                + "_"
                + Enum.GetName(typeof(AlgorithmType), MostProfitableAlgorithmType);
        }

        public string GetCurrentProfitableString()
        {
            return
                Enum.GetName(typeof(MinerBaseType), CurrentProfitableMinerBaseType)
                + "_"
                + Enum.GetName(typeof(AlgorithmType), CurrentProfitableAlgorithmType);
        }

        public AlgorithmType MostProfitableAlgorithmType { get; private set; }
        public string DeviceMostProfitableCoin { get; set; }

        public MinerBaseType MostProfitableMinerBaseType { get; private set; }

        // prev state
        public AlgorithmType CurrentProfitableAlgorithmType { get; private set; }
        public string DeviceCurrentMiningCoin { get; set; }

        public string CoinChange { get; set; } = "";

        public MinerBaseType CurrentProfitableMinerBaseType { get; private set; }

        private int GetMostProfitableIndex()
        {
            return Algorithms.FindIndex((a) =>
                a.DualZPoolID == MostProfitableAlgorithmType && a.MinerBaseType == MostProfitableMinerBaseType);
        }

        private int GetCurrentProfitableIndex()
        {
            return Algorithms.FindIndex((a) =>
                a.DualZPoolID == CurrentProfitableAlgorithmType && a.MinerBaseType == CurrentProfitableMinerBaseType);
        }

        private int GetMostProfitableCoinIndex()
        {
            return Algorithms.FindIndex((a) =>
                a.DualZPoolID == MostProfitableAlgorithmType && a.MinerBaseType == MostProfitableMinerBaseType);
        }

        private int GetCurrentProfitableCoinIndex()
        {
            return Algorithms.FindIndex((a) =>
                a.DualZPoolID == CurrentProfitableAlgorithmType && a.MinerBaseType == CurrentProfitableMinerBaseType);
        }

        public double GetMostProfitValueWithoutPower
        {
            get
            {
                var mostProfitableIndex = GetMostProfitableIndex();
                if (mostProfitableIndex > -1)
                {
                    return Algorithms[mostProfitableIndex].MostProfit;
                }
                return 0;
            }
        }
        public double GetMostProfitValueWithPower
        {
            get
            {
                var mostProfitableIndex = GetMostProfitableIndex();
                if (mostProfitableIndex > -1)
                {
                    return Algorithms[mostProfitableIndex].MostProfitWithPower;
                }
                return 0;
            }
        }
        public double GetCurrentProfitValue
        {
            get
            {
                var currentProfitableIndex = GetCurrentProfitableIndex();
                if (currentProfitableIndex > -1)
                {
                    return Algorithms[currentProfitableIndex].CurrentProfit;
                }
                else
                {
                    return 0;
                }
                
                return 0;
            }
        }

        public string GetCurrentProfitCoin
        {
            get
            {
                var currentProfitableIndex = GetCurrentProfitableCoinIndex();
                if (currentProfitableIndex > -1)
                {
                    try
                    {
                        return Algorithms.FindLast(a => a.DualZPoolID == CurrentProfitableAlgorithmType).CurrentMiningCoin;
                    }
                    catch (Exception ex)
                    {
                        return "None";
                    }
                }

                return "None";
            }
        }
        public string GetMostProfitCoin
        {
            get
            {
                var mostProfitableIndex = GetMostProfitableCoinIndex();
                if (mostProfitableIndex > -1)
                {
                    //return Algorithms[mostProfitableIndex].MostProfitCoin;
                    return Algorithms.FindLast(a => a.DualZPoolID == MostProfitableAlgorithmType).MostProfitCoin;
                }
                return "None";
            }
        }
        public double GetCurrentProfitValueWithPower
        {
            get
            {
                double p = 0d;
                var currentProfitableIndex = GetCurrentProfitableIndex();
                if (currentProfitableIndex > -1)
                {
                   // Helpers.ConsolePrint("---", CurrentProfitableAlgorithmType.ToString() + " (" +
                     //   DeviceCurrentMiningCoin + ") " + p.ToString() + " " + 
                       // Algorithms[currentProfitableIndex].CurrentProfitWithoutPower.ToString());
                    return Algorithms[currentProfitableIndex].CurrentProfitWithPower;
                }
                return 0;
            }
        }
        public MiningPair GetMostProfitablePair()
        {
            return new MiningPair(Device, Algorithms[GetMostProfitableIndex()]);
        }
        public MiningPair GetCurrentProfitablePair()
        {
            return new MiningPair(Device, Algorithms[GetCurrentProfitableIndex()]);
        }

        public bool HasProfitableAlgo()
        {
            return GetMostProfitableIndex() > -1;
        }
        public void RestoreOldProfitsState()
        {
            // restore last state
            MostProfitableAlgorithmType = CurrentProfitableAlgorithmType;
            MostProfitableMinerBaseType = CurrentProfitableMinerBaseType;
            DeviceMostProfitableCoin = DeviceCurrentMiningCoin;
            diff = 0;
            diffPercent = 0;
            needSwitch = false;
        }

        public void SetNotMining()
        {
            // device isn't mining (e.g. below profit threshold) so set state to none
            CurrentProfitableAlgorithmType = AlgorithmType.NONE;
            DeviceCurrentMiningCoin = "noNe";
            CurrentProfitableMinerBaseType = MinerBaseType.NONE;
            MostProfitableAlgorithmType = AlgorithmType.NONE;
            DeviceMostProfitableCoin = "noNe";
            MostProfitableMinerBaseType = MinerBaseType.NONE;
        }


        public void CalculateProfits(Dictionary<AlgorithmType, AlgorithmSwitchingManager.MostProfitableCoin> profits)
        {
            //string coin = null;
            if (MiningSetup._MiningPairs is object && MiningSetup._MiningPairs != null)

            // save last state
            CurrentProfitableAlgorithmType = MostProfitableAlgorithmType;
            CurrentProfitableMinerBaseType = MostProfitableMinerBaseType;
            // assume none is profitable
            MostProfitableAlgorithmType = AlgorithmType.NONE;
            MostProfitableMinerBaseType = MinerBaseType.NONE;

            foreach (var c in MiningSession.DevicesCoinList)
            {
                if (c._Algorithm.Equals(CurrentProfitableAlgorithmType) ||
                    c._DualAlgorithm.Equals(CurrentProfitableAlgorithmType))
                {
                    DeviceCurrentMiningCoin = c._Coin;
                }
            }

            try
            {
                // calculate new profits
                foreach (var algo in Algorithms)
                {
                    algo.UpdateCurProfit(profits, algo.DeviceType, algo.MinerBaseType);
                }

                // find max paying value and save key
                double maxProfit = 0;
                if (ConfigManager.GeneralConfig.Force_mining_if_nonprofitable)
                {
                    maxProfit = -1000000000000000;
                }

                foreach (var algo in Algorithms)
                {
                    if (maxProfit < algo.MostProfit)
                    {
                        maxProfit = algo.MostProfit;
                        MostProfitableAlgorithmType = algo.DualZPoolID;

                        algo.MostProfitCoin = profits[algo.PrimaryAlgorithmPoolID].coin;

                        if (string.IsNullOrEmpty(DeviceCurrentMiningCoin))
                        {
                            DeviceCurrentMiningCoin = "noNE";
                        }

                        if (!algo.CurrentMiningCoin.ToLower().Equals("none"))//переключение на новый алгоритм
                        {
                            /*
                            if (coin == null)
                            {
                                DeviceCurrentMiningCoin = algo.CurrentMiningCoin;//nonE????????
                            }
                            else
                            {
                                DeviceCurrentMiningCoin = coin;
                            }
                            */
                            //algo.CurrentMiningCoin = DeviceCurrentMiningCoin;
                        }
                        if (CurrentProfitableAlgorithmType == algo.PrimaryAlgorithmPoolID || CurrentProfitableAlgorithmType == algo.DualZPoolID)
                        {
                            algo.CurrentMiningCoin = DeviceCurrentMiningCoin;
                        }
                        if (algo is DualAlgorithm algoDual)
                        {
                            
                            algo.MostProfitCoin = profits[algo.PrimaryAlgorithmPoolID].coin + "+" + profits[algo.SecondaryAlgorithmPoolID].coin;
                        }
                        DeviceMostProfitableCoin = algo.MostProfitCoin;
                        MostProfitableAlgorithmType = algo.DualZPoolID;
                        MostProfitableMinerBaseType = algo.MinerBaseType;
                    }

                    if (algo.Forced)
                    {
                        maxProfit = algo.CurrentProfit;
                        if (maxProfit == 0)
                        {
                            algo.CurrentProfit = 0.001;
                            algo.MostProfit = 0.001;
                            algo.MostProfitWithPower = 0.001;
                        }
                        MostProfitableAlgorithmType = algo.DualZPoolID;

                        algo.MostProfitCoin = profits[algo.PrimaryAlgorithmPoolID].coin;

                        if (string.IsNullOrEmpty(DeviceCurrentMiningCoin))
                        {
                            DeviceCurrentMiningCoin = "noNE";
                        }
                        if (!algo.CurrentMiningCoin.ToLower().Equals("none"))//переключение на новый алгоритм
                        {
                            /*
                            if (coin == null)
                            {
                                DeviceCurrentMiningCoin = algo.CurrentMiningCoin;//nonE????????
                            }
                            else
                            {
                                DeviceCurrentMiningCoin = coin;
                            }
                            */
                            //algo.CurrentMiningCoin = DeviceCurrentMiningCoin;
                        }
                        algo.CurrentMiningCoin = DeviceCurrentMiningCoin;

                        if (algo is DualAlgorithm algoDual)
                        {
                            algo.MostProfitCoin = profits[algo.PrimaryAlgorithmPoolID].coin + "+" + profits[algo.SecondaryAlgorithmPoolID].coin;
                        }
                        DeviceMostProfitableCoin = algo.MostProfitCoin;
                        MostProfitableAlgorithmType = algo.DualZPoolID;
                        MostProfitableMinerBaseType = algo.MinerBaseType;

                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Helpers.ConsolePrintError("CalculateProfits", ex.ToString());
            }
        }
    }
}
