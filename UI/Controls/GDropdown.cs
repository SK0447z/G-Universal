using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace GUniversal
{
    public class GDropdown : Control
    {
        private readonly List<string> _items = new List<string>();
        private string _selectedItem = "";
        private readonly ContextMenuStrip _menu;
        public event EventHandler? SelectedChanged;

        public GDropdown()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = MainForm.ButtonBack;
            Font = new Font("Segoe UI", 8.5F);
            Cursor = Cursors.Hand;
            Height = 28;
            _menu = new ContextMenuStrip
            {
                BackColor = MainForm.CardBack,
                ForeColor = MainForm.TextMain,
                Renderer = new DarkMenuRenderer()
            };
        }

        public List<string> Items => _items;

        public string SelectedItem
        {
            get => _selectedItem;
            set { _selectedItem = value; Invalidate(); }
        }

        public void SetItems(string[] items, string selected)
        {
            _items.Clear();
            _items.AddRange(items);
            _menu.Items.Clear();
            foreach (string item in items)
            {
                var mi = new ToolStripMenuItem(item);
                string captured = item;
                mi.Click += (s, e) =>
                {
                    _selectedItem = captured;
                    Invalidate();
                    SelectedChanged?.Invoke(this, EventArgs.Empty);
                };
                _menu.Items.Add(mi);
            }
            _selectedItem = selected;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            bool hovered = ClientRectangle.Contains(PointToClient(Cursor.Position));
            Color bg = hovered ? Color.FromArgb(68, 68, 68) : MainForm.ButtonBack;
            using (SolidBrush b = new SolidBrush(bg))
                e.Graphics.FillRectangle(b, ClientRectangle);
            using (Pen p = new Pen(MainForm.BorderColor, 1))
                e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);

            Rectangle textRect = new Rectangle(6, 0, Width - 24, Height);
            TextRenderer.DrawText(e.Graphics, _selectedItem, Font, textRect, MainForm.TextMain,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);

            int tx = Width - 16, ty = Height / 2 - 2;
            Point[] tri = { new Point(tx, ty), new Point(tx + 8, ty), new Point(tx + 4, ty + 5) };
            using (SolidBrush tb = new SolidBrush(MainForm.TextMuted))
                e.Graphics.FillPolygon(tb, tri);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            _menu.Show(this, new Point(0, Height));
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); Invalidate(); }
    }
}

