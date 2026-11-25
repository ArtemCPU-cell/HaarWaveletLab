using System.Globalization;
using System.Text;
using HaarWaveletLab.Core;
using HaarWaveletLab.Models;
using HaarWaveletLab.Reporting;

namespace HaarWaveletLab.Export;

/// <summary>Экспорт таблиц и матриц в CSV (открываются в Excel, разделитель — запятая).</summary>
public static class CsvExporter
{
    private static readonly CultureInfo CI = CultureInfo.InvariantCulture;

    public static IReadOnlyList<string> ExportAll(ReportModel model, string directory)
    {
        Directory.CreateDirectory(directory);
        var files = new List<string>();

        files.Add(WriteBands(model, Path.Combine(directory, "01_bands.csv")));
        files.Add(WriteExperiments(model, Path.Combine(directory, "02_experiments.csv")));
        files.Add(WriteThresholds(model.Thresholds, Path.Combine(directory, "03_compression.csv")));
        files.Add(WriteCoefficients(model, Path.Combine(directory, "04_coefficients.csv")));
        files.Add(WriteSource(model, Path.Combine(directory, "05_source_image.csv")));
        files.Add(WriteLb3(model.Lb3Check, Path.Combine(directory, "06_lb3_check.csv")));
        files.Add(WriteApproximation(model, Path.Combine(directory, "07_approximation.csv")));
        return files;
    }

    private static string WriteApproximation(ReportModel model, string path)
    {
        var rows = model.Approximation.Select(point => new[]
        {
            point.Order.ToString(CI),
            Math.Round(Math.Sqrt(point.KeptCount)).ToString(CI),
            point.KeptCount.ToString(CI),
            point.TotalCount.ToString(CI),
            Number(point.KeptPercent, "F4"),
            Number(point.CompressionRatio, "F4"),
            Number(point.Metrics.Mse, "F6"),
            double.IsPositiveInfinity(point.Metrics.Psnr) ? "inf" : Number(point.Metrics.Psnr, "F4"),
            Number(point.Metrics.Ssim, "F6"),
            Number(point.Metrics.Mae, "F4")
        }).ToList();

        var header = new[]
        {
            "Порядок", "Размер_ALL", "Сохранено", "Всего", "Доля_%", "Сжатие_x",
            "MSE", "PSNR_дБ", "SSIM", "MAE"
        };

        return Write(path, header, rows);
    }

    private static string WriteBands(ReportModel model, string path)
    {
        var rows = new List<string[]>();
        foreach (var order in model.Orders)
        {
            foreach (var band in order.Bands)
            {
                var (low, high) = band.FrequencyBand;
                rows.Add(new[]
                {
                    order.Order.ToString(CI),
                    band.Level.ToString(CI),
                    band.Kind.Code(),
                    band.Size.ToString(CI),
                    band.CoefficientCount.ToString(CI),
                    band.NonZeroCount.ToString(CI),
                    Number(band.Min, "F4"),
                    Number(band.Max, "F4"),
                    Number(band.Mean, "F4"),
                    Number(band.Energy, "F4"),
                    Number(band.EnergyPercent, "F4"),
                    Number(low, "F4"),
                    Number(high, "F4")
                });
            }
        }

        var header = new[]
        {
            "Порядок", "Уровень", "Матрица", "Размер", "Коэффициентов", "Ненулевых",
            "Минимум", "Максимум", "Среднее", "Энергия", "Доля_энергии_%",
            "Полоса_от_Найквиста", "Полоса_до_Найквиста"
        };

        return Write(path, header, rows);
    }

    private static string WriteExperiments(ReportModel model, string path)
    {
        var rows = new List<string[]>();
        foreach (var order in model.Orders)
        {
            rows.Add(ExperimentRow(order.Baseline, "без удаления"));
            foreach (var removal in order.Removals)
                rows.Add(ExperimentRow(removal, removal.Label + order.Order));
        }

        var header = new[]
        {
            "Порядок", "Удалённые_матрицы", "Удалено_коэффициентов", "Удалено_%",
            "Потеря_энергии_%", "MSE", "PSNR_дБ", "SSIM", "MAE", "Макс_ошибка",
            "Корреляция", "Осталось_%", "Сжатие_x"
        };

        return Write(path, header, rows);
    }

    private static string[] ExperimentRow(RemovalExperiment experiment, string label) => new[]
    {
        experiment.Level.ToString(CI),
        label,
        experiment.RemovedCoefficientCount.ToString(CI),
        Number(experiment.RemovedCoefficientPercent, "F4"),
        Number(experiment.RemovedEnergyPercent, "F4"),
        Number(experiment.Metrics.Mse, "F6"),
        double.IsPositiveInfinity(experiment.Metrics.Psnr) ? "inf" : Number(experiment.Metrics.Psnr, "F4"),
        Number(experiment.Metrics.Ssim, "F6"),
        Number(experiment.Metrics.Mae, "F4"),
        Number(experiment.Metrics.MaxError, "F4"),
        Number(experiment.Metrics.Correlation, "F6"),
        Number(experiment.KeptCoefficientPercent, "F4"),
        Number(experiment.CompressionRatio, "F4")
    };

    private static string WriteThresholds(ThresholdStudy study, string path)
    {
        var rows = study.Points.Select(point => new[]
        {
            study.Order.ToString(CI),
            Number(point.TargetPercent, "F2"),
            Number(point.ActualPercent, "F4"),
            point.KeptCount.ToString(CI),
            point.TotalCount.ToString(CI),
            Number(point.Threshold, "F6"),
            Number(point.Metrics.Mse, "F6"),
            double.IsPositiveInfinity(point.Metrics.Psnr) ? "inf" : Number(point.Metrics.Psnr, "F4"),
            Number(point.Metrics.Ssim, "F6"),
            Number(point.CompressionRatio, "F4")
        }).ToList();

        var header = new[]
        {
            "Порядок", "Задано_%", "Фактически_%", "Сохранено", "Всего",
            "Порог", "MSE", "PSNR_дБ", "SSIM", "Сжатие_x"
        };

        return Write(path, header, rows);
    }

    private static string WriteCoefficients(ReportModel model, string path)
    {
        var decomposition = model.Orders[^1].Decomposition;
        int n = decomposition.Size;
        var header = new string[n + 1];
        header[0] = "Y\\X";
        for (int x = 0; x < n; x++) header[x + 1] = x.ToString(CI);

        var rows = new List<string[]>(n);
        for (int y = 0; y < n; y++)
        {
            var row = new string[n + 1];
            row[0] = y.ToString(CI);
            for (int x = 0; x < n; x++)
                row[x + 1] = Number(decomposition.Coefficients[y, x], "F4");
            rows.Add(row);
        }

        return Write(path, header, rows);
    }

    private static string WriteSource(ReportModel model, string path)
    {
        int n = model.Image.Size;
        var header = new string[n + 1];
        header[0] = "Y\\X";
        for (int x = 0; x < n; x++) header[x + 1] = x.ToString(CI);

        var rows = new List<string[]>(n);
        for (int y = 0; y < n; y++)
        {
            var row = new string[n + 1];
            row[0] = y.ToString(CI);
            for (int x = 0; x < n; x++)
                row[x + 1] = Number(model.Image.Pixels[y, x], "F1");
            rows.Add(row);
        }

        return Write(path, header, rows);
    }

    private static string WriteLb3(Lb3Check check, string path)
    {
        var rows = new List<string[]>();
        foreach (var item in check.Cases)
        {
            rows.Add(new[]
            {
                item.Title,
                string.Join(";", item.ZeroedIndices.Select(i => (i + 1).ToString(CI))),
                Number(item.OutputPower, "F6"),
                Number(item.ReferenceOutputPower, "F6"),
                Number(Math.Abs(item.OutputPower - item.ReferenceOutputPower), "F6")
            });
        }

        var header = new[] { "Вариант", "Обнулённые_коэффициенты", "P_вых_расчёт", "P_вых_эталон", "Разница" };
        return Write(path, header, rows);
    }

    private static string Write(string path, string[] header, IEnumerable<string[]> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("sep=,");
        sb.AppendLine(string.Join(",", header.Select(Escape)));
        foreach (var row in rows)
            sb.AppendLine(string.Join(",", row.Select(Escape)));

        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
        return path;
    }

    private static string Escape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }

    private static string Number(double value, string format) => value.ToString(format, CI);
}
