using System;
using System.Drawing;
using System.Management;
using HidSharp;

namespace GUniversal
{
    public class LenovoProvider : ILaptopProvider
    {
        private const string WmiNamespace = @"root\WMI";
        private const string WmiClass = "LENOVO_GAMEZONE_DATA";

        // Lenovo Legion ITE USB HID controllers (VID: 0x048D)
        private static readonly int[] LenovoIteProductIds = new int[] { 0xC955, 0xC965, 0xC975, 0xC985 };

        public string ProviderName => "Lenovo GameZone / ITE Controller";
        public bool IsConnected { get; private set; }
        public string ConnectionType { get; private set; } = "Disconnected";

        public bool SupportsPerformanceModes => true;
        public bool SupportsBatteryLimit => true;
        public bool SupportsKeyboardRgb => true;
        public bool SupportsKeyboardBrightness => true;
        public bool SupportsFanRpm => true;

        private int _lastKnownPerfMode = 0;
        private int _lastKnownBrightness = 2;

        public LenovoProvider()
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
                    ConnectionType = @"root\WMI (LENOVO_GAMEZONE_DATA)";
                    return;
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }

            IsConnected = false;
            ConnectionType = "Lenovo GameZone Not Found";
        }

        // Lenovo SmartFan: 1=Quiet, 2=Balanced, 3=Performance
        public int GetPerformanceMode()
        {
            try
            {
                int rawMode = CallLenovoMethod("GetSmartFanMode");
                int mapped = rawMode switch
                {
                    1 => 2, // Silent
                    2 => 0, // Balanced
                    3 => 1, // Turbo
                    _ => _lastKnownPerfMode
                };
                _lastKnownPerfMode = mapped;
                return mapped;
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                return _lastKnownPerfMode;
            }
        }

        public int SetPerformanceMode(int mode)
        {
            try
            {
                uint lenovoMode = mode switch
                {
                    2 => 1, // Quiet
                    1 => 3, // Performance
                    _ => 2  // Balanced
                };

                int res = CallLenovoSetMethod("SetSmartFanMode", lenovoMode);
                if (res >= 0)
                {
                    _lastKnownPerfMode = mode;
                }
                return res;
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                return -1;
            }
        }

        // Conservation Mode (~80% charge limit)
        public int SetBatteryChargeLimit(int limitPercent)
        {
            try
            {
                uint conservationValue = limitPercent <= 80 ? 1u : 0u;

                int res = CallLenovoSetMethod("SetFeatureValue", 1u, conservationValue);
                if (res < 0)
                {
                    res = CallLenovoOtherMethod("SetFeatureValue", 1u, conservationValue);
                }
                if (res < 0)
                {
                    res = CallLenovoOtherMethodSingle("SetConservationMode", conservationValue);
                }
                return res >= 0 ? 1 : 0;
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                return -1;
            }
        }

        // Live Sensors via LENOVO_GAMEZONE_DATA
        public int GetCpuTemperature()
        {
            int t = CallLenovoMethod("GetCpuTemperature");
            return (t > 0 && t <= 130) ? t : -1;
        }

        public int GetGpuTemperature()
        {
            int t = CallLenovoMethod("GetGpuTemperature");
            return (t > 0 && t <= 130) ? t : -1;
        }

        public int GetCpuFanSpeed()
        {
            int rpm = CallLenovoMethodWithArg("GetFanSpeed", 1u);
            if (rpm <= 0) rpm = CallLenovoMethod("GetCpuFanSpeed");
            return rpm > 0 ? rpm : 0;
        }

        public int GetGpuFanSpeed()
        {
            int rpm = CallLenovoMethodWithArg("GetFanSpeed", 2u);
            if (rpm <= 0) rpm = CallLenovoMethod("GetGpuFanSpeed");
            return rpm > 0 ? rpm : 0;
        }

        // Lenovo Legion ITE USB HID (VID 0x048D)
        public int GetKeyboardBrightness() => _lastKnownBrightness;

        public int SetKeyboardBrightness(int level)
        {
            level = Math.Clamp(level, 0, 3);
            _lastKnownBrightness = level;

            try
            {
                var devices = DeviceList.Local.GetHidDevices(0x048D);
                foreach (var dev in devices)
                {
                    if (Array.IndexOf(LenovoIteProductIds, dev.ProductID) >= 0)
                    {
                        if (dev.TryOpen(out HidStream stream))
                        {
                            using (stream)
                            {
                                byte[] packet = new byte[33];
                                packet[0] = 0xCC;
                                packet[1] = 0x01;
                                packet[2] = (byte)(level * 33);
                                WriteHidReport(stream, packet);
                            }
                        }
                    }
                }
                return 1;
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                return -1;
            }
        }

        public (string Name, int ModeId)[] GetSupportedRgbModes() => new[]
        {
            ("Static", 0),
            ("Breath", 1),
            ("Wave", 2),
            ("Rainbow", 3)
        };

        public int SetKeyboardRgb(int mode, Color color, int speed = 1)
        {
            try
            {
                var devices = DeviceList.Local.GetHidDevices(0x048D);
                foreach (var dev in devices)
                {
                    if (Array.IndexOf(LenovoIteProductIds, dev.ProductID) >= 0)
                    {
                        if (dev.TryOpen(out HidStream stream))
                        {
                            using (stream)
                            {
                                byte[] packet = new byte[33];
                                packet[0] = 0xCC;
                                packet[1] = 0x02;
                                packet[2] = (byte)mode;
                                packet[3] = (byte)Math.Clamp(speed, 1, 3);
                                packet[4] = color.R;
                                packet[5] = color.G;
                                packet[6] = color.B;

                                WriteHidReport(stream, packet);
                            }
                        }
                    }
                }
                return 1;
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                return -1;
            }
        }

        public int SetTufKeyboardRGB(int mode, Color color, int speed = 1) => SetKeyboardRgb(mode, color, speed);
        public int SetTufKeyboardPower(uint power = 0x00FF01BD) => -1;

        private static void WriteHidReport(HidStream stream, byte[] packet)
        {
            try
            {
                stream.SetFeature(packet);
            }
            catch
            {
                try
                {
                    stream.Write(packet);
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                }
            }
        }

        private int CallLenovoMethod(string methodName)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(WmiNamespace, $"SELECT * FROM {WmiClass}");
                foreach (ManagementObject instance in searcher.Get())
                {
                    ManagementBaseObject outParams = instance.InvokeMethod(methodName, null, null);
                    if (outParams != null && outParams["Data"] != null)
                    {
                        return Convert.ToInt32(outParams["Data"]);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
            return -1;
        }

        private int CallLenovoMethodWithArg(string methodName, uint argVal)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(WmiNamespace, $"SELECT * FROM {WmiClass}");
                foreach (ManagementObject instance in searcher.Get())
                {
                    ManagementBaseObject inParams = instance.GetMethodParameters(methodName);
                    inParams["Data"] = argVal;
                    ManagementBaseObject outParams = instance.InvokeMethod(methodName, inParams, null);
                    if (outParams != null && outParams["Data"] != null)
                    {
                        return Convert.ToInt32(outParams["Data"]);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
            return -1;
        }

        private int CallLenovoSetMethod(string methodName, uint dataVal)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(WmiNamespace, $"SELECT * FROM {WmiClass}");
                foreach (ManagementObject instance in searcher.Get())
                {
                    ManagementBaseObject inParams = instance.GetMethodParameters(methodName);
                    inParams["Data"] = dataVal;
                    instance.InvokeMethod(methodName, inParams, null);
                    return 1;
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
            return -1;
        }

        private int CallLenovoSetMethod(string methodName, uint arg1, uint arg2)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(WmiNamespace, $"SELECT * FROM {WmiClass}");
                foreach (ManagementObject instance in searcher.Get())
                {
                    ManagementBaseObject inParams = instance.GetMethodParameters(methodName);
                    inParams["FeatureId"] = arg1;
                    inParams["Value"] = arg2;
                    instance.InvokeMethod(methodName, inParams, null);
                    return 1;
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
            return -1;
        }

        private int CallLenovoOtherMethod(string methodName, uint arg1, uint arg2)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(WmiNamespace, "SELECT * FROM LENOVO_OTHER_METHOD");
                foreach (ManagementObject instance in searcher.Get())
                {
                    ManagementBaseObject inParams = instance.GetMethodParameters(methodName);
                    inParams["FeatureId"] = arg1;
                    inParams["Value"] = arg2;
                    instance.InvokeMethod(methodName, inParams, null);
                    return 1;
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
            return -1;
        }

        private int CallLenovoOtherMethodSingle(string methodName, uint dataVal)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(WmiNamespace, "SELECT * FROM LENOVO_OTHER_METHOD");
                foreach (ManagementObject instance in searcher.Get())
                {
                    ManagementBaseObject inParams = instance.GetMethodParameters(methodName);
                    inParams["Data"] = dataVal;
                    instance.InvokeMethod(methodName, inParams, null);
                    return 1;
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
            return -1;
        }

        public void Dispose() { }
    }
}

