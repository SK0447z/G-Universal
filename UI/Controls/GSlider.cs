using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace GUniversal
{
    public class GSlider : Control
    {
        private int _min = 0, _max = 100, _val = 50;
        private bool _dragging = false;
        public event EventHandler? ValueChanged;

        public GSlider()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Cursor = Cursors.Hand;
            Height = 26;
        }

        public int Minimum { get => _min; set { _min = value; Invalidate(); } }
        public int Maximum { get => _max; set { _max = value; Invalidate(); } }

        public int Value
        {
            get => _val;
            set
            {
                int v = Math.Clamp(value, _min, _max);
                if (_val != v)
                {
                    _val = v;
                    Invalidate();
                    ValueChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int trackH = 6, trackY = Height / 2 - trackH / 2;
            float pct = (_max > _min) ? (float)(_val - _min) / (_max - _min) : 0;
            int thumbX = (int)(pct * (Width - 14)) + 7;

            using (SolidBrush bg = new SolidBrush(MainForm.FormBack))
                e.Graphics.FillRectangle(bg, 0, trackY, Width, trackH);
            using (SolidBrush fill = new SolidBrush(MainForm.ActiveBlue))
                e.Graphics.FillRectangle(fill, 0, trackY, thumbX, trackH);
            using (SolidBrush tb = new SolidBrush(Color.White))
                e.Graphics.FillEllipse(tb, thumbX - 7, Height / 2 - 7, 14, 14);
        }

        private void UpdateFromMouse(int mx)
        {
            float pct = Math.Clamp((float)(mx - 7) / (Width - 14), 0, 1);
            Value = (int)(_min + pct * (_max - _min));
        }

        protected override void OnMouseDown(MouseEventArgs e) { _dragging = true; UpdateFromMouse(e.X); base.OnMouseDown(e); }
        protected override void OnMouseMove(MouseEventArgs e) { if (_dragging) UpdateFromMouse(e.X); base.OnMouseMove(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _dragging = false; base.OnMouseUp(e); }
    }
}

