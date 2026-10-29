using System;
using System.Drawing;
using System.Windows.Forms;

namespace GUniversal
{
    public class MetricsOverlayWindow : Form
    {
        private string _text = "CPU: --\u00B0C 0RPM   |   GPU: --\u00B0C 0RPM";
        private readonly Font _font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
        private readonly Pen _borderPen = new Pen(Color.FromArgb(62, 62, 62), 1);

        public MetricsOverlayWindow()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Color.FromArgb(28, 28, 28);
            ForeColor = Color.FromArgb(222, 222, 222);
            Size = new Size(330, 28);
            Location = new Point(16, 16);
            DoubleBuffered = true;
            Opacity = 0.90;
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
                cp.ExStyle |= 0x00080000; // WS_EX_LAYERED
                cp.ExStyle |= 0x00000020; // WS_EX_TRANSPARENT (Click-Through)
                return cp;
            }
        }

        public void UpdateMetrics(string telemetryText)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => UpdateMetrics(telemetryText)));
                return;
            }

            _text = telemetryText;
            Invalidate();
        }

        public void SetEnabled(bool enabled)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => SetEnabled(enabled)));
                return;
            }

            if (enabled)
            {
                if (!Visible) Show();
            }
            else
            {
                if (Visible) Hide();
            }
        }

        public bool Toggle()
        {
            if (Visible)
            {
                Hide();
                return false;
            }
            else
            {
                Show();
                return true;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;

            using (SolidBrush bgBrush = new SolidBrush(BackColor))
            {
                g.FillRectangle(bgBrush, ClientRectangle);
            }

            g.DrawRectangle(_borderPen, 0, 0, Width - 1, Height - 1);

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
                _font?.Dispose();
                _borderPen?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

