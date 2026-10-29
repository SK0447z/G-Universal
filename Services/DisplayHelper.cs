using System;
using System.Runtime.InteropServices;

namespace GUniversal
{
    public static class DisplayHelper
    {
        private const int ENUM_CURRENT_SETTINGS = -1;
        private const int DM_DISPLAYFREQUENCY = 0x00400000;
        private const int CDS_UPDATEREGISTRY = 0x00000001;
        private const int DISP_CHANGE_SUCCESSFUL = 0;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public int dmDisplayOrientation;
            public int dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
            public int dmICMMethod;
            public int dmICMIntent;
            public int dmMediaType;
            public int dmDitherType;
            public int dmReserved1;
            public int dmReserved2;
            public int dmPanningWidth;
            public int dmPanningHeight;
        }

        [DllImport("user32.dll", CharSet = CharSet.Ansi)]
        private static extern bool EnumDisplaySettingsA(string? lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode);

        [DllImport("user32.dll", CharSet = CharSet.Ansi)]
        private static extern int ChangeDisplaySettingsA(ref DEVMODE lpDevMode, int dwFlags);

        private static DEVMODE CreateDevMode()
        {
            DEVMODE dm = new DEVMODE
            {
                dmDeviceName = new string('\0', 32),
                dmFormName = new string('\0', 32)
            };
            dm.dmSize = (short)Marshal.SizeOf(dm);
            return dm;
        }

        public static int GetCurrentRefreshRate()
        {
            DEVMODE dm = CreateDevMode();
            if (EnumDisplaySettingsA(null, ENUM_CURRENT_SETTINGS, ref dm))
            {
                return dm.dmDisplayFrequency;
            }
            return 60;
        }

        public static int GetMaxRefreshRate()
        {
            DEVMODE current = CreateDevMode();
            if (!EnumDisplaySettingsA(null, ENUM_CURRENT_SETTINGS, ref current))
            {
                return 60;
            }

            int maxHz = current.dmDisplayFrequency;
            int modeIndex = 0;
            DEVMODE mode = CreateDevMode();

            while (EnumDisplaySettingsA(null, modeIndex, ref mode))
            {
                if (mode.dmPelsWidth == current.dmPelsWidth &&
                    mode.dmPelsHeight == current.dmPelsHeight &&
                    mode.dmDisplayFrequency > maxHz)
                {
                    maxHz = mode.dmDisplayFrequency;
                }
                modeIndex++;
            }

            return maxHz;
        }

        public static bool SetRefreshRate(int targetHz)
        {
            DEVMODE dm = CreateDevMode();
            if (!EnumDisplaySettingsA(null, ENUM_CURRENT_SETTINGS, ref dm))
            {
                return false;
            }

            if (dm.dmDisplayFrequency == targetHz)
            {
                return true;
            }

            dm.dmDisplayFrequency = targetHz;
            dm.dmFields = DM_DISPLAYFREQUENCY;

            int result = ChangeDisplaySettingsA(ref dm, CDS_UPDATEREGISTRY);
            return result == DISP_CHANGE_SUCCESSFUL;
        }
    }
}

