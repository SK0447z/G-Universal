using System;
using System.Management;
using System.Runtime.InteropServices;

namespace GUniversal
{
    public static class ExtraSettingsHelper
    {
        public const uint DEVID_FN_LOCK = 0x00100023;
        public const uint DEVID_BOOT_SOUND = 0x00130022;
        public const uint DEVID_PANEL_OVERDRIVE = 0x00050019;

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private const byte VK_VOLUME_MUTE = 0xAD;
        private const byte VK_MEDIA_PLAY_PAUSE = 0xB3;
        private const byte VK_SNAPSHOT = 0x2C;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        public static bool IsSupported(ILaptopProvider provider, uint devId)
        {
            if (provider is AsusProvider asus)
            {
                try
                {
                    return asus.DeviceGet(devId) >= 0;
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                    return false;
                }
            }

            if (devId == DEVID_FN_LOCK && provider is LenovoProvider)
            {
                try
                {
                    using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM LENOVO_OTHER_METHOD");
                    foreach (ManagementObject _ in searcher.Get())
                    {
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                }
            }

            return false;
        }

        public static int SetFnLock(ILaptopProvider provider, bool enabled)
        {
            uint val = enabled ? 1u : 0u;

            if (provider is AsusProvider asus)
            {
                return asus.DeviceSet(DEVID_FN_LOCK, val);
            }

            try
            {
                using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM LENOVO_OTHER_METHOD");
                foreach (ManagementObject instance in searcher.Get())
                {
                    ManagementBaseObject inParams = instance.GetMethodParameters("SetFnLock");
                    inParams["LockState"] = enabled ? 1 : 0;
                    instance.InvokeMethod("SetFnLock", inParams, null);
                    return 1;
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }

            return 0;
        }

        public static int SetBootSound(ILaptopProvider provider, bool enabled)
        {
            uint val = enabled ? 1u : 0u;
            if (provider is AsusProvider asus)
            {
                return asus.DeviceSet(DEVID_BOOT_SOUND, val);
            }
            return 0;
        }

        public static int SetPanelOverdrive(ILaptopProvider provider, bool enabled)
        {
            uint val = enabled ? 1u : 0u;
            if (provider is AsusProvider asus)
            {
                return asus.DeviceSet(DEVID_PANEL_OVERDRIVE, val);
            }
            return 0;
        }

        public static void ToggleMicMute()
        {
            try
            {
                keybd_event(VK_VOLUME_MUTE, 0, 0, UIntPtr.Zero);
                keybd_event(VK_VOLUME_MUTE, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        public static void SendPlayPause()
        {
            try
            {
                keybd_event(VK_MEDIA_PLAY_PAUSE, 0, 0, UIntPtr.Zero);
                keybd_event(VK_MEDIA_PLAY_PAUSE, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        public static void SendScreenshot()
        {
            try
            {
                keybd_event(VK_SNAPSHOT, 0, 0, UIntPtr.Zero);
                keybd_event(VK_SNAPSHOT, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }
    }
}

