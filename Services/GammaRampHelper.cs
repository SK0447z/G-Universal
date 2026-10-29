using System;
using System.Runtime.InteropServices;

namespace GUniversal
{
    public static class GammaRampHelper
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private struct RAMP
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public ushort[] Red;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public ushort[] Green;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public ushort[] Blue;
        }

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool SetDeviceGammaRamp(IntPtr hDC, ref RAMP lpRamp);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        public static void SetDimming(double percentage)
        {
            try
            {
                percentage = Math.Clamp(percentage, 0.20, 1.00);

                RAMP ramp = new RAMP
                {
                    Red = new ushort[256],
                    Green = new ushort[256],
                    Blue = new ushort[256]
                };

                for (int i = 0; i < 256; i++)
                {
                    int val = (int)Math.Round(i * 256 * percentage);
                    if (val > 65535) val = 65535;
                    ramp.Red[i] = (ushort)val;
                    ramp.Green[i] = (ushort)val;
                    ramp.Blue[i] = (ushort)val;
                }

                IntPtr hdc = GetDC(IntPtr.Zero);
                if (hdc != IntPtr.Zero)
                {
                    SetDeviceGammaRamp(hdc, ref ramp);
                    ReleaseDC(IntPtr.Zero, hdc);
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }
    }
}

