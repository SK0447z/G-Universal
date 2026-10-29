using System.Drawing;
using System.Windows.Forms;

namespace GUniversal
{
    public class GCard : Panel
    {
        private static readonly Pen BorderPen = new Pen(MainForm.BorderColor, 1);

        public GCard()
        {
            BackColor = MainForm.CardBack;
            DoubleBuffered = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.DrawRectangle(BorderPen, 0, 0, Width - 1, Height - 1);
        }
    }
}

