using System;
using System.Drawing;
using System.Management;

namespace GUniversal
{
    public class AcerProvider : ILaptopProvider
    {
        private const string WmiNamespace = @"root\WMI";
        private const string WmiClass = "AcerGamingFunction";

        public string ProviderName => "Acer Predator / Nitro Controller";
        public bool IsConnected { get; private set; }
        public string ConnectionType { get; private set; } = "Disconnected";

        public bool SupportsPerformanceModes => true;
        public bool SupportsBatteryLimit => true;
        public bool SupportsKeyboardRgb => true;
        public bool SupportsKeyboardBrightness => true;
        public bool SupportsFanRpm => false;

        private int _lastKnownPerfMode = 0;
        private int _lastKnownBrightness = 2;

        public AcerProvider()
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
                    ConnectionType = @"root\WMI (AcerGamingFunction)";
                    return;
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }

            IsConnected = false;
            ConnectionType = "Acer Gaming WMI Not Found";
        }

        // SetGamingMiscSetting / SetGamingProfile
        // Quiet=0x00, Balanced=0x01, Turbo/Extreme=0x04
        public int GetPerformanceMode()
        {
            try
            {
                ulong raw = CallAcerGetMethodUlong("GetGamingMiscSetting", 0x0BUL);
                int modeVal = (int)((raw >> 8) & 0xFF);
                int mapped = modeVal switch
                {
                    0x00 => 2, // Silent / Quiet
                    0x04 => 1, // Turbo / Extreme
                    _ => 0     // Balanced
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
                ulong acerMode = mode switch
                {
                    2 => 0x00UL, // Quiet
                    1 => 0x04UL, // Turbo / Extreme
                    _ => 0x01UL  // Balanced / Default
                };

                ulong gmInput = (acerMode << 8) | 0x0BUL;
                int res = CallAcerSetMethodUlong("SetGamingMiscSetting", gmInput);

                if (res < 0)
                {
                    res = CallAcerSetMethodUlong("SetGamingProfile", acerMode);
                }

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

        // Battery Charge Limit (80% / 100% via SetGamingMiscSetting)
        public int SetBatteryChargeLimit(int limitPercent)
        {
            try
            {
                ulong enableFlag = limitPercent <= 80 ? 1UL : 0UL;
                ulong gmInput = (enableFlag << 8) | 0x15UL;
                int res = CallAcerSetMethodUlong("SetGamingMiscSetting", gmInput);
                if (res < 0)
                {
                    ulong fallbackInput = (enableFlag << 8) | 0x05UL;
                    res = CallAcerSetMethodUlong("SetGamingMiscSetting", fallbackInput);
                }
                return res >= 0 ? 1 : 0;
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                return -1;
            }
        }

        // 4-zone RGB & Brightness
        public int GetKeyboardBrightness() => _lastKnownBrightness;

        public int SetKeyboardBrightness(int level)
        {
            level = Math.Clamp(level, 0, 3);
            _lastKnownBrightness = level;

            try
            {
                uint val = (uint)(level * 33);
                uint gmInput = (val << 8) | 0x01u;
                int res = CallAcerSetMethodUint("SetKeyboardBacklight", gmInput);
                return res >= 0 ? 1 : 0;
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
            ("Breathing", 1),
            ("Wave", 2),
            ("Zoom", 3)
        };

        public int SetKeyboardRgb(int mode, Color color, int speed = 1)
        {
            try
            {
                byte[] rgbPacket = new byte[16];
                rgbPacket[0] = (byte)mode;
                rgbPacket[1] = (byte)Math.Clamp(speed, 1, 3);
                rgbPacket[2] = 0x64; // Max Brightness 100%

                for (int z = 0; z < 4; z++)
                {
                    int offset = 4 + (z * 3);
                    rgbPacket[offset + 0] = color.R;
                    rgbPacket[offset + 1] = color.G;
                    rgbPacket[offset + 2] = color.B;
                }

                int res = CallAcerSetMethodBytes("SetKeyboardRGB", rgbPacket);
                return res >= 0 ? 1 : 0;
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                return -1;
            }
        }

        public int SetTufKeyboardRGB(int mode, Color color, int speed = 1) => SetKeyboardRgb(mode, color, speed);
        public int SetTufKeyboardPower(uint power = 0x00FF01BD) => -1;

        public int GetCpuTemperature() => -1;
        public int GetGpuTemperature() => -1;
        public int GetCpuFanSpeed() => 0;
        public int GetGpuFanSpeed() => 0;

        private ulong CallAcerGetMethodUlong(string methodName, ulong argVal)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(WmiNamespace, $"SELECT * FROM {WmiClass}");
                foreach (ManagementObject instance in searcher.Get())
                {
                    ManagementBaseObject inParams = instance.GetMethodParameters(methodName);
                    inParams["gmInput"] = argVal;
                    ManagementBaseObject outParams = instance.InvokeMethod(methodName, inParams, null);
                    if (outParams != null && outParams["gmOutput"] != null)
                    {
                        return Convert.ToUInt64(outParams["gmOutput"]);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
            return 0;
        }

        private int CallAcerSetMethodUlong(string methodName, ulong argVal)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(WmiNamespace, $"SELECT * FROM {WmiClass}");
                foreach (ManagementObject instance in searcher.Get())
                {
                    ManagementBaseObject inParams = instance.GetMethodParameters(methodName);
                    inParams["gmInput"] = argVal;
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

        private int CallAcerSetMethodUint(string methodName, uint argVal)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(WmiNamespace, $"SELECT * FROM {WmiClass}");
                foreach (ManagementObject instance in searcher.Get())
                {
                    ManagementBaseObject inParams = instance.GetMethodParameters(methodName);
                    inParams["gmInput"] = argVal;
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

        private int CallAcerSetMethodBytes(string methodName, byte[] bytes)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(WmiNamespace, $"SELECT * FROM {WmiClass}");
                foreach (ManagementObject instance in searcher.Get())
                {
                    ManagementBaseObject inParams = instance.GetMethodParameters(methodName);
                    inParams["gmInput"] = bytes;
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

