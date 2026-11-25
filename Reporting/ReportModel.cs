using HaarWaveletLab.Core;
using HaarWaveletLab.Models;

namespace HaarWaveletLab.Reporting;

/// <summary>Все результаты для одного порядка разложения.</summary>
public sealed class OrderReport
{
    public required int Order { get; init; }
    public required HaarDecomposition Decomposition { get; init; }
    public required RemovalExperiment Baseline { get; init; }
    public required IReadOnlyList<RemovalExperiment> Removals { get; init; }
    public required IReadOnlyList<BandStats> Bands { get; init; }

    public RemovalExperiment GetRemoval(SubbandKind kind) =>
        Removals.First(r => r.RemovedKinds.Count == 1 && r.RemovedKinds[0] == kind);
}

/// <summary>Полная модель отчёта: разложения 1…5, удаление матриц, сжатие, проверка по методичке.</summary>
public sealed class ReportModel
{
    public required LabImage Image { get; init; }
    public required string SourceLabel { get; init; }
    public required string? SourcePath { get; init; }
    public required IReadOnlyList<OrderReport> Orders { get; init; }
    public required IReadOnlyList<ApproximationPoint> Approximation { get; init; }
    public required ThresholdStudy Thresholds { get; init; }
    public required Lb3Check Lb3Check { get; init; }
    public required DateTime CreatedAt { get; init; }

    public int MaxOrder => Orders.Count;

    public static ReportModel Build(LabImage image, string sourceLabel, string? sourcePath = null, int? maxOrder = null)
    {
        int limit = Math.Min(5, SubbandGeometry.MaxOrder(image.Size));
        if (maxOrder.HasValue) limit = Math.Clamp(maxOrder.Value, 1, limit);
        if (limit < 1) limit = 1;

        var orders = new List<OrderReport>(limit);
        for (int order = 1; order <= limit; order++)
        {
            var decomposition = HaarDecomposition.Build(image, order);
            var baseline = LabAnalyzer.RunRemoval(decomposition, Array.Empty<SubbandKind>());
            var removals = LabAnalyzer.RunSingleRemovals(decomposition);
            orders.Add(new OrderReport
            {
                Order = order,
                Decomposition = decomposition,
                Baseline = baseline,
                Removals = removals,
                Bands = decomposition.GetBandStats(order)
            });
        }

        var thresholdDecomposition = HaarDecomposition.Build(image, limit);
        var thresholds = LabAnalyzer.RunThresholdStudy(thresholdDecomposition, LabAnalyzer.CompressionPercents);
        var approximation = LabAnalyzer.RunApproximationStudy(orders.Select(o => o.Decomposition).ToArray());

        return new ReportModel
        {
            Image = image,
            SourceLabel = sourceLabel,
            SourcePath = sourcePath,
            Orders = orders,
            Approximation = approximation,
            Thresholds = thresholds,
            Lb3Check = Lb3Check.Create(),
            CreatedAt = DateTime.Now
        };
    }
}
