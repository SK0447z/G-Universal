using System;
using System.Drawing;
using System.Windows.Forms;

namespace GUniversal
{
    public class GCheckBox : Control
    {
        private bool _checked = false;
        public event EventHandler? CheckedChanged;

        public GCheckBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Font = new Font("Segoe UI", 8.5F);
            Cursor = Cursors.Hand;
            Height = 24;
        }

        public bool Checked
        {
            get => _checked;
            set
            {
                if (_checked != value)
                {
                    _checked = value;
                    Invalidate();
                    CheckedChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Color parentBg = (BackColor != Color.Empty && BackColor != Control.DefaultBackColor) ? BackColor : MainForm.CardBack;
            using (SolidBrush bgBrush = new SolidBrush(parentBg))
                e.Graphics.FillRectangle(bgBrush, ClientRectangle);

            int boxSize = 16, boxY = (Height - boxSize) / 2;
            Color boxBg = _checked ? MainForm.ActiveBlue : MainForm.ButtonBack;
            using (SolidBrush b = new SolidBrush(boxBg))
                e.Graphics.FillRectangle(b, 0, boxY, boxSize, boxSize);
            using (Pen p = new Pen(_checked ? MainForm.ActiveBlue : MainForm.BorderColor, 1))
                e.Graphics.DrawRectangle(p, 0, boxY, boxSize - 1, boxSize - 1);

            if (_checked)
            {
                using Pen cp = new Pen(Color.White, 2);
                e.Graphics.DrawLine(cp, 3, boxY + 8, 6, boxY + 11);
                e.Graphics.DrawLine(cp, 6, boxY + 11, 12, boxY + 4);
            }

            Rectangle textRect = new Rectangle(boxSize + 6, 0, Width - boxSize - 6, Height);
            TextRenderer.DrawText(e.Graphics, Text, Font, textRect, MainForm.TextTelemetry,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            Checked = !_checked;
        }
    }
}

