using ZPoolMinerLegacy.Common.Enums;
using System;

namespace ZPoolMiner
{
    /// <summary>
    /// AlgorithmNames class is just a data container for mapping JSON API names to algo type
    /// </summary>
    public static class AlgorithmNames
    {
        public static string GetName(AlgorithmType type)
        {
            return Enum.GetName(typeof(AlgorithmType), type);
        }
    }
}
