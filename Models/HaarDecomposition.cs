using HaarWaveletLab.Core;

namespace HaarWaveletLab.Models;

/// <summary>
/// Результат прямого вейвлет-преобразования Хаара: полная матрица коэффициентов N×N
/// и сведения о разложении до заданного порядка.
/// </summary>
public sealed class HaarDecomposition
{
    private HaarDecomposition(LabImage image, int order, double[,] coefficients)
    {
        Image = image;
        Order = order;
        Coefficients = coefficients;

        double maxAbs = 0.0;
        for (int y = 0; y < image.Size; y++)
            for (int x = 0; x < image.Size; x++)
                maxAbs = Math.Max(maxAbs, Math.Abs(coefficients[y, x]));
        MaxAbsCoefficient = maxAbs;
    }

    public LabImage Image { get; }
    public int Size => Image.Size;
    public int Order { get; }

    /// <summary>Матрица коэффициентов N×N: [ALL AHL; ALH AHH], внутри ALL — следующие уровни.</summary>
    public double[,] Coefficients { get; }

    /// <summary>Суммарная энергия (сумма квадратов) — сохраняется преобразованием Хаара.</summary>
    public double Energy => Image.Energy;

    public double MaxAbsCoefficient { get; }

    public static HaarDecomposition Build(LabImage image, int order)
    {
        int maxOrder = SubbandGeometry.MaxOrder(image.Size);
        if (maxOrder < 1)
            throw new ArgumentException("Для разложения изображение должно иметь размер не менее 2×2.", nameof(image));

        order = Math.Clamp(order, 1, Math.Min(5, maxOrder));
        var coefficients = image.ClonePixels();
        for (int level = 1; level <= order; level++)
            HaarTransform.ForwardLevel(coefficients, image.Size, level);

        return new HaarDecomposition(image, order, coefficients);
    }

    public BandRect Rect(int level, SubbandKind kind) => SubbandGeometry.Rect(Size, level, kind);

    public BandStats GetBandStats(int level, SubbandKind kind)
    {
        var rect = Rect(level, kind);
        double min = double.MaxValue;
        double max = double.MinValue;
        double sum = 0.0;
        double energy = 0.0;
        int nonZero = 0;

        for (int y = rect.Row; y < rect.Row + rect.Size; y++)
        {
            for (int x = rect.Column; x < rect.Column + rect.Size; x++)
            {
                double v = Coefficients[y, x];
                if (Math.Abs(v) > 1e-12) nonZero++;
                min = Math.Min(min, v);
                max = Math.Max(max, v);
                sum += v;
                energy += v * v;
            }
        }

        return new BandStats
        {
            Level = level,
            Kind = kind,
            Size = rect.Size,
            Min = min,
            Max = max,
            Mean = sum / rect.CoefficientCount,
            Energy = energy,
            NonZeroCount = nonZero,
            EnergyShare = Energy > 1e-12 ? energy / Energy : 0.0
        };
    }

    public IReadOnlyList<BandStats> GetBandStats(int level) =>
        Subbands.All.Select(kind => GetBandStats(level, kind)).ToArray();

    public IReadOnlyList<BandStats> GetDeepestBandStats() => GetBandStats(Order);

    /// <summary>Копия матрицы коэффициентов с обнулёнными матрицами указанного уровня.</summary>
    public double[,] CopyWithZeroedBands(int level, IEnumerable<SubbandKind> kinds)
    {
        var copy = (double[,])Coefficients.Clone();
        foreach (var kind in kinds)
        {
            var rect = Rect(level, kind);
            for (int y = rect.Row; y < rect.Row + rect.Size; y++)
                for (int x = rect.Column; x < rect.Column + rect.Size; x++)
                    copy[y, x] = 0.0;
        }

        return copy;
    }

    /// <summary>Значения одной матрицы в виде отдельного двумерного массива.</summary>
    public double[,] GetBandValues(int level, SubbandKind kind)
    {
        var rect = Rect(level, kind);
        var band = new double[rect.Size, rect.Size];
        for (int y = 0; y < rect.Size; y++)
            for (int x = 0; x < rect.Size; x++)
                band[y, x] = Coefficients[rect.Row + y, rect.Column + x];
        return band;
    }

    /// <summary>Полное обратное преобразование без удаления матриц (для проверки обратимости).</summary>
    public double[,] ReconstructAll() => HaarTransform.Reconstruct(Coefficients, Order);

    /// <summary>Обратное преобразование после обнуления указанных матриц уровня.</summary>
    public double[,] ReconstructWithout(int level, IEnumerable<SubbandKind> kinds) =>
        HaarTransform.Reconstruct(CopyWithZeroedBands(level, kinds), Order);
}
