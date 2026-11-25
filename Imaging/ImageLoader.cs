using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using HaarWaveletLab.Models;

namespace HaarWaveletLab.Imaging;

/// <summary>Загрузка растровых изображений, приведение к квадрату N×N и перевод в яркость Y.</summary>
public static class ImageLoader
{
    public static readonly int[] SupportedSizes = { 128, 256, 512, 1024 };

    public const int MinSize = 64;
    public const int MaxSize = 1024;

    public static Bitmap LoadBitmap(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Файл изображения не найден.", path);

        using var source = Image.FromFile(path);
        var bitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format24bppRgb);
        using var g = Graphics.FromImage(bitmap);
        g.Clear(Color.White);
        g.DrawImage(source, new Rectangle(0, 0, source.Width, source.Height));
        return bitmap;
    }

    /// <summary>Центральный квадратный фрагмент, масштабированный до size×size.</summary>
    public static Bitmap PrepareSquare(Bitmap source, int size)
    {
        size = Math.Clamp(size, MinSize, MaxSize);
        int side = Math.Min(source.Width, source.Height);
        int left = (source.Width - side) / 2;
        int top = (source.Height - side) / 2;

        var result = new Bitmap(size, size, PixelFormat.Format24bppRgb);
        using var g = Graphics.FromImage(result);
        g.Clear(Color.White);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.CompositingQuality = CompositingQuality.HighQuality;

        g.DrawImage(source,
            new Rectangle(0, 0, size, size),
            new Rectangle(left, top, side, side),
            GraphicsUnit.Pixel);

        return result;
    }

    public static LabImage CreateLabImage(Bitmap source, int size, string name, string description)
    {
        using var square = PrepareSquare(source, size);
        var pixels = ToLuminance(square);
        return new LabImage(name, description, pixels);
    }

    /// <summary>Перевод RGB в яркость Y по BT.601: Y = 0.299R + 0.587G + 0.114B.</summary>
    public static double[,] ToLuminance(Bitmap bitmap)
    {
        int width = bitmap.Width;
        int height = bitmap.Height;
        var result = new double[height, width];

        using var rgb = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(rgb))
        {
            g.Clear(Color.White);
            g.DrawImage(bitmap, new Rectangle(0, 0, width, height));
        }

        var data = rgb.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        try
        {
            int stride = data.Stride;
            var buffer = new byte[stride * height];
            Marshal.Copy(data.Scan0, buffer, 0, buffer.Length);

            for (int y = 0; y < height; y++)
            {
                int row = y * stride;
                for (int x = 0; x < width; x++)
                {
                    int offset = row + x * 3;
                    byte b = buffer[offset];
                    byte g = buffer[offset + 1];
                    byte r = buffer[offset + 2];
                    result[y, x] = 0.299 * r + 0.587 * g + 0.114 * b;
                }
            }
        }
        finally
        {
            rgb.UnlockBits(data);
        }

        return result;
    }

    /// <summary>Ближайший допустимый размер (степень двойки).</summary>
    public static int NormalizeSize(int requested)
    {
        int size = Math.Clamp(requested, MinSize, MaxSize);
        int power = 1;
        while (power * 2 <= size) power *= 2;
        return power >= MinSize ? power : MinSize;
    }
}
