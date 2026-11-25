using System.Globalization;
using HaarWaveletLab.Core;
using HaarWaveletLab.Imaging;
using HaarWaveletLab.Models;
using HaarWaveletLab.Reporting;
using HaarWaveletLab.Theory;

namespace HaarWaveletLab.App;

/// <summary>Консольное меню в стиле лабораторной работы №1.</summary>
public static class ConsoleMenu
{
    private static readonly CultureInfo CI = CultureInfo.InvariantCulture;

    public static int Run(CommandLineOptions options)
    {
        string? imagePath = options.ImagePath;
        TestImageKind? testKind = options.TestImage ?? TestImageKind.TestCard;
        int size = options.Size;
        int order = options.Order;

        string name = testKind?.Title() ?? "Изображение";
        string description = testKind?.Description() ?? "";
        LabImage image = LoadImage(imagePath, testKind, size, name, description);
        HaarDecomposition decomposition = HaarDecomposition.Build(image, order);

        while (true)
        {
            TryClear();
            Console.WriteLine("============================================================");
            Console.WriteLine("  ВЕЙВЛЕТ-ПРЕОБРАЗОВАНИЕ ХААРА — учебная программа (лаб. №3)");
            Console.WriteLine("  Дисциплина: Основы цифрового телевидения");
            Console.WriteLine("============================================================");
            Console.WriteLine($"  Изображение: {image.Name} ({image.Size}×{image.Size})");
            Console.WriteLine($"  Порядок разложения: {order}    Матрицы: {decomposition.Order} уровень, {decomposition.Rect(order, SubbandKind.All).Size}×{decomposition.Rect(order, SubbandKind.All).Size}");
            Console.WriteLine("------------------------------------------------------------");
            Console.WriteLine(" 1. Загрузить изображение с диска");
            Console.WriteLine(" 2. Выбрать встроенное тестовое изображение");
            Console.WriteLine(" 3. Задать размер N и порядок разложения");
            Console.WriteLine(" 4. Показать разложение, матрицы и энергии");
            Console.WriteLine(" 5. Убрать матрицу и восстановить изображение");
            Console.WriteLine(" 6. Экспорт данных в CSV (Excel)");
            Console.WriteLine(" 7. Теория и контрольные вопросы");
            Console.WriteLine(" 8. HTML-отчёт с изображениями и графиками");
            Console.WriteLine(" 0. Выход");
            Console.Write("> ");

            string? choice = Console.ReadLine()?.Trim();
            if (choice is null) return 0;

            switch (choice)
            {
                case "1":
                    imagePath = AskImagePath();
                    if (imagePath is null) break;
                    testKind = null;
                    name = Path.GetFileName(imagePath);
                    description = "файл: " + imagePath;
                    image = LoadImage(imagePath, null, size, name, description);
                    order = Math.Min(order, Math.Min(5, SubbandGeometry.MaxOrder(image.Size)));
                    decomposition = HaarDecomposition.Build(image, order);
                    break;
                case "2":
                    testKind = ChooseTestImage();
                    if (testKind is null) break;
                    imagePath = null;
                    name = testKind.Value.Title();
                    description = testKind.Value.Description();
                    image = LoadImage(null, testKind, size, name, description);
                    order = Math.Min(order, Math.Min(5, SubbandGeometry.MaxOrder(image.Size)));
                    decomposition = HaarDecomposition.Build(image, order);
                    break;
                case "3":
                    size = AskChoice("Размер N×N", new[] { "128", "256", "512", "1024" }, 1) switch
                    {
                        1 => 128,
                        2 => 256,
                        3 => 512,
                        _ => 1024
                    };
                    order = AskInt("Порядок разложения (1…5): ", 1, Math.Min(5, SubbandGeometry.MaxOrder(size)));
                    image = LoadImage(imagePath, testKind, size, imagePath is null ? testKind?.Title() ?? name : name, description);
                    decomposition = HaarDecomposition.Build(image, order);
                    break;
                case "4":
                    ShowDecomposition(image, decomposition);
                    Pause();
                    break;
                case "5":
                    RemoveMatricesAndShow(image, decomposition);
                    Pause();
                    break;
                case "6":
                    ExportCsv(image, name, imagePath);
                    Pause();
                    break;
                case "7":
                    TryClear();
                    Console.WriteLine(LabTheory.PlainText());
                    Pause();
                    break;
                case "8":
                    GenerateReport(image, name, imagePath, order);
                    Pause();
                    break;
                case "0":
                    return 0;
                default:
                    Console.WriteLine("Неверный выбор.");
                    Pause();
                    break;
            }
        }
    }

    private static LabImage LoadImage(string? path, TestImageKind? kind, int size, string name, string description)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            using var source = ImageLoader.LoadBitmap(path);
            return ImageLoader.CreateLabImage(source, size, name, description);
        }

        using var bitmap = TestImages.Create(kind ?? TestImageKind.TestCard, size);
        var title = (kind ?? TestImageKind.TestCard).Title();
        return ImageLoader.CreateLabImage(bitmap, size, title, description);
    }

    private static void ShowDecomposition(LabImage image, HaarDecomposition decomposition)
    {
        TryClear();
        Console.WriteLine($"Разложение {image.Size}×{image.Size} до порядка {decomposition.Order}");
        Console.WriteLine();
        Console.WriteLine($"{"Матрица",-10}{"Размер",-9}{"Коэфф.",-9}{"Мин",-10}{"Макс",-10}{"Среднее",-10}{"Энергия",-13}{"Доля",-8}{"Полоса (доли Найквиста)"}");
        Console.WriteLine(new string('-', 105));

        foreach (var band in decomposition.GetBandStats(decomposition.Order))
        {
            var (low, high) = band.FrequencyBand;
            Console.WriteLine($"{band.Kind.Code(),-10}{$"{band.Size}×{band.Size}",-9}{band.CoefficientCount,-9}" +
                              $"{band.Min.ToString("F2", CI),-10}{band.Max.ToString("F2", CI),-10}{band.Mean.ToString("F2", CI),-10}" +
                              $"{band.Energy.ToString("F1", CI),-13}{band.EnergyPercent.ToString("F1", CI) + " %",-8}" +
                              $"{low:F3}…{high:F3}");
        }

        Console.WriteLine();
        foreach (var kind in Subbands.All)
        {
            var values = decomposition.GetBandValues(decomposition.Order, kind);
            Console.WriteLine($"Матрица {kind.Code()}{decomposition.Order} ({values.GetLength(0)}×{values.GetLength(1)}), фрагмент:");
            PrintMatrix(values, 8);
            Console.WriteLine();
        }

        Console.WriteLine(AnalysisText.DecompositionSummary(decomposition));
    }

    private static void RemoveMatricesAndShow(LabImage image, HaarDecomposition decomposition)
    {
        TryClear();
        Console.WriteLine("Удаление матрицы и обратное преобразование");
        Console.WriteLine(" 1. ALL  (приближение)");
        Console.WriteLine(" 2. ALH  (горизонтальные детали)");
        Console.WriteLine(" 3. AHL  (вертикальные детали)");
        Console.WriteLine(" 4. AHH  (диагональные детали)");
        Console.WriteLine(" 5. Все четыре сразу");
        Console.WriteLine(" 0. Назад");
        Console.Write("> ");
        string? choice = Console.ReadLine()?.Trim();

        SubbandKind[]? kinds = choice switch
        {
            "1" => new[] { SubbandKind.All },
            "2" => new[] { SubbandKind.Alh },
            "3" => new[] { SubbandKind.Ahl },
            "4" => new[] { SubbandKind.Ahh },
            "5" => Subbands.All,
            _ => null
        };

        if (kinds is null) return;

        var experiment = LabAnalyzer.RunRemoval(decomposition, kinds);
        Console.WriteLine();
        Console.WriteLine(AnalysisText.RemovalParagraph(experiment));
        Console.WriteLine($"Проверка: MSE = {experiment.Metrics.Mse:F3}, MAE = {experiment.Metrics.Mae:F2}, " +
                          $"макс. ошибка = {experiment.Metrics.MaxError:F1}, корреляция = {experiment.Metrics.Correlation:F4}.");

        string fileName = $"preview_{decomposition.Order}_{experiment.Label.Replace(" + ", "_")}.png";
        string path = Path.Combine(AppContext.BaseDirectory, fileName);
        using (var bitmap = ImageRenderer.RenderComparison(image.Pixels, experiment.Reconstructed))
            ImageRenderer.SavePng(bitmap, path);
        Console.WriteLine($"Сравнение сохранено: {path}");
    }

    private static void ExportCsv(LabImage image, string name, string? imagePath)
    {
        Console.WriteLine("Формирование CSV (разложение 1…5, все эксперименты)…");
        var input = new ReportService.InputInfo(image, imagePath is null ? "встроенное изображение" : "файл", imagePath);
        var model = ReportService.BuildReport(input, 5);
        var files = ReportService.ExportCsv(model, null);
        Console.WriteLine($"Сохранено {files.Count} файлов в {Path.GetDirectoryName(files[0])}:");
        foreach (string file in files)
            Console.WriteLine("  " + Path.GetFileName(file));
    }

    private static void GenerateReport(LabImage image, string name, string? imagePath, int order)
    {
        Console.WriteLine("Формирование HTML-отчёта (изображения, таблицы, графики)…");
        var input = new ReportService.InputInfo(image, imagePath is null ? "встроенное изображение" : "файл", imagePath);
        var model = ReportService.BuildReport(input, order);
        string path = ReportService.GenerateReport(model, null);
        Console.WriteLine($"Отчёт сохранён: {path}");
        Console.WriteLine($"Размер файла: {new FileInfo(path).Length / 1024.0 / 1024.0:F2} МБ");
    }

    private static string? AskImagePath()
    {
        Console.Write("Путь к изображению (PNG, JPG, BMP, GIF): ");
        string? raw = Console.ReadLine()?.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(raw)) return null;
        try
        {
            _ = ImageLoader.LoadBitmap(raw);
            return Path.GetFullPath(raw);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Не удалось открыть файл: " + ex.Message);
            return null;
        }
    }

    private static TestImageKind? ChooseTestImage()
    {
        Console.WriteLine();
        var kinds = TestImagesInfo.All;
        for (int i = 0; i < kinds.Length; i++)
            Console.WriteLine($" {i + 1}. {kinds[i].Title()} — {kinds[i].Description()}");
        int choice = AskInt("Номер изображения (0 — отмена): ", 0, kinds.Length);
        return choice == 0 ? null : kinds[choice - 1];
    }

    private static int AskChoice(string title, string[] values, int defaultValue)
    {
        Console.WriteLine();
        for (int i = 0; i < values.Length; i++)
            Console.WriteLine($" {i + 1}. {title}: {values[i]}");
        return AskInt($"Выбор (по умолчанию {defaultValue}): ", 1, values.Length);
    }

    private static int AskInt(string prompt, int min, int max)
    {
        while (true)
        {
            Console.Write(prompt);
            string? raw = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(raw)) return min;
            if (int.TryParse(raw.Trim(), out int value) && value >= min && value <= max)
                return value;
            Console.WriteLine($"Введите число от {min} до {max}.");
        }
    }

    private static void PrintMatrix(double[,] values, int max)
    {
        int rows = Math.Min(values.GetLength(0), max);
        int columns = Math.Min(values.GetLength(1), max);
        Console.WriteLine("      " + string.Join(" ", Enumerable.Range(0, columns).Select(x => $"{x,8}")));
        for (int y = 0; y < rows; y++)
        {
            Console.Write($"{y,4}: ");
            for (int x = 0; x < columns; x++)
                Console.Write($"{values[y, x],8:F1} ");
            Console.WriteLine();
        }

        if (values.GetLength(0) > rows || values.GetLength(1) > columns)
            Console.WriteLine($"  … показан фрагмент {rows}×{columns} из {values.GetLength(0)}×{values.GetLength(1)}");
    }

    private static void Pause()
    {
        if (Console.IsInputRedirected) return;
        Console.WriteLine("\nНажмите Enter для продолжения...");
        Console.ReadLine();
    }

    private static void TryClear()
    {
        try { Console.Clear(); } catch { }
    }
}
