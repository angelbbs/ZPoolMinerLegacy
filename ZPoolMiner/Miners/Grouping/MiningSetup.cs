using ZPoolMinerLegacy.Common.Enums;
using System.Collections.Generic;

namespace ZPoolMiner.Miners.Grouping
{
    public class MiningSetup
    {
        public List<MiningPair> MiningPairs { get; }
        public static List<MiningPair> _MiningPairs { get; set; }
        public string MinerPath { get; }
        public string MinerName { get; }
        public string AlgorithmName { get; }
        public DeviceType DeviceType { get; }
        public AlgorithmType CurrentAlgorithmType { get; }
        public AlgorithmType CurrentSecondaryAlgorithmType { get; }
        public bool IsInit { get; }

        public MiningSetup(List<MiningPair> miningPairs)
        {
            IsInit = false;
            CurrentAlgorithmType = AlgorithmType.NONE;
            if (miningPairs == null || miningPairs.Count <= 0) return;
            MiningPairs = miningPairs;
            MiningPairs.Sort((a, b) => a.Device.ID - b.Device.ID);
            MinerName = miningPairs[0].Algorithm.MinerBaseTypeName;
            AlgorithmName = miningPairs[0].Algorithm.AlgorithmName;
            CurrentAlgorithmType = miningPairs[0].Algorithm.PrimaryAlgorithmPoolID;
            DeviceType = miningPairs[0].Algorithm.DeviceType;
            CurrentSecondaryAlgorithmType = miningPairs[0].Algorithm.SecondaryAlgorithmPoolID;
            MinerPath = miningPairs[0].Algorithm.MinerBinaryPath;
            IsInit = MinerPaths.IsValidMinerPath(MinerPath);
            _MiningPairs = miningPairs;
        }
    }
}
