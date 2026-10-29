using System;
using System.Collections.Generic;
using System.Management;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace GUniversal
{
    public class HotkeyManager : IDisposable
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public const int HOTKEY_ID_MODE_SHORTCUT = 9001;   // Ctrl + Shift + F5
        public const int HOTKEY_ID_TOGGLE_WINDOW = 9002;    // Ctrl + Shift + G
        public const int HOTKEY_ID_GLOBAL_SHORTCUT = 9003; // Ctrl + Shift + F12
        public const int HOTKEY_ID_M3 = 9004;              // Ctrl + Shift + F20
        public const int HOTKEY_ID_OVERLAY = 9005;         // Ctrl + Shift + Alt + O

        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;
        public const uint MOD_WIN = 0x0008;
        public const uint MOD_NOREPEAT = 0x4000;

        public event Action? M4Triggered;
        public event Action? FanTriggered;
        public event Action? GlobalShortcutTriggered;
        public event Action? ModeShortcutTriggered;
        public event Action? M3Triggered;
        public event Action? ToggleOverlayTriggered;
        public event Action? ToggleWindowRequested;
        public event Action<int>? BrightnessStepRequested;
        public event Action<CustomHotkeyRule>? CustomHotkeyTriggered;

        private readonly IntPtr _windowHandle;
        private readonly List<ManagementEventWatcher> _watchers = new();
        private readonly Dictionary<int, CustomHotkeyRule> _dynamicHotkeys = new();
        private int _nextDynamicId = 9100;
        private bool _disposed = false;

        public HotkeyManager(IntPtr windowHandle)
        {
            _windowHandle = windowHandle;
            RegisterGlobalHotkeys();
            InitializeWmiWatchers();
        }

        private void RegisterGlobalHotkeys()
        {
            if (_windowHandle == IntPtr.Zero) return;

            RegisterHotKey(_windowHandle, HOTKEY_ID_MODE_SHORTCUT, MOD_CONTROL | MOD_SHIFT | MOD_NOREPEAT, (uint)Keys.F5);
            RegisterHotKey(_windowHandle, HOTKEY_ID_TOGGLE_WINDOW, MOD_CONTROL | MOD_SHIFT | MOD_NOREPEAT, (uint)Keys.G);
            RegisterHotKey(_windowHandle, HOTKEY_ID_GLOBAL_SHORTCUT, MOD_CONTROL | MOD_SHIFT | MOD_NOREPEAT, (uint)Keys.F12);

            try
            {
                RegisterHotKey(_windowHandle, HOTKEY_ID_M3, MOD_CONTROL | MOD_SHIFT | MOD_NOREPEAT, (uint)Keys.F20);
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }

            RegisterHotKey(_windowHandle, HOTKEY_ID_OVERLAY, MOD_CONTROL | MOD_SHIFT | MOD_ALT | MOD_NOREPEAT, (uint)Keys.O);
        }

        private void UnregisterGlobalHotkeys()
        {
            if (_windowHandle == IntPtr.Zero) return;
            UnregisterHotKey(_windowHandle, HOTKEY_ID_MODE_SHORTCUT);
            UnregisterHotKey(_windowHandle, HOTKEY_ID_TOGGLE_WINDOW);
            UnregisterHotKey(_windowHandle, HOTKEY_ID_GLOBAL_SHORTCUT);
            UnregisterHotKey(_windowHandle, HOTKEY_ID_M3);
            UnregisterHotKey(_windowHandle, HOTKEY_ID_OVERLAY);
        }

        public void RegisterCustomHotkeys(IEnumerable<CustomHotkeyRule> rules)
        {
            UnregisterAllCustomHotkeys();
            if (_windowHandle == IntPtr.Zero || rules == null) return;

            foreach (var rule in rules)
            {
                if (rule.VirtualKey == 0) continue;
                int id = _nextDynamicId++;
                rule.Id = id;
                uint mods = rule.Modifiers | MOD_NOREPEAT;
                try
                {
                    if (RegisterHotKey(_windowHandle, id, mods, rule.VirtualKey))
                    {
                        _dynamicHotkeys[id] = rule;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                }
            }
        }

        public void UnregisterAllCustomHotkeys()
        {
            if (_windowHandle == IntPtr.Zero) return;
            foreach (int id in _dynamicHotkeys.Keys)
            {
                try
                {
                    UnregisterHotKey(_windowHandle, id);
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                }
            }
            _dynamicHotkeys.Clear();
        }

        private void InitializeWmiWatchers()
        {
            try
            {
                var scope = new ManagementScope(@"root\wmi");
                var query = new EventQuery("SELECT * FROM AsusAtkWmiEvent");
                var watcher = new ManagementEventWatcher(scope, query);
                watcher.EventArrived += (s, e) =>
                {
                    try
                    {
                        object? val = e.NewEvent.Properties["EventData"]?.Value;
                        if (val != null)
                        {
                            int code = Convert.ToInt32(val);
                            HandleAsusEventCode(code);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(ex);
                    }
                };
                watcher.Start();
                _watchers.Add(watcher);
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }

            try
            {
                var scope = new ManagementScope(@"root\wmi");
                var query = new EventQuery("SELECT * FROM LENOVO_GAMEZONE_SMART_FAN_MODE_EVENT");
                var watcher = new ManagementEventWatcher(scope, query);
                watcher.EventArrived += (s, e) =>
                {
                    FanTriggered?.Invoke();
                };
                watcher.Start();
                _watchers.Add(watcher);
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }

            try
            {
                var scope = new ManagementScope(@"root\wmi");
                var query = new EventQuery("SELECT * FROM hpqBEvnt");
                var watcher = new ManagementEventWatcher(scope, query);
                watcher.EventArrived += (s, e) =>
                {
                    M4Triggered?.Invoke();
                };
                watcher.Start();
                _watchers.Add(watcher);
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        private void HandleAsusEventCode(int code)
        {
            switch (code)
            {
                case 0xAE: // Fn+F5
                    FanTriggered?.Invoke();
                    break;
                case 0x38: // M4 / ROG
                    M4Triggered?.Invoke();
                    break;
                case 0x7C: // M3 / Mic mute
                    M3Triggered?.Invoke();
                    break;
                case 0xC4: // Brightness Down
                    BrightnessStepRequested?.Invoke(-1);
                    break;
                case 0xC5: // Brightness Up
                    BrightnessStepRequested?.Invoke(1);
                    break;
                case 0xBA: // Backlight Cycle
                    BrightnessStepRequested?.Invoke(1);
                    break;
            }
        }

        public void ProcessHotKeyMessage(int id)
        {
            if (_dynamicHotkeys.TryGetValue(id, out var rule))
            {
                CustomHotkeyTriggered?.Invoke(rule);
                return;
            }

            switch (id)
            {
                case HOTKEY_ID_MODE_SHORTCUT:
                    ModeShortcutTriggered?.Invoke();
                    break;
                case HOTKEY_ID_TOGGLE_WINDOW:
                    ToggleWindowRequested?.Invoke();
                    break;
                case HOTKEY_ID_GLOBAL_SHORTCUT:
                    GlobalShortcutTriggered?.Invoke();
                    break;
                case HOTKEY_ID_M3:
                    M3Triggered?.Invoke();
                    break;
                case HOTKEY_ID_OVERLAY:
                    ToggleOverlayTriggered?.Invoke();
                    break;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            UnregisterGlobalHotkeys();
            UnregisterAllCustomHotkeys();

            foreach (var watcher in _watchers)
            {
                try
                {
                    watcher.Stop();
                    watcher.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                }
            }
            _watchers.Clear();
        }
    }
}

