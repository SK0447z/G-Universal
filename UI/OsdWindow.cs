using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace GUniversal
{
    public class OsdWindow : Form
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_ROUND = 2;

        private readonly System.Windows.Forms.Timer _hideTimer;
        private string _text = string.Empty;
        private readonly Font _font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
        private readonly Pen _borderPen = new Pen(Color.FromArgb(62, 62, 62), 1);

        public OsdWindow()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Color.FromArgb(37, 37, 37);
            ForeColor = Color.FromArgb(222, 222, 222);
            Size = new Size(200, 44);
            DoubleBuffered = true;

            int cornerPref = DWMWCP_ROUND;
            DwmSetWindowAttribute(Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPref, sizeof(int));

            _hideTimer = new System.Windows.Forms.Timer { Interval = 1500 };
            _hideTimer.Tick += (s, e) =>
            {
                _hideTimer.Stop();
                Hide();
            };
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
                cp.ExStyle |= 0x00000008; // WS_EX_TOPMOST
                return cp;
            }
        }

        public void ShowToast(string message)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => ShowToast(message)));
                return;
            }

            _text = message;

            Size textSize = TextRenderer.MeasureText(_text, _font);
            int width = Math.Max(200, textSize.Width + 36);
            Size = new Size(width, 44);

            Rectangle workingArea = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
            int x = workingArea.Left + (workingArea.Width - Width) / 2;
            int y = workingArea.Bottom - Height - 60;
            Location = new Point(x, y);

            Invalidate();

            if (!Visible)
            {
                Show();
            }

            _hideTimer.Stop();
            _hideTimer.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;

            using (SolidBrush bgBrush = new SolidBrush(BackColor))
            {
                g.FillRectangle(bgBrush, ClientRectangle);
            }

            Rectangle borderRect = new Rectangle(0, 0, Width - 1, Height - 1);
            g.DrawRectangle(_borderPen, borderRect);

            TextRenderer.DrawText(
                g,
                _text,
                _font,
                ClientRectangle,
                ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _hideTimer?.Dispose();
                _font?.Dispose();
                _borderPen?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

