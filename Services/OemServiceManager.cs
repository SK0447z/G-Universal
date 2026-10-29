using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;

namespace GUniversal
{
    public static class OemServiceManager
    {
        public static readonly string[] MonitoredServices = new[]
        {
            // ASUS
            "LightningService", "AsusROGLSLService", "AsusCertService", "ASUSOptimization",
            "ASUSSoftwareManager", "ASUSLinkNear", "ASUSLinkRemote", "ASUSSwitch",
            "ASUSSystemAnalysis", "ASUSSystemDiagnosis", "ArmouryCrateService",
            "ArmouryCrateControlInterface", "AsusAppService",

            // Lenovo
            "ImControllerService", "LenovoVantageService", "LenovoFnAndFunctionKeys",

            // HP
            "OmenCap", "HPAppHelperCap", "HPNetworkCap", "HPSysInfoCap", "HPAudioAnalytics",

            // Acer
            "PredatorSenseService", "NitroSenseService", "AcerLightingService",

            // Dell
            "AWCCService", "DellClientManagementService"
        };

        public static int GetRunningServiceCount()
        {
            int count = 0;
            try
            {
                var monitoredSet = new HashSet<string>(MonitoredServices, StringComparer.OrdinalIgnoreCase);
                using var searcher = new ManagementObjectSearcher("SELECT Name, State FROM Win32_Service");
                foreach (ManagementObject obj in searcher.Get())
                {
                    string? name = obj["Name"]?.ToString();
                    string? state = obj["State"]?.ToString();

                    if (name != null && monitoredSet.Contains(name) &&
                        state != null && state.Equals("Running", StringComparison.OrdinalIgnoreCase))
                    {
                        count++;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
            return count;
        }

        public static void StopOemServices()
        {
            foreach (string name in MonitoredServices)
            {
                RunScCommand("config", name, "start=", "disabled");
                RunScCommand("stop", name);
            }
        }

        public static void StartOemServices()
        {
            foreach (string name in MonitoredServices)
            {
                RunScCommand("config", name, "start=", "auto");
                RunScCommand("start", name);
            }
        }

        private static void RunScCommand(params string[] args)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "sc.exe",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                foreach (string arg in args)
                {
                    psi.ArgumentList.Add(arg);
                }

                using Process? proc = Process.Start(psi);
                proc?.WaitForExit(1500);
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        public static int KillDgpuBackgroundApps()
        {
            int killedCount = 0;
            var killedPids = new HashSet<int>();

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "nvidia-smi.exe",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                psi.ArgumentList.Add("--query-compute-apps=pid");
                psi.ArgumentList.Add("--format=csv,noheader");

                using Process? proc = Process.Start(psi);
                if (proc != null)
                {
                    string output = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit(2000);

                    string[] lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string line in lines)
                    {
                        if (int.TryParse(line.Trim(), out int pid) && pid > 4)
                        {
                            if (killedPids.Add(pid))
                            {
                                try
                                {
                                    Process p = Process.GetProcessById(pid);
                                    p.Kill();
                                    killedCount++;
                                }
                                catch (Exception ex)
                                {
                                    Logger.Log(ex);
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

            string[] gpuWakingProcesses = new[]
            {
                "ArmouryCrate.UserSessionHelper", "ArmourySocketServer", "ArmouryCrate.Service",
                "LightingService", "ROG Live Service"
            };

            foreach (string procName in gpuWakingProcesses)
            {
                try
                {
                    foreach (var p in Process.GetProcessesByName(procName))
                    {
                        if (killedPids.Add(p.Id))
                        {
                            try
                            {
                                p.Kill();
                                killedCount++;
                            }
                            catch (Exception ex)
                            {
                                Logger.Log(ex);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                }
            }

            return killedCount;
        }

        public static void RestartGpuDriver()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT PNPDeviceID, Name FROM Win32_VideoController");
                foreach (ManagementObject obj in searcher.Get())
                {
                    string? pnpId = obj["PNPDeviceID"]?.ToString();
                    string? name = obj["Name"]?.ToString()?.ToLowerInvariant();

                    if (!string.IsNullOrEmpty(pnpId) && name != null)
                    {
                        if (name.Contains("nvidia") || name.Contains("geforce") ||
                            name.Contains("radeon") || name.Contains("amd"))
                        {
                            var psi = new ProcessStartInfo
                            {
                                FileName = "pnputil.exe",
                                CreateNoWindow = true,
                                UseShellExecute = false
                            };
                            psi.ArgumentList.Add("/restart-device");
                            psi.ArgumentList.Add(pnpId);

                            using Process? proc = Process.Start(psi);
                            proc?.WaitForExit(3000);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }
    }
}

