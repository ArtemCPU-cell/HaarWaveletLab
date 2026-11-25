namespace HaarWaveletLab.UI;

/// <summary>Цвета и шрифты интерфейса.</summary>
public static class Theme
{
    public static readonly Color Background = Color.FromArgb(244, 246, 251);
    public static readonly Color Surface = Color.White;
    public static readonly Color SurfaceAlt = Color.FromArgb(248, 250, 252);
    public static readonly Color Header = Color.FromArgb(15, 42, 74);
    public static readonly Color Accent = Color.FromArgb(37, 99, 235);
    public static readonly Color AccentDark = Color.FromArgb(30, 64, 175);
    public static readonly Color Text = Color.FromArgb(15, 23, 42);
    public static readonly Color Muted = Color.FromArgb(100, 116, 139);
    public static readonly Color Border = Color.FromArgb(226, 232, 240);
    public static readonly Color ImageBackground = Color.FromArgb(11, 14, 20);
    public static readonly Color Good = Color.FromArgb(22, 163, 74);
    public static readonly Color Warn = Color.FromArgb(217, 119, 6);
    public static readonly Color Bad = Color.FromArgb(220, 38, 38);

    public static readonly Font UiFont = new("Segoe UI", 9.5f);
    public static readonly Font UiFontBold = new("Segoe UI", 9.5f, FontStyle.Bold);
    public static readonly Font SmallFont = new("Segoe UI", 8.5f);
    public static readonly Font TitleFont = new("Segoe UI", 10.5f, FontStyle.Bold);
    public static readonly Font SectionFont = new("Segoe UI", 12f, FontStyle.Bold);
    public static readonly Font HeroFont = new("Segoe UI", 15f, FontStyle.Bold);
    public static readonly Font TheoryBody = new("Segoe UI", 9.5f);
    public static readonly Font TheoryHeading = new("Segoe UI", 12f, FontStyle.Bold);
    public static readonly Font TheorySubHeading = new("Segoe UI", 10f, FontStyle.Bold);

    public static Color ForPsnr(double psnr)
    {
        if (double.IsPositiveInfinity(psnr) || psnr >= 40) return Color.FromArgb(220, 252, 231);
        if (psnr >= 30) return Color.FromArgb(236, 253, 245);
        if (psnr >= 20) return Color.FromArgb(254, 249, 195);
        return Color.FromArgb(254, 226, 226);
    }

    public static Color TextForPsnr(double psnr)
    {
        if (double.IsPositiveInfinity(psnr) || psnr >= 40) return Color.FromArgb(22, 101, 52);
        if (psnr >= 30) return Color.FromArgb(21, 128, 61);
        if (psnr >= 20) return Color.FromArgb(146, 64, 14);
        return Color.FromArgb(153, 27, 27);
    }
}

/// <summary>Светлая палитра для меню и панели инструментов.</summary>
public sealed class LabColorTable : ProfessionalColorTable
{
    public override Color ToolStripGradientBegin => Theme.Surface;
    public override Color ToolStripGradientMiddle => Theme.Surface;
    public override Color ToolStripGradientEnd => Theme.Surface;
    public override Color ToolStripBorder => Theme.Border;
    public override Color MenuStripGradientBegin => Theme.Surface;
    public override Color MenuStripGradientEnd => Theme.Surface;
    public override Color MenuItemSelected => Color.FromArgb(219, 234, 254);
    public override Color MenuItemSelectedGradientBegin => Color.FromArgb(219, 234, 254);
    public override Color MenuItemSelectedGradientEnd => Color.FromArgb(219, 234, 254);
    public override Color MenuItemBorder => Theme.Border;
    public override Color ButtonSelectedHighlight => Color.FromArgb(219, 234, 254);
    public override Color ButtonSelectedGradientBegin => Color.FromArgb(219, 234, 254);
    public override Color ButtonSelectedGradientEnd => Color.FromArgb(219, 234, 254);
    public override Color ButtonPressedGradientBegin => Color.FromArgb(191, 219, 254);
    public override Color ButtonPressedGradientEnd => Color.FromArgb(191, 219, 254);
    public override Color ButtonPressedBorder => Theme.Accent;
    public override Color ButtonSelectedBorder => Theme.Accent;
    public override Color SeparatorDark => Theme.Border;
    public override Color SeparatorLight => Theme.Surface;
    public override Color StatusStripGradientBegin => Theme.Surface;
    public override Color StatusStripGradientEnd => Theme.Surface;
}
