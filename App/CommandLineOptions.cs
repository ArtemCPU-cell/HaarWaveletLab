using HaarWaveletLab.Models;

namespace HaarWaveletLab.App;

public enum RunMode
{
    Gui,
    Console,
    Report,
    SelfTest,
    Help
}

/// <summary>Разбор параметров командной строки.</summary>
public sealed class CommandLineOptions
{
    public RunMode Mode { get; private set; } = RunMode.Gui;
    public string? ImagePath { get; private set; }
    public TestImageKind? TestImage { get; private set; }
    public int Size { get; private set; } = 256;
    public int Order { get; private set; } = 5;
    public string? OutputPath { get; private set; }
    public string? CsvDirectory { get; private set; }
    public bool SkipCsv { get; private set; }
    public IReadOnlyList<SubbandKind> Removed { get; private set; } = Array.Empty<SubbandKind>();
    public string? StartupTab { get; private set; }

    public static CommandLineOptions Parse(string[] args)
    {
        var options = new CommandLineOptions();

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i].Trim();
            string key = arg.ToLowerInvariant();

            switch (key)
            {
                case "--report":
                case "-r":
                case "report":
                case "--demo":
                case "demo":
                    options.Mode = RunMode.Report;
                    break;
                case "--console":
                case "-c":
                case "console":
                    if (options.Mode == RunMode.Gui) options.Mode = RunMode.Console;
                    break;
                case "--selftest":
                case "selftest":
                    options.Mode = RunMode.SelfTest;
                    break;
                case "--help":
                case "-h":
                case "/?":
                case "help":
                    options.Mode = RunMode.Help;
                    break;
                case "--image":
                case "-i":
                    options.ImagePath = Next(args, ref i, "--image");
                    break;
                case "--test":
                    string test = Next(args, ref i, "--test");
                    if (!TestImagesInfo.TryParseCommandId(test, out var parsedKind))
                        throw new FormatException($"Неизвестное тестовое изображение: {test}");
                    options.TestImage = parsedKind;
                    break;
                case "--size":
                    string size = Next(args, ref i, "--size");
                    if (!int.TryParse(size, out int parsedSize))
                        throw new FormatException($"Размер должен быть числом: {size}");
                    options.Size = Imaging.ImageLoader.NormalizeSize(parsedSize);
                    break;
                case "--order":
                    string order = Next(args, ref i, "--order");
                    if (!int.TryParse(order, out int parsedOrder))
                        throw new FormatException($"Порядок должен быть числом: {order}");
                    options.Order = Math.Clamp(parsedOrder, 1, 5);
                    break;
                case "--out":
                case "-o":
                    options.OutputPath = Next(args, ref i, "--out");
                    break;
                case "--csv":
                    options.CsvDirectory = Next(args, ref i, "--csv");
                    break;
                case "--no-csv":
                    options.SkipCsv = true;
                    break;
                case "--remove":
                    string removed = Next(args, ref i, "--remove");
                    var kinds = new List<SubbandKind>();
                    foreach (string part in removed.Split(new[] { ',', ';', '+' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        string code = part.Trim().ToUpperInvariant();
                        if (!Subbands.All.Any(k => k.Code() == code))
                            throw new FormatException($"Неизвестная матрица: {part}");
                        kinds.Add(Subbands.All.First(k => k.Code() == code));
                    }

                    options.Removed = kinds.Distinct().ToArray();
                    break;
                case "--tab":
                    string tab = Next(args, ref i, "--tab").ToLowerInvariant();
                    string[] allowed = { "pyramid", "matrices", "difference", "analysis", "tables", "theory" };
                    if (!allowed.Contains(tab))
                        throw new FormatException($"Неизвестная вкладка: {tab}");
                    options.StartupTab = tab;
                    break;
                default:
                    throw new FormatException($"Неизвестный параметр: {arg}");
            }
        }

        return options;
    }

    private static string Next(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            throw new FormatException($"После {option} требуется значение.");
        return args[++index];
    }

    public static string HelpText => """
        ВЕЙВЛЕТ-ПРЕОБРАЗОВАНИЕ ХААРА — лабораторная работа №3
        =======================================================

        Режимы:
          HaarWaveletLab.exe                 окно программы (Windows Forms)
          HaarWaveletLab.exe --console       консольное меню
          HaarWaveletLab.exe --report        HTML-отчёт без интерфейса + CSV
          HaarWaveletLab.exe --selftest      самопроверка математики и отчёта
          HaarWaveletLab.exe --help          эта справка

        Параметры:
          --image <файл>     исходное изображение (PNG, JPG, BMP, GIF, TIFF)
          --test <id>        встроенное изображение: testcard, bars, geometry,
                             portrait, texture (по умолчанию testcard)
          --size <N>         размер N×N, степень двойки: 128, 256, 512, 1024
          --order <1..5>     максимальный порядок разложения (по умолчанию 5)
          --out <файл.html>  путь HTML-отчёта (по умолчанию рядом с программой)
          --csv <папка>      папка CSV-файлов (по умолчанию csv рядом с программой)
          --no-csv           не создавать CSV в режиме --report
          --remove ALL,ALH   матрицы, удалённые при открытии окна
          --tab <вкладка>    стартовая вкладка: pyramid, matrices, difference,
                             analysis, tables, theory

        Примеры:
          HaarWaveletLab.exe --report
          HaarWaveletLab.exe --report --test portrait --size 512 --order 5
          HaarWaveletLab.exe --report --image C:\foto.jpg --out C:\reports\lab3.html
          HaarWaveletLab.exe --image C:\foto.jpg --order 3 --remove AHH

        Горячие клавиши окна:
          Ctrl+O — открыть изображение, F5 — пересчитать,
          Ctrl+1…4 — убрать ALL / ALH / AHL / AHH, Ctrl+0 — сбросить удаление,
          F1 — теория, Ctrl+R — HTML-отчёт.
        """;
}
