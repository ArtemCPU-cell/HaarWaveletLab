using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using HaarWaveletLab.Core;
using HaarWaveletLab.Models;

namespace HaarWaveletLab.Imaging;

/// <summary>Превращение матриц яркости и коэффициентов в изображения (PNG, теплокарты, сравнения).</summary>
public static class ImageRenderer
{
    private static readonly Color Dark = Color.FromArgb(13, 15, 20);

    public static Bitmap ToGrayscaleBitmap(double[,] pixels)
    {
        int height = pixels.GetLength(0);
        int width = pixels.GetLength(1);
        var bitmap = new Bitmap(width, height, PixelFormat.Format8bppIndexed);
        SetGrayPalette(bitmap);

        var data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format8bppIndexed);
        try
        {
            int stride = data.Stride;
            var buffer = new byte[stride * height];
            for (int y = 0; y < height; y++)
            {
                int row = y * stride;
                for (int x = 0; x < width; x++)
                    buffer[row + x] = PixelMath.ToByte(pixels[y, x]);
            }

            Marshal.Copy(buffer, 0, data.Scan0, buffer.Length);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return bitmap;
    }

    private static void SetGrayPalette(Bitmap bitmap)
    {
        var palette = bitmap.Palette;
        for (int i = 0; i < 256; i++)
            palette.Entries[i] = Color.FromArgb(255, i, i, i);
        bitmap.Palette = palette;
    }

    /// <summary>Теплокарта одной матрицы коэффициентов: синий — отрицательные, жёлтый — положительные.</summary>
    public static Bitmap RenderBand(HaarDecomposition decomposition, int level, SubbandKind kind)
    {
        var rect = decomposition.Rect(level, kind);
        var stats = decomposition.GetBandStats(level, kind);
        double detailMax = DetailMax(decomposition, level);

        var bitmap = new Bitmap(rect.Size, rect.Size, PixelFormat.Format24bppRgb);
        var data = bitmap.LockBits(new Rectangle(0, 0, rect.Size, rect.Size), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
        try
        {
            int stride = data.Stride;
            var buffer = new byte[stride * rect.Size];
            for (int y = 0; y < rect.Size; y++)
            {
                int row = y * stride;
                for (int x = 0; x < rect.Size; x++)
                {
                    double v = decomposition.Coefficients[rect.Row + y, rect.Column + x];
                    Color color = kind == SubbandKind.All
                        ? GrayFromRange(v, stats.Min, stats.Max)
                        : DetailColor(v, detailMax);
                    int offset = row + x * 3;
                    buffer[offset] = color.B;
                    buffer[offset + 1] = color.G;
                    buffer[offset + 2] = color.R;
                }
            }

            Marshal.Copy(buffer, 0, data.Scan0, buffer.Length);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return bitmap;
    }

    /// <summary>Классическая пирамида: ALL — градации серого, детали — цветовая нормировка по уровню.</summary>
    public static Bitmap RenderPyramid(HaarDecomposition decomposition, bool drawLabels = true)
    {
        int n = decomposition.Size;
        var bitmap = new Bitmap(n, n, PixelFormat.Format24bppRgb);
        var data = bitmap.LockBits(new Rectangle(0, 0, n, n), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
        try
        {
            int stride = data.Stride;
            var buffer = new byte[stride * n];

            for (int level = decomposition.Order; level >= 1; level--)
            {
                double detailMax = DetailMax(decomposition, level);
                foreach (var kind in Subbands.All)
                {
                    // Матрицу ALL рисуем только для самого глубокого уровня:
                    // на остальных уровнях эта область занята более мелкими поддиапазонами.
                    if (kind == SubbandKind.All && level != decomposition.Order) continue;

                    var rect = decomposition.Rect(level, kind);
                    var stats = decomposition.GetBandStats(level, kind);
                    for (int y = 0; y < rect.Size; y++)
                    {
                        int row = (rect.Row + y) * stride;
                        for (int x = 0; x < rect.Size; x++)
                        {
                            double v = decomposition.Coefficients[rect.Row + y, rect.Column + x];
                            Color color = kind == SubbandKind.All
                                ? GrayFromRange(v, stats.Min, stats.Max)
                                : DetailColor(v, detailMax);
                            int offset = row + (rect.Column + x) * 3;
                            buffer[offset] = color.B;
                            buffer[offset + 1] = color.G;
                            buffer[offset + 2] = color.R;
                        }
                    }
                }
            }

            Marshal.Copy(buffer, 0, data.Scan0, buffer.Length);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        if (drawLabels) DrawPyramidLabels(bitmap, decomposition);
        return bitmap;
    }

    private static double DetailMax(HaarDecomposition decomposition, int level)
    {
        double max = 0.0;
        foreach (var kind in Subbands.Details)
        {
            var stats = decomposition.GetBandStats(level, kind);
            max = Math.Max(max, Math.Max(Math.Abs(stats.Min), Math.Abs(stats.Max)));
        }

        return max;
    }

    private static void DrawPyramidLabels(Bitmap bitmap, HaarDecomposition decomposition)
    {
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

        using var border = new Pen(Color.FromArgb(120, 255, 255, 255), 1f);
        using var shadow = new SolidBrush(Color.FromArgb(180, 0, 0, 0));
        using var text = new SolidBrush(Color.FromArgb(240, 240, 245));

        for (int level = 1; level <= decomposition.Order; level++)
        {
            foreach (var kind in Subbands.All)
            {
                if (kind == SubbandKind.All && level != decomposition.Order) continue;
                var rect = decomposition.Rect(level, kind);
                g.DrawRectangle(border, rect.Column, rect.Row, rect.Size - 1, rect.Size - 1);

                if (rect.Size < 30) continue;
                string label = kind.Code() + level;
                float fontPixels = rect.Size >= 80 ? 11f : rect.Size >= 48 ? 8.5f : 7f;
                using var font = new Font("Segoe UI", fontPixels, FontStyle.Bold, GraphicsUnit.Pixel);
                float x = rect.Column + 4;
                float y = rect.Row + 4;
                g.DrawString(label, font, shadow, x + 1, y + 1);
                g.DrawString(label, font, text, x, y);
            }
        }
    }

    /// <summary>Сравнение: исходное | восстановленное | карта разности (усилена).</summary>
    public static Bitmap RenderComparison(double[,] original, double[,] reconstructed, double differenceGain = 4.0)
    {
        int n = original.GetLength(0);
        int gap = Math.Max(2, n / 128);
        int width = n * 3 + gap * 2;
        var bitmap = new Bitmap(width, n, PixelFormat.Format24bppRgb);
        var data = bitmap.LockBits(new Rectangle(0, 0, width, n), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
        try
        {
            int stride = data.Stride;
            var buffer = new byte[stride * n];
            for (int y = 0; y < n; y++)
            {
                int row = y * stride;
                for (int x = 0; x < n; x++)
                {
                    Color left = GrayColor(PixelMath.Clamp(original[y, x]));
                    Color middle = GrayColor(PixelMath.Clamp(reconstructed[y, x]));
                    double difference = Math.Abs(PixelMath.Clamp(original[y, x]) - PixelMath.Clamp(reconstructed[y, x]));
                    Color right = DifferenceColor(difference * differenceGain);

                    WritePixel(buffer, row, x, left);
                    WritePixel(buffer, row, n + gap + x, middle);
                    WritePixel(buffer, row, 2 * n + 2 * gap + x, right);
                }
            }

            Marshal.Copy(buffer, 0, data.Scan0, buffer.Length);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return bitmap;
    }

    public static Bitmap RenderDifference(double[,] original, double[,] reconstructed, double gain = 4.0)
    {
        int n = original.GetLength(0);
        var bitmap = new Bitmap(n, n, PixelFormat.Format24bppRgb);
        var data = bitmap.LockBits(new Rectangle(0, 0, n, n), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
        try
        {
            int stride = data.Stride;
            var buffer = new byte[stride * n];
            for (int y = 0; y < n; y++)
            {
                int row = y * stride;
                for (int x = 0; x < n; x++)
                {
                    double d = Math.Abs(PixelMath.Clamp(original[y, x]) - PixelMath.Clamp(reconstructed[y, x]));
                    WritePixel(buffer, row, x, DifferenceColor(d * gain));
                }
            }

            Marshal.Copy(buffer, 0, data.Scan0, buffer.Length);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return bitmap;
    }

    private static void WritePixel(byte[] buffer, int row, int x, Color color)
    {
        int offset = row + x * 3;
        buffer[offset] = color.B;
        buffer[offset + 1] = color.G;
        buffer[offset + 2] = color.R;
    }

    private static Color GrayColor(double value)
    {
        int v = (int)Math.Round(Math.Clamp(value, 0, 255));
        return Color.FromArgb(v, v, v);
    }

    private static Color GrayFromRange(double value, double min, double max)
    {
        double span = max - min;
        double t = span > 1e-9 ? (value - min) / span : 0.0;
        int v = (int)Math.Round(Math.Clamp(t, 0, 1) * 255);
        return Color.FromArgb(v, v, v);
    }

    private static Color DetailColor(double value, double maxAbs)
    {
        double magnitude = Math.Abs(value);
        double t = maxAbs > 1e-12
            ? Math.Log(1.0 + magnitude / maxAbs * 48.0) / Math.Log(49.0)
            : 0.0;
        t = Math.Clamp(t, 0, 1);

        if (value >= 0)
        {
            return t < 0.5
                ? Lerp(Dark, Color.FromArgb(198, 72, 38), t * 2)
                : Lerp(Color.FromArgb(198, 72, 38), Color.FromArgb(255, 226, 120), (t - 0.5) * 2);
        }

        return t < 0.5
            ? Lerp(Dark, Color.FromArgb(42, 86, 196), t * 2)
            : Lerp(Color.FromArgb(42, 86, 196), Color.FromArgb(136, 214, 255), (t - 0.5) * 2);
    }

    private static Color DifferenceColor(double value)
    {
        double t = Math.Clamp(value / 255.0, 0, 1);
        return t < 0.55
            ? Lerp(Dark, Color.FromArgb(205, 32, 32), t / 0.55)
            : Lerp(Color.FromArgb(205, 32, 32), Color.FromArgb(255, 232, 120), (t - 0.55) / 0.45);
    }

    private static Color Lerp(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return Color.FromArgb(
            (int)Math.Round(a.R + (b.R - a.R) * t),
            (int)Math.Round(a.G + (b.G - a.G) * t),
            (int)Math.Round(a.B + (b.B - a.B) * t));
    }

    public static void SavePng(Bitmap bitmap, string path)
    {
        var folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
        bitmap.Save(path, ImageFormat.Png);
    }

    public static string ToBase64Png(Bitmap bitmap)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        return Convert.ToBase64String(stream.ToArray());
    }

    /// <summary>Сохраняет PNG и возвращает data-URL для встраивания в HTML.</summary>
    public static string SaveAndEmbed(Bitmap bitmap, string path)
    {
        SavePng(bitmap, path);
        return "data:image/png;base64," + ToBase64Png(bitmap);
    }
}
