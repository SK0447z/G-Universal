using System;
using System.Drawing;

namespace GUniversal
{
    public class GenericProvider : ILaptopProvider
    {
        public string ProviderName => "Universal Windows PC Controller";
        public bool IsConnected => true;
        public string ConnectionType => "Standard Windows ACPI / Universal Sensors";

        public bool SupportsPerformanceModes => false;
        public bool SupportsBatteryLimit => false;
        public bool SupportsKeyboardRgb => false;
        public bool SupportsKeyboardBrightness => false;
        public bool SupportsFanRpm => false;

        public int GetPerformanceMode() => -1;
        public int SetPerformanceMode(int mode) => -1;

        public int GetKeyboardBrightness() => 0;
        public int SetKeyboardBrightness(int level) => -1;

        public (string Name, int ModeId)[] GetSupportedRgbModes() => Array.Empty<(string Name, int ModeId)>();
        public int SetKeyboardRgb(int mode, Color color, int speed = 1) => -1;
        public int SetTufKeyboardRGB(int mode, Color color, int speed = 1) => -1;
        public int SetTufKeyboardPower(uint power = 0x00FF01BD) => -1;

        public int SetBatteryChargeLimit(int limitPercent) => -1;

        public int GetCpuTemperature() => -1;
        public int GetGpuTemperature() => -1;
        public int GetCpuFanSpeed() => 0;
        public int GetGpuFanSpeed() => 0;

        public void Dispose() { }
    }
}

