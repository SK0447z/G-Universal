using System;
using System.Drawing;

namespace GUniversal
{
    public interface ILaptopProvider : IDisposable
    {
        string ProviderName { get; }
        bool IsConnected { get; }
        string ConnectionType { get; }

        bool SupportsPerformanceModes { get; }
        bool SupportsBatteryLimit { get; }
        bool SupportsKeyboardRgb { get; }
        bool SupportsKeyboardBrightness { get; }
        bool SupportsFanRpm { get; }

        int GetPerformanceMode();
        int SetPerformanceMode(int mode);

        int GetKeyboardBrightness();
        int SetKeyboardBrightness(int level);

        (string Name, int ModeId)[] GetSupportedRgbModes();
        int SetKeyboardRgb(int mode, Color color, int speed = 1);
        int SetTufKeyboardRGB(int mode, Color color, int speed = 1);
        int SetTufKeyboardPower(uint power = 0x00FF01BD);

        int SetBatteryChargeLimit(int limitPercent);

        int GetCpuTemperature();
        int GetGpuTemperature();
        int GetCpuFanSpeed();
        int GetGpuFanSpeed();
    }
}

