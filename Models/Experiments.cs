using System.Globalization;

namespace HaarWaveletLab.Models;

/// <summary>Результат обратного преобразования после удаления одной или нескольких матриц.</summary>
public sealed class RemovalExperiment
{
    public required int Level { get; init; }
    public required IReadOnlyList<SubbandKind> RemovedKinds { get; init; }
    public required double[,] Reconstructed { get; init; }
    public required QualityMetrics Metrics { get; init; }
    public required IReadOnlyList<BandStats> RemovedBands { get; init; }
    public required int TotalCoefficientCount { get; init; }

    public string Label => string.Join(" + ", RemovedKinds.Select(k => k.Code()));

    public int RemovedCoefficientCount => RemovedBands.Sum(b => b.CoefficientCount);

    public double RemovedCoefficientPercent =>
        100.0 * RemovedCoefficientCount / Math.Max(1, TotalCoefficientCount);

    public double RemovedEnergyShare => RemovedBands.Sum(b => b.EnergyShare);

    public double RemovedEnergyPercent => RemovedEnergyShare * 100.0;

    public double KeptCoefficientPercent => 100.0 - RemovedCoefficientPercent;

    /// <summary>Условный коэффициент сжатия по числу сохранённых коэффициентов.</summary>
    public double CompressionRatio => TotalCoefficientCount / (double)Math.Max(1, TotalCoefficientCount - RemovedCoefficientCount);

    public string CompressionText => CompressionRatio.ToString("F2", CultureInfo.InvariantCulture) + "×";
}

/// <summary>Одна точка исследования сжатия: оставлена заданная доля наибольших коэффициентов.</summary>
public sealed class ThresholdPoint
{
    public required double TargetPercent { get; init; }
    public required int KeptCount { get; init; }
    public required int TotalCount { get; init; }
    public required double Threshold { get; init; }
    public required QualityMetrics Metrics { get; init; }

    public double ActualPercent => 100.0 * KeptCount / Math.Max(1, TotalCount);

    public double CompressionRatio => TotalCount / (double)Math.Max(1, KeptCount);

    public string RetentionText => ActualPercent.ToString("F1", CultureInfo.InvariantCulture) + " %";
}

/// <summary>Результаты исследования сжатия отбрасыванием малых коэффициентов.</summary>
public sealed class ThresholdStudy
{
    public required int Order { get; init; }
    public required IReadOnlyList<ThresholdPoint> Points { get; init; }
}

/// <summary>Восстановление только по приближению ALL_k (все детали отброшены).</summary>
public sealed class ApproximationPoint
{
    public required int Order { get; init; }
    public required double[,] Reconstructed { get; init; }
    public required QualityMetrics Metrics { get; init; }
    public required int KeptCount { get; init; }
    public required int TotalCount { get; init; }

    public double KeptPercent => 100.0 * KeptCount / Math.Max(1, TotalCount);

    public double CompressionRatio => TotalCount / (double)Math.Max(1, KeptCount);
}
