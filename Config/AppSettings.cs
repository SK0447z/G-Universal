using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;

namespace GUniversal
{
    public class CustomHotkeyRule
    {
        public int Id { get; set; } = 0;
        public string KeyCombination { get; set; } = "Ctrl+Shift+1";
        public uint Modifiers { get; set; } = 0x0006; // MOD_CONTROL (2) | MOD_SHIFT (4)
        public uint VirtualKey { get; set; } = (uint)Keys.D1;
        public string Action { get; set; } = "Profile: Gaming (Turbo + MaxHz + Boost On)";
        public string CustomAppPath { get; set; } = "";
    }

    public class AppSettings
    {
        public bool AutoScreenRefreshOnBattery { get; set; } = true;
        public int PerformanceMode { get; set; } = 0; // 0 = Balanced, 1 = Turbo, 2 = Silent
        public int CpuBoostMode { get; set; } = 2;    // 0 = Disabled, 4 = Efficient, 2 = Aggressive
        public int KeyboardBrightness { get; set; } = 1; // 0 = Off, 1 = Low, 2 = Med, 3 = High
        public int KeyboardRgbMode { get; set; } = 0;
        public string KeyboardRgbColor { get; set; } = "#FF0032";
        public int KeyboardRgbSpeed { get; set; } = 1; // 0 = Slow, 1 = Medium, 2 = Fast
        public List<string> SavedColors { get; set; } = new List<string>
        {
            "#FF0032", "#00E5FF", "#00E676", "#FFEA00",
            "#D500F9", "#FF6D00", "#2979FF", "#FFFFFF"
        };
        public int BatteryChargeLimit { get; set; } = 80;
        public bool RunOnStartup { get; set; } = false;
        public int BrandPreviewIndex { get; set; } = 0;

        public string M4Action { get; set; } = "Toggle Window";
        public string FnF5Action { get; set; } = "Cycle Mode";
        public string M3Action { get; set; } = "Mute Microphone";
        public string GlobalShortcutAction { get; set; } = "Toggle Window";
        public string ModeShortcutAction { get; set; } = "Cycle Mode";
        public bool FnLock { get; set; } = false;
        public bool BootSound { get; set; } = true;
        public bool PanelOverdrive { get; set; } = false;
        public bool InGameOverlay { get; set; } = false;
        public int ScreenDimmingPercent { get; set; } = 100;

        public string CustomAppPath { get; set; } = "";
        public List<CustomHotkeyRule> CustomHotkeys { get; set; } = new();

        private static readonly string SettingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
        private static readonly object _saveLock = new object();

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    string json = File.ReadAllText(SettingsFilePath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    if (settings != null)
                    {
                        settings.CustomHotkeys ??= new List<CustomHotkeyRule>();
                        if (settings.SavedColors == null || settings.SavedColors.Count == 0)
                        {
                            settings.SavedColors = new List<string>
                            {
                                "#FF0032", "#00E5FF", "#00E676", "#FFEA00",
                                "#D500F9", "#FF6D00", "#2979FF", "#FFFFFF"
                            };
                        }
                        return settings;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }

            return new AppSettings();
        }

        public void Save()
        {
            lock (_saveLock)
            {
                try
                {
                    string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(SettingsFilePath, json);
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                }
            }
        }
    }
}

