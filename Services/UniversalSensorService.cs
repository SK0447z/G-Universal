using System;
using System.Diagnostics;
using System.Globalization;
using System.Management;
using LibreHardwareMonitor.Hardware;

namespace GUniversal
{
    public class UniversalSensorService : IDisposable
    {
        private static UniversalSensorService? _instance;
        private static readonly object _lock = new object();

        public static UniversalSensorService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new UniversalSensorService();
                    }
                }
                return _instance;
            }
        }

        private Computer? _computer;
        private bool _lhmInitialized = false;
        private readonly object _lhmLock = new object();

        private UniversalSensorService()
        {
        }

        private void EnsureLhmInitialized()
        {
            if (_lhmInitialized) return;
            lock (_lhmLock)
            {
                if (_lhmInitialized) return;
                try
                {
                    _computer = new Computer
                    {
                        IsCpuEnabled = true,
                        IsGpuEnabled = true,
                        IsControllerEnabled = true,
                        IsMotherboardEnabled = true
                    };
                    _computer.Open();
                    _lhmInitialized = true;
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                    _lhmInitialized = false;
                }
            }
        }

        public int GetCpuTemperature(ILaptopProvider provider)
        {
            try
            {
                int t1 = provider.GetCpuTemperature();
                if (t1 > 0 && t1 <= 130)
                {
                    return t1;
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }

            EnsureLhmInitialized();
            if (_lhmInitialized && _computer != null)
            {
                try
                {
                    foreach (IHardware hw in _computer.Hardware)
                    {
                        if (hw.HardwareType == HardwareType.Cpu)
                        {
                            hw.Update();
                            foreach (ISensor s in hw.Sensors)
                            {
                                if (s.SensorType == SensorType.Temperature && s.Value.HasValue)
                                {
                                    int val = (int)Math.Round(s.Value.Value);
                                    if (val > 0 && val <= 130)
                                    {
                                        return val;
                                    }
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

            return GetFallbackWmiCpuTemp();
        }

        public int GetGpuTemperature(ILaptopProvider provider)
        {
            try
            {
                int t1 = provider.GetGpuTemperature();
                if (t1 > 0 && t1 <= 130)
                {
                    return t1;
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }

            EnsureLhmInitialized();
            if (_lhmInitialized && _computer != null)
            {
                try
                {
                    foreach (IHardware hw in _computer.Hardware)
                    {
                        if (hw.HardwareType == HardwareType.GpuNvidia ||
                            hw.HardwareType == HardwareType.GpuAmd ||
                            hw.HardwareType == HardwareType.GpuIntel)
                        {
                            hw.Update();
                            foreach (ISensor s in hw.Sensors)
                            {
                                if (s.SensorType == SensorType.Temperature && s.Value.HasValue)
                                {
                                    int val = (int)Math.Round(s.Value.Value);
                                    if (val > 0 && val <= 130)
                                    {
                                        return val;
                                    }
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

            return QueryNvidiaSmiTemp();
        }

        public int GetCpuFanSpeed(ILaptopProvider provider)
        {
            try
            {
                int fan = provider.GetCpuFanSpeed();
                if (fan > 0)
                {
                    return fan;
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }

            EnsureLhmInitialized();
            if (_lhmInitialized && _computer != null)
            {
                try
                {
                    foreach (IHardware hw in _computer.Hardware)
                    {
                        if (hw.HardwareType == HardwareType.Motherboard ||
                            hw.HardwareType == HardwareType.SuperIO ||
                            hw.HardwareType == HardwareType.Cpu)
                        {
                            hw.Update();
                            foreach (ISensor s in hw.Sensors)
                            {
                                if (s.SensorType == SensorType.Fan && s.Value.HasValue && s.Value.Value > 0)
                                {
                                    return (int)Math.Round(s.Value.Value);
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

            return 0;
        }

        public int GetGpuFanSpeed(ILaptopProvider provider)
        {
            try
            {
                int fan = provider.GetGpuFanSpeed();
                if (fan > 0)
                {
                    return fan;
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }

            EnsureLhmInitialized();
            if (_lhmInitialized && _computer != null)
            {
                try
                {
                    foreach (IHardware hw in _computer.Hardware)
                    {
                        if (hw.HardwareType == HardwareType.GpuNvidia ||
                            hw.HardwareType == HardwareType.GpuAmd)
                        {
                            hw.Update();
                            foreach (ISensor s in hw.Sensors)
                            {
                                if (s.SensorType == SensorType.Fan && s.Value.HasValue && s.Value.Value > 0)
                                {
                                    return (int)Math.Round(s.Value.Value);
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

            return 0;
        }

        private static int GetFallbackWmiCpuTemp()
        {
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

        private DateTime _lastNvidiaSmiQuery = DateTime.MinValue;
        private int _cachedNvidiaSmiTemp = -1;

        private int QueryNvidiaSmiTemp()
        {
            if ((DateTime.UtcNow - _lastNvidiaSmiQuery).TotalMilliseconds < 2500 && _cachedNvidiaSmiTemp > 0)
            {
                return _cachedNvidiaSmiTemp;
            }
            _lastNvidiaSmiQuery = DateTime.UtcNow;

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
                                _cachedNvidiaSmiTemp = t;
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

            _cachedNvidiaSmiTemp = -1;
            return -1;
        }

        public void Dispose()
        {
            try
            {
                if (_lhmInitialized && _computer != null)
                {
                    _computer.Close();
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }
    }
}

