using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Mint;

public class ModernTrayRenderer : ToolStripProfessionalRenderer
{
    public ModernTrayRenderer() : base(new ModernColorTable()) { }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        bool isDark = ThemeManager.IsDarkThemeActive;
        using var pen = new Pen(isDark ? Color.FromArgb(44, 44, 53) : Color.FromArgb(210, 213, 220));
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
        Color hoverColor = isDark ? Color.FromArgb(36, 36, 44) : Color.FromArgb(229, 231, 235);

        var rect = new Rectangle(4, 1, e.Item.Width - 8, e.Item.Height - 2);
        using var brush = new SolidBrush(hoverColor);
        using var path = GetRoundedRectangle(rect, 5);

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.FillPath(brush, path);
    }

    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
    {
        bool isDark = ThemeManager.IsDarkThemeActive;
        Color bg = isDark ? Color.FromArgb(24, 24, 29) : Color.FromArgb(242, 243, 246);
        using var brush = new SolidBrush(bg);
        e.Graphics.FillRectangle(brush, e.AffectedBounds);
    }

    protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e)
    {
        if (e.Image != null)
        {
            // Lock image to the exact vertical center of the item
            int centerY = (e.Item.Height - e.ImageRectangle.Height) / 2;
            var rect = new Rectangle(e.ImageRectangle.X, centerY, e.ImageRectangle.Width, e.ImageRectangle.Height);
            e.Graphics.DrawImage(e.Image, rect);
            return;
        }
        base.OnRenderItemImage(e);
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        // Force the text to share the exact same vertical center line as the icon
        var rect = e.TextRectangle;
        rect.Y = 0;
        rect.Height = e.Item.Height;
        e.TextRectangle = rect;
        e.TextFormat = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine;
        e.TextColor = e.Item.ForeColor;

        base.OnRenderItemText(e);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        bool isDark = ThemeManager.IsDarkThemeActive;
        using var pen = new Pen(isDark ? Color.FromArgb(44, 44, 53) : Color.FromArgb(218, 220, 226));
        int y = e.Item.Height / 2;
        e.Graphics.DrawLine(pen, 12, y, e.Item.Width - 12, y);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        bool isDark = ThemeManager.IsDarkThemeActive;
        Color arrowColor = isDark ? Color.FromArgb(184, 186, 196) : Color.FromArgb(90, 96, 109);

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(arrowColor, 1.8f);

        int midX = e.ArrowRectangle.X + (e.ArrowRectangle.Width / 2);
        int midY = e.ArrowRectangle.Y + (e.ArrowRectangle.Height / 2);

        var points = new[]
        {
            new Point(midX - 2, midY - 4),
            new Point(midX + 2, midY),
            new Point(midX - 2, midY + 4)
        };

        e.Graphics.DrawLines(pen, points);
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
            ThemeManager.IsDarkThemeActive ? Color.FromArgb(24, 24, 29) : Color.FromArgb(242, 243, 246);

        public override Color ImageMarginGradientBegin => ToolStripDropDownBackground;
        public override Color ImageMarginGradientMiddle => ToolStripDropDownBackground;
        public override Color ImageMarginGradientEnd => ToolStripDropDownBackground;

        public override Color MenuBorder =>
            ThemeManager.IsDarkThemeActive ? Color.FromArgb(44, 44, 53) : Color.FromArgb(210, 213, 220);

        public override Color MenuItemBorder => Color.Transparent;
    }
}
