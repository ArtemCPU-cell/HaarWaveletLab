using HaarWaveletLab.Models;

namespace HaarWaveletLab.Core;

/// <summary>Эксперименты: удаление матриц и сжатие отбрасыванием малых коэффициентов.</summary>
public static class LabAnalyzer
{
    /// <summary>Поочерёдное удаление каждой из четырёх матриц текущего порядка.</summary>
    public static IReadOnlyList<RemovalExperiment> RunSingleRemovals(HaarDecomposition decomposition)
    {
        var results = new List<RemovalExperiment>(4);
        foreach (var kind in Subbands.All)
            results.Add(RunRemoval(decomposition, new[] { kind }));
        return results;
    }

    public static RemovalExperiment RunRemoval(HaarDecomposition decomposition, IReadOnlyCollection<SubbandKind> kinds)
    {
        int level = decomposition.Order;
        int total = decomposition.Size * decomposition.Size;
        var selected = kinds.Count == 0 ? Array.Empty<SubbandKind>() : kinds.ToArray();

        double[,] reconstructed = selected.Length == 0
            ? decomposition.ReconstructAll()
            : decomposition.ReconstructWithout(level, selected);

        var bands = selected.Select(kind => decomposition.GetBandStats(level, kind)).ToArray();
        var metrics = Metrics.Compare(decomposition.Image.Pixels, reconstructed);

        return new RemovalExperiment
        {
            Level = level,
            RemovedKinds = selected,
            Reconstructed = reconstructed,
            Metrics = metrics,
            RemovedBands = bands,
            TotalCoefficientCount = total
        };
    }

    /// <summary>
    /// Оставляем заданную долю наибольших по модулю коэффициентов, остальные обнуляем.
    /// Это модель сжатия: «хвост» упорядоченного массива отбрасывается.
    /// </summary>
    public static ThresholdStudy RunThresholdStudy(HaarDecomposition decomposition, IEnumerable<double> percents)
    {
        int n = decomposition.Size;
        int total = n * n;
        var results = new List<ThresholdPoint>();

        var magnitudes = new double[total];
        int index = 0;
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
                magnitudes[index++] = Math.Abs(decomposition.Coefficients[y, x]);
        }

        Array.Sort(magnitudes);
        int nonZero = magnitudes.Count(v => v > 1e-12);

        foreach (double percent in percents)
        {
            double target = Math.Clamp(percent, 0.01, 100.0);
            int keep = Math.Clamp((int)Math.Round(total * target / 100.0), 1, total);
            bool keepEverything = target >= 99.99;

            // Нулевые коэффициенты не нужно хранить: ограничиваем выборку числом ненулевых.
            if (!keepEverything) keep = Math.Min(keep, Math.Max(1, nonZero));

            double threshold = keepEverything ? 0.0 : magnitudes[total - keep];
            var filtered = (double[,])decomposition.Coefficients.Clone();
            int kept = 0;

            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    double v = filtered[y, x];
                    if (Math.Abs(v) >= threshold)
                    {
                        kept++;
                    }
                    else
                    {
                        filtered[y, x] = 0.0;
                    }
                }
            }

            var reconstructed = HaarTransform.Reconstruct(filtered, decomposition.Order);
            var metrics = Metrics.Compare(decomposition.Image.Pixels, reconstructed);

            results.Add(new ThresholdPoint
            {
                TargetPercent = percent,
                KeptCount = kept,
                TotalCount = total,
                Threshold = threshold,
                Metrics = metrics
            });
        }

        return new ThresholdStudy { Order = decomposition.Order, Points = results };
    }

    /// <summary>Стандартный набор долей сохранённых коэффициентов для исследования сжатия.</summary>
    public static readonly double[] CompressionPercents = { 0.5, 1, 2, 5, 10, 20, 40, 70, 100 };

    /// <summary>
    /// Оставляем только матрицу приближения ALL_k, все детали обнуляем.
    /// Показывает, как с ростом порядка растёт сжатие и падает качество.
    /// </summary>
    public static IReadOnlyList<ApproximationPoint> RunApproximationStudy(IReadOnlyList<HaarDecomposition> decompositions)
    {
        var results = new List<ApproximationPoint>();
        foreach (var decomposition in decompositions)
        {
            int n = decomposition.Size;
            int order = decomposition.Order;
            var rect = decomposition.Rect(order, SubbandKind.All);
            var filtered = new double[n, n];

            for (int y = 0; y < rect.Size; y++)
                for (int x = 0; x < rect.Size; x++)
                    filtered[y, x] = decomposition.Coefficients[rect.Row + y, rect.Column + x];

            var reconstructed = HaarTransform.Reconstruct(filtered, order);
            var metrics = Metrics.Compare(decomposition.Image.Pixels, reconstructed);

            results.Add(new ApproximationPoint
            {
                Order = order,
                Reconstructed = reconstructed,
                Metrics = metrics,
                KeptCount = rect.CoefficientCount,
                TotalCount = n * n
            });
        }

        return results;
    }
}
