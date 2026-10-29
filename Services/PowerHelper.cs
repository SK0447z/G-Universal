using System;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace GUniversal
{
    public enum CpuBoostMode : int
    {
        Disabled = 0,
        Aggressive = 2,
        Efficient = 4
    }

    public static class PowerHelper
    {
        public const string SubgroupProcessorGuid = "54533251-82be-4824-96c1-47b60b740d00";
        public const string PerfBoostModeGuid = "be337238-0d82-4146-a960-4f3749d470c7";
        public const string ProcThrottleMaxGuid = "bc5038f7-23e0-4960-96da-33abaf5935ec";

        private static bool _attributesUnhidden = false;

        private static string RunPowerCfg(params string[] args)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powercfg.exe",
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
                if (proc == null) return string.Empty;

                string output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit(3000);
                return output;
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                return string.Empty;
            }
        }

        public static void EnsureBoostAttributeUnhidden()
        {
            if (_attributesUnhidden) return;
            RunPowerCfg("-attributes", SubgroupProcessorGuid, PerfBoostModeGuid, "-ATTRIB_HIDE");
            _attributesUnhidden = true;
        }

        public static int SetBoostMode(CpuBoostMode mode)
        {
            int val = (int)mode;
            EnsureBoostAttributeUnhidden();

            RunPowerCfg("/setacvalueindex", "SCHEME_CURRENT", SubgroupProcessorGuid, PerfBoostModeGuid, val.ToString());
            RunPowerCfg("/setdcvalueindex", "SCHEME_CURRENT", SubgroupProcessorGuid, PerfBoostModeGuid, val.ToString());

            RunPowerCfg("/setacvalueindex", "SCHEME_CURRENT", SubgroupProcessorGuid, ProcThrottleMaxGuid, "100");
            RunPowerCfg("/setdcvalueindex", "SCHEME_CURRENT", SubgroupProcessorGuid, ProcThrottleMaxGuid, "100");

            RunPowerCfg("/setactive", "SCHEME_CURRENT");

            return GetCurrentBoostMode();
        }

        public static int GetCurrentBoostMode()
        {
            EnsureBoostAttributeUnhidden();

            string output = RunPowerCfg("/query", "SCHEME_CURRENT", SubgroupProcessorGuid, PerfBoostModeGuid);
            if (!string.IsNullOrWhiteSpace(output))
            {
                MatchCollection matches = Regex.Matches(output, @"0x([0-9a-fA-F]{8})");
                if (matches.Count > 0)
                {
                    string hex = matches[0].Groups[1].Value;
                    if (int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int acIndex))
                    {
                        return acIndex;
                    }
                }
            }

            return (int)CpuBoostMode.Aggressive;
        }
    }
}

