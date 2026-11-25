using System.Diagnostics;
using System.Globalization;
using HaarWaveletLab.Models;
using HaarWaveletLab.Reporting;

namespace HaarWaveletLab.App;

/// <summary>Неинтерактивный режим --report: расчёт, отчёт и CSV без интерфейса.</summary>
public static class HeadlessRunner
{
    private static readonly CultureInfo CI = CultureInfo.InvariantCulture;

    public static int Run(CommandLineOptions options)
    {
        var total = Stopwatch.StartNew();
        Console.WriteLine("================================================================");
        Console.WriteLine("  ВЕЙВЛЕТ-ПРЕОБРАЗОВАНИЕ ХААРА — лабораторная работа №3");
        Console.WriteLine("  Неинтерактивный режим: отчёт + CSV");
        Console.WriteLine("================================================================");

        var loadWatch = Stopwatch.StartNew();
        var input = ReportService.LoadInput(options);
        loadWatch.Stop();
        var image = input.Image;

        Console.WriteLine($"Изображение : {image.Name}");
        Console.WriteLine($"Источник    : {input.SourcePath ?? input.SourceLabel}");
        Console.WriteLine($"Размер      : {image.Size} × {image.Size}; яркость Y, диапазон {image.Min:F1}…{image.Max:F1}");
        Console.WriteLine($"Порядки     : 1…{Math.Min(options.Order, 5)}");
        Console.WriteLine();

        var buildWatch = Stopwatch.StartNew();
        var model = ReportService.BuildReport(input, options.Order);
        buildWatch.Stop();

        PrintSummary(model);

        var reportWatch = Stopwatch.StartNew();
        string reportPath = ReportService.GenerateReport(model, options.OutputPath);
        reportWatch.Stop();

        Console.WriteLine("Результаты:");
        Console.WriteLine($"  отчёт  : {reportPath}");
        var reportInfo = new FileInfo(reportPath);
        Console.WriteLine($"           {reportInfo.Length / 1024.0 / 1024.0:F2} МБ, {reportWatch.Elapsed.TotalSeconds:F1} с");

        if (!options.SkipCsv)
        {
            var csvWatch = Stopwatch.StartNew();
            var files = ReportService.ExportCsv(model, options.CsvDirectory);
            csvWatch.Stop();
            Console.WriteLine($"  CSV    : {Path.GetDirectoryName(files[0])} ({files.Count} файлов, {csvWatch.Elapsed.TotalSeconds:F1} с)");
            foreach (string file in files)
                Console.WriteLine("           " + Path.GetFileName(file));
        }

        Console.WriteLine();
        Console.WriteLine("Краткий вывод:");
        foreach (string line in FirstParagraphs(AnalysisText.FinalConclusion(model), 2))
            Console.WriteLine("  " + line);

        total.Stop();
        Console.WriteLine();
        Console.WriteLine($"Готово за {total.Elapsed.TotalSeconds:F1} с. Откройте файл отчёта в браузере.");
        return 0;
    }

    private static void PrintSummary(ReportModel model)
    {
        Console.WriteLine("----------------------------------------------------------------");
        Console.WriteLine("Сводка: PSNR (дБ) после удаления матрицы и доля энергии ALL");
        Console.WriteLine("----------------------------------------------------------------");
        Console.WriteLine($"{"Порядок",-8}{"ALL",-10}{"ALH",-10}{"AHL",-10}{"AHH",-10}{"ALL, %",-9}{"AHH, %",-9}");
        Console.WriteLine(new string('-', 66));

        foreach (var order in model.Orders)
        {
            var all = order.GetRemoval(SubbandKind.All).Metrics;
            var alh = order.GetRemoval(SubbandKind.Alh).Metrics;
            var ahl = order.GetRemoval(SubbandKind.Ahl).Metrics;
            var ahh = order.GetRemoval(SubbandKind.Ahh).Metrics;

            Console.WriteLine($"{order.Order,-8}{Psnr(all),-10}{Psnr(alh),-10}{Psnr(ahl),-10}{Psnr(ahh),-10}" +
                              $"{order.Bands[0].EnergyPercent.ToString("F1", CI),-9}{order.Bands[3].EnergyPercent.ToString("F1", CI),-9}");
        }

        Console.WriteLine();
        var threshold = model.Thresholds;
        var point = threshold.Points.Where(p => p.Metrics.Psnr >= 30)
            .OrderByDescending(p => p.CompressionRatio)
            .FirstOrDefault();
        if (point is not null)
        {
            Console.WriteLine($"Сжатие: при сохранении {point.ActualPercent:F1} % коэффициентов " +
                              $"PSNR = {point.Metrics.Psnr:F2} дБ, условное сжатие {point.CompressionRatio:F1}×.");
        }

        if (model.Approximation.Count > 1)
        {
            var first = model.Approximation[0];
            var last = model.Approximation[^1];
            Console.WriteLine($"Только приближение: PSNR {first.Metrics.Psnr:F2} дБ при сжатии {first.CompressionRatio:F1}× " +
                              $"→ {last.Metrics.Psnr:F2} дБ при {last.CompressionRatio:F1}×.");
        }

        Console.WriteLine();
    }

    private static string Psnr(QualityMetrics metrics) =>
        double.IsPositiveInfinity(metrics.Psnr) ? "∞" : metrics.Psnr.ToString("F2", CI);

    private static IEnumerable<string> FirstParagraphs(string text, int count) =>
        text.Split('\n').Select(p => p.Trim()).Where(p => p.Length > 0).Take(count);
}
