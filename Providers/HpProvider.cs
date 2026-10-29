using System;
using System.Drawing;
using System.Management;

namespace GUniversal
{
    public class HpProvider : ILaptopProvider
    {
        private const string WmiNamespace = @"root\WMI";
        private const string WmiClass = "hpqBIntM";
        private const string InputClass = "hpqBDataIn";

        private static readonly byte[] SecuSignature = new byte[] { 0x53, 0x45, 0x43, 0x55 }; // "SECU"

        public string ProviderName => "HP Omen / Victus BIOS Controller";
        public bool IsConnected { get; private set; }
        public string ConnectionType { get; private set; } = "Disconnected";

        public bool SupportsPerformanceModes => true;
        public bool SupportsBatteryLimit => false;
        public bool SupportsKeyboardRgb => true;
        public bool SupportsKeyboardBrightness => true;
        public bool SupportsFanRpm => true;

        private int _lastKnownPerfMode = 0;
        private int _lastKnownBrightness = 2;

        public HpProvider()
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
                    ConnectionType = @"root\WMI (hpqBIntM)";
                    return;
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }

            IsConnected = false;
            ConnectionType = "HP Omen BIOS Interface Not Found";
        }

        // Performance modes: Command=0x20008, CommandType=0x1A
        // V1: Silent=0x50, Balanced=0x30, Turbo=0x31
        // V2: Silent=0x02, Balanced=0x00, Performance=0x01
        public int GetPerformanceMode()
        {
            try
            {
                byte[]? res = SendOmenBiosWmi(0x1A, new byte[] { 0, 0, 0, 0 }, 4, 0x20008);
                if (res != null && res.Length >= 2)
                {
                    byte modeByte = res[1];
                    int mapped = modeByte switch
                    {
                        0x50 => 2, // V1 Silent
                        0x31 => 1, // V1 Turbo
                        0x30 => 0, // V1 Balanced
                        0x02 => 2, // V2 Cool / Silent
                        0x01 => 1, // V2 Performance
                        0x00 => 0, // V2 Balanced
                        _ => 0
                    };
                    _lastKnownPerfMode = mapped;
                    return mapped;
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }

            return _lastKnownPerfMode;
        }

        public int SetPerformanceMode(int mode)
        {
            try
            {
                byte modeByteV1 = mode switch
                {
                    2 => 0x50, // Silent
                    1 => 0x31, // Turbo
                    _ => 0x30  // Balanced
                };

                byte[] payloadV1 = new byte[] { 0xFF, modeByteV1, 0x00, 0x00 };
                var (rc1, res1) = SendOmenBiosWmiWithStatus(0x1A, payloadV1, 4, 0x20008);
                if (rc1 == 0 && res1 != null)
                {
                    _lastKnownPerfMode = mode;
                    return 1;
                }

                byte modeByteV2 = mode switch
                {
                    2 => 0x02, // Cool / Silent
                    1 => 0x01, // Performance
                    _ => 0x00  // Balanced
                };

                byte[] payloadV2 = new byte[] { 0xFF, modeByteV2, 0x00, 0x00 };
                var (rc2, res2) = SendOmenBiosWmiWithStatus(0x1A, payloadV2, 4, 0x20008);
                if (rc2 == 0 && res2 != null)
                {
                    _lastKnownPerfMode = mode;
                    return 1;
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }

            return -1;
        }

        // 4-zone RGB & Brightness (Command = 0x20009)
        public int GetKeyboardBrightness() => _lastKnownBrightness;

        public int SetKeyboardBrightness(int level)
        {
            level = Math.Clamp(level, 0, 3);
            _lastKnownBrightness = level;

            try
            {
                byte bVal = level switch
                {
                    0 => 0x00,
                    1 => 0x20,
                    2 => 0x50,
                    3 => 0x64,
                    _ => 0x64
                };

                byte[] payload = new byte[] { 0, 0, bVal, 0 };
                byte[]? res = SendOmenBiosWmi(0x10, payload, 4, 0x20009);
                return res != null ? 1 : 0;
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
            ("Breathe", 1),
            ("Cycle", 2),
            ("Blink", 3)
        };

        public int SetKeyboardRgb(int mode, Color color, int speed = 1)
        {
            try
            {
                byte r = color.R;
                byte g = color.G;
                byte b = color.B;

                byte[] payload = new byte[128];
                for (int zone = 0; zone < 4; zone++)
                {
                    int offset = zone * 4;
                    payload[offset + 0] = r;
                    payload[offset + 1] = g;
                    payload[offset + 2] = b;
                    payload[offset + 3] = 0xFF;
                }

                byte[]? res = SendOmenBiosWmi(0x20, payload, 128, 0x20009);
                if (res == null)
                {
                    byte[] shortPayload = new byte[] { r, g, b, 0xFF };
                    res = SendOmenBiosWmi(0x20, shortPayload, 4, 0x20009);
                }

                return res != null ? 1 : 0;
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                return -1;
            }
        }

        public int SetTufKeyboardRGB(int mode, Color color, int speed = 1) => SetKeyboardRgb(mode, color, speed);
        public int SetTufKeyboardPower(uint power = 0x00FF01BD) => -1;

        public int SetBatteryChargeLimit(int limitPercent) => -1;

        // Live Sensors: Command=0x20008, CommandType=0x15 / 0x13
        public int GetCpuTemperature()
        {
            try
            {
                byte[]? res = SendOmenBiosWmi(0x15, new byte[] { 0, 0, 0, 0 }, 4, 0x20008);
                if (res != null && res.Length >= 2)
                {
                    int temp = res[0];
                    if (temp > 0 && temp <= 130) return temp;
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
            return -1;
        }

        public int GetGpuTemperature()
        {
            try
            {
                byte[]? res = SendOmenBiosWmi(0x15, new byte[] { 1, 0, 0, 0 }, 4, 0x20008);
                if (res != null && res.Length >= 2)
                {
                    int temp = res[1];
                    if (temp > 0 && temp <= 130) return temp;
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
            return -1;
        }

        public int GetCpuFanSpeed()
        {
            try
            {
                byte[]? res = SendOmenBiosWmi(0x13, new byte[] { 0, 0, 0, 0 }, 4, 0x20008);
                if (res != null && res.Length >= 2)
                {
                    int rpm = BitConverter.ToUInt16(res, 0);
                    return (rpm > 0 && rpm < 10000) ? rpm : 0;
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
            return 0;
        }

        public int GetGpuFanSpeed()
        {
            try
            {
                byte[]? res = SendOmenBiosWmi(0x13, new byte[] { 1, 0, 0, 0 }, 4, 0x20008);
                if (res != null && res.Length >= 4)
                {
                    int rpm = BitConverter.ToUInt16(res, 2);
                    return (rpm > 0 && rpm < 10000) ? rpm : 0;
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
            return 0;
        }

        private (int returnCode, byte[]? data) SendOmenBiosWmiWithStatus(uint commandType, byte[] data, int outputSize, uint command = 0x20008)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(WmiNamespace, $"SELECT * FROM {WmiClass}");
                foreach (ManagementObject instance in searcher.Get())
                {
                    string methodName = $"hpqBIOSInt{outputSize}";
                    ManagementBaseObject inParams = instance.GetMethodParameters(methodName);

                    using var inputClass = new ManagementClass(WmiNamespace, InputClass, null);
                    ManagementObject inputObj = inputClass.CreateInstance();
                    inputObj["Sign"] = SecuSignature;
                    inputObj["Command"] = command;
                    inputObj["CommandType"] = commandType;
                    inputObj["Size"] = (uint)data.Length;
                    inputObj["hpqBData"] = data;

                    inParams["In"] = inputObj;

                    ManagementBaseObject outParams = instance.InvokeMethod(methodName, inParams, null);
                    if (outParams != null)
                    {
                        int returnCode = 0;
                        if (outParams["ReturnValue"] != null)
                        {
                            returnCode = Convert.ToInt32(outParams["ReturnValue"]);
                        }
                        if (outParams["out"] is ManagementBaseObject outObj && outObj["Return"] != null)
                        {
                            int biosReturn = Convert.ToInt32(outObj["Return"]);
                            if (biosReturn != 0) returnCode = biosReturn;
                        }

                        byte[]? payload = null;
                        if (outParams["Data"] is byte[] outData)
                        {
                            payload = outData;
                        }
                        else if (outParams["out"] is ManagementBaseObject outObj2 && outObj2["hpqBData"] is byte[] nestedData)
                        {
                            payload = nestedData;
                        }
                        else
                        {
                            payload = new byte[outputSize];
                        }

                        return (returnCode, payload);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }

            return (-1, null);
        }

        private byte[]? SendOmenBiosWmi(uint commandType, byte[] data, int outputSize, uint command = 0x20008)
        {
            var (rc, payload) = SendOmenBiosWmiWithStatus(commandType, data, outputSize, command);
            return rc == 0 ? payload : null;
        }

        public void Dispose() { }
    }
}

