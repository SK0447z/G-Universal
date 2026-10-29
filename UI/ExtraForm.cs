using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GUniversal
{
    public class ExtraForm : Form
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_ROUND = 2;

        private readonly MainForm _mainForm;
        private readonly ILaptopProvider _provider;
        private readonly AppSettings _settings;
        private readonly OsdWindow _osd;
        private readonly MetricsOverlayWindow _overlay;

        private CustomHotkeyRule? _recordingRule;
        private GButton? _recordingButton;

        private GDropdown _cmbM4 = null!;
        private GDropdown _cmbFan = null!;

        private GCheckBox _cbOverlay = null!;
        private Label _lblDimming = null!;
        private GSlider _sliderDimming = null!;
        private GCheckBox? _cbFnLock;
        private GCheckBox? _cbBootSound;
        private GCheckBox? _cbOverdrive;

        private Label _lblServices = null!;
        private GButton _btnStopServices = null!;
        private GButton _btnStartServices = null!;
        private GButton _btnKillDgpu = null!;
        private GButton _btnRestartGpu = null!;

        private static readonly string[] AvailableActions = new[]
        {
            "Toggle Window",
            "Cycle Mode",
            "Toggle Turbo/Silent",
            "Toggle CPU Boost",
            "Toggle 60Hz/MaxHz",
            "Toggle Overlay",
            "Mute Mic",
            "Kill GPU Apps",
            "Play/Pause Media",
            "Screenshot",
            "Profile: Gaming (Turbo + MaxHz + Boost On)",
            "Profile: Eco/Study (Silent + 60Hz + Boost Off)",
            "Launch Custom App (.exe)..."
        };

        public ExtraForm(
            MainForm mainForm,
            ILaptopProvider provider,
            AppSettings settings,
            OsdWindow osd,
            MetricsOverlayWindow overlay)
        {
            _mainForm = mainForm;
            _provider = provider;
            _settings = settings;
            _osd = osd;
            _overlay = overlay;

            InitializeWindow();
            EnsureDefaultCustomHotkeys();
            BuildUI();
            LoadState();
        }

        private void InitializeWindow()
        {
            Text = "Extra Settings";
            ClientSize = new Size(380, 620);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = MainForm.FormBack;
            ForeColor = MainForm.TextMain;
            Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            DoubleBuffered = true;
            KeyPreview = true;
            AutoScroll = true;

            int darkMode = 1;
            DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));
            int cornerPref = DWMWCP_ROUND;
            DwmSetWindowAttribute(Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPref, sizeof(int));
        }

        private void EnsureDefaultCustomHotkeys()
        {
            if (_settings.CustomHotkeys == null || _settings.CustomHotkeys.Count == 0)
            {
                _settings.CustomHotkeys = new List<CustomHotkeyRule>
                {
                    new CustomHotkeyRule
                    {
                        KeyCombination = "Ctrl+Shift+1",
                        Modifiers = HotkeyManager.MOD_CONTROL | HotkeyManager.MOD_SHIFT,
                        VirtualKey = (uint)Keys.D1,
                        Action = "Profile: Gaming (Turbo + MaxHz + Boost On)"
                    },
                    new CustomHotkeyRule
                    {
                        KeyCombination = "Ctrl+Shift+2",
                        Modifiers = HotkeyManager.MOD_CONTROL | HotkeyManager.MOD_SHIFT,
                        VirtualKey = (uint)Keys.D2,
                        Action = "Profile: Eco/Study (Silent + 60Hz + Boost Off)"
                    }
                };
                Task.Run(() => _settings.Save());
            }
        }

        private void BuildUI()
        {
            Controls.Clear();

            int margin = 8;
            int cardWidth = 364;
            int innerW = cardWidth - 20;
            int currentY = 8;

            int labelColW = 140;
            int dropColW = innerW - labelColW - 6;
            int dropX = 10 + labelColW + 6;

            int customRulesCount = _settings.CustomHotkeys?.Count ?? 0;
            int cardKeysHeight = 100 + (customRulesCount * 34) + 36 + 10;
            GCard cardKeys = new GCard { Location = new Point(margin, currentY), Size = new Size(cardWidth, cardKeysHeight) };
            cardKeys.Controls.Add(CreateCardHeaderLabel("Hotkeys & Custom Tasks", 10, 8));

            Label lblM4 = CreateFieldLabel("M4 / OEM Key", 10, 36);
            _cmbM4 = new GDropdown { Location = new Point(dropX, 32), Size = new Size(dropColW, 26) };
            _cmbM4.SetItems(AvailableActions, NormalizeAction(_settings.M4Action, AvailableActions, "Toggle Window"));
            _cmbM4.SelectedChanged += (s, e) =>
            {
                HandleActionSelection(_cmbM4.SelectedItem, act => _settings.M4Action = act);
            };
            cardKeys.Controls.Add(lblM4);
            cardKeys.Controls.Add(_cmbM4);

            Label lblFan = CreateFieldLabel("Fn+F5 / Fan Key", 10, 68);
            _cmbFan = new GDropdown { Location = new Point(dropX, 64), Size = new Size(dropColW, 26) };
            _cmbFan.SetItems(AvailableActions, NormalizeAction(_settings.FnF5Action, AvailableActions, "Cycle Mode"));
            _cmbFan.SelectedChanged += (s, e) =>
            {
                HandleActionSelection(_cmbFan.SelectedItem, act => _settings.FnF5Action = act);
            };
            cardKeys.Controls.Add(lblFan);
            cardKeys.Controls.Add(_cmbFan);

            Label lblCustomHeader = CreateSubLabel("Custom Hotkey Rules (Interactive Recorder):", 10, 96);
            cardKeys.Controls.Add(lblCustomHeader);

            int ruleY = 118;
            if (_settings.CustomHotkeys != null)
            {
                foreach (var rule in _settings.CustomHotkeys)
                {
                    var capturedRule = rule;

                    GButton btnRecord = new GButton
                    {
                        Text = string.IsNullOrWhiteSpace(capturedRule.KeyCombination) ? "Record Key..." : capturedRule.KeyCombination,
                        Location = new Point(10, ruleY),
                        Size = new Size(110, 26),
                        Font = new Font("Segoe UI", 8F)
                    };
                    btnRecord.Click += (s, e) => ToggleKeyRecording(capturedRule, btnRecord);

                    int actionDropW = innerW - 110 - 32 - 12;
                    GDropdown cmbAction = new GDropdown
                    {
                        Location = new Point(126, ruleY),
                        Size = new Size(actionDropW, 26)
                    };
                    cmbAction.SetItems(AvailableActions, NormalizeAction(capturedRule.Action, AvailableActions, "Profile: Gaming (Turbo + MaxHz + Boost On)"));
                    cmbAction.SelectedChanged += (s, e) =>
                    {
                        HandleActionSelection(cmbAction.SelectedItem, act => capturedRule.Action = act, capturedRule);
                    };

                    GButton btnDelete = new GButton
                    {
                        Text = "\u00D7",
                        Location = new Point(126 + actionDropW + 6, ruleY),
                        Size = new Size(26, 26),
                        Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
                    };
                    btnDelete.Click += (s, e) =>
                    {
                        _settings.CustomHotkeys?.Remove(capturedRule);
                        Task.Run(() => _settings.Save());
                        _mainForm.ReRegisterCustomHotkeys();
                        BuildUI();
                    };

                    cardKeys.Controls.Add(btnRecord);
                    cardKeys.Controls.Add(cmbAction);
                    cardKeys.Controls.Add(btnDelete);

                    ruleY += 34;
                }
            }

            GButton btnAddRule = new GButton
            {
                Text = "+ Add Custom Hotkey Rule",
                Location = new Point(10, ruleY),
                Size = new Size(innerW, 26),
                Font = new Font("Segoe UI", 8.5F)
            };
            btnAddRule.Click += (s, e) =>
            {
                (_settings.CustomHotkeys ??= new List<CustomHotkeyRule>()).Add(new CustomHotkeyRule
                {
                    KeyCombination = "Record Key...",
                    Modifiers = 0,
                    VirtualKey = 0,
                    Action = "Profile: Gaming (Turbo + MaxHz + Boost On)"
                });
                Task.Run(() => _settings.Save());
                BuildUI();
            };
            cardKeys.Controls.Add(btnAddRule);

            Controls.Add(cardKeys);
            currentY += cardKeys.Height + 8;

            int card2Y = 32;

            _cbOverlay = new GCheckBox
            {
                Text = "In-Game Metrics Overlay (Ctrl+Shift+Alt+O)",
                Location = new Point(10, card2Y),
                Size = new Size(innerW, 24),
                BackColor = MainForm.CardBack,
                Checked = _settings.InGameOverlay
            };
            _cbOverlay.CheckedChanged += (s, e) =>
            {
                _settings.InGameOverlay = _cbOverlay.Checked;
                _overlay.SetEnabled(_cbOverlay.Checked);
                Task.Run(() => _settings.Save());
            };
            card2Y += 30;

            _lblDimming = CreateSubLabel($"Screen Dimmer (Flicker-Free): {_settings.ScreenDimmingPercent}%", 10, card2Y);
            card2Y += 20;

            _sliderDimming = new GSlider
            {
                Location = new Point(10, card2Y),
                Size = new Size(innerW, 26),
                Minimum = 20,
                Maximum = 100,
                Value = Math.Clamp(_settings.ScreenDimmingPercent, 20, 100)
            };
            _sliderDimming.ValueChanged += (s, e) =>
            {
                _lblDimming.Text = $"Screen Dimmer (Flicker-Free): {_sliderDimming.Value}%";
                _settings.ScreenDimmingPercent = _sliderDimming.Value;
                GammaRampHelper.SetDimming(_sliderDimming.Value / 100.0);
                Task.Run(() => _settings.Save());
            };
            card2Y += 32;

            bool supFnLock = ExtraSettingsHelper.IsSupported(_provider, ExtraSettingsHelper.DEVID_FN_LOCK);
            bool supBootSound = ExtraSettingsHelper.IsSupported(_provider, ExtraSettingsHelper.DEVID_BOOT_SOUND);
            bool supOverdrive = ExtraSettingsHelper.IsSupported(_provider, ExtraSettingsHelper.DEVID_PANEL_OVERDRIVE);

            if (supFnLock)
            {
                _cbFnLock = new GCheckBox
                {
                    Text = "Fn-Lock (Invert F1-F12 Keys)",
                    Location = new Point(10, card2Y),
                    Size = new Size(innerW, 24),
                    BackColor = MainForm.CardBack,
                    Checked = _settings.FnLock
                };
                _cbFnLock.CheckedChanged += (s, e) =>
                {
                    _settings.FnLock = _cbFnLock.Checked;
                    Task.Run(() =>
                    {
                        ExtraSettingsHelper.SetFnLock(_provider, _cbFnLock.Checked);
                        _settings.Save();
                    });
                };
                card2Y += 28;
            }

            if (supBootSound)
            {
                _cbBootSound = new GCheckBox
                {
                    Text = "Boot Sound",
                    Location = new Point(10, card2Y),
                    Size = new Size(innerW, 24),
                    BackColor = MainForm.CardBack,
                    Checked = _settings.BootSound
                };
                _cbBootSound.CheckedChanged += (s, e) =>
                {
                    _settings.BootSound = _cbBootSound.Checked;
                    Task.Run(() =>
                    {
                        ExtraSettingsHelper.SetBootSound(_provider, _cbBootSound.Checked);
                        _settings.Save();
                    });
                };
                card2Y += 28;
            }

            if (supOverdrive)
            {
                _cbOverdrive = new GCheckBox
                {
                    Text = "Panel Overdrive",
                    Location = new Point(10, card2Y),
                    Size = new Size(innerW, 24),
                    BackColor = MainForm.CardBack,
                    Checked = _settings.PanelOverdrive
                };
                _cbOverdrive.CheckedChanged += (s, e) =>
                {
                    _settings.PanelOverdrive = _cbOverdrive.Checked;
                    Task.Run(() =>
                    {
                        ExtraSettingsHelper.SetPanelOverdrive(_provider, _cbOverdrive.Checked);
                        _settings.Save();
                    });
                };
                card2Y += 28;
            }

            int card2Height = card2Y + 8;
            GCard cardExtras = new GCard { Location = new Point(margin, currentY), Size = new Size(cardWidth, card2Height) };
            cardExtras.Controls.Add(CreateCardHeaderLabel("Display & Overlay Tools", 10, 8));
            cardExtras.Controls.Add(_cbOverlay);
            cardExtras.Controls.Add(_lblDimming);
            cardExtras.Controls.Add(_sliderDimming);
            if (_cbFnLock != null) cardExtras.Controls.Add(_cbFnLock);
            if (_cbBootSound != null) cardExtras.Controls.Add(_cbBootSound);
            if (_cbOverdrive != null) cardExtras.Controls.Add(_cbOverdrive);

            Controls.Add(cardExtras);
            currentY += cardExtras.Height + 8;

            GCard cardServices = new GCard { Location = new Point(margin, currentY), Size = new Size(cardWidth, 172) };
            cardServices.Controls.Add(CreateCardHeaderLabel("OEM Services & GPU Control", 10, 8));

            _lblServices = CreateSubLabel("Running OEM Services: Calculating...", 10, 32);
            cardServices.Controls.Add(_lblServices);

            int btnHalfW = (innerW - 6) / 2;
            _btnStopServices = new GButton { Text = "Stop Services", Location = new Point(10, 56), Size = new Size(btnHalfW, 30) };
            _btnStartServices = new GButton { Text = "Start Services", Location = new Point(10 + btnHalfW + 6, 56), Size = new Size(btnHalfW, 30) };

            _btnStopServices.Click += (s, e) =>
            {
                _osd.ShowToast("Stopping OEM Services...");
                Task.Run(() =>
                {
                    OemServiceManager.StopOemServices();
                    RefreshRunningServiceCount();
                });
            };

            _btnStartServices.Click += (s, e) =>
            {
                _osd.ShowToast("Starting OEM Services...");
                Task.Run(() =>
                {
                    OemServiceManager.StartOemServices();
                    RefreshRunningServiceCount();
                });
            };

            cardServices.Controls.Add(_btnStopServices);
            cardServices.Controls.Add(_btnStartServices);

            _btnKillDgpu = new GButton { Text = "Kill dGPU Background Apps", Location = new Point(10, 94), Size = new Size(innerW, 30) };
            _btnKillDgpu.Click += (s, e) =>
            {
                _osd.ShowToast("Scanning dGPU processes...");
                Task.Run(() =>
                {
                    int killed = OemServiceManager.KillDgpuBackgroundApps();
                    if (!IsDisposed && IsHandleCreated)
                    {
                        BeginInvoke(new Action(() =>
                        {
                            _osd.ShowToast(killed > 0 ? $"Killed {killed} dGPU Apps" : "No dGPU Apps Running");
                            RefreshRunningServiceCount();
                        }));
                    }
                });
            };
            cardServices.Controls.Add(_btnKillDgpu);

            _btnRestartGpu = new GButton { Text = "Restart GPU Driver (pnputil Reset)", Location = new Point(10, 130), Size = new Size(innerW, 30) };
            _btnRestartGpu.Click += (s, e) =>
            {
                _osd.ShowToast("Restarting GPU Driver...");
                Task.Run(() => OemServiceManager.RestartGpuDriver());
            };
            cardServices.Controls.Add(_btnRestartGpu);

            Controls.Add(cardServices);
            currentY += cardServices.Height + 12;

            int targetH = currentY;
            int maxH = (Screen.PrimaryScreen?.WorkingArea.Height ?? 900) - 60;
            ClientSize = new Size(380, Math.Min(targetH, maxH));

            _mainForm.PositionExtraForm();
        }

        private void ToggleKeyRecording(CustomHotkeyRule rule, GButton btn)
        {
            if (_recordingRule == rule)
            {
                btn.Text = string.IsNullOrWhiteSpace(rule.KeyCombination) ? "Record Key..." : rule.KeyCombination;
                btn.IsActive = false;
                _recordingRule = null;
                _recordingButton = null;
                return;
            }

            if (_recordingButton != null && _recordingRule != null)
            {
                _recordingButton.Text = string.IsNullOrWhiteSpace(_recordingRule.KeyCombination) ? "Record Key..." : _recordingRule.KeyCombination;
                _recordingButton.IsActive = false;
            }

            _recordingRule = rule;
            _recordingButton = btn;
            btn.Text = "Press keys...";
            btn.IsActive = true;
            btn.ActiveColor = Color.FromArgb(220, 50, 70);
            Focus();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (_recordingRule != null && _recordingButton != null)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;

                if (e.KeyCode == Keys.Escape)
                {
                    _recordingButton.Text = string.IsNullOrWhiteSpace(_recordingRule.KeyCombination) ? "Record Key..." : _recordingRule.KeyCombination;
                    _recordingButton.IsActive = false;
                    _recordingRule = null;
                    _recordingButton = null;
                    return;
                }

                if (e.KeyCode == Keys.ControlKey || e.KeyCode == Keys.ShiftKey ||
                    e.KeyCode == Keys.Menu || e.KeyCode == Keys.LWin || e.KeyCode == Keys.RWin ||
                    e.KeyCode == Keys.None || e.KeyCode == Keys.Capital)
                {
                    return;
                }

                uint mods = 0;
                List<string> parts = new();
                if (e.Control) { mods |= HotkeyManager.MOD_CONTROL; parts.Add("Ctrl"); }
                if (e.Alt) { mods |= HotkeyManager.MOD_ALT; parts.Add("Alt"); }
                if (e.Shift) { mods |= HotkeyManager.MOD_SHIFT; parts.Add("Shift"); }

                string keyName = FormatKeyName(e.KeyCode);
                parts.Add(keyName);
                string combo = string.Join("+", parts);

                _recordingRule.KeyCombination = combo;
                _recordingRule.Modifiers = mods;
                _recordingRule.VirtualKey = (uint)e.KeyCode;

                _recordingButton.Text = combo;
                _recordingButton.IsActive = false;
                _recordingRule = null;
                _recordingButton = null;

                Task.Run(() => _settings.Save());
                _mainForm.ReRegisterCustomHotkeys();
                _osd.ShowToast($"Hotkey Registered: {combo}");
                return;
            }

            base.OnKeyDown(e);
        }

        private static string FormatKeyName(Keys key)
        {
            if (key >= Keys.D0 && key <= Keys.D9) return ((int)(key - Keys.D0)).ToString();
            if (key >= Keys.NumPad0 && key <= Keys.NumPad9) return $"Num{((int)(key - Keys.NumPad0))}";
            return key switch
            {
                Keys.Oemtilde => "`",
                Keys.OemMinus => "-",
                Keys.Oemplus => "=",
                Keys.OemOpenBrackets => "[",
                Keys.OemCloseBrackets => "]",
                Keys.Oem1 => ";",
                Keys.Oem7 => "'",
                Keys.Oemcomma => ",",
                Keys.OemPeriod => ".",
                Keys.OemQuestion => "/",
                Keys.OemBackslash => "\\",
                _ => key.ToString()
            };
        }

        private void HandleActionSelection(string selected, Action<string> applyAction, CustomHotkeyRule? rule = null)
        {
            if (selected.StartsWith("Launch Custom App", StringComparison.OrdinalIgnoreCase))
            {
                using var ofd = new OpenFileDialog
                {
                    Title = "Select Application to Launch (.exe / .lnk)",
                    Filter = "Executables (*.exe;*.lnk)|*.exe;*.lnk"
                };
                if (ofd.ShowDialog(this) == DialogResult.OK)
                {
                    string ext = Path.GetExtension(ofd.FileName);
                    if (File.Exists(ofd.FileName) && (ext.Equals(".exe", StringComparison.OrdinalIgnoreCase) || ext.Equals(".lnk", StringComparison.OrdinalIgnoreCase)))
                    {
                        if (rule != null) rule.CustomAppPath = ofd.FileName;
                        _settings.CustomAppPath = ofd.FileName;
                        _osd.ShowToast($"Custom App: {Path.GetFileName(ofd.FileName)}");
                    }
                    else
                    {
                        _osd.ShowToast("Invalid executable (.exe or .lnk required)");
                    }
                }
            }

            applyAction(selected);
            Task.Run(() => _settings.Save());
        }

        private static string NormalizeAction(string current, string[] validActions, string fallback)
        {
            if (string.IsNullOrWhiteSpace(current)) return fallback;
            foreach (string action in validActions)
            {
                if (action.Equals(current, StringComparison.OrdinalIgnoreCase))
                    return action;
            }
            return fallback;
        }

        private void LoadState()
        {
            int dim = Math.Clamp(_settings.ScreenDimmingPercent, 20, 100);
            _sliderDimming.Value = dim;
            _lblDimming.Text = $"Screen Dimmer (Flicker-Free): {dim}%";
            if (dim < 100)
            {
                GammaRampHelper.SetDimming(dim / 100.0);
            }

            _overlay.SetEnabled(_settings.InGameOverlay);
            RefreshRunningServiceCount();
        }

        public void SyncOverlayCheckbox(bool isEnabled)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => SyncOverlayCheckbox(isEnabled)));
                return;
            }
            if (_cbOverlay.Checked != isEnabled)
            {
                _cbOverlay.Checked = isEnabled;
            }
        }

        private void RefreshRunningServiceCount()
        {
            Task.Run(() =>
            {
                int count = OemServiceManager.GetRunningServiceCount();
                if (!IsDisposed && IsHandleCreated)
                {
                    BeginInvoke(new Action(() =>
                    {
                        _lblServices.Text = $"Running OEM Services: {count}";
                        _lblServices.ForeColor = count > 0 ? MainForm.ActiveBlue : MainForm.TextMuted;
                    }));
                }
            });
        }

        private static Label CreateCardHeaderLabel(string text, int x, int y)
        {
            return new Label
            {
                Text = text,
                Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
                ForeColor = MainForm.TextMain,
                Location = new Point(x, y),
                AutoSize = true,
                UseMnemonic = false
            };
        }

        private static Label CreateFieldLabel(string text, int x, int y)
        {
            return new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = MainForm.TextMain,
                Location = new Point(x, y),
                AutoSize = true,
                UseMnemonic = false
            };
        }

        private static Label CreateSubLabel(string text, int x, int y)
        {
            return new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 8F, FontStyle.Regular),
                ForeColor = MainForm.TextMuted,
                Location = new Point(x, y),
                AutoSize = true,
                UseMnemonic = false
            };
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_recordingButton != null && _recordingRule != null)
            {
                _recordingButton.Text = string.IsNullOrWhiteSpace(_recordingRule.KeyCombination) ? "Record Key..." : _recordingRule.KeyCombination;
                _recordingButton.IsActive = false;
                _recordingRule = null;
                _recordingButton = null;
            }

            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                return;
            }
            base.OnFormClosing(e);
        }
    }
}

