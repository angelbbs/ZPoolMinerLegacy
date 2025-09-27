namespace ZPoolMinerLegacy.Common.Enums
{
    public enum AlgorithmType
    {

        // dual algos for grouping
        ZIL = -200,
        KawPowLite = -100,

        EthashSHA256dt = -19,
        Ethashb3SHA256dt = -18,
        Ethashb3SHA512256d = -17,
        KarlsenHashV2HooHash = -13,
        EthashSHA512256d = -12,

        INVALID = -2,
        NONE = -1,

        Empty = 0,

        sha256_UNUSED = 100,
        lyra2z_UNUSED = 101,
        equihash_UNUSED = 102,
        qubit_UNUSED = 103,
        myr_gr_UNUSED = 104,
        scrypt_UNUSED = 105,
        skein_UNUSED = 106,
        token_UNUSED = 107,
        x11_UNUSED = 108,
        quark_UNUSED = 109,
        groestl_UNUSED = 110,
        sib_UNUSED = 111,
        kheavyhash_UNUSED = 112,
        lbry_UNUSED = 113,
        lyra2v2_UNUSED = 114,
        x13_UNUSED = 115,
        blake2s_UNUSED = 116,
        x16r_UNUSED = 117,

        Allium = 1000,
        Anime_UNUSED = 1002,
        Argon2d500_UNUSED = 1004,
        Argon2d1000_UNUSED = 1005,
        Argon2d16000 = 1006,
        Balloon = 1008,//T-Rex, SRBMiner?
        BMW512_UNUSED = 1010,
        Curve = 1050,//curvehash
        Equihash125 = 1070,
        Equihash144 = 1080,
        Equihash192 = 1090,
        Flex = 1120,
        FiroPow = 1130,
        Ghostrider = 1150,
        HeavyHash = 1160,
        Hmq1725 = 1165,//CryptoDredge, T-Rex?
        Honeycomb = 1169,//TT-Miner, T-Rex?
        Interchained = 1180,
        KawPow = 1200,
        Keccakc = 1210,
        Lyra2z330_UNUSED = 1215,
        M7m = 1219,//SRBMiner?
        Megabtx = 1220,
        Megamec = 1222,//T-Rex?
        MeowPow = 1225,
        Meraki = 1230,//1gb
        Mike = 1240,
        Minotaurx = 1250,
        NeoScrypt = 1260,
        Neoscrypt_xaya = 1270,
        Odocrypt_UNUSED = 1280,
        Phi2 = 1291,//CryptoDredge,TeamRedMiner, Z-Enemy
        PhiHash = 1293,
        Power2b = 1295,
        //ProgPowZ = 1297,
        //RandomX = 1310,
        RinHash = 1335,
        Sccpow = 1338,//SRBMiner, GMiner, NanoMiner
        Scryptn2 = 1340, //https://github.com/JayDDee/cpuminer-opt
        //SHA3d = 1360,
        SHA256csm = 1370,
        SHA512256d = 1390,
        Skein2_UNUSED = 1393,
        Skydoge = 1395,//TT-Miner
        Timetravel = 1396,//T-Rex, Z-Enemy
        Tribus = 1397,//T-Rex, Z-Enemy, Cryptodredge
        VertHash = 1400,//1.5gb
        VerusHash = 1410,//1gb
        Whirlpool = 1420,
        X16RT = 1430,
        X16RV2 = 1440,
        X21S = 1450,
        X25X = 1460,
        Xelisv2_Pepew = 1470,
        Yescrypt = 1480,
        YescryptR8 = 1490,
        YescryptR16 = 1500,
        YescryptR32 = 1510,
        Yespower = 1520,
        YespowerADVC = 1522,
        YespowerEQPAY = 1526,
        YespowerLTNCG = 1530,
        YespowerMGPC = 1540,
        YespowerR16 = 1550,//SRBMiner 0.4.7
        YespowerSUGAR = 1560,
        YespowerTIDE = 1570,
        YespowerURX = 1580
    }
}
