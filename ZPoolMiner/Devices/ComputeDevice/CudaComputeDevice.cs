using ManagedCuda.Nvml;
using ZPoolMiner.Configs;
using ZPoolMiner.Devices.Algorithms;
using ZPoolMiner.Forms;
using ZPoolMinerLegacy.Common.Enums;
using NVIDIA.NVAPI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;


namespace ZPoolMiner.Devices
{
    [Serializable]
    internal class CudaComputeDevice : ComputeDevice
    {
        private readonly NvPhysicalGpuHandle _nvHandle; // For NVAPI
        private readonly nvmlDevice _nvmlDevice; // For NVML
        private readonly CudaDevices2 _cudaDevice; // For NVML
        private const int GpuCorePState = 0; // memcontroller = 1, videng = 2
        protected int SMMajor;
        protected int SMMinor;
        public readonly bool ShouldRunEthlargement;
        private int errorcount = 0;
        public override float Load
        {
            get
            {
                if (ConfigManager.GeneralConfig.DisableMonitoringNVIDIA)
                {
                    return -1;
                }
                try
                {
                    foreach (var d in Form_Main.gpuList)
                    {
                        if (_cudaDevice.DeviceID == d.nGpu)
                        {
                            return d.load;
                        }
                    }
                }
                catch (Exception)
                {
                    //Helpers.ConsolePrint("NVML", e.ToString());
                }
                return -1;
            }
        }
        public override float MemLoad
        {
            get
            {
                if (ConfigManager.GeneralConfig.DisableMonitoringNVIDIA)
                {
                    return -1;
                }
                try
                {
                    foreach (var d in Form_Main.gpuList)
                    {
                        if (_cudaDevice.DeviceID == d.nGpu)
                        {
                            return d.loadMem;
                        }
                    }
                }
                catch (Exception)
                {
                    //Helpers.ConsolePrint("NVML", e.ToString());
                }
                return -1;
            }
        }

        public override float Temp
        {
            get
            {
                if (ConfigManager.GeneralConfig.DisableMonitoringNVIDIA)
                {
                    return -1;
                }
                try
                {
                    foreach (var d in Form_Main.gpuList)
                    {
                        if (_cudaDevice.DeviceID == d.nGpu)
                        {
                            return d.temp;
                        }
                    }
                }
                catch (Exception e)
                {
                    Helpers.ConsolePrint("NVML", e.ToString());
                }
                return -1;
            }
        }

        public override float TempMemory
        {
            get
            {
                if (ConfigManager.GeneralConfig.DisableMonitoringNVIDIA || Form_Main.NvAPIerror)
                {
                    return -1;
                }
                try
                {
                    foreach (var d in Form_Main.gpuList)
                    {
                        if (_cudaDevice.DeviceID == d.nGpu)
                        {
                            return d.tempMem;
                        }
                    }
                }
                catch (Exception e)
                {
                    Helpers.ConsolePrint("NVML", e.ToString());
                }
                return -1;
            }
        }

        public override int FanSpeed //percent
        {
            get
            {
                if (ConfigManager.GeneralConfig.DisableMonitoringNVIDIA)
                {
                    return -1;
                }

                try
                {
                    foreach (var d in Form_Main.gpuList)
                    {
                        if (_cudaDevice.DeviceID == d.nGpu)
                        {
                            return (int)d.fan;
                        }
                    }
                }
                catch (Exception e)
                {
                    Helpers.ConsolePrint("NVML", e.ToString());
                }
                return -1;
            }
        }

        public override int FanSpeedRPM
        {
            get
            {
                if (ConfigManager.GeneralConfig.DisableMonitoringNVIDIA ||
                    (Form_Main.NvAPIerror))
                {
                    return -1;
                }

                try
                {
                    foreach (var d in Form_Main.gpuList)
                    {
                        if (_cudaDevice.DeviceID == d.nGpu)
                        {
                            return (int)d.fanRPM;
                        }

                    }
                }
                catch (Exception e)
                {
                    Helpers.ConsolePrint("NVML", e.ToString());
                }
                return -1;
            }
        }

        public override double PowerUsage
        {
            get
            {
                if (ConfigManager.GeneralConfig.DisableMonitoringNVIDIA)
                {
                    return -1;
                }

                try
                {
                    foreach (var d in Form_Main.gpuList)
                    {
                        if (_cudaDevice.DeviceID == d.nGpu)
                        {
                            if (d.power > 1000)
                            {
                                return d.power / 1000;
                            }
                            else
                            {
                                return d.power;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Helpers.ConsolePrint("NVML", e.ToString());
                }
                return -1;
            }
        }
        public CudaComputeDevice(CudaDevices2 cudaDevice, DeviceGroupType group, int gpuCount,
            NvPhysicalGpuHandle nvHandle, nvmlDevice nvmlHandle)
            : base((int)cudaDevice.DeviceID,
                cudaDevice.GetName(),
                true,
                group,
                cudaDevice.IsEtherumCapable(),
                DeviceType.NVIDIA,
                string.Format(International.GetText("ComputeDevice_Short_Name_NVIDIA_GPU"), gpuCount),
                cudaDevice.DeviceGlobalMemory, cudaDevice.CUDAManufacturer, cudaDevice.MonitorConnected, cudaDevice.NvidiaLHR)
        {
            BusID = cudaDevice.pciBusID;
            SMMajor = cudaDevice.SM_major;
            SMMinor = cudaDevice.SM_minor;
            Uuid = cudaDevice.UUID;
            AlgorithmSettings = GroupAlgorithms.CreateForDeviceList(this);
            Index = ID + ComputeDeviceManager.Available.AvailCpus; // increment by CPU count

            _nvHandle = nvHandle;
            _nvmlDevice = nvmlHandle;
            _cudaDevice = cudaDevice;
            ShouldRunEthlargement = cudaDevice.DeviceName.Contains("1080") || cudaDevice.DeviceName.Contains("Titan Xp");
            Form_Main.ShouldRunEthlargement = ShouldRunEthlargement;
        }
    }
}
