using System;
using System.IO;

namespace GUniversal
{
    public static class Logger
    {
        private static readonly object _lock = new object();
        private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.log");

        public static void Log(string message)
        {
            try
            {
                lock (_lock)
                {
                    File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
                }
            }
            catch
            {
            }
        }

        public static void Log(Exception ex)
        {
            try
            {
                lock (_lock)
                {
                    File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [ERROR] {ex.GetType().Name}: {ex.Message}{Environment.NewLine}{ex.StackTrace}{Environment.NewLine}");
                }
            }
            catch
            {
            }
        }
    }
}

