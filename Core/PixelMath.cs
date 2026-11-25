namespace HaarWaveletLab.Core;

/// <summary>Ограничение значений яркости диапазоном 8-битного изображения.</summary>
public static class PixelMath
{
    public static double Clamp(double value) => value < 0 ? 0 : value > 255 ? 255 : value;

    public static byte ToByte(double value) => (byte)Math.Round(Clamp(value), MidpointRounding.AwayFromZero);

    public static double[,] ClampAll(double[,] pixels)
    {
        int h = pixels.GetLength(0);
        int w = pixels.GetLength(1);
        var result = new double[h, w];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                result[y, x] = Clamp(pixels[y, x]);
        return result;
    }
}
