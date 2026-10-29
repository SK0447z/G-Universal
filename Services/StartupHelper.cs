using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace GUniversal
{
    public static class StartupHelper
    {
        private const string TaskName = "G-Universal";

        public static bool IsStartupEnabled()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                psi.ArgumentList.Add("/query");
                psi.ArgumentList.Add("/tn");
                psi.ArgumentList.Add(TaskName);

                using Process? proc = Process.Start(psi);
                if (proc == null) return false;
                proc.WaitForExit(2000);
                return proc.ExitCode == 0;
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                return false;
            }
        }

        public static void SetStartup(bool enable)
        {
            try
            {
                if (enable)
                {
                    string exePath = Application.ExecutablePath;
                    var psi = new ProcessStartInfo
                    {
                        FileName = "schtasks.exe",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                    psi.ArgumentList.Add("/create");
                    psi.ArgumentList.Add("/tn");
                    psi.ArgumentList.Add(TaskName);
                    psi.ArgumentList.Add("/tr");
                    psi.ArgumentList.Add($"\"{exePath}\"");
                    psi.ArgumentList.Add("/sc");
                    psi.ArgumentList.Add("onlogon");
                    psi.ArgumentList.Add("/rl");
                    psi.ArgumentList.Add("highest");
                    psi.ArgumentList.Add("/f");

                    using Process? proc = Process.Start(psi);
                    proc?.WaitForExit(3000);
                }
                else
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "schtasks.exe",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                    psi.ArgumentList.Add("/delete");
                    psi.ArgumentList.Add("/tn");
                    psi.ArgumentList.Add(TaskName);
                    psi.ArgumentList.Add("/f");

                    using Process? proc = Process.Start(psi);
                    proc?.WaitForExit(3000);
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }
    }
}

