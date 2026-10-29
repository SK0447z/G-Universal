using System;
using System.Drawing;
using System.Management;

namespace GUniversal
{
    public class DellProvider : ILaptopProvider
    {
        private const string WmiNamespace = @"root\WMI";
        private const string WmiClass = "AWCCWmiMethodFunction";

        public string ProviderName => "Dell Alienware / G-Series WMI Controller";
        public bool IsConnected { get; private set; }
        public string ConnectionType { get; private set; } = "Disconnected";

        public bool SupportsPerformanceModes => true;
        public bool SupportsBatteryLimit => false;
        public bool SupportsKeyboardRgb => false;
        public bool SupportsKeyboardBrightness => false;
        public bool SupportsFanRpm => true;

        private int _lastKnownPerfMode = 0;

        public DellProvider()
        {
            InitializeConnection();
        }

        private void InitializeConnection()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(WmiNamespace, $"SELECT * FROM {WmiClass}");
                foreach (ManagementObject _ in searcher.Get())
                {
                    IsConnected = true;
                    ConnectionType = @"root\WMI (AWCCWmiMethodFunction)";
                    return;
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }

            IsConnected = false;
            ConnectionType = "Dell AWCC WMI Interface Not Found";
        }

        // AWCC Thermal_Control: (profileCode << 8) | 0x01
        public int GetPerformanceMode() => _lastKnownPerfMode;

        public int SetPerformanceMode(int mode)
        {
            try
            {
                // Mode 2 = Silent (0x96, 0xA3)
                // Mode 0 = Balanced (0x97, 0xA0)
                // Mode 1 = Turbo / G-Mode (0xAB, 0x99, 0xA4)
                uint[] profileCodes = mode switch
                {
                    2 => new uint[] { 0x96, 0xA3 },
                    1 => new uint[] { 0xAB, 0x99, 0xA4 },
                    _ => new uint[] { 0x97, 0xA0 }
                };

                foreach (uint code in profileCodes)
                {
                    uint arg2 = (code << 8) | 0x01;
                    int res = CallDellThermalControl(arg2);
                    if (res == 0)
                    {
                        _lastKnownPerfMode = mode;
                        return 1;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }

            return -1;
        }

        // Thermal_Information: (sensorId << 8) | command
        public int GetCpuTemperature()
        {
            uint res = CallDellThermalInfo((0x01 << 8) | 0x04);
            return (res > 0 && res <= 130) ? (int)res : -1;
        }

        public int GetGpuTemperature()
        {
            uint res = CallDellThermalInfo((0x06 << 8) | 0x04);
            return (res > 0 && res <= 130) ? (int)res : -1;
        }

        public int GetCpuFanSpeed()
        {
            uint res = CallDellThermalInfo((0x32 << 8) | 0x05);
            return (res > 0 && res != 0xFFFFFFFF) ? (int)res : 0;
        }

        public int GetGpuFanSpeed()
        {
            uint res = CallDellThermalInfo((0x33 << 8) | 0x05);
            return (res > 0 && res != 0xFFFFFFFF) ? (int)res : 0;
        }

        public int GetKeyboardBrightness() => 0;
        public int SetKeyboardBrightness(int level) => -1;
        public (string Name, int ModeId)[] GetSupportedRgbModes() => Array.Empty<(string Name, int ModeId)>();
        public int SetKeyboardRgb(int mode, Color color, int speed = 1) => -1;
        public int SetTufKeyboardRGB(int mode, Color color, int speed = 1) => -1;
        public int SetTufKeyboardPower(uint power = 0x00FF01BD) => -1;
        public int SetBatteryChargeLimit(int limitPercent) => -1;

        private int CallDellThermalControl(uint arg2)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(WmiNamespace, $"SELECT * FROM {WmiClass}");
                foreach (ManagementObject instance in searcher.Get())
                {
                    ManagementBaseObject inParams = instance.GetMethodParameters("Thermal_Control");
                    inParams["arg2"] = arg2;
                    ManagementBaseObject outParams = instance.InvokeMethod("Thermal_Control", inParams, null);
                    if (outParams != null && outParams["returnValue"] != null)
                    {
                        return Convert.ToInt32(outParams["returnValue"]);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
            return -1;
        }

        private uint CallDellThermalInfo(uint arg2)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(WmiNamespace, $"SELECT * FROM {WmiClass}");
                foreach (ManagementObject instance in searcher.Get())
                {
                    ManagementBaseObject inParams = instance.GetMethodParameters("Thermal_Information");
                    inParams["arg2"] = arg2;
                    ManagementBaseObject outParams = instance.InvokeMethod("Thermal_Information", inParams, null);
                    if (outParams != null && outParams["returnValue"] != null)
                    {
                        return Convert.ToUInt32(outParams["returnValue"]);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
            return 0xFFFFFFFF;
        }

        public void Dispose() { }
    }
}

