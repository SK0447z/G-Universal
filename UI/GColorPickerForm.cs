using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GUniversal
{
    public class GColorPickerForm : Form
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_ROUND = 2;

        private readonly MainForm _mainForm;
        private readonly ILaptopProvider _provider;
        private readonly AppSettings _settings;
        private readonly Action<Color> _onColorChanged;
        private readonly Action<int> _onSpeedChanged;

        private int _currentMode;
        private Color _currentColor;
        private int _currentSpeed;

        private double _hue = 0.0;
        private double _saturation = 1.0;
        private double _value = 1.0;

        private ColorCanvasControl _canvas = null!;
        private HueSliderControl _hueSlider = null!;
        private ColorPreviewControl _previewBox = null!;
        private TextBox _txtHex = null!;
        private Label _lblRgb = null!;
        private GButton _btnSpeedSlow = null!;
        private GButton _btnSpeedMed = null!;
        private GButton _btnSpeedFast = null!;
        private FlowLayoutPanel _swatchesFlow = null!;
        private GButton _btnDone = null!;

        private readonly object _throttleLock = new object();
        private DateTime _lastHardwareUpdate = DateTime.MinValue;
        private Color _latestColor;
        private bool _hasPendingColor = false;
        private System.Threading.Timer? _debounceTimer;

        public GColorPickerForm(
            MainForm mainForm,
            ILaptopProvider provider,
            AppSettings settings,
            int currentMode,
            Color initialColor,
            int initialSpeed,
            Action<Color> onColorChanged,
            Action<int> onSpeedChanged)
        {
            _mainForm = mainForm;
            _provider = provider;
            _settings = settings;
            _currentMode = currentMode;
            _currentColor = initialColor;
            _latestColor = initialColor;
            _currentSpeed = Math.Clamp(initialSpeed, 0, 2);
            _onColorChanged = onColorChanged;
            _onSpeedChanged = onSpeedChanged;

            ColorToHsv(_currentColor, out _hue, out _saturation, out _value);

            InitializeWindow();
            BuildUI();
            UpdateSpeedButtonsHighlight();
            UpdateColorUI(_currentColor, updateHexText: true);
        }

        private void InitializeWindow()
        {
            Text = "Keyboard Lighting";
            ClientSize = new Size(320, 440);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = MainForm.FormBack;
            ForeColor = MainForm.TextMain;
            Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            DoubleBuffered = true;

            int darkMode = 1;
            DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));
            int cornerPref = DWMWCP_ROUND;
            DwmSetWindowAttribute(Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPref, sizeof(int));
        }

        public void PositionNearMainForm(MainForm main)
        {
            int x = main.Left - Width - 8;
            if (x < 10) x = main.Right + 8;
            int y = Math.Max(10, main.Bottom - Height);
            Location = new Point(x, y);
        }

        public void UpdateCurrentMode(int mode)
        {
            _currentMode = mode;
        }

        private void BuildUI()
        {
            Controls.Clear();

            Label lblTitle = new Label
            {
                Text = "Keyboard Lighting & Color",
                Location = new Point(14, 10),
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold),
                ForeColor = MainForm.TextMain
            };
            GButton btnCloseX = new GButton
            {
                Text = "\u00D7",
                Location = new Point(282, 6),
                Size = new Size(24, 24),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold)
            };
            btnCloseX.Click += (s, e) => Hide();

            Controls.Add(lblTitle);
            Controls.Add(btnCloseX);

            _canvas = new ColorCanvasControl
            {
                Location = new Point(14, 38),
                Size = new Size(200, 150)
            };
            _canvas.ColorChanged += (s, e) =>
            {
                _saturation = _canvas.Saturation;
                _value = _canvas.Value;
                Color newColor = ColorFromHsv(_hue, _saturation, _value);
                _previewBox.CurrentColor = newColor;
                UpdateColorUI(newColor, updateHexText: true);
                QueueHardwarePreview(newColor);
            };

            _hueSlider = new HueSliderControl
            {
                Location = new Point(222, 38),
                Size = new Size(24, 150),
                Hue = _hue
            };
            _hueSlider.HueChanged += (s, e) =>
            {
                _hue = _hueSlider.Hue;
                _canvas.Hue = _hue;
                Color newColor = ColorFromHsv(_hue, _saturation, _value);
                _previewBox.CurrentColor = newColor;
                UpdateColorUI(newColor, updateHexText: true);
                QueueHardwarePreview(newColor);
            };

            _previewBox = new ColorPreviewControl
            {
                Location = new Point(254, 38),
                Size = new Size(52, 150),
                CurrentColor = _currentColor
            };

            Controls.Add(_canvas);
            Controls.Add(_hueSlider);
            Controls.Add(_previewBox);

            GCard cardHex = new GCard
            {
                Location = new Point(14, 196),
                Size = new Size(292, 36)
            };
            Label lblHex = new Label
            {
                Text = "Hex:",
                Location = new Point(8, 8),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = MainForm.TextMuted
            };
            _txtHex = new TextBox
            {
                Location = new Point(42, 6),
                Size = new Size(80, 24),
                BackColor = MainForm.ButtonBack,
                ForeColor = MainForm.TextMain,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 9F)
            };
            _txtHex.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    ApplyHexInput(_txtHex.Text);
                }
            };
            _txtHex.Leave += (s, e) => ApplyHexInput(_txtHex.Text);

            _lblRgb = new Label
            {
                Text = "R: 255  G: 0  B: 50",
                Location = new Point(130, 8),
                Size = new Size(154, 20),
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = MainForm.TextMuted,
                TextAlign = ContentAlignment.MiddleRight
            };

            cardHex.Controls.Add(lblHex);
            cardHex.Controls.Add(_txtHex);
            cardHex.Controls.Add(_lblRgb);
            Controls.Add(cardHex);

            Label lblSpeed = new Label
            {
                Text = "Animation Speed",
                Location = new Point(14, 238),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = MainForm.TextMuted
            };
            Controls.Add(lblSpeed);

            int speedW = (292 - 12) / 3;
            _btnSpeedSlow = new GButton { Text = "Slow", Location = new Point(14, 258), Size = new Size(speedW, 28) };
            _btnSpeedMed = new GButton { Text = "Medium", Location = new Point(14 + speedW + 6, 258), Size = new Size(speedW, 28) };
            _btnSpeedFast = new GButton { Text = "Fast", Location = new Point(14 + (speedW + 6) * 2, 258), Size = new Size(292 - (speedW + 6) * 2, 28) };

            _btnSpeedSlow.Click += (s, e) => ApplySpeed(0);
            _btnSpeedMed.Click += (s, e) => ApplySpeed(1);
            _btnSpeedFast.Click += (s, e) => ApplySpeed(2);

            Controls.Add(_btnSpeedSlow);
            Controls.Add(_btnSpeedMed);
            Controls.Add(_btnSpeedFast);

            Label lblSaved = new Label
            {
                Text = "Saved Swatches (Right-click to remove)",
                Location = new Point(14, 294),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = MainForm.TextMuted
            };
            Controls.Add(lblSaved);

            _swatchesFlow = new FlowLayoutPanel
            {
                Location = new Point(14, 314),
                Size = new Size(292, 68),
                BackColor = Color.Transparent,
                WrapContents = true,
                AutoScroll = false
            };
            Controls.Add(_swatchesFlow);
            RenderSwatches();

            _btnDone = new GButton
            {
                Text = "Done",
                Location = new Point(14, 392),
                Size = new Size(292, 32),
                Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
                ActiveColor = MainForm.ActiveBlue,
                IsActive = true
            };
            _btnDone.Click += (s, e) =>
            {
                _settings.KeyboardRgbColor = ColorTranslator.ToHtml(_currentColor);
                Task.Run(() => _settings.Save());
                Hide();
            };
            Controls.Add(_btnDone);

            _canvas.SetHsv(_hue, _saturation, _value);
        }

        private void RenderSwatches()
        {
            _swatchesFlow.Controls.Clear();
            _swatchesFlow.SuspendLayout();

            var colors = _settings.SavedColors ?? new List<string>();
            foreach (string hex in colors)
            {
                string capturedHex = hex;
                Color swatchColor;
                try
                {
                    swatchColor = ColorTranslator.FromHtml(capturedHex);
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                    continue;
                }

                Panel swatch = new Panel
                {
                    Size = new Size(26, 26),
                    Margin = new Padding(0, 0, 6, 6),
                    BackColor = swatchColor,
                    Cursor = Cursors.Hand
                };

                swatch.Paint += (s, e) =>
                {
                    using Pen p = new Pen(Color.FromArgb(70, 70, 70), 1);
                    e.Graphics.DrawRectangle(p, 0, 0, swatch.Width - 1, swatch.Height - 1);
                };

                swatch.MouseDown += (s, e) =>
                {
                    if (e.Button == MouseButtons.Left)
                    {
                        SelectColor(swatchColor);
                    }
                    else if (e.Button == MouseButtons.Right)
                    {
                        _settings.SavedColors?.Remove(capturedHex);
                        Task.Run(() => _settings.Save());
                        RenderSwatches();
                    }
                };

                _swatchesFlow.Controls.Add(swatch);
            }

            GButton btnSaveSwatch = new GButton
            {
                Text = "+ Save",
                Size = new Size(58, 26),
                Margin = new Padding(0, 0, 6, 6),
                Font = new Font("Segoe UI", 8F)
            };
            btnSaveSwatch.Click += (s, e) =>
            {
                string hex = ColorTranslator.ToHtml(_currentColor);
                _settings.SavedColors ??= new List<string>();
                if (!_settings.SavedColors.Contains(hex))
                {
                    _settings.SavedColors.Add(hex);
                    Task.Run(() => _settings.Save());
                    RenderSwatches();
                }
            };
            _swatchesFlow.Controls.Add(btnSaveSwatch);

            _swatchesFlow.ResumeLayout();
        }

        private void SelectColor(Color color)
        {
            _currentColor = color;
            ColorToHsv(color, out _hue, out _saturation, out _value);
            _canvas.SetHsv(_hue, _saturation, _value);
            _hueSlider.Hue = _hue;
            _previewBox.CurrentColor = color;

            UpdateColorUI(color, updateHexText: true);

            _onColorChanged?.Invoke(color);
            _settings.KeyboardRgbColor = ColorTranslator.ToHtml(color);
            Task.Run(() =>
            {
                try
                {
                    _provider.SetKeyboardRgb(_currentMode, color, _currentSpeed);
                    _settings.Save();
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                }
            });
        }

        private void ApplyHexInput(string hexText)
        {
            if (string.IsNullOrWhiteSpace(hexText)) return;
            string clean = hexText.Trim();
            if (!clean.StartsWith("#")) clean = "#" + clean;

            try
            {
                Color color = ColorTranslator.FromHtml(clean);
                SelectColor(color);
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        private void ApplySpeed(int speed)
        {
            _currentSpeed = speed;
            _settings.KeyboardRgbSpeed = speed;
            _onSpeedChanged?.Invoke(speed);
            UpdateSpeedButtonsHighlight();

            Task.Run(() =>
            {
                try
                {
                    _provider.SetKeyboardRgb(_currentMode, _currentColor, _currentSpeed);
                    _settings.Save();
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                }
            });
        }

        private void UpdateSpeedButtonsHighlight()
        {
            _btnSpeedSlow.IsActive = (_currentSpeed == 0);
            _btnSpeedMed.IsActive = (_currentSpeed == 1);
            _btnSpeedFast.IsActive = (_currentSpeed == 2);
        }

        private void UpdateColorUI(Color color, bool updateHexText)
        {
            _currentColor = color;
            _previewBox.CurrentColor = color;
            _lblRgb.Text = $"R: {color.R}  G: {color.G}  B: {color.B}";
            if (updateHexText && !_txtHex.Focused)
            {
                _txtHex.Text = ColorTranslator.ToHtml(color);
            }
        }

        private void QueueHardwarePreview(Color color)
        {
            _latestColor = color;
            _onColorChanged?.Invoke(color);

            lock (_throttleLock)
            {
                _hasPendingColor = true;
                var now = DateTime.UtcNow;
                var elapsed = (now - _lastHardwareUpdate).TotalMilliseconds;

                if (elapsed >= 75)
                {
                    _lastHardwareUpdate = now;
                    _hasPendingColor = false;
                    Color target = _latestColor;
                    Task.Run(() =>
                    {
                        try
                        {
                            _provider.SetKeyboardRgb(_currentMode, target, _currentSpeed);
                        }
                        catch (Exception ex)
                        {
                            Logger.Log(ex);
                        }
                    });
                }
                else
                {
                    int delay = (int)(75 - elapsed);
                    _debounceTimer?.Dispose();
                    _debounceTimer = new System.Threading.Timer(_ =>
                    {
                        lock (_throttleLock)
                        {
                            if (_hasPendingColor)
                            {
                                _hasPendingColor = false;
                                _lastHardwareUpdate = DateTime.UtcNow;
                                Color target = _latestColor;
                                Task.Run(() =>
                                {
                                    try
                                    {
                                        _provider.SetKeyboardRgb(_currentMode, target, _currentSpeed);
                                    }
                                    catch (Exception ex)
                                    {
                                        Logger.Log(ex);
                                    }
                                });
                            }
                        }
                    }, null, delay, Timeout.Infinite);
                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _debounceTimer?.Dispose();
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                _settings.KeyboardRgbColor = ColorTranslator.ToHtml(_currentColor);
                Task.Run(() => _settings.Save());
                Hide();
                return;
            }
            base.OnFormClosing(e);
        }

        public static Color ColorFromHsv(double hue, double saturation, double value)
        {
            int hi = Convert.ToInt32(Math.Floor(hue / 60.0)) % 6;
            double f = (hue / 60.0) - Math.Floor(hue / 60.0);

            value = Math.Clamp(value, 0.0, 1.0);
            saturation = Math.Clamp(saturation, 0.0, 1.0);

            int v = Convert.ToInt32(value * 255.0);
            int p = Convert.ToInt32(value * (1.0 - saturation) * 255.0);
            int q = Convert.ToInt32(value * (1.0 - (f * saturation)) * 255.0);
            int t = Convert.ToInt32(value * (1.0 - ((1.0 - f) * saturation)) * 255.0);

            return hi switch
            {
                0 => Color.FromArgb(v, t, p),
                1 => Color.FromArgb(q, v, p),
                2 => Color.FromArgb(p, v, t),
                3 => Color.FromArgb(p, q, v),
                4 => Color.FromArgb(t, p, v),
                _ => Color.FromArgb(v, p, q)
            };
        }

        public static void ColorToHsv(Color color, out double hue, out double saturation, out double value)
        {
            int max = Math.Max(color.R, Math.Max(color.G, color.B));
            int min = Math.Min(color.R, Math.Min(color.G, color.B));

            hue = color.GetHue();
            saturation = (max == 0) ? 0.0 : 1.0 - (1.0 * min / max);
            value = max / 255.0;
        }

        private class ColorCanvasControl : Control
        {
            private double _hue = 0;
            private double _sat = 1;
            private double _val = 1;
            private bool _isDragging = false;

            public event EventHandler? ColorChanged;

            public double Saturation => _sat;
            public double Value => _val;

            public double Hue
            {
                get => _hue;
                set { _hue = value; Invalidate(); }
            }

            public ColorCanvasControl()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
                Cursor = Cursors.Cross;
            }

            public void SetHsv(double hue, double sat, double val)
            {
                _hue = hue;
                _sat = Math.Clamp(sat, 0.0, 1.0);
                _val = Math.Clamp(val, 0.0, 1.0);
                Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                int w = Width, h = Height;
                Color pureHue = ColorFromHsv(_hue, 1.0, 1.0);

                using (var hBrush = new LinearGradientBrush(new Rectangle(0, 0, w, h), Color.White, pureHue, LinearGradientMode.Horizontal))
                {
                    e.Graphics.FillRectangle(hBrush, 0, 0, w, h);
                }

                using (var vBrush = new LinearGradientBrush(new Rectangle(0, 0, w, h), Color.FromArgb(0, 0, 0, 0), Color.FromArgb(255, 0, 0, 0), LinearGradientMode.Vertical))
                {
                    e.Graphics.FillRectangle(vBrush, 0, 0, w, h);
                }

                using (var pen = new Pen(Color.FromArgb(60, 60, 60), 1))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, w - 1, h - 1);
                }

                int cx = (int)Math.Round(_sat * (w - 1));
                int cy = (int)Math.Round((1.0 - _val) * (h - 1));
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var blackPen = new Pen(Color.Black, 2.5f))
                {
                    e.Graphics.DrawEllipse(blackPen, cx - 5, cy - 5, 10, 10);
                }
                using (var whitePen = new Pen(Color.White, 1.5f))
                {
                    e.Graphics.DrawEllipse(whitePen, cx - 5, cy - 5, 10, 10);
                }
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);
                if (e.Button == MouseButtons.Left)
                {
                    _isDragging = true;
                    Capture = true;
                    UpdateFromMouse(e.X, e.Y);
                }
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                if (_isDragging)
                {
                    UpdateFromMouse(e.X, e.Y);
                }
            }

            protected override void OnMouseUp(MouseEventArgs e)
            {
                base.OnMouseUp(e);
                if (_isDragging)
                {
                    _isDragging = false;
                    Capture = false;
                    UpdateFromMouse(e.X, e.Y);
                }
            }

            private void UpdateFromMouse(int mx, int my)
            {
                _sat = Math.Clamp((double)mx / (Width - 1), 0.0, 1.0);
                _val = Math.Clamp(1.0 - ((double)my / (Height - 1)), 0.0, 1.0);
                Invalidate();
                ColorChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private class HueSliderControl : Control
        {
            private double _hue = 0.0;
            private bool _isDragging = false;
            public event EventHandler? HueChanged;

            public double Hue
            {
                get => _hue;
                set { _hue = Math.Clamp(value, 0.0, 360.0); Invalidate(); }
            }

            public HueSliderControl()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
                Cursor = Cursors.Hand;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                int w = Width, h = Height;

                using (var hueBrush = new LinearGradientBrush(new Rectangle(0, 0, w, h), Color.Red, Color.Red, LinearGradientMode.Vertical))
                {
                    hueBrush.InterpolationColors = new ColorBlend
                    {
                        Positions = new float[] { 0f, 0.166f, 0.333f, 0.5f, 0.666f, 0.833f, 1f },
                        Colors = new Color[]
                        {
                            Color.FromArgb(255, 0, 0),     // 0°
                            Color.FromArgb(255, 255, 0),   // 60°
                            Color.FromArgb(0, 255, 0),     // 120°
                            Color.FromArgb(0, 255, 255),   // 180°
                            Color.FromArgb(0, 0, 255),     // 240°
                            Color.FromArgb(255, 0, 255),   // 300°
                            Color.FromArgb(255, 0, 0)      // 360°
                        }
                    };
                    e.Graphics.FillRectangle(hueBrush, 0, 0, w, h);
                }

                using (var pen = new Pen(Color.FromArgb(60, 60, 60), 1))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, w - 1, h - 1);
                }

                int hy = (int)Math.Round((_hue / 360.0) * (h - 1));
                using (var whitePen = new Pen(Color.White, 2))
                {
                    e.Graphics.DrawLine(whitePen, 0, hy, w, hy);
                }
                using (var blackPen = new Pen(Color.Black, 1))
                {
                    if (hy > 0) e.Graphics.DrawLine(blackPen, 0, hy - 1, w, hy - 1);
                    if (hy < h - 1) e.Graphics.DrawLine(blackPen, 0, hy + 1, w, hy + 1);
                }
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);
                if (e.Button == MouseButtons.Left)
                {
                    _isDragging = true;
                    Capture = true;
                    UpdateFromMouse(e.Y);
                }
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                if (_isDragging)
                {
                    UpdateFromMouse(e.Y);
                }
            }

            protected override void OnMouseUp(MouseEventArgs e)
            {
                base.OnMouseUp(e);
                if (_isDragging)
                {
                    _isDragging = false;
                    Capture = false;
                    UpdateFromMouse(e.Y);
                }
            }

            private void UpdateFromMouse(int my)
            {
                _hue = Math.Clamp(((double)my / (Height - 1)) * 360.0, 0.0, 360.0);
                Invalidate();
                HueChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private class ColorPreviewControl : Control
        {
            private Color _color = Color.Red;

            public Color CurrentColor
            {
                get => _color;
                set { _color = value; Invalidate(); }
            }

            public ColorPreviewControl()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                using (var brush = new SolidBrush(_color))
                {
                    e.Graphics.FillRectangle(brush, ClientRectangle);
                }
                using (var border = new Pen(Color.FromArgb(60, 60, 60), 1))
                {
                    e.Graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
                }
            }
        }
    }
}

