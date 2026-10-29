using System.Drawing;
using System.Windows.Forms;

namespace GUniversal
{
    public class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkColorTable()) { }
    }

    public class DarkColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => MainForm.CardBack;
        public override Color ImageMarginGradientBegin => MainForm.CardBack;
        public override Color ImageMarginGradientMiddle => MainForm.CardBack;
        public override Color ImageMarginGradientEnd => MainForm.CardBack;
        public override Color MenuBorder => MainForm.BorderColor;
        public override Color MenuItemBorder => MainForm.BorderColor;
        public override Color MenuItemSelected => MainForm.ButtonBack;
        public override Color MenuStripGradientBegin => MainForm.CardBack;
        public override Color MenuStripGradientEnd => MainForm.CardBack;
        public override Color MenuItemSelectedGradientBegin => MainForm.ButtonBack;
        public override Color MenuItemSelectedGradientEnd => MainForm.ButtonBack;
        public override Color MenuItemPressedGradientBegin => MainForm.CardBack;
        public override Color MenuItemPressedGradientEnd => MainForm.CardBack;
        public override Color SeparatorDark => MainForm.BorderColor;
        public override Color SeparatorLight => MainForm.BorderColor;
    }
}

