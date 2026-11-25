using System.Globalization;
using System.Text;
using HaarWaveletLab.Core;
using HaarWaveletLab.Export;
using HaarWaveletLab.Imaging;
using HaarWaveletLab.Models;
using HaarWaveletLab.Reporting;

namespace HaarWaveletLab.App;

/// <summary>Самопроверка --selftest: математика, метрики, изображения, отчёт и CSV.</summary>
public static class SelfTest
{
    private static readonly CultureInfo CI = CultureInfo.InvariantCulture;
    private static readonly List<(string Name, bool Ok, string Info)> Results = new();

    public static int Run()
    {
        Results.Clear();
        Console.WriteLine("================================================================");
        Console.WriteLine("  САМОПРОВЕРКА ПРОГРАММЫ (--selftest)");
        Console.WriteLine("================================================================");

        Test1DForward();
        Test1DInverse();
        Test2DRoundTrip();
        TestConstantApproximation();
        TestParseval();
        TestBandPartition();
        TestLb3();
        TestMetrics();
        TestBuiltInImages();
        TestNormalizeSize();
        TestReport();
        TestCsv();

        int passed = Results.Count(r => r.Ok);
        int failed = Results.Count - passed;

        Console.WriteLine();
        Console.WriteLine($"Итог: {passed} успешно, {failed} ошибок.");
        foreach (var failure in Results.Where(r => !r.Ok))
            Console.WriteLine($"  FAIL: {failure.Name} — {failure.Info}");

        string logPath = Path.Combine(AppContext.BaseDirectory, "selftest.log");
        var log = new StringBuilder();
        foreach (var (name, ok, info) in Results)
            log.AppendLine($"[{(ok ? "PASS" : "FAIL")}] {name}{(info.Length > 0 ? " — " + info : "")}");
        log.AppendLine();
        log.AppendLine($"Итог: {passed} успешно, {failed} ошибок.");
        try { File.WriteAllText(logPath, log.ToString(), new UTF8Encoding(true)); } catch { }
        Console.WriteLine($"Журнал проверки: {logPath}");

        return failed == 0 ? 0 : 1;
    }

    private static void Test1DForward()
    {
        var data = new double[] { 0, 3, 1, 2, 7, 9, 4, 5 };
        HaarTransform.Forward1D(data);

        double[] expected =
        {
            31.0 / Math.Sqrt(8),
            -19.0 / Math.Sqrt(8),
            0.0,
            3.5,
            -3.0 / Math.Sqrt(2),
            -1.0 / Math.Sqrt(2),
            -2.0 / Math.Sqrt(2),
            -1.0 / Math.Sqrt(2)
        };

        double maxDiff = data.Zip(expected, (a, b) => Math.Abs(a - b)).Max();
        Record("1D прямое преобразование (окно Lb3_Haar.xlsx)", maxDiff < 1e-9, $"max|Δ| = {maxDiff:E2}");
    }

    private static void Test1DInverse()
    {
        var random = new Random(12345);
        var data = Enumerable.Range(0, 64).Select(_ => random.NextDouble() * 255).ToArray();
        var original = (double[])data.Clone();
        HaarTransform.Forward1D(data);
        HaarTransform.Inverse1D(data);
        double maxDiff = data.Zip(original, (a, b) => Math.Abs(a - b)).Max();
        Record("1D обратное преобразование (64 отсчёта)", maxDiff < 1e-9, $"max|Δ| = {maxDiff:E2}");
    }

    private static void Test2DRoundTrip()
    {
        var image = CreateRandomImage(64);
        var decomposition = HaarDecomposition.Build(image, 5);
        var reconstructed = decomposition.ReconstructAll();

        double maxDiff = 0;
        for (int y = 0; y < image.Size; y++)
            for (int x = 0; x < image.Size; x++)
                maxDiff = Math.Max(maxDiff, Math.Abs(image.Pixels[y, x] - reconstructed[y, x]));

        Record("2D прямое/обратное преобразование (64×64, порядок 5)", maxDiff < 1e-8, $"max|Δ| = {maxDiff:E2}");
    }

    private static void TestConstantApproximation()
    {
        int n = 16;
        var pixels = new double[n, n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                pixels[y, x] = 100.0;

        var image = new LabImage("constant", "константа", pixels);
        var decomposition = HaarDecomposition.Build(image, 3);
        var rect = decomposition.Rect(3, SubbandKind.All);
        var filtered = new double[n, n];
        for (int y = 0; y < rect.Size; y++)
            for (int x = 0; x < rect.Size; x++)
                filtered[y, x] = decomposition.Coefficients[y, x];

        var reconstructed = HaarTransform.Reconstruct(filtered, 3);
        double maxDiff = 0;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                maxDiff = Math.Max(maxDiff, Math.Abs(reconstructed[y, x] - 100.0));

        Record("Приближение константного изображения восстанавливает константу", maxDiff < 1e-9,
            $"max|Δ| = {maxDiff:E2}");
    }

    private static void TestParseval()    {
        var image = CreateRandomImage(64);
        var decomposition = HaarDecomposition.Build(image, 5);
        double coefficientEnergy = 0;
        for (int y = 0; y < image.Size; y++)
            for (int x = 0; x < image.Size; x++)
                coefficientEnergy += decomposition.Coefficients[y, x] * decomposition.Coefficients[y, x];

        double relative = Math.Abs(coefficientEnergy - image.Energy) / Math.Max(1e-9, image.Energy);
        Record("Сохранение энергии (равенство Парсеваля)", relative < 1e-10, $"отн. погрешность = {relative:E2}");
    }

    private static void TestBandPartition()
    {
        var image = CreateRandomImage(64);
        var decomposition = HaarDecomposition.Build(image, 5);
        var bands = decomposition.GetBandStats(1);
        double energy = bands.Sum(b => b.Energy);
        int count = bands.Sum(b => b.CoefficientCount);

        bool ok = Math.Abs(energy - image.Energy) / image.Energy < 1e-10 && count == image.Size * image.Size;
        Record("Матрицы первого уровня покрывают все N² коэффициентов", ok, $"коэффициентов {count}, энергия {energy:F1}");
    }

    private static void TestLb3()
    {
        var check = Lb3Check.Create();
        double diffPower = check.Cases.Max(c => Math.Abs(c.OutputPower - c.ReferenceOutputPower));
        double diffCoefficients = 0;
        for (int y = 0; y < 2; y++)
            for (int x = 0; x < 8; x++)
                diffCoefficients = Math.Max(diffCoefficients, Math.Abs(check.Coefficients[y, x] - check.ReferenceCoefficients[y, x]));

        bool ok = diffPower < 0.01 && diffCoefficients < 1e-4;
        Record("Проверка по Lb3_Haar.xlsx", ok, $"max|ΔP| = {diffPower:F5}, max|Δc| = {diffCoefficients:E2}");
    }

    private static void TestMetrics()
    {
        var a = new double[8, 8];
        var b = new double[8, 8];
        for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
            {
                a[y, x] = 100;
                b[y, x] = 110;
            }

        var identical = Metrics.Compare(a, a);
        var shifted = Metrics.Compare(a, b);
        bool ok = identical.IsPerfect && Math.Abs(identical.Ssim - 1) < 1e-9 &&
                  Math.Abs(shifted.Mse - 100) < 1e-9 && Math.Abs(shifted.Psnr - 28.1308) < 0.001;
        Record("Метрики MSE/PSNR/SSIM", ok,
            $"MSE = {shifted.Mse:F2}, PSNR = {shifted.Psnr:F3}, SSIM(identical) = {identical.Ssim:F3}");
    }

    private static void TestBuiltInImages()
    {
        bool ok = true;
        var details = new List<string>();
        foreach (var kind in TestImagesInfo.All)
        {
            using var bitmap = TestImages.Create(kind, 128);
            var pixels = ImageLoader.ToLuminance(bitmap);
            double min = double.MaxValue, max = double.MinValue;
            for (int y = 0; y < pixels.GetLength(0); y++)
                for (int x = 0; x < pixels.GetLength(1); x++)
                {
                    min = Math.Min(min, pixels[y, x]);
                    max = Math.Max(max, pixels[y, x]);
                }

            bool useful = max - min > 30;
            ok &= useful;
            details.Add($"{kind.Title()}: {min:F0}…{max:F0}");
        }

        Record("Встроенные тестовые изображения", ok, string.Join("; ", details));
    }

    private static void TestNormalizeSize()
    {
        bool ok = ImageLoader.NormalizeSize(300) == 256 &&
                  ImageLoader.NormalizeSize(1000) == 512 &&
                  ImageLoader.NormalizeSize(128) == 128;
        Record("Приведение размера к степени двойки", ok,
            $"300→{ImageLoader.NormalizeSize(300)}, 1000→{ImageLoader.NormalizeSize(1000)}");
    }

    private static void TestReport()
    {
        string folder = Path.Combine(Path.GetTempPath(), "HaarWaveletLab_selftest");
        try
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
            Directory.CreateDirectory(folder);

            using var bitmap = TestImages.Create(TestImageKind.TestCard, 128);
            var image = ImageLoader.CreateLabImage(bitmap, 128, "Самопроверка", "тестовая таблица 128×128");
            var model = ReportModel.Build(image, "selftest", null, 3);
            string path = new HtmlReportGenerator().Generate(model, Path.Combine(folder, "report.html"));
            string html = File.ReadAllText(path);
            string assets = Path.Combine(folder, "report_assets");
            int png = Directory.Exists(assets) ? Directory.GetFiles(assets, "*.png").Length : 0;

            bool ok = File.Exists(path) && new FileInfo(path).Length > 100_000 &&
                      html.Contains("Порядок 3", StringComparison.Ordinal) &&
                      html.Contains("Lb3_Haar.xlsx", StringComparison.Ordinal) &&
                      html.Contains("Контрольные вопросы", StringComparison.Ordinal) &&
                      html.Contains("<svg", StringComparison.Ordinal) &&
                      png >= 20;

            Record("Генерация HTML-отчёта", ok,
                $"{new FileInfo(path).Length / 1024} КБ, PNG: {png}, SVG: {CountOccurrences(html, "<svg")}");
        }
        catch (Exception ex)
        {
            Record("Генерация HTML-отчёта", false, ex.Message);
        }
        finally
        {
            try { if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true); } catch { }
        }
    }

    private static void TestCsv()
    {
        string folder = Path.Combine(Path.GetTempPath(), "HaarWaveletLab_selftest_csv");
        try
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
            using var bitmap = TestImages.Create(TestImageKind.Geometry, 64);
            var image = ImageLoader.CreateLabImage(bitmap, 64, "CSV", "тест");
            var model = ReportModel.Build(image, "selftest", null, 2);
            var files = CsvExporter.ExportAll(model, folder);
            string experiments = File.ReadAllText(Path.Combine(folder, "02_experiments.csv"));
            bool ok = files.Count == 7 && files.All(File.Exists) &&
                      experiments.Contains("PSNR_дБ", StringComparison.Ordinal) &&
                      experiments.Split('\n').Length >= 10;
            Record("Экспорт CSV", ok, $"{files.Count} файлов, строк в 02_experiments: {experiments.Split('\n').Length}");
        }
        catch (Exception ex)
        {
            Record("Экспорт CSV", false, ex.Message);
        }
        finally
        {
            try { if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true); } catch { }
        }
    }

    private static LabImage CreateRandomImage(int size)
    {
        var random = new Random(42);
        var pixels = new double[size, size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                pixels[y, x] = random.Next(0, 256);
        return new LabImage("random", "случайное изображение", pixels);
    }

    private static int CountOccurrences(string text, string value)
    {
        int count = 0, index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static void Record(string name, bool ok, string info)
    {
        Results.Add((name, ok, info));
        Console.WriteLine($"[{(ok ? "PASS" : "FAIL")}] {name}" + (info.Length > 0 ? $" — {info}" : ""));
    }
}
