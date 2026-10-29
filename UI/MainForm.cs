using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace GUniversal
{
    public class MainForm : Form
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_ROUND = 2;

        public static readonly Color FormBack = Color.FromArgb(28, 28, 28);
        public static readonly Color CardBack = Color.FromArgb(37, 37, 37);
        public static readonly Color ButtonBack = Color.FromArgb(55, 55, 55);
        public static readonly Color BorderColor = Color.FromArgb(62, 62, 62);
        public static readonly Color TextMain = Color.FromArgb(222, 222, 222);
        public static readonly Color TextMuted = Color.FromArgb(145, 145, 145);
        public static readonly Color TextTelemetry = Color.FromArgb(176, 176, 176);

        public static readonly Color ActiveSilent = Color.FromArgb(6, 180, 138);
        public static readonly Color ActiveBlue = Color.FromArgb(58, 148, 239);
        public static readonly Color ActiveTurbo = Color.FromArgb(210, 50, 50);

        private ILaptopProvider _provider;
        private readonly AppSettings _settings;
        private readonly OsdWindow _osd;
        private readonly MetricsOverlayWindow _overlayWindow;
        private ExtraForm? _extraForm;
        private HotkeyManager? _hotkeyManager;
        private readonly System.Windows.Forms.Timer _sensorTimer;
        private NotifyIcon _trayIcon = null!;

        private int _isPollingSensors = 0;
        private int _maxRefreshRate = 60;
        private int _currentPerfMode = 0;       // 0=Balanced, 1=Turbo, 2=Silent
        private int _currentBoostMode = 2;      // 0=Disabled, 4=Efficient, 2=Aggressive
        private int _currentBrightness = 1;     // 0=Off, 1=Low, 2=Med, 3=High
        private int _currentRgbMode = 0;        // 0=Static, 1=Breathe, 2=Cycle, 10=Strobe
        private int _currentRgbSpeed = 1;       // 0=Slow, 1=Medium, 2=Fast
        private Color _currentRgbColor = Color.FromArgb(255, 0, 50);
        private int _currentBatteryLimit = 80;
        private int _pluggedInPerfMode = 0;
        private bool _reallyQuit = false;
        private GColorPickerForm? _colorPickerForm;

        private Panel _headerPanel = null!;
        private Label _lblModel = null!;
        private Label _lblTelemetry = null!;

        private Panel _footerPanel = null!;
        private GCheckBox _cbStartup = null!;
        private GDropdown _cmbBrandSelector = null!;
        private GButton _btnQuit = null!;

        private GCard _cardPerf = null!;
        private GCard _cardScreen = null!;
        private GCard _cardKbd = null!;
        private GCard _cardBattery = null!;

        private GButton _btnSilent = null!;
        private GButton _btnBalanced = null!;
        private GButton _btnTurbo = null!;
        private GButton _btnBoostDisabled = null!;
        private GButton _btnBoostEfficient = null!;
        private GButton _btnBoostAggressive = null!;

        private GCheckBox _cbAutoRefresh = null!;
        private GButton _btn60Hz = null!;
        private GButton _btnMaxHz = null!;

        private GButton _btnExtra = null!;
        private GButton _btnRgbStatic = null!;
        private GButton _btnRgbBreathe = null!;
        private GButton _btnRgbCycle = null!;
        private GButton _btnRgbStrobe = null!;
        private GColorButton _btnColor = null!;
        private GButton _btnBrightOff = null!;
        private GButton _btnBrightLow = null!;
        private GButton _btnBrightMed = null!;
        private GButton _btnBrightHigh = null!;

        private GButton _btnBat60 = null!;
        private GButton _btnBat80 = null!;
        private GButton _btnBat100 = null!;

        public MainForm()
        {
            _settings = AppSettings.Load();
            _osd = new OsdWindow();
            _overlayWindow = new MetricsOverlayWindow();

            _provider = CreateProviderByIndex(_settings.BrandPreviewIndex);

            InitializeWindow();
            BuildUI();
            InitializeTrayIcon();
            RearrangeCards();

            _hotkeyManager = new HotkeyManager(Handle);
            _hotkeyManager.M4Triggered += () => ExecuteAction(_settings.M4Action);
            _hotkeyManager.FanTriggered += () => ExecuteAction(_settings.FnF5Action);
            _hotkeyManager.GlobalShortcutTriggered += () => ExecuteAction(_settings.GlobalShortcutAction);
            _hotkeyManager.ModeShortcutTriggered += () => ExecuteAction(_settings.ModeShortcutAction);
            _hotkeyManager.M3Triggered += () => ExecuteAction(_settings.M3Action);
            _hotkeyManager.ToggleOverlayTriggered += ToggleOverlay;
            _hotkeyManager.ToggleWindowRequested += () =>
            {
                if (InvokeRequired) BeginInvoke(new Action(ToggleWindow));
                else ToggleWindow();
            };
            _hotkeyManager.BrightnessStepRequested += (delta) =>
            {
                if (InvokeRequired) BeginInvoke(new Action(() => StepKeyboardBrightness(delta)));
                else StepKeyboardBrightness(delta);
            };
            _hotkeyManager.CustomHotkeyTriggered += (rule) =>
            {
                if (InvokeRequired) BeginInvoke(new Action(() => ExecuteCustomRule(rule)));
                else ExecuteCustomRule(rule);
            };
            _hotkeyManager.RegisterCustomHotkeys(_settings.CustomHotkeys);

            SystemEvents.PowerModeChanged += OnPowerModeChanged;

            _sensorTimer = new System.Windows.Forms.Timer { Interval = 2000 };
            _sensorTimer.Tick += (s, e) => UpdateLiveSensorsAsync();
        }

        private static ILaptopProvider CreateProviderByIndex(int index) => index switch
        {
            0 => ProviderFactory.CreateAutoDetectedProvider(),
            1 => new LenovoProvider(),
            2 => new HpProvider(),
            3 => new AcerProvider(),
            4 => new DellProvider(),
            _ => new GenericProvider()
        };

        private void InitializeWindow()
        {
            Text = "G-Universal";
            ClientSize = new Size(420, 500);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = FormBack;
            ForeColor = TextMain;
            Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            DoubleBuffered = true;

            int darkMode = 1;
            DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));
            int cornerPref = DWMWCP_ROUND;
            DwmSetWindowAttribute(Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPref, sizeof(int));

            PositionNearSystemTray();
        }

        private void PositionNearSystemTray()
        {
            Rectangle workingArea = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
            int x = workingArea.Right - Width - 12;
            int y = workingArea.Bottom - Height - 12;
            Location = new Point(Math.Max(0, x), Math.Max(0, y));

            PositionExtraForm();
            if (_colorPickerForm != null && !_colorPickerForm.IsDisposed && _colorPickerForm.Visible)
            {
                _colorPickerForm.PositionNearMainForm(this);
            }
        }

        public void PositionExtraForm()
        {
            if (_extraForm == null || _extraForm.IsDisposed) return;
            int x = Left - _extraForm.Width - 8;
            if (x < 10) x = Right + 8;
            int y = Math.Max(10, Bottom - _extraForm.Height);
            _extraForm.Location = new Point(x, y);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            PositionNearSystemTray();
            LoadInitialState();
            _sensorTimer.Start();
        }

        protected override void OnLocationChanged(EventArgs e)
        {
            base.OnLocationChanged(e);
            if (_extraForm != null && _extraForm.Visible)
            {
                PositionExtraForm();
            }
            if (_colorPickerForm != null && !_colorPickerForm.IsDisposed && _colorPickerForm.Visible)
            {
                _colorPickerForm.PositionNearMainForm(this);
            }
        }

        private void BuildUI()
        {
            int cardMargin = 8;
            int cardWidth = ClientSize.Width - (cardMargin * 2);
            int innerW = cardWidth - 20;

            _headerPanel = new Panel
            {
                Location = new Point(cardMargin, 6),
                Size = new Size(cardWidth, 28),
                BackColor = FormBack
            };

            _lblModel = new Label
            {
                Text = HardwareScanner.GetLaptopBrandAndModel(),
                Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
                ForeColor = TextMain,
                Location = new Point(0, 4),
                Size = new Size(115, 20),
                AutoEllipsis = true,
                UseMnemonic = false
            };

            _lblTelemetry = new Label
            {
                Text = "CPU: --\u00B0C 0RPM | GPU: --\u00B0C 0RPM",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = TextTelemetry,
                Location = new Point(118, 4),
                Size = new Size(cardWidth - 118, 20),
                TextAlign = ContentAlignment.MiddleRight,
                UseMnemonic = false
            };

            _headerPanel.Controls.Add(_lblModel);
            _headerPanel.Controls.Add(_lblTelemetry);
            Controls.Add(_headerPanel);

            _cardPerf = new GCard { Size = new Size(cardWidth, 130) };

            Label lblPerfHeader = CreateCardHeaderLabel("Mode", 10, 8);

            int btn3W = (innerW - 12) / 3;
            _btnSilent = new GButton { Text = "Silent", Location = new Point(10, 30), Size = new Size(btn3W, 32), ActiveColor = ActiveSilent };
            _btnBalanced = new GButton { Text = "Balanced", Location = new Point(10 + btn3W + 6, 30), Size = new Size(btn3W, 32), ActiveColor = ActiveBlue };
            _btnTurbo = new GButton { Text = "Turbo", Location = new Point(10 + (btn3W + 6) * 2, 30), Size = new Size(btn3W, 32), ActiveColor = ActiveTurbo };

            _btnSilent.Click += (s, e) => ApplyPerformanceMode(2);
            _btnBalanced.Click += (s, e) => ApplyPerformanceMode(0);
            _btnTurbo.Click += (s, e) => ApplyPerformanceMode(1);

            Label lblBoost = CreateSubLabel("CPU Boost", 10, 68);

            _btnBoostDisabled = new GButton { Text = "Disabled", Location = new Point(10, 90), Size = new Size(btn3W, 30), ActiveColor = ActiveBlue };
            _btnBoostEfficient = new GButton { Text = "Efficient", Location = new Point(10 + btn3W + 6, 90), Size = new Size(btn3W, 30), ActiveColor = ActiveBlue };
            _btnBoostAggressive = new GButton { Text = "Aggressive", Location = new Point(10 + (btn3W + 6) * 2, 90), Size = new Size(btn3W, 30), ActiveColor = ActiveBlue };

            _btnBoostDisabled.Click += (s, e) => ApplyCpuBoost(CpuBoostMode.Disabled);
            _btnBoostEfficient.Click += (s, e) => ApplyCpuBoost(CpuBoostMode.Efficient);
            _btnBoostAggressive.Click += (s, e) => ApplyCpuBoost(CpuBoostMode.Aggressive);

            _cardPerf.Controls.AddRange(new Control[] {
                lblPerfHeader, _btnSilent, _btnBalanced, _btnTurbo,
                lblBoost, _btnBoostDisabled, _btnBoostEfficient, _btnBoostAggressive
            });
            Controls.Add(_cardPerf);

            _cardScreen = new GCard { Size = new Size(cardWidth, 72) };

            Label lblScreen = CreateCardHeaderLabel("Laptop Screen", 10, 8);

            _cbAutoRefresh = new GCheckBox
            {
                Text = "Auto 60Hz on Battery",
                BackColor = CardBack,
                Size = new Size(180, 24),
                Checked = _settings.AutoScreenRefreshOnBattery
            };
            _cbAutoRefresh.Location = new Point(cardWidth - 10 - 180, 6);
            _cbAutoRefresh.CheckedChanged += (s, e) =>
            {
                _settings.AutoScreenRefreshOnBattery = _cbAutoRefresh.Checked;
                Task.Run(() => _settings.Save());
            };

            int btn2W = (innerW - 6) / 2;
            _btn60Hz = new GButton { Text = "60Hz", Location = new Point(10, 32), Size = new Size(btn2W, 30), ActiveColor = ActiveBlue };
            _btnMaxHz = new GButton { Text = "144Hz", Location = new Point(10 + btn2W + 6, 32), Size = new Size(btn2W, 30), ActiveColor = ActiveBlue };

            _btn60Hz.Click += (s, e) => ApplyRefreshRate(60);
            _btnMaxHz.Click += (s, e) => ApplyRefreshRate(_maxRefreshRate);

            _cardScreen.Controls.AddRange(new Control[] { lblScreen, _cbAutoRefresh, _btn60Hz, _btnMaxHz });
            Controls.Add(_cardScreen);

            _cardKbd = new GCard { Size = new Size(cardWidth, 132) };

            Label lblKbd = CreateCardHeaderLabel("Laptop Keyboard", 10, 8);

            _btnExtra = new GButton
            {
                Text = "Extra",
                Location = new Point(cardWidth - 10 - 64, 6),
                Size = new Size(64, 24),
                Font = new Font("Segoe UI", 8.5F)
            };
            _btnExtra.Click += (s, e) => ToggleExtraForm();

            int btn5W = (innerW - 20) / 5;
            _btnRgbStatic = new GButton { Text = "Static", Location = new Point(10, 34), Size = new Size(btn5W, 30), ActiveColor = ActiveBlue };
            _btnRgbBreathe = new GButton { Text = "Breathe", Location = new Point(10 + (btn5W + 5) * 1, 34), Size = new Size(btn5W, 30), ActiveColor = ActiveBlue };
            _btnRgbCycle = new GButton { Text = "Cycle", Location = new Point(10 + (btn5W + 5) * 2, 34), Size = new Size(btn5W, 30), ActiveColor = ActiveBlue };
            _btnRgbStrobe = new GButton { Text = "Strobe", Location = new Point(10 + (btn5W + 5) * 3, 34), Size = new Size(btn5W, 30), ActiveColor = ActiveBlue };

            int colorBtnW = innerW - (4 * (btn5W + 5));
            _btnColor = new GColorButton { Location = new Point(10 + (btn5W + 5) * 4, 34), Size = new Size(colorBtnW, 30) };
            _btnColor.Click += BtnColor_Click;

            _btnRgbStatic.Click += (s, e) => { if (_btnRgbStatic.Tag is int m) ApplyKeyboardRgbMode(m, _btnRgbStatic.Text); else ApplyKeyboardRgbMode(0, "Static"); };
            _btnRgbBreathe.Click += (s, e) => { if (_btnRgbBreathe.Tag is int m) ApplyKeyboardRgbMode(m, _btnRgbBreathe.Text); else ApplyKeyboardRgbMode(1, "Breathe"); };
            _btnRgbCycle.Click += (s, e) => { if (_btnRgbCycle.Tag is int m) ApplyKeyboardRgbMode(m, _btnRgbCycle.Text); else ApplyKeyboardRgbMode(2, "Cycle"); };
            _btnRgbStrobe.Click += (s, e) => { if (_btnRgbStrobe.Tag is int m) ApplyKeyboardRgbMode(m, _btnRgbStrobe.Text); else ApplyKeyboardRgbMode(10, "Strobe"); };

            Label lblBright = CreateSubLabel("Backlight Brightness", 10, 70);

            int btn4W = (innerW - 18) / 4;
            _btnBrightOff = new GButton { Text = "Off", Location = new Point(10, 90), Size = new Size(btn4W, 30), ActiveColor = ActiveBlue };
            _btnBrightLow = new GButton { Text = "Low", Location = new Point(10 + (btn4W + 6) * 1, 90), Size = new Size(btn4W, 30), ActiveColor = ActiveBlue };
            _btnBrightMed = new GButton { Text = "Medium", Location = new Point(10 + (btn4W + 6) * 2, 90), Size = new Size(btn4W, 30), ActiveColor = ActiveBlue };
            _btnBrightHigh = new GButton { Text = "High", Location = new Point(10 + (btn4W + 6) * 3, 90), Size = new Size(innerW - (btn4W + 6) * 3, 30), ActiveColor = ActiveBlue };

            _btnBrightOff.Click += (s, e) => ApplyKeyboardBrightness(0);
            _btnBrightLow.Click += (s, e) => ApplyKeyboardBrightness(1);
            _btnBrightMed.Click += (s, e) => ApplyKeyboardBrightness(2);
            _btnBrightHigh.Click += (s, e) => ApplyKeyboardBrightness(3);

            _cardKbd.Controls.AddRange(new Control[] {
                lblKbd, _btnExtra, _btnRgbStatic, _btnRgbBreathe, _btnRgbCycle, _btnRgbStrobe, _btnColor,
                lblBright, _btnBrightOff, _btnBrightLow, _btnBrightMed, _btnBrightHigh
            });
            Controls.Add(_cardKbd);

            _cardBattery = new GCard { Size = new Size(cardWidth, 70) };

            Label lblBat = CreateCardHeaderLabel("Battery Charge Limit", 10, 8);

            _btnBat60 = new GButton { Text = "60%", Location = new Point(10, 30), Size = new Size(btn3W, 30), ActiveColor = ActiveBlue };
            _btnBat80 = new GButton { Text = "80%", Location = new Point(10 + btn3W + 6, 30), Size = new Size(btn3W, 30), ActiveColor = ActiveBlue };
            _btnBat100 = new GButton { Text = "100%", Location = new Point(10 + (btn3W + 6) * 2, 30), Size = new Size(btn3W, 30), ActiveColor = ActiveBlue };

            _btnBat60.Click += (s, e) => ApplyBatteryLimit(60);
            _btnBat80.Click += (s, e) => ApplyBatteryLimit(80);
            _btnBat100.Click += (s, e) => ApplyBatteryLimit(100);

            _cardBattery.Controls.AddRange(new Control[] { lblBat, _btnBat60, _btnBat80, _btnBat100 });
            Controls.Add(_cardBattery);

            _footerPanel = new Panel
            {
                Size = new Size(cardWidth, 28),
                BackColor = FormBack
            };

            _cbStartup = new GCheckBox
            {
                Text = "Run on Startup",
                BackColor = FormBack,
                Size = new Size(130, 24),
                Location = new Point(0, 2),
                Checked = _settings.RunOnStartup
            };
            _cbStartup.CheckedChanged += (s, e) =>
            {
                StartupHelper.SetStartup(_cbStartup.Checked);
                _settings.RunOnStartup = _cbStartup.Checked;
                Task.Run(() => _settings.Save());
            };

            _cmbBrandSelector = new GDropdown
            {
                Location = new Point(cardWidth - 52 - 120, 1),
                Size = new Size(110, 24)
            };
            string[] brandNames = new[] { "Auto", "Lenovo", "HP", "Acer", "Dell", "Generic" };
            int brandIdx = Math.Clamp(_settings.BrandPreviewIndex, 0, 5);
            _cmbBrandSelector.SetItems(brandNames, brandNames[brandIdx]);
            _cmbBrandSelector.SelectedChanged += (s, e) =>
            {
                int idx = _cmbBrandSelector.SelectedItem switch
                {
                    "Auto" => 0, "Lenovo" => 1, "HP" => 2, "Acer" => 3, "Dell" => 4, _ => 5
                };
                SwitchBrandProvider(idx);
            };

            _btnQuit = new GButton
            {
                Text = "Quit",
                Location = new Point(cardWidth - 52, 2),
                Size = new Size(52, 24),
                Font = new Font("Segoe UI", 8.5F)
            };
            _btnQuit.Click += (s, e) => RealQuit();

            _footerPanel.Controls.Add(_cbStartup);
            _footerPanel.Controls.Add(_cmbBrandSelector);
            _footerPanel.Controls.Add(_btnQuit);
            Controls.Add(_footerPanel);
        }

        private static Label CreateCardHeaderLabel(string text, int x, int y)
        {
            return new Label
            {
                Text = text,
                Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
                ForeColor = TextMain,
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
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = TextMuted,
                Location = new Point(x, y),
                AutoSize = true,
                UseMnemonic = false
            };
        }

        private void RearrangeCards()
        {
            int margin = 8;
            int currentY = _headerPanel.Bottom + 8;

            bool showPerf = _provider.SupportsPerformanceModes;
            bool showScreen = true;
            bool showKbd = _provider.SupportsKeyboardBrightness || _provider.SupportsKeyboardRgb;
            bool showBat = _provider.SupportsBatteryLimit;

            _cardPerf.Visible = showPerf;
            _cardScreen.Visible = showScreen;
            _cardKbd.Visible = showKbd;
            _cardBattery.Visible = showBat;

            (Panel card, bool show)[] items = new[]
            {
                ((Panel)_cardPerf, showPerf),
                ((Panel)_cardScreen, showScreen),
                ((Panel)_cardKbd, showKbd),
                ((Panel)_cardBattery, showBat)
            };

            foreach (var item in items)
            {
                if (item.show)
                {
                    item.card.Location = new Point(margin, currentY);
                    currentY += item.card.Height + 8;
                }
            }

            _footerPanel.Location = new Point(margin, currentY);
            currentY += _footerPanel.Height + 10;

            ClientSize = new Size(420, currentY);
            PositionNearSystemTray();
        }

        private void LoadInitialState()
        {
            string rawModel = _lblModel.Text;
            string clean = rawModel.Replace("ASUSTeK COMPUTER INC.", "", StringComparison.OrdinalIgnoreCase)
                                   .Replace("ASUSTeK", "", StringComparison.OrdinalIgnoreCase)
                                   .Replace("ASUS", "", StringComparison.OrdinalIgnoreCase)
                                   .Trim();
            int underscoreIdx = clean.IndexOf('_');
            if (underscoreIdx > 0) clean = clean.Substring(0, underscoreIdx);
            int fxIdx = clean.IndexOf(" FX", StringComparison.OrdinalIgnoreCase);
            if (fxIdx > 0) clean = clean.Substring(0, fxIdx);
            clean = clean.Trim();
            _lblModel.Text = string.IsNullOrWhiteSpace(clean) ? "Laptop" : clean;

            _maxRefreshRate = DisplayHelper.GetMaxRefreshRate();
            _btnMaxHz.Text = $"{_maxRefreshRate}Hz";
            int activeHz = DisplayHelper.GetCurrentRefreshRate();
            HighlightRefreshRate(activeHz);

            if (_provider.SupportsPerformanceModes)
            {
                int currentMode = _provider.GetPerformanceMode();
                if (currentMode < 0 || currentMode > 2) currentMode = _settings.PerformanceMode;
                _currentPerfMode = currentMode;
                HighlightPerformanceMode(_currentPerfMode);
                UpdateTrayIcon(_currentPerfMode);
            }
            else
            {
                UpdateTrayIcon(0);
            }

            int boost = PowerHelper.GetCurrentBoostMode();
            _currentBoostMode = boost;
            HighlightBoostMode(boost);

            if (_provider.SupportsKeyboardBrightness)
            {
                _currentBrightness = _provider.GetKeyboardBrightness();
                if (_currentBrightness < 0 || _currentBrightness > 3) _currentBrightness = _settings.KeyboardBrightness;
                HighlightBrightness(_currentBrightness);
            }

            if (_provider.SupportsKeyboardRgb)
            {
                UpdateRgbModeButtons();
                _currentRgbMode = _settings.KeyboardRgbMode;
                _currentRgbSpeed = Math.Clamp(_settings.KeyboardRgbSpeed, 0, 2);
                try
                {
                    _currentRgbColor = ColorTranslator.FromHtml(_settings.KeyboardRgbColor);
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                    _currentRgbColor = Color.FromArgb(255, 0, 50);
                }
                _btnColor.SelectedColor = _currentRgbColor;
                HighlightRgbMode(_currentRgbMode);

                Task.Run(() =>
                {
                    try
                    {
                        _provider.SetKeyboardRgb(_currentRgbMode, _currentRgbColor, _currentRgbSpeed);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(ex);
                    }
                });
            }

            if (_provider.SupportsBatteryLimit)
            {
                _currentBatteryLimit = _settings.BatteryChargeLimit;
                HighlightBatteryLimit(_currentBatteryLimit);
            }

            UpdateLiveSensorsAsync();
        }

        private void UpdateLiveSensorsAsync()
        {
            if (!Visible && !_overlayWindow.Visible) return;
            if (Interlocked.CompareExchange(ref _isPollingSensors, 1, 0) != 0) return;

            Task.Run(() =>
            {
                try
                {
                    int cpuTemp = UniversalSensorService.Instance.GetCpuTemperature(_provider);
                    int gpuTemp = UniversalSensorService.Instance.GetGpuTemperature(_provider);
                    int cpuFan = UniversalSensorService.Instance.GetCpuFanSpeed(_provider);
                    int gpuFan = UniversalSensorService.Instance.GetGpuFanSpeed(_provider);

                    string cpuTempStr = cpuTemp > 0 ? $"{cpuTemp}\u00B0C" : "--\u00B0C";
                    string gpuTempStr = gpuTemp > 0 ? $"{gpuTemp}\u00B0C" : "0\u00B0C";
                    string cpuFanStr = cpuFan > 0 ? $"{cpuFan}RPM" : "0RPM";
                    string gpuFanStr = gpuFan > 0 ? $"{gpuFan}RPM" : "0RPM";
                    string text = $"CPU: {cpuTempStr} {cpuFanStr} | GPU: {gpuTempStr} {gpuFanStr}";

                    if (!IsDisposed && IsHandleCreated)
                    {
                        BeginInvoke(new Action(() =>
                        {
                            _lblTelemetry.Text = text;
                            _overlayWindow.UpdateMetrics(text);
                        }));
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                }
                finally
                {
                    Interlocked.Exchange(ref _isPollingSensors, 0);
                }
            });
        }

        private void SwitchBrandProvider(int index)
        {
            _settings.BrandPreviewIndex = index;
            Task.Run(() => _settings.Save());

            ILaptopProvider old = _provider;
            _provider = CreateProviderByIndex(index);

            if (old != null && !ReferenceEquals(old, _provider))
            {
                try { old.Dispose(); } catch (Exception ex) { Logger.Log(ex); }
            }

            _lblModel.Text = index == 0 ? HardwareScanner.GetLaptopBrandAndModel() : _provider.ProviderName;

            UpdateRgbModeButtons();
            if (_provider.SupportsKeyboardRgb)
            {
                var modes = _provider.GetSupportedRgbModes();
                if (modes != null && modes.Length > 0 && Array.FindIndex(modes, m => m.ModeId == _currentRgbMode) < 0)
                {
                    _currentRgbMode = modes[0].ModeId;
                    _settings.KeyboardRgbMode = _currentRgbMode;
                }
            }

            RearrangeCards();
            LoadInitialState();
        }

        public void CyclePerformanceMode()
        {
            if (!_provider.SupportsPerformanceModes) return;

            int next = _currentPerfMode switch
            {
                2 => 0, // Silent -> Balanced
                0 => 1, // Balanced -> Turbo
                1 => 2, // Turbo -> Silent
                _ => 0
            };
            ApplyPerformanceMode(next);
        }

        public void ApplyPerformanceMode(int mode, bool showOsd = true)
        {
            _currentPerfMode = mode;
            _settings.PerformanceMode = mode;
            HighlightPerformanceMode(mode);
            UpdateTrayIcon(mode);

            string modeName = mode switch
            {
                2 => "Silent",
                1 => "Turbo",
                _ => "Balanced"
            };

            if (showOsd)
            {
                _osd.ShowToast($"{modeName} Mode");
            }

            Task.Run(() =>
            {
                try
                {
                    _provider.SetPerformanceMode(mode);
                    _settings.Save();
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                }
            });

            UpdateLiveSensorsAsync();
        }

        private void HighlightPerformanceMode(int mode)
        {
            _btnSilent.IsActive = (mode == 2);
            _btnBalanced.IsActive = (mode == 0);
            _btnTurbo.IsActive = (mode == 1);
        }

        public void ApplyCpuBoost(CpuBoostMode mode, bool showOsd = true)
        {
            _currentBoostMode = (int)mode;
            _settings.CpuBoostMode = (int)mode;
            HighlightBoostMode((int)mode);

            string label = mode switch
            {
                CpuBoostMode.Disabled => "Disabled",
                CpuBoostMode.Efficient => "Efficient",
                CpuBoostMode.Aggressive => "Aggressive",
                _ => "Aggressive"
            };

            if (showOsd)
            {
                _osd.ShowToast($"CPU Boost: {label}");
            }

            Task.Run(() =>
            {
                try
                {
                    int confirmed = PowerHelper.SetBoostMode(mode);
                    if (!IsDisposed && IsHandleCreated)
                    {
                        BeginInvoke(new Action(() => HighlightBoostMode(confirmed)));
                    }
                    _settings.Save();
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                }
            });
        }

        private void HighlightBoostMode(int boostIndex)
        {
            _btnBoostDisabled.IsActive = (boostIndex == 0);
            _btnBoostEfficient.IsActive = (boostIndex == 3 || boostIndex == 4);
            _btnBoostAggressive.IsActive = (boostIndex == 1 || boostIndex == 2 || boostIndex > 4);
        }

        public void ApplyRefreshRate(int targetHz, bool showOsd = true)
        {
            HighlightRefreshRate(targetHz);

            if (showOsd)
            {
                _osd.ShowToast($"Screen: {targetHz}Hz");
            }

            Task.Run(() =>
            {
                try
                {
                    DisplayHelper.SetRefreshRate(targetHz);
                    int activeHz = DisplayHelper.GetCurrentRefreshRate();
                    if (!IsDisposed && IsHandleCreated)
                    {
                        BeginInvoke(new Action(() => HighlightRefreshRate(activeHz)));
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                }
            });
        }

        private void HighlightRefreshRate(int currentHz)
        {
            bool is60 = Math.Abs(currentHz - 60) <= 2;
            _btn60Hz.IsActive = is60;
            _btnMaxHz.IsActive = !is60;
        }

        public void StepKeyboardBrightness(int delta)
        {
            if (!_provider.SupportsKeyboardBrightness) return;
            int next = Math.Clamp(_currentBrightness + delta, 0, 3);
            ApplyKeyboardBrightness(next);
        }

        public void ApplyKeyboardBrightness(int level, bool showOsd = true)
        {
            _currentBrightness = level;
            _settings.KeyboardBrightness = level;
            HighlightBrightness(level);

            string label = level switch
            {
                0 => "Off",
                1 => "Low",
                2 => "Medium",
                3 => "High",
                _ => "Low"
            };

            if (showOsd)
            {
                _osd.ShowToast($"Keyboard: {label}");
            }

            Task.Run(() =>
            {
                try
                {
                    _provider.SetKeyboardBrightness(level);
                    _settings.Save();
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                }
            });
        }

        private void HighlightBrightness(int level)
        {
            _btnBrightOff.IsActive = (level == 0);
            _btnBrightLow.IsActive = (level == 1);
            _btnBrightMed.IsActive = (level == 2);
            _btnBrightHigh.IsActive = (level == 3);
        }

        private void UpdateRgbModeButtons()
        {
            var modes = _provider.GetSupportedRgbModes();
            if (modes != null && modes.Length >= 4)
            {
                _btnRgbStatic.Text = modes[0].Name;
                _btnRgbStatic.Tag = modes[0].ModeId;

                _btnRgbBreathe.Text = modes[1].Name;
                _btnRgbBreathe.Tag = modes[1].ModeId;

                _btnRgbCycle.Text = modes[2].Name;
                _btnRgbCycle.Tag = modes[2].ModeId;

                _btnRgbStrobe.Text = modes[3].Name;
                _btnRgbStrobe.Tag = modes[3].ModeId;
            }
            else
            {
                _btnRgbStatic.Text = "Static";
                _btnRgbStatic.Tag = 0;
                _btnRgbBreathe.Text = "Breathe";
                _btnRgbBreathe.Tag = 1;
                _btnRgbCycle.Text = "Cycle";
                _btnRgbCycle.Tag = 2;
                _btnRgbStrobe.Text = "Strobe";
                _btnRgbStrobe.Tag = 10;
            }
        }

        public void ApplyKeyboardRgbMode(int mode, string modeName, bool showOsd = true)
        {
            _currentRgbMode = mode;
            _settings.KeyboardRgbMode = mode;
            HighlightRgbMode(mode);
            _colorPickerForm?.UpdateCurrentMode(mode);

            if (showOsd)
            {
                _osd.ShowToast($"Keyboard RGB: {modeName}");
            }

            Task.Run(() =>
            {
                try
                {
                    _provider.SetKeyboardRgb(mode, _currentRgbColor, _currentRgbSpeed);
                    _settings.Save();
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                }
            });
        }

        private void HighlightRgbMode(int mode)
        {
            _btnRgbStatic.IsActive = (_btnRgbStatic.Tag is int m0 && m0 == mode);
            _btnRgbBreathe.IsActive = (_btnRgbBreathe.Tag is int m1 && m1 == mode);
            _btnRgbCycle.IsActive = (_btnRgbCycle.Tag is int m2 && m2 == mode);
            _btnRgbStrobe.IsActive = (_btnRgbStrobe.Tag is int m3 && m3 == mode);
        }

        private void BtnColor_Click(object? sender, EventArgs e)
        {
            if (_colorPickerForm == null || _colorPickerForm.IsDisposed)
            {
                _colorPickerForm = new GColorPickerForm(
                    this,
                    _provider,
                    _settings,
                    _currentRgbMode,
                    _currentRgbColor,
                    _currentRgbSpeed,
                    (newColor) =>
                    {
                        _currentRgbColor = newColor;
                        _btnColor.SelectedColor = newColor;
                        _settings.KeyboardRgbColor = ColorTranslator.ToHtml(newColor);
                    },
                    (newSpeed) =>
                    {
                        _currentRgbSpeed = newSpeed;
                        _settings.KeyboardRgbSpeed = newSpeed;
                    });
            }

            if (_colorPickerForm.Visible)
            {
                _colorPickerForm.Hide();
            }
            else
            {
                _colorPickerForm.UpdateCurrentMode(_currentRgbMode);
                _colorPickerForm.PositionNearMainForm(this);
                _colorPickerForm.Show();
                _colorPickerForm.Activate();
            }
        }

        public void ApplyBatteryLimit(int limit, bool showOsd = true)
        {
            _currentBatteryLimit = limit;
            _settings.BatteryChargeLimit = limit;
            HighlightBatteryLimit(limit);

            if (showOsd)
            {
                _osd.ShowToast($"Battery Limit: {limit}%");
            }

            Task.Run(() =>
            {
                try
                {
                    _provider.SetBatteryChargeLimit(limit);
                    _settings.Save();
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                }
            });
        }

        private void HighlightBatteryLimit(int limit)
        {
            _btnBat60.IsActive = (limit == 60);
            _btnBat80.IsActive = (limit == 80);
            _btnBat100.IsActive = (limit == 100);
        }

        public void ToggleExtraForm()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(ToggleExtraForm));
                return;
            }

            if (_extraForm == null || _extraForm.IsDisposed)
            {
                _extraForm = new ExtraForm(this, _provider, _settings, _osd, _overlayWindow);
            }

            if (_extraForm.Visible)
            {
                _extraForm.Hide();
            }
            else
            {
                PositionExtraForm();
                _extraForm.Show();
                _extraForm.Activate();
            }
        }

        public void ToggleOverlay()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(ToggleOverlay));
                return;
            }

            bool visible = _overlayWindow.Toggle();
            _settings.InGameOverlay = visible;
            Task.Run(() => _settings.Save());
            _extraForm?.SyncOverlayCheckbox(visible);
            _osd.ShowToast(visible ? "In-Game Overlay: ON" : "In-Game Overlay: OFF");
        }

        public HotkeyManager? HotkeyManager => _hotkeyManager;
        public void ReRegisterCustomHotkeys() => _hotkeyManager?.RegisterCustomHotkeys(_settings.CustomHotkeys);

        public void ExecuteCustomRule(CustomHotkeyRule rule)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => ExecuteCustomRule(rule)));
                return;
            }

            if (rule.Action.StartsWith("Launch Custom App", StringComparison.OrdinalIgnoreCase))
            {
                LaunchCustomApp(rule.CustomAppPath);
            }
            else
            {
                ExecuteAction(rule.Action);
            }
        }

        public void ExecuteAction(string action)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => ExecuteAction(action)));
                return;
            }

            switch (action)
            {
                case "Toggle Window":
                case "Toggle G-Universal":
                    ToggleWindow();
                    break;

                case "Profile: Gaming (Turbo + MaxHz + Boost On)":
                    ApplyPerformanceMode(1, showOsd: false);
                    ApplyRefreshRate(_maxRefreshRate, showOsd: false);
                    ApplyCpuBoost(CpuBoostMode.Aggressive, showOsd: false);
                    if (_provider.SupportsKeyboardBrightness) ApplyKeyboardBrightness(3, showOsd: false);
                    _osd.ShowToast("Profile: Gaming (Turbo + MaxHz + Boost On)");
                    break;

                case "Profile: Eco/Study (Silent + 60Hz + Boost Off)":
                    ApplyPerformanceMode(2, showOsd: false);
                    ApplyRefreshRate(60, showOsd: false);
                    ApplyCpuBoost(CpuBoostMode.Disabled, showOsd: false);
                    if (_provider.SupportsKeyboardBrightness) ApplyKeyboardBrightness(0, showOsd: false);
                    _osd.ShowToast("Profile: Eco/Study (Silent + 60Hz + Boost Off)");
                    break;

                case "Launch Custom App (.exe)...":
                case "Launch Custom App":
                    LaunchCustomApp();
                    break;

                case "Cycle Performance Mode":
                case "Cycle Mode":
                    CyclePerformanceMode();
                    break;

                case "Toggle Turbo/Silent":
                    int targetMode = (_currentPerfMode == 1) ? 2 : 1;
                    ApplyPerformanceMode(targetMode);
                    break;

                case "Toggle CPU Boost":
                    int targetBoost = (_currentBoostMode == 0) ? 2 : 0;
                    ApplyCpuBoost((CpuBoostMode)targetBoost);
                    break;

                case "Toggle 60Hz/MaxHz":
                    int currentHz = DisplayHelper.GetCurrentRefreshRate();
                    int targetHz = (Math.Abs(currentHz - 60) <= 2) ? _maxRefreshRate : 60;
                    ApplyRefreshRate(targetHz);
                    break;

                case "Mute Microphone":
                case "Mute Mic":
                    ExtraSettingsHelper.ToggleMicMute();
                    _osd.ShowToast("Microphone Mute Toggled");
                    break;

                case "Kill GPU Apps":
                    Task.Run(() =>
                    {
                        int killed = OemServiceManager.KillDgpuBackgroundApps();
                        if (!IsDisposed && IsHandleCreated)
                            BeginInvoke(new Action(() => _osd.ShowToast($"Killed {killed} dGPU Apps")));
                    });
                    break;

                case "Toggle Overlay":
                    ToggleOverlay();
                    break;

                case "Play/Pause Media":
                    ExtraSettingsHelper.SendPlayPause();
                    _osd.ShowToast("Media Play / Pause");
                    break;

                case "Screenshot":
                    ExtraSettingsHelper.SendScreenshot();
                    _osd.ShowToast("Screenshot");
                    break;

                default:
                    CyclePerformanceMode();
                    break;
            }
        }

        public void LaunchCustomApp(string? specificPath = null)
        {
            string target = !string.IsNullOrWhiteSpace(specificPath) ? specificPath : _settings.CustomAppPath;
            if (string.IsNullOrWhiteSpace(target) || !File.Exists(target))
            {
                using OpenFileDialog ofd = new OpenFileDialog
                {
                    Title = "Select Application to Launch (.exe / .lnk)",
                    Filter = "Executables (*.exe;*.lnk)|*.exe;*.lnk"
                };
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    target = ofd.FileName;
                    _settings.CustomAppPath = target;
                    Task.Run(() => _settings.Save());
                }
                else
                {
                    return;
                }
            }

            string ext = Path.GetExtension(target);
            if (!File.Exists(target) || (!ext.Equals(".exe", StringComparison.OrdinalIgnoreCase) && !ext.Equals(".lnk", StringComparison.OrdinalIgnoreCase)))
            {
                _osd.ShowToast("Invalid executable (.exe or .lnk required)");
                return;
            }

            try
            {
                var psi = new ProcessStartInfo("explorer.exe")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                psi.ArgumentList.Add(target);
                Process.Start(psi);
                _osd.ShowToast($"Launched {Path.GetFileName(target)}");
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                _osd.ShowToast($"Failed: {ex.Message}");
            }
        }

        private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode != PowerModes.StatusChange) return;
            if (!_settings.AutoScreenRefreshOnBattery) return;

            PowerLineStatus status = SystemInformation.PowerStatus.PowerLineStatus;

            if (status == PowerLineStatus.Offline)
            {
                if (_currentPerfMode != 2)
                {
                    _pluggedInPerfMode = _currentPerfMode;
                }

                BeginInvoke(new Action(() =>
                {
                    ApplyPerformanceMode(2, showOsd: false);
                    ApplyRefreshRate(60, showOsd: false);
                    _osd.ShowToast("Silent Mode  |  60Hz");
                }));
            }
            else if (status == PowerLineStatus.Online)
            {
                int restoreMode = (_pluggedInPerfMode == 1) ? 1 : 0;
                int maxHz = _maxRefreshRate > 60 ? _maxRefreshRate : 144;

                BeginInvoke(new Action(() =>
                {
                    ApplyPerformanceMode(restoreMode, showOsd: false);
                    ApplyRefreshRate(maxHz, showOsd: false);
                    string modeName = restoreMode == 1 ? "Turbo" : "Balanced";
                    _osd.ShowToast($"{modeName} Mode  |  {maxHz}Hz");
                }));
            }
        }

        private void InitializeTrayIcon()
        {
            ContextMenuStrip trayMenu = new ContextMenuStrip
            {
                BackColor = CardBack,
                ForeColor = TextMain,
                Renderer = new DarkMenuRenderer()
            };

            trayMenu.Items.Add("Silent", null, (s, e) => ApplyPerformanceMode(2));
            trayMenu.Items.Add("Balanced", null, (s, e) => ApplyPerformanceMode(0));
            trayMenu.Items.Add("Turbo", null, (s, e) => ApplyPerformanceMode(1));
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add("60Hz", null, (s, e) => ApplyRefreshRate(60));
            trayMenu.Items.Add($"{_maxRefreshRate}Hz", null, (s, e) => ApplyRefreshRate(_maxRefreshRate));
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add("Show", null, (s, e) => ShowNearTray());
            trayMenu.Items.Add("Quit", null, (s, e) => RealQuit());

            _trayIcon = new NotifyIcon
            {
                Text = "G-Universal",
                ContextMenuStrip = trayMenu,
                Visible = true
            };

            _trayIcon.MouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    ToggleWindow();
                }
            };

            UpdateTrayIcon(_currentPerfMode);
        }

        private void UpdateTrayIcon(int mode)
        {
            Color iconColor = mode switch
            {
                2 => ActiveSilent,
                1 => ActiveTurbo,
                _ => ActiveBlue
            };

            using Bitmap bmp = new Bitmap(16, 16);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                using Brush brush = new SolidBrush(iconColor);
                g.FillEllipse(brush, 1, 1, 14, 14);

                using Pen pen = new Pen(Color.FromArgb(30, 30, 30), 1);
                g.DrawEllipse(pen, 1, 1, 13, 13);
            }

            IntPtr hIcon = bmp.GetHicon();
            Icon newIcon = (Icon)Icon.FromHandle(hIcon).Clone();
            DestroyIcon(hIcon);

            Icon? oldIcon = _trayIcon.Icon;
            _trayIcon.Icon = newIcon;
            oldIcon?.Dispose();
        }

        public void ShowNearTray()
        {
            PositionNearSystemTray();
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
            UpdateLiveSensorsAsync();
        }

        public void ToggleWindow()
        {
            if (Visible && WindowState != FormWindowState.Minimized)
            {
                Hide();
                _extraForm?.Hide();
                _colorPickerForm?.Hide();
            }
            else
            {
                ShowNearTray();
            }
        }

        private void RealQuit()
        {
            _reallyQuit = true;
            _trayIcon.Visible = false;
            _overlayWindow.Hide();
            _extraForm?.Close();
            _colorPickerForm?.Close();
            Application.Exit();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_reallyQuit && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                _extraForm?.Hide();
                return;
            }

            base.OnFormClosing(e);
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_HOTKEY = 0x0312;
            if (m.Msg == WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                _hotkeyManager?.ProcessHotKeyMessage(id);
            }

            base.WndProc(ref m);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                SystemEvents.PowerModeChanged -= OnPowerModeChanged;
                _sensorTimer?.Stop();
                _sensorTimer?.Dispose();
                _hotkeyManager?.Dispose();
                _extraForm?.Dispose();
                _overlayWindow?.Dispose();
                _osd?.Dispose();
                _trayIcon?.Dispose();
                _provider?.Dispose();
                UniversalSensorService.Instance.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

