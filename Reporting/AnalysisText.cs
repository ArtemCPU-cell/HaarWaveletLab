using System.Globalization;
using System.Text;
using HaarWaveletLab.Models;

namespace HaarWaveletLab.Reporting;

/// <summary>
/// Автоматические текстовые выводы: что произошло с изображением после удаления матриц,
/// как меняются энергия, объём данных и качество при росте порядка разложения.
/// </summary>
public static class AnalysisText
{
    private static readonly CultureInfo CI = CultureInfo.InvariantCulture;

    public static string Num(int value) => value.ToString("N0", CI).Replace(",", " ");

    /// <summary>Согласование слова «коэффициент» с числом.</summary>
    public static string CoeffWord(int count)
    {
        int lastTwo = count % 100;
        if (lastTwo is >= 11 and <= 14) return "коэффициентов";
        return (count % 10) switch
        {
            1 => "коэффициент",
            2 or 3 or 4 => "коэффициента",
            _ => "коэффициентов"
        };
    }

    public static string Percent(double value) => value.ToString("F1", CI) + " %";

    public static string Db(double value) =>
        double.IsPositiveInfinity(value) ? "∞" : value.ToString("F2", CI);

    public static string DecompositionSummary(OrderReport order) => DecompositionSummary(order.Decomposition);

    public static string DecompositionSummary(HaarDecomposition decomposition)
    {
        int order = decomposition.Order;
        var bands = decomposition.GetBandStats(order);
        int bandSize = bands[0].Size;
        int levelCoefficients = bands.Sum(b => b.CoefficientCount);
        int totalCoefficients = decomposition.Size * decomposition.Size;
        double levelShare = 100.0 * levelCoefficients / totalCoefficients;
        var sb = new StringBuilder();

        sb.Append($"Порядок {order}: изображение {decomposition.Size}×{decomposition.Size} разложено на четыре матрицы ")
          .Append($"{bandSize}×{bandSize}. Матрицы этого уровня содержат {Num(levelCoefficients)} {CoeffWord(levelCoefficients)} ")
          .Append($"({Percent(levelShare)} полного массива N²). ");
        sb.Append(order == 1
            ? "Они покрывают весь массив коэффициентов. "
            : "Остальные коэффициенты — детали предыдущих уровней. ");
        sb.Append($"Полная пирамида по-прежнему содержит {Num(totalCoefficients)} {CoeffWord(totalCoefficients)} — столько же, сколько пикселей: ")
          .Append("преобразование обратимо и само по себе объём данных не уменьшает.\n");

        sb.Append("Доли энергии матриц этого уровня от полной энергии изображения: ");
        sb.Append(string.Join(", ", bands.Select(b => $"{b.Kind.Code()} — {Percent(b.EnergyPercent)}")));
        sb.Append(".\n");

        sb.Append("Полосы пространственных частот (в долях от частоты Найквиста): ");
        sb.Append(string.Join("; ", bands.Select(b =>
        {
            var (low, high) = b.FrequencyBand;
            return $"{b.Kind.Code()} {low.ToString("F3", CI)}…{high.ToString("F3", CI)}";
        })));
        sb.Append(".\n");

        if (order == 1)
        {
            double allShare = bands[0].EnergyPercent;
            if (allShare >= 95)
                sb.Append("Почти вся энергия сосредоточена в приближении ALL: именно эта матрица определяет яркость и крупные формы, ")
                  .Append("а детали лишь уточняют изображение. Поэтому коэффициенты деталей можно квантовать грубее.\n");
            else if (allShare >= 80)
                sb.Append("Основная энергия по-прежнему в ALL, но заметная её часть приходится на детали — изображение богато контрастными границами.\n");
            else
                sb.Append("Энергия распределена между матрицами сравнительно равномерно: изображение насыщено мелкомасштабными деталями.\n");
        }
        else
        {
            sb.Append($"Матрица ALL{order} остаётся главной по энергии: на неё приходится {Percent(bands[0].EnergyPercent)} полной энергии, ")
              .Append($"но она состоит всего из {Num(bands[0].CoefficientCount)} {CoeffWord(bands[0].CoefficientCount)} — это компактное описание крупных форм изображения. ")
              .Append($"Остальные детали (включая матрицы этого уровня) несут оставшиеся {Percent(100.0 - bands[0].EnergyPercent)} энергии. ")
              .Append("Чем выше порядок, тем меньше коэффициентов требуется для приближения — в этом и состоит энергетическая компактность вейвлет-преобразования.\n");
        }

        return sb.ToString();
    }

    public static string RemovalParagraph(RemovalExperiment experiment)
    {
        var sb = new StringBuilder();
        string code = experiment.Label;
        sb.Append($"Удаление {code}: обнулено {Num(experiment.RemovedCoefficientCount)} {CoeffWord(experiment.RemovedCoefficientCount)} ")
          .Append($"({Percent(experiment.RemovedCoefficientPercent)} объёма), потеряно {Percent(experiment.RemovedEnergyPercent)} энергии. ")
          .Append($"PSNR = {Db(experiment.Metrics.Psnr)} дБ, SSIM = {experiment.Metrics.Ssim.ToString("F3", CI)} — ")
          .Append($"{experiment.Metrics.QualityPhrase}.\n");

        if (experiment.RemovedKinds.Count == 1)
        {
            var (low, high) = experiment.RemovedKinds[0].FrequencyBand(experiment.Level);
            sb.Append($"Удалена полоса частот {low.ToString("F3", CI)}…{high.ToString("F3", CI)} от частоты Найквиста. ");
            sb.Append(EffectSentence(experiment.RemovedKinds[0]));
        }
        else
        {
            sb.Append("Удалено несколько матриц одновременно, поэтому искажения складываются.\n");
        }

        if (experiment.RemovedKinds.Count == 1 && experiment.RemovedKinds[0] == SubbandKind.All)
        {
            if (experiment.RemovedEnergyPercent < 10)
                sb.Append("Хотя доля энергии невелика, удалена самая низкочастотная составляющая: изображение теряет яркость и контраст, ")
                  .Append("поэтому визуальный эффект сильнее, чем у матриц деталей.\n");
        }
        else if (experiment.RemovedEnergyPercent < 0.5)
        {
            sb.Append("Потеря энергии мала, поэтому на глаз изображение почти не меняется.\n");
        }
        else if (experiment.RemovedEnergyPercent > 20)
        {
            sb.Append("Потеряна значительная часть энергии — изменения хорошо видны.\n");
        }

        return sb.ToString();
    }

    private static string EffectSentence(SubbandKind kind) => kind switch
    {
        SubbandKind.All =>
            "Без матрицы ALL исчезает средняя яркость: восстановленное изображение темнеет, остаются только контуры и перепады. " +
            "Это самая тяжёлая потеря — приближение несёт основную информацию.\n",
        SubbandKind.Alh =>
            "Пропадают горизонтальные перепады яркости — горизонтальные линии и границы; вертикальные структуры сохраняются.\n",
        SubbandKind.Ahl =>
            "Пропадают вертикальные перепады яркости — вертикальные линии и границы; горизонтальные структуры сохраняются.\n",
        SubbandKind.Ahh =>
            "Удаляются самые мелкие диагональные детали, текстуры и шум; крупные формы и контуры остаются.\n",
        _ => ""
    };

    public static string OrderTrend(OrderReport current, OrderReport? previous)
    {
        if (previous is null) return "";

        var sb = new StringBuilder();
        sb.Append($"По сравнению с порядком {previous.Order}: ");
        var parts = new List<string>();

        foreach (var kind in Subbands.All)
        {
            var before = previous.GetRemoval(kind).Metrics.Psnr;
            var after = current.GetRemoval(kind).Metrics.Psnr;
            parts.Add($"для {kind.Code()} PSNR {DescribePsnrChange(before, after)}");
        }

        sb.Append(string.Join("; ", parts)).Append(".\n");
        return sb.ToString();
    }

    /// <summary>Словесное изменение PSNR, корректно работающее с бесконечностью.</summary>
    private static string DescribePsnrChange(double before, double after)
    {
        bool beforeInfinite = double.IsPositiveInfinity(before);
        bool afterInfinite = double.IsPositiveInfinity(after);

        if (beforeInfinite && afterInfinite) return "не изменился (∞ в обоих случаях)";
        if (afterInfinite) return $"стал бесконечным (было {Db(before)} дБ)";
        if (beforeInfinite) return $"перестал быть бесконечным (стало {Db(after)} дБ)";

        double delta = after - before;
        if (delta < -0.05) return $"снизился на {Math.Abs(delta).ToString("F2", CI)} дБ";
        if (delta > 0.05) return $"вырос на {Math.Abs(delta).ToString("F2", CI)} дБ";
        return "не изменился";
    }

    public static string CompareWithPrevious(RemovalExperiment current, OrderReport? previous)
    {
        if (previous is null || current.RemovedKinds.Count != 1) return "";

        var kind = current.RemovedKinds[0];
        var before = previous.GetRemoval(kind).Metrics;
        return $"По сравнению с порядком {previous.Order} PSNR для {kind.Code()} " +
               $"{DescribePsnrChange(before.Psnr, current.Metrics.Psnr)} " +
               $"(было {Db(before.Psnr)} дБ, стало {Db(current.Metrics.Psnr)} дБ). ";
    }

    public static string OrderNarrative(OrderReport order, OrderReport? previous)    {
        var sb = new StringBuilder();
        sb.Append(DecompositionSummary(order)).Append('\n');

        foreach (var removal in order.Removals)
            sb.Append(RemovalParagraph(removal)).Append('\n');

        var trend = OrderTrend(order, previous);
        if (trend.Length > 0) sb.Append(trend);

        var worst = order.Removals.OrderBy(r => r.Metrics.Psnr).First();
        var best = order.Removals.OrderByDescending(r => r.Metrics.Psnr).First();
        sb.Append($"Итог порядка {order.Order}: наибольшие искажения даёт удаление {worst.Label} ")
          .Append($"(PSNR {Db(worst.Metrics.Psnr)} дБ), наименьшие — удаление {best.Label} ")
          .Append($"(PSNR {Db(best.Metrics.Psnr)} дБ).\n");

        return sb.ToString();
    }

    public static string LiveSummary(
        HaarDecomposition decomposition,
        IReadOnlyList<SubbandKind> removed,
        RemovalExperiment combined,
        IReadOnlyList<RemovalExperiment> singles)
    {
        var sb = new StringBuilder();
        sb.Append(DecompositionSummary(decomposition)).Append('\n');

        if (removed.Count == 0)
        {
            sb.Append("Все четыре матрицы сохранены: обратное преобразование восстанавливает изображение без потерь ")
              .Append("(PSNR = ∞, SSIM = 1.000). Отметьте матрицу, чтобы увидеть, какую информацию она несёт.\n");
            return sb.ToString();
        }

        sb.Append("ТЕКУЩИЙ ЭКСПЕРИМЕНТ\n");
        sb.Append(RemovalParagraph(combined)).Append('\n');

        if (removed.Count > 1)
        {
            sb.Append("Для сравнения — потери при удалении каждой матрицы по отдельности:\n");
            foreach (var kind in removed)
            {
                var single = singles.First(r => r.RemovedKinds.Count == 1 && r.RemovedKinds[0] == kind);
                sb.Append($"• {kind.Code()}: PSNR {Db(single.Metrics.Psnr)} дБ, SSIM ")
                  .Append($"{single.Metrics.Ssim.ToString("F3", CI)}, энергия {Percent(single.RemovedEnergyPercent)}.\n");
            }
        }

        return sb.ToString();
    }

    public static string ThresholdSummary(ThresholdStudy study)
    {
        var sb = new StringBuilder();
        sb.Append($"Исследование выполнено для разложения до порядка {study.Order}. ")
          .Append("Коэффициенты упорядочены по модулю; сохраняется заданная доля наибольших, остальные заменяются нулями.\n\n");

        foreach (double target in new[] { 10.0, 5.0, 2.0, 1.0, 0.5 })
        {
            var point = study.Points.OrderBy(p => Math.Abs(p.TargetPercent - target)).First();
            sb.Append($"• оставлено {Percent(point.ActualPercent)} коэффициентов (порог |c| ≥ ")
              .Append($"{point.Threshold.ToString("F3", CI)}): PSNR {Db(point.Metrics.Psnr)} дБ, SSIM ")
              .Append($"{point.Metrics.Ssim.ToString("F3", CI)}, условное сжатие ")
              .Append($"{point.CompressionRatio.ToString("F1", CI)}×.\n");
        }

        var good = study.Points
            .Where(p => p.Metrics.Psnr >= 30)
            .OrderByDescending(p => p.CompressionRatio)
            .FirstOrDefault();

        if (good is not null)
        {
            sb.Append($"\nКомпромисс: сохранив {Percent(good.ActualPercent)} коэффициентов, получаем PSNR ")
              .Append($"{Db(good.Metrics.Psnr)} дБ при условном сжатии {good.CompressionRatio.ToString("F1", CI)}×. ")
              .Append("Это соответствует визуально высокому качеству.\n");
        }

        sb.Append("Заметная часть коэффициентов деталей близка к нулю, поэтому их можно не передавать. ")
          .Append("Условное сжатие посчитано по числу сохранённых коэффициентов; реальный выигрыш зависит ещё и от энтропийного кодирования.\n");
        return sb.ToString();
    }

    public static string FinalConclusion(ReportModel model)
    {
        var orders = model.Orders;
        var averages = Subbands.All.ToDictionary(k => k, k => orders.Average(o => o.GetRemoval(k).Metrics.Psnr));
        var worstKind = averages.OrderBy(pair => pair.Value).First().Key;
        var bestKind = averages.OrderByDescending(pair => pair.Value).First().Key;
        double worstPsnr = averages[worstKind];
        double bestPsnr = averages[bestKind];

        double allFirstShare = orders[0].GetRemoval(SubbandKind.All).RemovedEnergyPercent;
        double allLastShare = orders[^1].GetRemoval(SubbandKind.All).RemovedEnergyPercent;
        double ahhMin = orders.Min(o => o.GetRemoval(SubbandKind.Ahh).RemovedEnergyPercent);
        double ahhMax = orders.Max(o => o.GetRemoval(SubbandKind.Ahh).RemovedEnergyPercent);

        var sb = new StringBuilder();
        sb.Append("В ходе работы изучено прямое и обратное вейвлет-преобразование изображений на основе вейвлета Хаара. ")
          .Append($"Изображение {model.Image.Size}×{model.Image.Size} разложено рекурсивно до {model.MaxOrder} порядка; ")
          .Append("проверено, что при сохранении всех коэффициентов обратное преобразование восстанавливает изображение без потерь.\n\n");

        if (model.MaxOrder == 1)
        {
            sb.Append($"На первом уровне матрица приближения ALL1 содержит {Percent(allFirstShare)} энергии — это основная информация ")
              .Append("о яркости и крупных формах изображения. Матрицы ALH1, AHL1 и AHH1 несут мелкие детали; их удаление ")
              .Append("сказывается на изображении значительно слабее, чем удаление приближения.\n\n");
        }
        else
        {
            sb.Append($"На первом уровне матрица приближения ALL1 содержит {Percent(allFirstShare)} энергии — это основная информация ")
              .Append("о яркости и крупных формах изображения. С ростом порядка ALL остаётся главной по энергии ")
              .Append($"({Percent(allLastShare)} на порядке {model.MaxOrder}), но состоит из всё меньшего числа коэффициентов: ")
              .Append($"на порядке {model.MaxOrder} остаётся всего {Num(orders[^1].Bands[0].CoefficientCount)} {CoeffWord(orders[^1].Bands[0].CoefficientCount)} — это компактный, но грубый эскиз. ")
              .Append("Остальные детали (в том числе матрицы этого уровня) несут оставшуюся энергию и уточняют мелкие структуры. Поэтому удаление ALL на любом уровне ")
              .Append("резко снижает яркость и контраст изображения.\n\n");
        }

        sb.Append($"В среднем по всем экспериментам наибольшие искажения даёт удаление {worstKind.Code()} ")
          .Append($"(средний PSNR {Db(worstPsnr)} дБ), наименьшие — удаление {bestKind.Code()} ")
          .Append($"({Db(bestPsnr)} дБ). Доля энергии матрицы AHH составляет от {Percent(ahhMin)} до {Percent(ahhMax)} — ")
          .Append("это самые тонкие детали, и их потеря меньше всего влияет на восприятие.\n\n");

        var first = orders[0];
        var last = orders[^1];
        if (orders.Count > 1)
        {
            double allFirst = first.GetRemoval(SubbandKind.All).Metrics.Psnr;
            double allLast = last.GetRemoval(SubbandKind.All).Metrics.Psnr;
            double delta = allLast - allFirst;
            string verb = delta < -0.05 ? "снижается" : delta > 0.05 ? "возрастает" : "остаётся практически неизменным";
            sb.Append($"При увеличении порядка разложения с 1 до {model.MaxOrder} размер каждой матрицы уменьшается в четыре раза за шаг, ")
              .Append($"а PSNR после удаления ALL {verb} (с {Db(allFirst)} до {Db(allLast)} дБ): ")
              .Append("одна матрица высокого порядка затрагивает меньшую долю коэффициентов, хотя и лишает изображение яркости. ")
              .Append("Если же сохранить только приближение и отбросить все детали, качество падает с ростом порядка (раздел 7.1).\n\n");
        }

        var study = model.Thresholds;
        var compromise = study.Points.Where(p => p.Metrics.Psnr >= 30)
            .OrderByDescending(p => p.CompressionRatio)
            .FirstOrDefault();

        if (model.Approximation.Count > 1)
        {
            var firstApprox = model.Approximation[0];
            var lastApprox = model.Approximation[^1];
            sb.Append("Если отбросить все детали и оставить только приближение ALL_k, качество закономерно падает с ростом порядка: ")
              .Append($"PSNR уменьшается с {Db(firstApprox.Metrics.Psnr)} дБ (k = 1) до {Db(lastApprox.Metrics.Psnr)} дБ (k = {lastApprox.Order}), ")
              .Append($"а условное сжатие растёт с {firstApprox.CompressionRatio.ToString("F1", CI)}× до ")
              .Append($"{lastApprox.CompressionRatio.ToString("F1", CI)}×. Это и есть основной компромисс между сжатием и качеством.\n\n");
        }

        if (compromise is not null)
        {
            sb.Append($"Отбрасывание малых коэффициентов подтверждает компромисс между сжатием и качеством: ")
              .Append($"при сохранении {Percent(compromise.ActualPercent)} коэффициентов достигнуто сжатие ")
              .Append($"{compromise.CompressionRatio.ToString("F1", CI)}× с PSNR {Db(compromise.Metrics.Psnr)} дБ.\n\n");
        }

        sb.Append("Таким образом, вейвлет-преобразование Хаара разделяет изображение на компоненты с разными пространственными ")
          .Append("частотами. Приближение ALL (особенно на первом уровне) содержит основную энергию, детали ALH и AHL отвечают ")
          .Append("за горизонтальные и вертикальные перепады, а AHH — за тонкие диагональные структуры. Рост порядка разложения ")
          .Append("и отбрасывание малых коэффициентов позволяют уменьшить объём данных, но снижают качество восстановленного изображения. ")
          .Append("Это и есть основной компромисс между степенью сжатия и сохранностью визуальной информации.");

        return sb.ToString();
    }

    public static string CompressionTableNote => "Сжатие условное: по числу сохранённых коэффициентов, без энтропийного кодирования.";
}
