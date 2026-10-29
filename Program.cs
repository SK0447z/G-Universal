using System;
using System.Threading;
using System.Windows.Forms;

namespace GUniversal
{
    internal static class Program
    {
        private static Mutex? _mutex;

        [STAThread]
        static void Main()
        {
            const string appName = "GUniversal_SingleInstance_Mutex";
            _mutex = new Mutex(true, appName, out bool createdNew);

            if (!createdNew)
            {
                return;
            }

            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
    }
}
