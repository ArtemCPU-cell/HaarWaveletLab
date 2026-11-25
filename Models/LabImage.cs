namespace HaarWaveletLab.Models;

/// <summary>Полутоновое изображение N×N, приведённое к диапазону 0…255.</summary>
public sealed class LabImage
{
    public LabImage(string name, string description, double[,] pixels)
    {
        Name = name;
        Description = description;
        Pixels = pixels;
        Size = pixels.GetLength(0);
        if (pixels.GetLength(1) != Size)
            throw new ArgumentException("Изображение должно быть квадратным.", nameof(pixels));

        double min = double.MaxValue;
        double max = double.MinValue;
        double sum = 0.0;
        double energy = 0.0;
        int count = Size * Size;

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                double v = pixels[y, x];
                min = Math.Min(min, v);
                max = Math.Max(max, v);
                sum += v;
                energy += v * v;
            }
        }

        Min = min;
        Max = max;
        Mean = sum / count;
        Energy = energy;
    }

    public string Name { get; }
    public string Description { get; }
    public int Size { get; }
    public double[,] Pixels { get; }
    public double Min { get; }
    public double Max { get; }
    public double Mean { get; }
    public double Energy { get; }

    public double[,] ClonePixels() => (double[,])Pixels.Clone();
}
