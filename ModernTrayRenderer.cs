using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Mint
{
    public class ModernTrayRenderer : ToolStripProfessionalRenderer
    {
        public ModernTrayRenderer() : base(new ModernColorTable()) { }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            bool isDark = ThemeManager.IsDarkThemeActive;
            using var pen = new Pen(isDark ? Color.FromArgb(46, 46, 54) : Color.FromArgb(220, 221, 226));
            var rect = new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
            e.Graphics.DrawRectangle(pen, rect);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected && !e.Item.Pressed)
            {
                base.OnRenderMenuItemBackground(e);
                return;
            }

            bool isDark = ThemeManager.IsDarkThemeActive;
            Color hoverColor = isDark ? Color.FromArgb(38, 38, 44) : Color.FromArgb(235, 236, 239);

            var rect = new Rectangle(4, 1, e.Item.Width - 8, e.Item.Height - 2);
            using var brush = new SolidBrush(hoverColor);
            using var path = GetRoundedRectangle(rect, 4);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.FillPath(brush, path);
        }

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
        {
            // Eradicate the obsolete 3D gutter/white margin
            bool isDark = ThemeManager.IsDarkThemeActive;
            using var brush = new SolidBrush(isDark ? Color.FromArgb(24, 24, 28) : Color.FromArgb(255, 255, 255));
            e.Graphics.FillRectangle(brush, e.AffectedBounds);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            bool isDark = ThemeManager.IsDarkThemeActive;
            using var pen = new Pen(isDark ? Color.FromArgb(46, 46, 54) : Color.FromArgb(220, 221, 226));
            int y = e.Item.Height / 2;
            e.Graphics.DrawLine(pen, 8, y, e.Item.Width - 8, y);
        }

        private static GraphicsPath GetRoundedRectangle(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private class ModernColorTable : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground =>
                ThemeManager.IsDarkThemeActive ? Color.FromArgb(24, 24, 28) : Color.FromArgb(255, 255, 255);

            public override Color ImageMarginGradientBegin => ToolStripDropDownBackground;
            public override Color ImageMarginGradientMiddle => ToolStripDropDownBackground;
            public override Color ImageMarginGradientEnd => ToolStripDropDownBackground;

            public override Color MenuBorder =>
                ThemeManager.IsDarkThemeActive ? Color.FromArgb(46, 46, 54) : Color.FromArgb(220, 221, 226);

            public override Color MenuItemBorder => Color.Transparent;
        }
    }
}
