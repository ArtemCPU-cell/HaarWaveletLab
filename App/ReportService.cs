using HaarWaveletLab.Export;
using HaarWaveletLab.Imaging;
using HaarWaveletLab.Models;
using HaarWaveletLab.Reporting;

namespace HaarWaveletLab.App;

/// <summary>Загрузка изображения и запуск общих операций отчёта/CSV для всех режимов.</summary>
public static class ReportService
{
    public sealed record InputInfo(LabImage Image, string SourceLabel, string? SourcePath);

    public static InputInfo LoadInput(CommandLineOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ImagePath))
        {
            string path = Path.GetFullPath(options.ImagePath);
            using var source = ImageLoader.LoadBitmap(path);
            var image = ImageLoader.CreateLabImage(source, options.Size, Path.GetFileName(path), $"файл: {path}");
            return new InputInfo(image, "загруженный файл", path);
        }

        var kind = options.TestImage ?? TestImageKind.TestCard;
        using var bitmap = TestImages.Create(kind, options.Size);
        var test = ImageLoader.CreateLabImage(bitmap, options.Size, kind.Title(), kind.Description());
        return new InputInfo(test, "встроенное тестовое изображение", null);
    }

    public static ReportModel BuildReport(InputInfo input, int maxOrder) =>
        ReportModel.Build(input.Image, input.SourceLabel, input.SourcePath, maxOrder);

    public static string GenerateReport(ReportModel model, string? outputPath) =>
        new HtmlReportGenerator().Generate(model, ResolveReportPath(outputPath));

    public static IReadOnlyList<string> ExportCsv(ReportModel model, string? directory) =>
        CsvExporter.ExportAll(model, ResolveCsvDirectory(directory));

    public static string DefaultReportPath => Path.Combine(AppContext.BaseDirectory, "report.html");

    public static string DefaultCsvDirectory => Path.Combine(AppContext.BaseDirectory, "csv");

    public static string ResolveReportPath(string? requested)
    {
        if (string.IsNullOrWhiteSpace(requested)) return DefaultReportPath;

        string path = requested;
        bool looksLikeDirectory = path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith('/') ||
                                  Directory.Exists(path);
        if (looksLikeDirectory)
            path = Path.Combine(path, "report.html");

        if (string.IsNullOrEmpty(Path.GetExtension(path))) path += ".html";
        return Path.GetFullPath(path);
    }

    public static string ResolveCsvDirectory(string? requested) =>
        Path.GetFullPath(string.IsNullOrWhiteSpace(requested) ? DefaultCsvDirectory : requested);
}
