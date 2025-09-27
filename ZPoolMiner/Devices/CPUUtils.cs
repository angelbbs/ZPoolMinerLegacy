using System;
using System.Collections;
using System.Collections.Generic;
using System.Management;
using ZPoolMinerLegacy.Common.Enums;

namespace ZPoolMiner.Devices
{
    public static class CpuUtils
    {
        // this is the order we check and initialize if automatic
        private static CpuExtensionType[] _detectOrder =
        {
            CpuExtensionType.AVX2_AES,
            CpuExtensionType.AVX2,
            CpuExtensionType.AVX_AES,
            CpuExtensionType.AVX,
            CpuExtensionType.AES,
            CpuExtensionType.SSE2, // disabled
        };

        private static bool HasAvxSupport()
        {
            try
            {
                return (GetEnabledXStateFeatures() & 4) != 0;
            }
            catch
            {
                return false;
            }
        }
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern long GetEnabledXStateFeatures();

        /// <summary>
        /// Checks if CPU mining is capable, CPU must have AES support
        /// </summary>
        /// <returns></returns>
        public static bool IsCpuMiningCapable()
        {
            //return HasExtensionSupport(CpuExtensionType.SSE2);
            //return HasAvxSupport();
            return true;
        }
        public static int PhysicalProcessorCount()
        {
            ArrayList processors = new ArrayList();

            foreach (ManagementObject mo in new ManagementClass("Win32_Processor").GetInstances())
            {
                string id = (string)mo.Properties["SocketDesignation"].Value;

                if (!processors.Contains(id))
                    processors.Add(id);
            }

            return processors.Count;
        }

        public static int GetCoresCount()
        {
            int coreCount = 0;
            foreach (var item in new System.Management.ManagementObjectSearcher("Select * from Win32_Processor").Get())
            {
                coreCount += int.Parse(item["NumberOfCores"].ToString());
            }
            return coreCount;
        }

        public static string GetCpuManufacturer()
        {
            try
            {
                ManagementObjectSearcher searcher =
                    new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_Processor");
                foreach (ManagementObject queryObj in searcher.Get())
                {
                    return queryObj["Manufacturer"].ToString();
                }
            }
            catch (Exception e)
            {
                return "Unknown";
            }
            return "Unknown";
        }

        public static string GetCpuName()
        {
            string CPUName = Convert.ToString(Microsoft.Win32.Registry.GetValue("HKEY_LOCAL_MACHINE\\HARDWARE\\DESCRIPTION\\SYSTEM\\CentralProcessor\\0", "ProcessorNameString", null));
            return CPUName;
        }

        public static string GetCpuName0()
        {
            string Description = "";
            ManagementClass myManagementClass = new ManagementClass("Win32_Processor");
            ManagementObjectCollection myManagementCollection = myManagementClass.GetInstances();
            PropertyDataCollection myProperties = myManagementClass.Properties;
            Dictionary<string, object> myPropertyResults = new Dictionary<string, object>();

            foreach (var obj in myManagementCollection)
            {
                foreach (var myProperty in myProperties)
                {
                    if (myProperty.Name.Equals("Name"))
                    {
                        return obj.Properties[myProperty.Name].Value.ToString();
                    }
                    if (myProperty.Name.Equals("Description"))
                    {
                        Description = obj.Properties[myProperty.Name].Value.ToString();
                    }
                    //myPropertyResults.Add(myProperty.Name,
                    // obj.Properties[myProperty.Name].Value);
                }
            }
            return Description;

        }
    }
}
