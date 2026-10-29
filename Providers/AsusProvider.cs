using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Management;
using System.Runtime.InteropServices;
using HidSharp;
using Microsoft.Win32.SafeHandles;

namespace GUniversal
{
    public class AsusProvider : ILaptopProvider
    {
        private const uint GENERIC_READ = 0x80000000;
        private const uint GENERIC_WRITE = 0x40000000;
        private const uint FILE_SHARE_READ = 0x00000001;
        private const uint FILE_SHARE_WRITE = 0x00000002;
        private const uint OPEN_EXISTING = 3;
        private const uint FILE_ATTRIBUTE_NORMAL = 0x80;

        private const uint IOCTL_ATKACPI = 0x0022240C;
        private const uint METHOD_DSTS = 0x53545344; // "DSTS"
        private const uint METHOD_DEVS = 0x53564544; // "DEVS"

        // ATKACPI Device IDs
        public const uint DEVID_THROTTLE_THERMAL_POLICY = 0x00120075; // 0=Balanced, 1=Turbo, 2=Silent
        public const uint DEVID_KBD_BRIGHTNESS = 0x00050021;          // 0x80 | level (0..3)
        public const uint DEVID_TUF_RGB_KB1 = 0x00100056;             // 6-byte RGB payload
        public const uint DEVID_TUF_RGB_KB2 = 0x0010005A;             // Fallback 6-byte RGB payload
        public const uint DEVID_TUF_RGB_POWER = 0x00100057;           // TUF Keyboard Power Awake (0x00FF01BD)
        public const uint DEVID_TUF_RGB_STATE = 0x00100057;
        public const uint DEVID_BATTERY_LIMIT = 0x00120057;           // 60..100
        public const uint DEVID_CPU_TEMP = 0x00120094;                // DSTS - 65536
        public const uint DEVID_GPU_TEMP = 0x00120097;                // DSTS - 65536
        public const uint DEVID_CPU_FAN = 0x00110013;                 // DSTS
        public const uint DEVID_GPU_FAN = 0x00110014;                 // DSTS

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern SafeFileHandle CreateFile(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool DeviceIoControl(
            SafeFileHandle hDevice,
            uint dwIoControlCode,
            byte[] lpInBuffer,
            int nInBufferSize,
            byte[] lpOutBuffer,
            int nOutBufferSize,
            out uint lpBytesReturned,
            IntPtr lpOverlapped);

        private SafeFileHandle? _atkHandle;
        private bool _wmiAvailable;
        private int _lastKnownPerfMode = 0;
        private int _lastKnownBrightness = 2;

        public string ProviderName => "ASUS ATKACPI / WMI Controller";
        public bool IsConnected => (_atkHandle != null && !_atkHandle.IsInvalid) || _wmiAvailable;
        public string ConnectionType { get; private set; } = "Disconnected";

        public bool SupportsPerformanceModes => true;
        public bool SupportsBatteryLimit => true;
        public bool SupportsKeyboardRgb => true;
        public bool SupportsKeyboardBrightness => true;
        public bool SupportsFanRpm => true;

        public AsusProvider()
        {
            InitializeConnection();
        }

        private void InitializeConnection()
        {
            try
            {
                _atkHandle = CreateFile(
                    @"\\.\ATKACPI",
                    GENERIC_READ | GENERIC_WRITE,
                    FILE_SHARE_READ | FILE_SHARE_WRITE,
                    IntPtr.Zero,
                    OPEN_EXISTING,
                    FILE_ATTRIBUTE_NORMAL,
                    IntPtr.Zero);

                if (_atkHandle != null && !_atkHandle.IsInvalid)
                {
                    ConnectionType = @"\\.\ATKACPI (Direct Kernel IOCTL)";
                    SetTufKeyboardPower(0x00FF01BD);
                    return;
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }

            try
            {
                using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM AsusAtkWmi_WMNB");
                foreach (ManagementObject _ in searcher.Get())
                {
                    _wmiAvailable = true;
                    ConnectionType = @"root\WMI (AsusAtkWmi_WMNB)";
                    break;
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                _wmiAvailable = false;
                ConnectionType = "No ASUS ACPI Interface Found";
            }
        }

        private bool TryCallAtkAcpi(uint methodId, byte[] args, out int result)
        {
            result = -1;
            if (_atkHandle == null || _atkHandle.IsInvalid) return false;

            int inSize = 8 + args.Length;
            byte[] inBuffer = new byte[inSize];
            BitConverter.GetBytes(methodId).CopyTo(inBuffer, 0);
            BitConverter.GetBytes((uint)args.Length).CopyTo(inBuffer, 4);
            Array.Copy(args, 0, inBuffer, 8, args.Length);

            byte[] outBuffer = new byte[16];

            bool success = DeviceIoControl(
                _atkHandle,
                IOCTL_ATKACPI,
                inBuffer,
                inBuffer.Length,
                outBuffer,
                outBuffer.Length,
                out _,
                IntPtr.Zero);

            if (success)
            {
                result = BitConverter.ToInt32(outBuffer, 0);
                return true;
            }

            return false;
        }

        private int CallWmiFallback(string methodName, uint deviceId, uint val = 0)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM AsusAtkWmi_WMNB");
                foreach (ManagementObject instance in searcher.Get())
                {
                    ManagementBaseObject inParams = instance.GetMethodParameters(methodName);
                    inParams["Device_Id"] = deviceId;
                    if (methodName == "DEVS")
                    {
                        inParams["Control_status"] = val;
                    }

                    ManagementBaseObject outParams = instance.InvokeMethod(methodName, inParams, null);
                    if (outParams != null && outParams.Properties["result"]?.Value != null)
                    {
                        return Convert.ToInt32(outParams.Properties["result"].Value);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }

            return -1;
        }

        public int DeviceGet(uint deviceId)
        {
            byte[] args = BitConverter.GetBytes(deviceId);
            if (TryCallAtkAcpi(METHOD_DSTS, args, out int res))
            {
                return res >= 65536 ? res - 65536 : res;
            }

            int wmiRes = CallWmiFallback("DSTS", deviceId);
            return wmiRes >= 65536 ? wmiRes - 65536 : wmiRes;
        }

        public int DeviceSet(uint deviceId, uint val)
        {
            byte[] args = new byte[8];
            BitConverter.GetBytes(deviceId).CopyTo(args, 0);
            BitConverter.GetBytes(val).CopyTo(args, 4);

            if (TryCallAtkAcpi(METHOD_DEVS, args, out int res))
            {
                return res;
            }

            return CallWmiFallback("DEVS", deviceId, val);
        }

        public int DeviceSet(uint deviceId, byte[] payload)
        {
            byte[] args = new byte[4 + payload.Length];
            BitConverter.GetBytes(deviceId).CopyTo(args, 0);
            Array.Copy(payload, 0, args, 4, payload.Length);

            if (TryCallAtkAcpi(METHOD_DEVS, args, out int res))
            {
                return res;
            }

            uint packed = 0;
            if (payload.Length >= 4)
            {
                packed = BitConverter.ToUInt32(payload, 0);
            }
            return CallWmiFallback("DEVS", deviceId, packed);
        }

        // ATKACPI 0x00120075: 0=Balanced, 1=Turbo, 2=Silent
        public int GetPerformanceMode()
        {
            int mode = DeviceGet(DEVID_THROTTLE_THERMAL_POLICY);
            if (mode >= 0 && mode <= 2)
            {
                _lastKnownPerfMode = mode;
                return mode;
            }
            return _lastKnownPerfMode;
        }

        public int SetPerformanceMode(int mode)
        {
            int res = DeviceSet(DEVID_THROTTLE_THERMAL_POLICY, (uint)mode);
            if (res >= 0)
            {
                _lastKnownPerfMode = mode;
            }
            return res;
        }

        // ATKACPI 0x00050021: 0x80 | level (0..3)
        public int GetKeyboardBrightness()
        {
            int val = DeviceGet(DEVID_KBD_BRIGHTNESS);
            if (val >= 0)
            {
                int level = val & 0x7F;
                if (level >= 0 && level <= 3)
                {
                    _lastKnownBrightness = level;
                    return level;
                }
            }
            return _lastKnownBrightness;
        }

        public int SetKeyboardBrightness(int level)
        {
            level = Math.Clamp(level, 0, 3);
            uint payload = (uint)(0x80 | (level & 0x7F));
            int res = DeviceSet(DEVID_KBD_BRIGHTNESS, payload);
            _lastKnownBrightness = level;
            if (level > 0)
            {
                SetTufKeyboardPower(0x00FF01BD);
            }

            SendRogAuraBrightness(level);
            return res;
        }

        // ATKACPI 0x00100057: Keep TUF keyboard power awake
        public int SetTufKeyboardPower(uint power = 0x00FF01BD)
        {
            return DeviceSet(DEVID_TUF_RGB_POWER, power);
        }

        public (string Name, int ModeId)[] GetSupportedRgbModes() => new[]
        {
            ("Static", 0),
            ("Breathe", 1),
            ("Cycle", 2),
            ("Strobe", 10)
        };

        // TUF keyboard RGB payload: [0xB4, mode, R, G, B, speed]
        public int SetKeyboardRgb(int mode, Color color, int speed = 1)
        {
            byte speedByte = speed switch
            {
                0 => 0xE1, // Slow
                1 => 0xEB, // Medium
                2 => 0xF5, // Fast
                _ => (byte)speed
            };

            SetTufKeyboardPower(0x00FF01BD);

            if (GetKeyboardBrightness() <= 0)
            {
                SetKeyboardBrightness(3);
            }

            byte[] packetB4 = new byte[]
            {
                0xB4,
                (byte)mode,
                color.R,
                color.G,
                color.B,
                speedByte
            };

            byte[] packetB3 = new byte[]
            {
                0xB3,
                (byte)mode,
                color.R,
                color.G,
                color.B,
                speedByte
            };

            int res = DeviceSet(DEVID_TUF_RGB_KB1, packetB4);
            DeviceSet(DEVID_TUF_RGB_KB2, packetB3);
            int res2 = DeviceSet(DEVID_TUF_RGB_KB2, packetB4);

            if (res != 1 && res2 == 1)
            {
                res = res2;
            }

            SetTufKeyboardPower(0x00FF01BD);
            SendRogAuraHidPackets((byte)mode, color, speedByte);

            return res;
        }

        public int SetTufKeyboardRGB(int mode, Color color, int speed = 1) => SetKeyboardRgb(mode, color, speed);

        // ROG USB HID Aura support (VID 0x0B05)
        private static readonly int[] RogProductIds = new[] { 0x1854, 0x1866, 0x1869, 0x19B6, 0x1A30 };

        private void SendRogAuraHidPackets(byte mode, Color color, byte auraSpeed)
        {
            try
            {
                var devices = DeviceList.Local.GetHidDevices(0x0B05);
                foreach (var dev in devices)
                {
                    if (Array.IndexOf(RogProductIds, dev.ProductID) >= 0)
                    {
                        if (dev.TryOpen(out HidStream stream))
                        {
                            using (stream)
                            {
                                int reportLen = Math.Max(64, dev.GetMaxFeatureReportLength());
                                byte[] colorPacket = new byte[reportLen];
                                colorPacket[0] = 0x5D;
                                colorPacket[1] = 0xB3;
                                colorPacket[2] = 0x00;
                                colorPacket[3] = mode;
                                colorPacket[4] = color.R;
                                colorPacket[5] = color.G;
                                colorPacket[6] = color.B;
                                colorPacket[7] = auraSpeed;

                                byte[] setPacket = new byte[reportLen];
                                setPacket[0] = 0x5D;
                                setPacket[1] = 0xB5;

                                byte[] applyPacket = new byte[reportLen];
                                applyPacket[0] = 0x5D;
                                applyPacket[1] = 0xB4;

                                WriteHidReport(stream, colorPacket);
                                WriteHidReport(stream, setPacket);
                                WriteHidReport(stream, applyPacket);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        private void SendRogAuraBrightness(int level)
        {
            try
            {
                var devices = DeviceList.Local.GetHidDevices(0x0B05);
                foreach (var dev in devices)
                {
                    if (Array.IndexOf(RogProductIds, dev.ProductID) >= 0)
                    {
                        if (dev.TryOpen(out HidStream stream))
                        {
                            using (stream)
                            {
                                int reportLen = Math.Max(64, dev.GetMaxFeatureReportLength());
                                byte[] brightPacket = new byte[reportLen];
                                brightPacket[0] = 0x5A;
                                brightPacket[1] = 0xBA;
                                brightPacket[2] = 0xC5;
                                brightPacket[3] = 0xC4;
                                brightPacket[4] = (byte)level;

                                WriteHidReport(stream, brightPacket);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

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

        // ATKACPI 0x00120057: Battery limit (60..100)
        public int SetBatteryChargeLimit(int limitPercent)
        {
            limitPercent = Math.Clamp(limitPercent, 40, 100);
            return DeviceSet(DEVID_BATTERY_LIMIT, (uint)limitPercent);
        }

        // ATKACPI 0x00120094: CPU Temperature
        public int GetCpuTemperature()
        {
            int cpuTemp = DeviceGet(DEVID_CPU_TEMP);
            if (cpuTemp > 0 && cpuTemp <= 130)
            {
                return cpuTemp;
            }

            try
            {
                using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");
                foreach (ManagementObject obj in searcher.Get())
                {
                    if (obj["CurrentTemperature"] != null)
                    {
                        double rawK = Convert.ToDouble(obj["CurrentTemperature"]);
                        int c = (int)Math.Round((rawK / 10.0) - 273.15);
                        if (c > 0 && c <= 130)
                        {
                            return c;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }

            try
            {
                using var searcher2 = new ManagementObjectSearcher(@"root\CIMV2", "SELECT HighPrecisionTemperature FROM Win32_PerfFormattedData_Counters_ThermalZoneInformation");
                foreach (ManagementObject obj in searcher2.Get())
                {
                    if (obj["HighPrecisionTemperature"] != null)
                    {
                        double rawK = Convert.ToDouble(obj["HighPrecisionTemperature"]);
                        int c = (int)Math.Round((rawK / 10.0) - 273.15);
                        if (c > 0 && c <= 130)
                        {
                            return c;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }

            return -1;
        }

        // ATKACPI 0x00120097: GPU Temperature
        public int GetGpuTemperature()
        {
            int gpuTemp = DeviceGet(DEVID_GPU_TEMP);
            if (gpuTemp > 0 && gpuTemp <= 130)
            {
                return gpuTemp;
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "nvidia-smi.exe",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                psi.ArgumentList.Add("--query-gpu=temperature.gpu");
                psi.ArgumentList.Add("--format=csv,noheader,nounits");

                using Process? proc = Process.Start(psi);
                if (proc != null)
                {
                    string outStr = proc.StandardOutput.ReadToEnd().Trim();
                    proc.WaitForExit(1000);

                    if (!string.IsNullOrEmpty(outStr))
                    {
                        string firstLine = outStr.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[0].Trim();
                        if (double.TryParse(firstLine, NumberStyles.Any, CultureInfo.InvariantCulture, out double val))
                        {
                            int t = (int)Math.Round(val);
                            if (t > 0 && t <= 130)
                            {
                                return t;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }

            return -1;
        }

        // ATKACPI 0x00110013 / 0x00110014: Fan RPM
        public int GetCpuFanSpeed()
        {
            return ReadFanRpm(DEVID_CPU_FAN);
        }

        public int GetGpuFanSpeed()
        {
            return ReadFanRpm(DEVID_GPU_FAN);
        }

        private int ReadFanRpm(uint deviceId)
        {
            int raw = DeviceGet(deviceId);
            if (raw == -1 || raw == -65536 || raw == -65535) return 0;
            int fan = raw & 0xFFFF;
            return (fan > 0 && fan <= 120) ? fan * 100 : 0;
        }

        public void Dispose()
        {
            if (_atkHandle != null && !_atkHandle.IsInvalid)
            {
                _atkHandle.Dispose();
            }
        }
    }
}

