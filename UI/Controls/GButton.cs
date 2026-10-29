using System;
using System.Drawing;
using System.Windows.Forms;

namespace GUniversal
{
    public class GButton : Button
    {
        private bool _isActive = false;
        private Color _activeColor = MainForm.ActiveBlue;

        public GButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            BackColor = MainForm.ButtonBack;
            ForeColor = MainForm.TextMain;
            Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        public bool IsActive
        {
            get => _isActive;
            set { _isActive = value; Invalidate(); }
        }

        public Color ActiveColor
        {
            get => _activeColor;
            set { _activeColor = value; if (_isActive) Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            bool hovered = ClientRectangle.Contains(PointToClient(Cursor.Position));
            Color bg = _isActive ? _activeColor : (hovered ? Color.FromArgb(68, 68, 68) : MainForm.ButtonBack);
            Color border = _isActive ? _activeColor : MainForm.BorderColor;
            Color fg = _isActive ? Color.White : MainForm.TextMain;

            using (SolidBrush b = new SolidBrush(bg))
                e.Graphics.FillRectangle(b, ClientRectangle);
            using (Pen p = new Pen(border, 1))
                e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);

            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); Invalidate(); }
        protected override bool ShowFocusCues => false;
    }

    public class GColorButton : GButton
    {
        private Color _color = Color.FromArgb(255, 0, 50);

        public Color SelectedColor
        {
            get => _color;
            set { _color = value; Invalidate(); }
        }

        public GColorButton()
        {
            Text = "Color";
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            bool hovered = ClientRectangle.Contains(PointToClient(Cursor.Position));
            Color bg = hovered ? Color.FromArgb(68, 68, 68) : MainForm.ButtonBack;
            using (SolidBrush b = new SolidBrush(bg))
                e.Graphics.FillRectangle(b, ClientRectangle);
            using (Pen p = new Pen(MainForm.BorderColor, 1))
                e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);

            int swatchSize = 10;
            int sx = 8;
            int sy = (Height - swatchSize) / 2;
            using (SolidBrush brush = new SolidBrush(_color))
                e.Graphics.FillRectangle(brush, sx, sy, swatchSize, swatchSize);
            using (Pen pen = new Pen(MainForm.BorderColor, 1))
                e.Graphics.DrawRectangle(pen, sx, sy, swatchSize, swatchSize);

            Rectangle textRect = new Rectangle(sx + swatchSize + 4, 0, Width - sx - swatchSize - 8, Height);
            TextRenderer.DrawText(e.Graphics, Text, Font, textRect, MainForm.TextMain,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        }
    }
}

