namespace HaarWaveletLab.Models;

/// <summary>Положение матрицы (поддиапазона) в общей матрице коэффициентов N×N.</summary>
public readonly record struct BandRect(int Row, int Column, int Size)
{
    public int CoefficientCount => Size * Size;
}

/// <summary>Статистика одной матрицы коэффициентов.</summary>
public sealed class BandStats
{
    public int Level { get; init; }
    public SubbandKind Kind { get; init; }
    public int Size { get; init; }
    public int NonZeroCount { get; init; }
    public double Min { get; init; }
    public double Max { get; init; }
    public double Mean { get; init; }
    public double Energy { get; init; }

    /// <summary>Доля энергии матрицы в полной энергии изображения (0…1).</summary>
    public double EnergyShare { get; internal set; }

    public int CoefficientCount => Size * Size;

    public double EnergyPercent => EnergyShare * 100.0;

    /// <summary>Границы полосы частот в долях от частоты Найквиста.</summary>
    public (double Low, double High) FrequencyBand => Kind.FrequencyBand(Level);
}

/// <summary>Геометрия размещения четырёх матриц внутри общего массива коэффициентов.</summary>
public static class SubbandGeometry
{
    /// <summary>
    /// Матрица уровня <paramref name="level"/> занимает четверть блока N/2^(level−1).
    /// Раскладка: [ALL AHL; ALH AHH] — вторая буква кода отвечает за ось X, третья за ось Y.
    /// </summary>
    public static BandRect Rect(int imageSize, int level, SubbandKind kind)
    {
        int block = imageSize >> (level - 1);
        int half = block / 2;

        return kind switch
        {
            SubbandKind.All => new BandRect(0, 0, half),
            SubbandKind.Ahl => new BandRect(0, half, half),
            SubbandKind.Alh => new BandRect(half, 0, half),
            SubbandKind.Ahh => new BandRect(half, half, half),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    /// <summary>Максимальный порядок разложения для изображения N×N (не более 10).</summary>
    public static int MaxOrder(int imageSize)
    {
        int order = 0;
        int n = imageSize;
        while (n >= 2)
        {
            n /= 2;
            order++;
        }

        return Math.Min(order, 10);
    }
}
