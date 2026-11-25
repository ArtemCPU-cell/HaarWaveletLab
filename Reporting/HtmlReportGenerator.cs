using System.Globalization;
using System.Net;
using System.Text;
using HaarWaveletLab.Imaging;
using HaarWaveletLab.Models;

namespace HaarWaveletLab.Reporting;

/// <summary>Сборка автономного HTML-отчёта со встроенными изображениями и SVG-графиками.</summary>
public sealed class HtmlReportGenerator
{
    private static readonly CultureInfo CI = CultureInfo.InvariantCulture;
    private string _assetsDir = "";
    private string _assetsName = "";

    public string Generate(ReportModel model, string outputPath)
    {
        if (!outputPath.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
            outputPath += ".html";

        string fullPath = Path.GetFullPath(outputPath);
        string directory = Path.GetDirectoryName(fullPath) ?? AppContext.BaseDirectory;
        Directory.CreateDirectory(directory);

        _assetsName = Path.GetFileNameWithoutExtension(fullPath) + "_assets";
        _assetsDir = Path.Combine(directory, _assetsName);
        Directory.CreateDirectory(_assetsDir);

        foreach (string old in Directory.GetFiles(_assetsDir, "*.png"))
        {
            try { File.Delete(old); } catch { /* файл занят — не критично */ }
        }

        string html = Build(model);
        File.WriteAllText(fullPath, html, new UTF8Encoding(false));
        return fullPath;
    }

    private string Build(ReportModel model)
    {
        var sb = new StringBuilder(4_000_000);
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"ru\">");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"UTF-8\">");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine($"<title>Лабораторная работа №3 — вейвлет Хаара — {H(model.Image.Name)}</title>");
        sb.AppendLine("<style>");
        sb.AppendLine(Css);
        sb.AppendLine("</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");

        AppendHero(sb, model);
        sb.AppendLine("<main>");
        AppendToc(sb, model);
        AppendGoal(sb, model);
        AppendTheory(sb, model);
        AppendSource(sb, model);
        AppendPyramid(sb, model);
        AppendOrders(sb, model);
        AppendSummary(sb, model);
        AppendCompression(sb, model);
        AppendLb3(sb, model);
        AppendConclusion(sb, model);
        AppendQuestions(sb);
        AppendAppendix(sb, model);
        sb.AppendLine("</main>");
        AppendFooter(sb, model);

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        return sb.ToString();
    }

    private void AppendHero(StringBuilder sb, ReportModel model)
    {
        sb.AppendLine("<header class=\"hero\"><div class=\"wrap\">");
        sb.AppendLine("<div class=\"eyebrow\">Лабораторная работа №3 · Основы цифрового телевидения</div>");
        sb.AppendLine("<h1>Вейвлет-преобразование изображений</h1>");
        sb.AppendLine("<div class=\"sub\">Прямое и обратное преобразование Хаара · матрицы ALL, ALH, AHL, AHH · порядки 1–" + model.MaxOrder + "</div>");
        sb.AppendLine("<div class=\"meta\">");
        Meta(sb, "Изображение", model.Image.Name);
        Meta(sb, "Размер", $"{model.Image.Size} × {model.Image.Size}");
        Meta(sb, "Порядков разложения", model.MaxOrder.ToString(CI));
        Meta(sb, "Коэффициентов", N(model.Image.Size * model.Image.Size));
        Meta(sb, "Дата", model.CreatedAt.ToString("dd.MM.yyyy HH:mm", CI));
        sb.AppendLine("</div></div></header>");
    }

    private static void Meta(StringBuilder sb, string label, string value)
    {
        sb.AppendLine($"<div>{H(label)}<b>{H(value)}</b></div>");
    }

    private static void AppendToc(StringBuilder sb, ReportModel model)
    {
        sb.AppendLine("<nav class=\"toc\"><b>Содержание:</b> ");
        var links = new List<(string Id, string Title)>
        {
            ("goal", "Цель"),
            ("theory", "Теория"),
            ("source", "Исходное изображение"),
            ("pyramid", "Пирамида"),
            ("summary", "Сводная таблица"),
            ("compression", "Сжатие"),
            ("lb3", "Проверка по Lb3_Haar.xlsx"),
            ("conclusion", "Выводы"),
            ("questions", "Контрольные вопросы"),
            ("appendix", "Приложение")
        };

        for (int order = 1; order <= model.MaxOrder; order++)
            links.Insert(4 + order - 1, ($"order-{order}", $"Порядок {order}"));

        foreach (var (id, title) in links)
            sb.AppendLine($"<a href=\"#{id}\">{H(title)}</a>");
        sb.AppendLine("</nav>");
    }

    private static void AppendGoal(StringBuilder sb, ReportModel model)
    {
        sb.AppendLine("<section class=\"card\" id=\"goal\"><h2>1. Цель и параметры эксперимента</h2>");
        sb.AppendLine("<p><b>Цель работы:</b> изучить прямое и обратное вейвлет-преобразование изображений на основе вейвлета Хаара, " +
                      "исследовать информационное содержание матриц ALL, ALH, AHL, AHH и оценить компромисс между сжатием и качеством.</p>");

        sb.AppendLine("<div class=\"two\"><div>");
        AppendTable(sb, new[] { "Параметр", "Значение" }, new (string[], string)[]
        {
            (new[] { "Изображение", H(model.Image.Name) }, ""),
            (new[] { "Источник", H(model.SourceLabel) }, ""),
            (new[] { "Размер матрицы", $"{model.Image.Size} × {model.Image.Size} (степень двойки)" }, ""),
            (new[] { "Обработка", "яркость Y = 0.299R + 0.587G + 0.114B" }, ""),
            (new[] { "Порядки разложения", $"1 … {model.MaxOrder}" }, ""),
            (new[] { "Всего коэффициентов", N(model.Image.Size * model.Image.Size) }, ""),
            (new[] { "Метрики качества", "MSE, PSNR, SSIM, MAE, корреляция" }, "")
        });
        sb.AppendLine("</div><div class=\"note\">");
        sb.AppendLine("<b>Как читать отчёт.</b> Для каждого порядка показаны четыре матрицы коэффициентов, " +
                      "затем — результат обратного преобразования после удаления каждой из них. " +
                      "Карта разности усилена в 4 раза, чтобы слабые искажения были заметны. " +
                      "Все изображения встроены в файл, поэтому отчёт можно отправлять одним HTML-файлом.");
        sb.AppendLine("</div></div>");

        sb.AppendLine("<h3>Что содержит отчёт</h3><ul>");
        sb.AppendLine("<li>краткую теорию, формулы Хаара и структурные схемы;</li>");
        sb.AppendLine("<li>разложение изображения на четырёх уровнях с теплокартами и числовыми матрицами;</li>");
        sb.AppendLine("<li>результаты удаления каждой матрицы: изображение, карта разности, PSNR, SSIM, MAE и текстовый вывод;</li>");
        sb.AppendLine("<li>сводную таблицу по всем 4 × " + model.MaxOrder + " экспериментам и графики;</li>");
        sb.AppendLine("<li>исследование сжатия отбрасыванием малых коэффициентов и кривую «сжатие — качество»;</li>");
        sb.AppendLine("<li>проверку программы по данным файла Lb3_Haar.xlsx;</li>");
        sb.AppendLine("<li>ответы на контрольные вопросы и структуру программы.</li>");
        sb.AppendLine("</ul></section>");
    }

    private static void AppendTheory(StringBuilder sb, ReportModel model)
    {
        sb.AppendLine("<section class=\"card\" id=\"theory\"><h2>2. Краткие теоретические сведения</h2>");
        foreach (var section in Theory.LabTheory.Sections)
        {
            if (section.Title.StartsWith("8.")) continue;
            sb.AppendLine($"<h3>{H(section.Title)}</h3>");
            foreach (var paragraph in section.Paragraphs)
            {
                if (paragraph.StartsWith("    "))
                    sb.AppendLine($"<div class=\"formula\">{H(paragraph.Trim())}</div>");
                else
                    sb.AppendLine($"<p>{H(paragraph)}</p>");
            }
        }

        sb.AppendLine("<h3>Базисные функции Хаара</h3>");
        sb.AppendLine(Chart(SvgCharts.HaarBasis8()));

        sb.AppendLine("<h3>Структурная схема</h3>");
        sb.AppendLine("<p>Прямое преобразование выполняется в два прохода: WTx по строкам, затем WTy по столбцам. " +
                      "Обратное преобразование выполняется в обратном порядке. Удаляемые матрицы заменяются нулями.</p>");
        sb.AppendLine(Chart(SvgCharts.StructuralScheme()));

        sb.AppendLine("<h3>Раскладка матриц</h3>");
        sb.AppendLine(Chart(SvgCharts.QuadrantScheme()));

        sb.AppendLine("<h3>Частотные полосы</h3>");
        sb.AppendLine($"<p>При разложении до порядка {model.MaxOrder} приближение занимает нижнюю часть спектра, " +
                      "а каждая матрица деталей — свою полосу. Каждый следующий порядок сужает полосу приближения вдвое.</p>");
        sb.AppendLine(Chart(SvgCharts.FrequencyLadder(model.MaxOrder)));
        sb.AppendLine("</section>");
    }

    private void AppendSource(StringBuilder sb, ReportModel model)
    {
        var (originalUrl, originalFile) = Embed(ImageRenderer.ToGrayscaleBitmap(model.Image.Pixels), "original.png");
        sb.AppendLine("<section class=\"card\" id=\"source\"><h2>3. Исходное изображение</h2>");
        sb.AppendLine("<div class=\"two\">");
        sb.AppendLine(Figure(originalUrl, originalFile, "Исходное изображение", "Исходное изображение после приведения к квадрату и перевода в яркость Y", ""));
        sb.AppendLine("<div>");
        AppendTable(sb, new[] { "Характеристика", "Значение" }, new (string[], string)[]
        {
            (new[] { "Размер", $"{model.Image.Size} × {model.Image.Size}" }, ""),
            (new[] { "Минимальная яркость", F1(model.Image.Min) }, ""),
            (new[] { "Максимальная яркость", F1(model.Image.Max) }, ""),
            (new[] { "Средняя яркость", F2(model.Image.Mean) }, ""),
            (new[] { "Энергия изображения", F1(model.Image.Energy) }, ""),
            (new[] { "Доля нулевых коэффициентов", ZeroShare(model) }, "")
        });
        sb.AppendLine("<p class=\"muted\">Изображение переводится в полутоновое по формуле яркости BT.601. " +
                      "Цветовые каналы обрабатывались бы тем же преобразованием независимо.</p>");
        sb.AppendLine("</div></div>");
        sb.AppendLine("<h3>Гистограмма яркости</h3>");
        sb.AppendLine(Chart(SvgCharts.Histogram(model.Image.Pixels)));
        sb.AppendLine("</section>");
    }

    private string ZeroShare(ReportModel model)
    {
        var decomposition = model.Orders[^1].Decomposition;
        int total = decomposition.Size * decomposition.Size;
        int zero = 0;
        for (int y = 0; y < decomposition.Size; y++)
            for (int x = 0; x < decomposition.Size; x++)
                if (Math.Abs(decomposition.Coefficients[y, x]) < 1e-9) zero++;
        return $"{100.0 * zero / total:F1} %";
    }

    private void AppendPyramid(StringBuilder sb, ReportModel model)
    {
        var decomposition = model.Orders[^1].Decomposition;
        var (url, file) = Embed(ImageRenderer.RenderPyramid(decomposition), "pyramid_full.png");

        sb.AppendLine("<section class=\"card\" id=\"pyramid\"><h2>4. Пирамида коэффициентов</h2>");
        sb.AppendLine($"<p>Ниже показана полная матрица коэффициентов {model.Image.Size}×{model.Image.Size} после разложения до порядка {model.MaxOrder}. " +
                      "Серым показана матрица приближения, цветом — детали: синий цвет соответствует отрицательным коэффициентам, " +
                      "жёлтый — положительным, яркость растёт логарифмически.</p>");
        sb.AppendLine(Figure(url, file, "Пирамида коэффициентов", "Полная пирамида: ALL — серые градации, детали — цветовая нормировка по уровню", "wide"));
        sb.AppendLine("<p class=\"muted\">Область ALL разлагается повторно, поэтому на картинке видна «лестница» уровней: " +
                      "каждый следующий уровень занимает четверть предыдущего квадрата.</p>");

        sb.AppendLine("<h3>Объём данных по порядкам</h3>");
        var rows = new List<(string[], string)>();
        int totalCoefficients = model.Image.Size * model.Image.Size;
        foreach (var order in model.Orders)
        {
            int band = order.Bands[0].Size;
            int levelCoefficients = band * band * 4;
            double share = 100.0 * levelCoefficients / totalCoefficients;
            rows.Add((new[]
            {
                order.Order.ToString(CI),
                $"{band} × {band}",
                N(band * band),
                N(levelCoefficients),
                N(totalCoefficients),
                $"{share:F1} %"
            }, ""));
        }

        AppendTable(sb, new[] { "Порядок", "Размер одной матрицы", "Коэффициентов в матрице", "В четырёх матрицах", "Пикселей в изображении", "Доля от N²" }, rows);
        sb.AppendLine("<p class=\"muted\">Четыре матрицы очередного уровня занимают лишь часть полного массива: с каждым порядком их суммарная доля уменьшается вчетверо. " +
                      "Остальные коэффициенты — это детали предыдущих уровней. Полная пирамида после любого числа уровней по-прежнему содержит N² коэффициентов: " +
                      "само преобразование Хаара обратимо, а сжатие достигается квантованием и отбрасыванием малых коэффициентов.</p>");
        sb.AppendLine("</section>");
    }

    private void AppendOrders(StringBuilder sb, ReportModel model)
    {
        sb.AppendLine("<section class=\"card\" id=\"orders\"><h2>5. Разложение и удаление матриц по порядкам</h2>");
        sb.AppendLine("<p>Для каждого порядка выполнено прямое преобразование, затем поочерёдно обнулялась каждая матрица и выполнялось обратное преобразование. " +
                      "В таблицах и на графиках используются обозначения ALL, ALH, AHL, AHH.</p>");

        for (int index = 0; index < model.Orders.Count; index++)
        {
            var order = model.Orders[index];
            var previous = index > 0 ? model.Orders[index - 1] : null;
            var decomposition = order.Decomposition;

            sb.AppendLine($"<article class=\"order\" id=\"order-{order.Order}\">");
            sb.AppendLine($"<h3>Порядок {order.Order}</h3>");
            sb.AppendLine($"<p class=\"muted\">Четыре матрицы {order.Bands[0].Size}×{order.Bands[0].Size}; " +
                          $"энергия ALL — {F1(order.Bands[0].EnergyPercent)} %.</p>");

            AppendParagraphs(sb, AnalysisText.DecompositionSummary(order));

            var (pyramidUrl, pyramidFile) = Embed(ImageRenderer.RenderPyramid(decomposition), $"o{order.Order}_pyramid.png");
            sb.AppendLine(Figure(pyramidUrl, pyramidFile, $"Пирамида порядка {order.Order}",
                $"Разложение до порядка {order.Order}: зелёные и синие области — приближение и детали предыдущих уровней", ""));

            sb.AppendLine("<div class=\"grid2\">");
            foreach (var kind in Subbands.All)
            {
                var band = order.Bands.First(b => b.Kind == kind);
                var (bandUrl, bandFile) = Embed(ImageRenderer.RenderBand(decomposition, order.Order, kind),
                    $"o{order.Order}_{kind.Code()}_band.png");
                sb.AppendLine(Figure(bandUrl, bandFile, $"{kind.Code()}{order.Order}",
                    $"<b>{kind.Code()}{order.Order}</b> · {band.Size}×{band.Size} · {N(band.CoefficientCount)} коэфф.<br>" +
                    $"{H(kind.Axes())}; энергия {F1(band.EnergyPercent)} %; диапазон {F1(band.Min)}…{F1(band.Max)}",
                    "heat"));
            }
            sb.AppendLine("</div>");

            sb.AppendLine("<details><summary>Числовые значения матриц (фрагмент 8×8)</summary><div class=\"grid2\">");
            foreach (var kind in Subbands.All)
            {
                var values = decomposition.GetBandValues(order.Order, kind);
                sb.AppendLine($"<div><h4>{kind.Code()}{order.Order}, {values.GetLength(0)}×{values.GetLength(1)}</h4>");
                sb.AppendLine(MatrixTable(values, 8, 8));
                sb.AppendLine("</div>");
            }
            sb.AppendLine("</div><p class=\"muted\">Для матриц больше 8×8 показан левый верхний фрагмент; полные матрицы — в CSV-файле коэффициентов.</p></details>");

            sb.AppendLine("<h4>Обратное преобразование после удаления матрицы</h4>");
            var trend = AnalysisText.OrderTrend(order, previous);
            if (trend.Length > 0) sb.AppendLine($"<p>{H(trend.Trim())}</p>");

            foreach (var removal in order.Removals)
            {
                var kind = removal.RemovedKinds[0];
                var (comparisonUrl, comparisonFile) = Embed(
                    ImageRenderer.RenderComparison(model.Image.Pixels, removal.Reconstructed),
                    $"o{order.Order}_rm_{kind.Code()}.png");

                sb.AppendLine("<div class=\"removal\">");
                sb.AppendLine(Figure(comparisonUrl, comparisonFile, $"Удаление {kind.Code()}{order.Order}",
                    "Слева — исходное, в центре — восстановленное, справа — карта разности (усилена ×4)", "compare"));

                sb.AppendLine("<div class=\"removal-text\">");
                sb.AppendLine($"<h5>Удаление {kind.Code()}{order.Order}</h5>");
                sb.AppendLine("<div class=\"chips\">");
                Chip(sb, "PSNR", PsnrText(removal.Metrics) + " дБ");
                Chip(sb, "SSIM", removal.Metrics.Ssim.ToString("F3", CI));
                Chip(sb, "MAE", removal.Metrics.Mae.ToString("F2", CI));
                Chip(sb, "Удалено", $"{F1(removal.RemovedCoefficientPercent)} %");
                Chip(sb, "Энергия", $"{F1(removal.RemovedEnergyPercent)} %");
                Chip(sb, "Сжатие", removal.CompressionText);
                sb.AppendLine("</div>");
                AppendParagraphs(sb, AnalysisText.RemovalParagraph(removal));
                var compare = AnalysisText.CompareWithPrevious(removal, previous);
                if (compare.Length > 0) sb.AppendLine($"<p class=\"muted\">{H(compare)}</p>");
                sb.AppendLine("</div></div>");
            }

            sb.AppendLine("<h4>Сводка по порядку</h4>");
            var tableRows = new List<(string[], string)>();
            foreach (var removal in order.Removals)
            {
                tableRows.Add((new[]
                {
                    removal.Label,
                    F1(removal.RemovedCoefficientPercent),
                    F1(removal.RemovedEnergyPercent),
                    PsnrText(removal.Metrics),
                    removal.Metrics.Ssim.ToString("F3", CI),
                    removal.Metrics.Mae.ToString("F2", CI)
                }, removal.RemovedKinds[0] == SubbandKind.All ? "all" : ""));
            }

            AppendTable(sb, new[] { "Матрица", "Удалено коэфф., %", "Потеря энергии, %", "PSNR, дБ", "SSIM", "MAE" }, tableRows);
            sb.AppendLine("</article>");
        }

        sb.AppendLine("</section>");
    }

    private void AppendSummary(StringBuilder sb, ReportModel model)
    {
        sb.AppendLine("<section class=\"card\" id=\"summary\"><h2>6. Сводная таблица результатов</h2>");
        sb.AppendLine("<p>Все эксперименты по удалению одной матрицы. Строка «без удаления» показывает обратимость преобразования.</p>");

        var rows = new List<(string[], string)>();
        foreach (var order in model.Orders)
        {
            rows.Add((new[]
            {
                order.Order.ToString(CI),
                "без удаления",
                $"{order.Bands[0].Size}×{order.Bands[0].Size}",
                "0.0",
                "0.0",
                "∞",
                "1.000",
                "0.00",
                "100.0",
                "1.00×"
            }, "baseline"));

            foreach (var removal in order.Removals)
            {
                rows.Add((new[]
                {
                    order.Order.ToString(CI),
                    removal.Label,
                    $"{order.Bands[0].Size}×{order.Bands[0].Size}",
                    F1(removal.RemovedCoefficientPercent),
                    F1(removal.RemovedEnergyPercent),
                    PsnrText(removal.Metrics),
                    removal.Metrics.Ssim.ToString("F3", CI),
                    removal.Metrics.Mae.ToString("F2", CI),
                    F1(removal.KeptCoefficientPercent),
                    removal.CompressionRatio.ToString("F2", CI) + "×"
                }, removal.RemovedKinds[0] == SubbandKind.All ? "all" : "detail"));
            }
        }

        AppendTable(sb, new[]
        {
            "Порядок", "Удалённая матрица", "Размер", "Удалено коэфф., %", "Потеря энергии, %",
            "PSNR, дБ", "SSIM", "MAE", "Осталось объёма, %", "Сжатие"
        }, rows);

        sb.AppendLine("<h3>Графики</h3>");
        sb.AppendLine("<div class=\"grid2\">");
        sb.AppendLine("<div><h4>PSNR после удаления матрицы</h4>" + Chart(SvgCharts.PsnrByOrder(model)) + "</div>");
        sb.AppendLine("<div><h4>Доля энергии матриц</h4>" + Chart(SvgCharts.EnergyShareByOrder(model)) + "</div>");
        sb.AppendLine("</div>");

        var allShares = model.Orders.Select(o => o.Bands[0].EnergyPercent).ToList();
        sb.AppendLine($"<p>На первом уровне матрица ALL1 аккумулирует {F1(allShares[0])} % энергии изображения. На порядке {model.MaxOrder} " +
                      $"доля ALL{model.MaxOrder} составляет {F1(allShares[^1])} % — приближение остаётся главным по энергии, но содержит всего " +
                      $"{N(model.Orders[^1].Bands[0].CoefficientCount)} коэффициентов. Это наглядно показывает энергетическую компактность " +
                      "вейвлет-преобразования: небольшое число низкочастотных коэффициентов описывает основную информацию. " +
                      "Удаление ALL резко снижает яркость, а удаление AHH — тонкие диагональные детали — почти незаметно.</p>");
        sb.AppendLine("</section>");
    }

    private void AppendCompression(StringBuilder sb, ReportModel model)
    {
        sb.AppendLine("<section class=\"card\" id=\"compression\"><h2>7. Сжатие изображения</h2>");
        sb.AppendLine("<p>Рассмотрены два способа уменьшить объём данных: оставить только грубое приближение ALL_k " +
                      "(отбросить все детали) и отбросить малую часть коэффициентов по порогу. Оба показывают компромисс " +
                      "между степенью сжатия и качеством.</p>");

        sb.AppendLine("<h3>7.1. Только приближение ALL_k</h3>");
        sb.AppendLine("<p>Если сохранить только матрицу ALL_k, а все детали заменить нулями, объём данных уменьшается в 4<sup>k</sup> раз, " +
                      "но изображение становится всё более размытым. Это простейшая модель вейвлетного сжатия.</p>");

        var approxRows = new List<(string[], string)>();
        foreach (var point in model.Approximation)
        {
            approxRows.Add((new[]
            {
                point.Order.ToString(CI),
                $"{Math.Sqrt(point.KeptCount):F0}×{Math.Sqrt(point.KeptCount):F0}",
                N(point.KeptCount),
                F1(point.KeptPercent),
                point.CompressionRatio.ToString("F2", CI) + "×",
                PsnrText(point.Metrics),
                point.Metrics.Ssim.ToString("F3", CI),
                point.Metrics.Mae.ToString("F2", CI)
            }, ""));
        }

        AppendTable(sb, new[]
        {
            "Порядок", "Размер ALL_k", "Сохранено коэффициентов", "Доля от N², %", "Сжатие", "PSNR, дБ", "SSIM", "MAE"
        }, approxRows);
        sb.AppendLine(Chart(SvgCharts.ApproximationCurve(model)));
        sb.AppendLine("<p class=\"muted\">Сохранены только коэффициенты приближения; все матрицы деталей обнулены. " +
                      "С ростом k сжатие растёт, а PSNR снижается — изображение теряет мелкие и средние детали.</p>");

        sb.AppendLine("<h3>7.2. Отбрасывание малых коэффициентов</h3>");
        AppendParagraphs(sb, AnalysisText.ThresholdSummary(model.Thresholds));

        var rows = new List<(string[], string)>();
        foreach (var point in model.Thresholds.Points)
        {
            rows.Add((new[]
            {
                F1(point.TargetPercent),
                F1(point.ActualPercent),
                point.KeptCount.ToString(CI),
                N(point.TotalCount),
                F3(point.Threshold),
                PsnrText(point.Metrics),
                point.Metrics.Ssim.ToString("F3", CI),
                point.CompressionRatio.ToString("F2", CI) + "×"
            }, ""));
        }

        AppendTable(sb, new[]
        {
            "Задано, %", "Фактически сохранено, %", "Коэффициентов", "Всего", "Порог |c|", "PSNR, дБ", "SSIM", "Сжатие"
        }, rows);
        sb.AppendLine(Chart(SvgCharts.CompressionCurve(model.Thresholds)));
        sb.AppendLine("<div class=\"warnbox\">Условное сжатие рассчитано по числу сохранённых коэффициентов. " +
                      "Фактическая доля может быть меньше заданной, если остальные коэффициенты уже равны нулю и не требуют хранения. " +
                      "Реальный коэффициент сжатия зависит от разрядности коэффициентов и энтропийного кодирования.</div>");
        sb.AppendLine("</section>");
    }

    private void AppendLb3(StringBuilder sb, ReportModel model)
    {
        var check = model.Lb3Check;
        sb.AppendLine("<section class=\"card\" id=\"lb3\"><h2>8. Проверка по данным Lb3_Haar.xlsx</h2>");
        sb.AppendLine("<p>В файле Lb3_Haar.xlsx приведены базисная матрица Хаара 8×8, два окна по восемь отсчётов и три варианта " +
                      "обнуления коэффициентов с расчётом средней мощности P<sub>вх</sub> и P<sub>вых</sub>. " +
                      "Ниже те же величины пересчитаны программой.</p>");

        sb.AppendLine("<h3>Базисная матрица H (8×8)</h3>");
        var basisRows = new List<(string[], string)>();
        for (int row = 0; row < 8; row++)
        {
            var cells = new string[9];
            cells[0] = $"ψ{row + 1}";
            for (int column = 0; column < 8; column++)
                cells[column + 1] = F4(check.Basis[row, column]);
            basisRows.Add((cells, ""));
        }

        AppendTable(sb, new[] { "Строка", "1", "2", "3", "4", "5", "6", "7", "8" }, basisRows);

        sb.AppendLine("<h3>Отсчёты и коэффициенты</h3>");
        var coeffRows = new List<(string[], string)>();
        for (int row = 0; row < 2; row++)
        {
            coeffRows.Add((new[]
            {
                $"Окно {row + 1}, отсчёты",
                string.Join(" · ", Enumerable.Range(0, 8).Select(i => F0(check.Samples[row, i])))
            }, ""));
            coeffRows.Add((new[]
            {
                $"Окно {row + 1}, расчёт (базис из файла)",
                string.Join(" · ", Enumerable.Range(0, 8).Select(i => F4(check.Coefficients[row, i])))
            }, ""));
            coeffRows.Add((new[]
            {
                $"Окно {row + 1}, Lb3_Haar.xlsx",
                string.Join(" · ", Enumerable.Range(0, 8).Select(i => F4(check.ReferenceCoefficients[row, i])))
            }, ""));
            coeffRows.Add((new[]
            {
                $"Окно {row + 1}, точный базис 1/√2",
                string.Join(" · ", Enumerable.Range(0, 8).Select(i => F4(check.ExactCoefficients[row, i])))
            }, ""));
        }

        AppendTable(sb, new[] { "Величина", "Значения по восьми отсчётам" }, coeffRows);

        sb.AppendLine("<h3>Варианты обнуления коэффициентов</h3>");
        var caseRows = check.Cases.Select(item => (new[]
        {
            H(item.Title),
            item.ZeroedIndices.Length == 0 ? "—" : string.Join(", ", item.ZeroedIndices.Select(i => (i + 1).ToString(CI))),
            F5(item.OutputPower),
            F5(item.ReferenceOutputPower),
            F5(Math.Abs(item.OutputPower - item.ReferenceOutputPower))
        }, "")).ToList();

        AppendTable(sb, new[] { "Вариант", "Обнулены коэффициенты", "P<sub>вых</sub> расчёт", "P<sub>вых</sub> эталон", "|Δ|" }, caseRows);

        sb.AppendLine("<div class=\"note\"><b>О расхождении.</b> В Lb3_Haar.xlsx базисная и обратная матрицы записаны с округлением " +
                      "до четырёх знаков, поэтому эталонные P<sub>вых</sub> немного отличаются от точного ортонормированного преобразования " +
                      "(например, 77.5384 вместо 77.5 при сохранении всех коэффициентов). Программа воспроизводит значения файла " +
                      "при использовании его округлённой матрицы; в основном расчёте применяются точные коэффициенты 1/√2. " +
                      "Это и объясняет различия в четвёртом знаке.</div>");

        AppendTable(sb, new[] { "Показатель", "Значение" }, new (string[], string)[]
        {
            (new[] { "P<sub>вх</sub> (средняя мощность 16 отсчётов)", F2(Core.Lb3Check.InputPower) }, ""),
            (new[] { "P<sub>вых</sub> при всех коэффициентах (файл)", F5(check.Cases[0].ReferenceOutputPower) }, ""),
            (new[] { "P<sub>вых</sub> без первых двух (файл)", F5(check.Cases[1].ReferenceOutputPower) }, ""),
            (new[] { "P<sub>вых</sub> без 4-го и 5-го (файл)", F5(check.Cases[2].ReferenceOutputPower) }, ""),
            (new[] { "P<sub>вых</sub> без последних двух (файл)", F5(check.Cases[3].ReferenceOutputPower) }, "")
        });
        sb.AppendLine("</section>");
    }

    private static void AppendConclusion(StringBuilder sb, ReportModel model)
    {
        sb.AppendLine("<section class=\"card\" id=\"conclusion\"><h2>9. Выводы</h2>");
        AppendParagraphs(sb, AnalysisText.FinalConclusion(model));
        sb.AppendLine("<div class=\"note\"><b>Основной вывод.</b> Приближение ALL содержит основную энергию и определяет " +
                      "воспринимаемое изображение; детали AHL и ALH отвечают за вертикальные и горизонтальные перепады, " +
                      "а AHH — за тонкие диагональные структуры. Чем выше порядок разложения и чем больше коэффициентов " +
                      "отброшено, тем меньше объём данных и тем ниже качество. Компромисс между сжатием и качеством " +
                      "количественно показан в разделах 6 и 7.</div>");
        sb.AppendLine("</section>");
    }

    private static void AppendQuestions(StringBuilder sb)
    {
        sb.AppendLine("<section class=\"card\" id=\"questions\"><h2>10. Контрольные вопросы</h2>");
        foreach (var (question, answer) in Theory.LabTheory.ControlQuestions)
        {
            sb.AppendLine("<details><summary>" + H(question) + "</summary>");
            sb.AppendLine("<p>" + H(answer) + "</p>");
            sb.AppendLine("</details>");
        }
        sb.AppendLine("</section>");
    }

    private void AppendAppendix(StringBuilder sb, ReportModel model)
    {
        sb.AppendLine("<section class=\"card\" id=\"appendix\"><h2>11. Приложение</h2>");
        sb.AppendLine("<h3>Структура программы</h3>");
        AppendTable(sb, new[] { "Файл", "Ответственность" },
            Theory.LabTheory.Structure.Select(item => (new[] { "<code>" + H(item.File) + "</code>", H(item.Responsibility) }, "")).ToList());

        sb.AppendLine("<h3>Режимы запуска</h3>");
        sb.AppendLine("<div class=\"formula\">HaarWaveletLab.exe                 — окно программы (WinForms)\n" +
                      "HaarWaveletLab.exe --console       — консольное меню\n" +
                      "HaarWaveletLab.exe --report        — отчёт без интерфейса\n" +
                      "HaarWaveletLab.exe --report --image photo.jpg --order 5 --size 256\n" +
                      "HaarWaveletLab.exe --selftest      — самопроверка математики и отчёта</div>");

        sb.AppendLine("<h3>Выходные файлы</h3>");
        sb.AppendLine("<ul>" +
                      "<li><code>report.html</code> — этот автономный отчёт;</li>" +
                      "<li><code>report_assets/*.png</code> — изображения, теплокарты и сравнения;</li>" +
                      "<li><code>csv/01_bands.csv</code> — размеры, диапазоны и энергия матриц;</li>" +
                      "<li><code>csv/02_experiments.csv</code> — метрики всех экспериментов;</li>" +
                      "<li><code>csv/03_compression.csv</code> — исследование сжатия;</li>" +
                      "<li><code>csv/04_coefficients.csv</code> — полная матрица коэффициентов;</li>" +
                      "<li><code>csv/05_source_image.csv</code> — исходные яркости;</li>" +
                      "<li><code>csv/06_lb3_check.csv</code> — проверка по Lb3_Haar.xlsx;</li>" +
                      "<li><code>csv/07_approximation.csv</code> — сжатие до одного приближения.</li>" +
                      "</ul>");

        sb.AppendLine("<h3>Параметры расчёта</h3>");
        AppendTable(sb, new[] { "Параметр", "Значение" }, new (string[], string)[]
        {
            (new[] { "Источник изображения", H(model.SourcePath ?? model.SourceLabel) }, ""),
            (new[] { "Размер N", model.Image.Size.ToString(CI) }, ""),
            (new[] { "Максимальный порядок", model.MaxOrder.ToString(CI) }, ""),
            (new[] { "Всего коэффициентов", N(model.Image.Size * model.Image.Size) }, ""),
            (new[] { "Дата формирования", model.CreatedAt.ToString("dd.MM.yyyy HH:mm:ss", CI) }, "")
        });
        sb.AppendLine("</section>");
    }

    private void AppendFooter(StringBuilder sb, ReportModel model)
    {
        sb.AppendLine("<footer>");
        sb.AppendLine($"<p>Отчёт сформирован программой <b>HaarWaveletLab</b> (C# / .NET 10, WinForms) · " +
                      $"{model.CreatedAt:dd.MM.yyyy HH:mm}</p>");
        sb.AppendLine("<p class=\"muted\">Дисциплина «Основы цифрового телевидения», лабораторная работа №3. " +
                      "Все изображения и графики встроены в HTML; внешние файлы не требуются.</p>");
        sb.AppendLine("</footer>");
    }

    private static void AppendParagraphs(StringBuilder sb, string text)
    {
        foreach (string raw in text.Split('\n'))
        {
            string paragraph = raw.Trim();
            if (paragraph.Length == 0) continue;
            sb.AppendLine("<p>" + H(paragraph) + "</p>");
        }
    }

    private static void AppendTable(
        StringBuilder sb,
        string[] headers,
        IEnumerable<(string[] Cells, string ClassName)> rows,
        string cssClass = "data")
    {
        sb.AppendLine($"<table class=\"{cssClass}\">");
        sb.AppendLine("<thead><tr>" + string.Concat(headers.Select(h => $"<th>{h}</th>")) + "</tr></thead>");
        sb.AppendLine("<tbody>");
        foreach (var (cells, className) in rows)
        {
            string cls = string.IsNullOrEmpty(className) ? "" : $" class=\"{className}\"";
            sb.AppendLine($"<tr{cls}>" + string.Concat(cells.Select(c => $"<td>{c}</td>")) + "</tr>");
        }
        sb.AppendLine("</tbody></table>");
    }

    private static string MatrixTable(double[,] values, int maxRows, int maxColumns)
    {
        int height = values.GetLength(0);
        int width = values.GetLength(1);
        int rows = Math.Min(height, maxRows);
        int columns = Math.Min(width, maxColumns);

        var sb = new StringBuilder();
        sb.AppendLine("<div class=\"matrix-wrap\"><table class=\"matrix\">");
        sb.AppendLine("<thead><tr><th>y\\x</th>" +
                      string.Concat(Enumerable.Range(0, columns).Select(x => $"<th>{x}</th>")) + "</tr></thead><tbody>");
        for (int y = 0; y < rows; y++)
        {
            sb.AppendLine("<tr><th>" + y + "</th>");
            for (int x = 0; x < columns; x++)
            {
                double value = values[y, x];
                string cls = value < -0.05 ? "neg" : value > 0.05 ? "pos" : "";
                sb.AppendLine($"<td class=\"{cls}\">{Coefficient(value)}</td>");
            }
            sb.AppendLine("</tr>");
        }
        sb.AppendLine("</tbody></table></div>");

        if (height > rows || width > columns)
            sb.AppendLine($"<p class=\"muted\">Показан фрагмент {rows}×{columns} из {height}×{width}.</p>");
        return sb.ToString();
    }

    private (string Url, string FileName) Embed(Bitmap bitmap, string fileName)
    {
        try
        {
            string path = Path.Combine(_assetsDir, fileName);
            string url = ImageRenderer.SaveAndEmbed(bitmap, path);
            return (url, fileName);
        }
        finally
        {
            bitmap.Dispose();
        }
    }

    private string Figure(string dataUrl, string fileName, string alt, string caption, string cssClass)
    {
        string href = Uri.EscapeDataString(_assetsName) + "/" + Uri.EscapeDataString(fileName);
        var sb = new StringBuilder();
        sb.AppendLine($"<figure class=\"{cssClass}\">");
        sb.AppendLine($"<a href=\"{href}\" target=\"_blank\"><img src=\"{dataUrl}\" alt=\"{H(alt)}\"></a>");
        sb.AppendLine($"<figcaption>{caption}</figcaption>");
        sb.AppendLine("</figure>");
        return sb.ToString();
    }

    private static string Chart(string svg) => "<div class=\"chart\">" + svg + "</div>";

    private static void Chip(StringBuilder sb, string label, string value) =>
        sb.AppendLine($"<span class=\"chip\">{H(label)} <b>{H(value)}</b></span>");

    private static string PsnrText(QualityMetrics metrics) =>
        double.IsPositiveInfinity(metrics.Psnr) ? "∞" : metrics.Psnr.ToString("F2", CI);

    private static string N(int value) => value.ToString("N0", CI).Replace(",", " ");

    private static string F0(double value) => value.ToString("F0", CI);

    private static string F1(double value) => value.ToString("F1", CI);

    private static string F2(double value) => value.ToString("F2", CI);

    private static string F3(double value) => value.ToString("F3", CI);

    private static string F4(double value) => value.ToString("F4", CI);

    private static string F5(double value) => value.ToString("F5", CI);

    private static string Coefficient(double value)
    {
        if (Math.Abs(value) < 0.05) return "0";
        return value.ToString("F1", CI);
    }

    private static string H(string value) => WebUtility.HtmlEncode(value);

    private const string Css = """
        :root { --ink:#0f172a; --muted:#64748b; --line:#e2e8f0; --accent:#2563eb; --bg:#f4f6fb; }
        * { box-sizing:border-box; }
        html { scroll-behavior:smooth; }
        body { margin:0; background:var(--bg); color:var(--ink); font-family:'Segoe UI','Inter',Arial,sans-serif; line-height:1.55; }
        a { color:#1d4ed8; }
        .hero { background:linear-gradient(135deg,#0d2542 0%,#123f73 55%,#1d5fa8 100%); color:#fff; padding:46px 24px 38px; }
        .hero .wrap { max-width:1180px; margin:0 auto; }
        .eyebrow { letter-spacing:.14em; text-transform:uppercase; font-size:12px; color:#bcd7f7; font-weight:600; }
        .hero h1 { font-size:36px; line-height:1.18; margin:10px 0 8px; font-weight:750; }
        .hero .sub { color:#d7e6f8; font-size:15px; max-width:900px; }
        .meta { display:grid; grid-template-columns:repeat(auto-fit,minmax(170px,1fr)); gap:12px; margin-top:26px; }
        .meta div { background:rgba(255,255,255,.09); border:1px solid rgba(255,255,255,.18); border-radius:12px; padding:10px 14px; font-size:12px; color:#cfe2f8; }
        .meta b { display:block; font-size:17px; margin-top:3px; color:#fff; font-weight:650; }
        main { max-width:1180px; margin:0 auto; padding:10px 24px 60px; }
        .toc { background:#fff; border:1px solid var(--line); border-radius:14px; padding:16px 22px; margin:22px 0; box-shadow:0 1px 2px rgba(15,23,42,.05); }
        .toc b { margin-right:12px; }
        .toc a { color:#1d4ed8; text-decoration:none; margin-right:14px; font-size:13.5px; display:inline-block; padding:3px 0; }
        .toc a:hover { text-decoration:underline; }
        section.card { background:#fff; border:1px solid var(--line); border-radius:16px; padding:26px 30px; margin:22px 0; box-shadow:0 1px 2px rgba(15,23,42,.04); }
        h2 { font-size:24px; margin:0 0 18px; padding-bottom:10px; border-bottom:2px solid #e2e8f0; font-weight:700; }
        h3 { font-size:19px; margin:24px 0 10px; color:#0f172a; }
        h4 { font-size:16px; margin:20px 0 8px; color:#1e293b; }
        h5 { font-size:15px; margin:0 0 8px; color:#0f172a; }
        p { margin:0 0 12px; }
        ul { margin:0 0 12px 22px; padding:0; }
        li { margin:4px 0; }
        .muted { color:var(--muted); font-size:13px; }
        .note { background:#eff6ff; border:1px solid #bfdbfe; border-radius:12px; padding:14px 18px; font-size:13.5px; color:#1e3a8a; }
        .warnbox { background:#fffbeb; border:1px solid #fde68a; color:#92400e; border-radius:12px; padding:12px 16px; font-size:13px; margin-top:14px; }
        .two { display:grid; grid-template-columns:1.05fr .95fr; gap:22px; align-items:start; }
        .grid2 { display:grid; grid-template-columns:repeat(2,minmax(0,1fr)); gap:18px; }
        figure { margin:0; background:#f8fafc; border:1px solid var(--line); border-radius:12px; padding:10px; }
        figure img { width:100%; height:auto; display:block; border-radius:6px; }
        figure.heat img { image-rendering:pixelated; background:#0b0e14; }
        figure.compare img { background:#0b0e14; }
        figure.wide { margin-bottom:14px; }
        figcaption { font-size:12px; color:#475569; margin-top:8px; line-height:1.45; }
        figcaption b { color:#0f172a; }
        table.data { width:100%; border-collapse:collapse; font-size:12.5px; margin:12px 0; }
        table.data th, table.data td { border:1px solid #dbe2ee; padding:6px 8px; text-align:right; vertical-align:top; }
        table.data th { background:#eef3fb; color:#1e3a8a; font-weight:650; text-align:center; }
        table.data td:first-child, table.data th:first-child { text-align:left; }
        table.data tr.all td { background:#eff6ff; }
        table.data tr.baseline td { background:#f1f5f9; color:#334155; }
        table.data tr.detail td { background:#fff; }
        .chips { display:flex; flex-wrap:wrap; gap:8px; margin:0 0 10px; }
        .chip { background:#f1f5f9; border:1px solid #e2e8f0; border-radius:999px; padding:4px 10px; font-size:12px; color:#334155; }
        .chip b { color:#0f172a; }
        .formula { background:#f8fafc; border-left:4px solid var(--accent); padding:12px 16px; margin:12px 0; font-family:'Cascadia Mono',Consolas,monospace; font-size:12.5px; white-space:pre-wrap; border-radius:0 8px 8px 0; }
        details { background:#f8fafc; border:1px solid var(--line); border-radius:10px; padding:10px 16px; margin:14px 0; }
        summary { cursor:pointer; font-weight:650; color:#1e3a8a; }
        .matrix-wrap { overflow:auto; max-height:380px; margin:8px 0; }
        table.matrix { border-collapse:collapse; font-family:Consolas,'Cascadia Mono',monospace; font-size:10px; }
        table.matrix td, table.matrix th { border:1px solid #e2e8f0; padding:2px 5px; text-align:right; min-width:38px; }
        table.matrix th { background:#f1f5f9; color:#475569; position:sticky; top:0; z-index:1; }
        table.matrix td.neg, table.matrix td.pos { font-weight:600; }
        table.matrix td.neg { color:#1d4ed8; }
        table.matrix td.pos { color:#b91c1c; }
        .chart { background:#fff; border:1px solid #eef2f7; border-radius:12px; padding:8px; margin:12px 0; }
        .chart svg { display:block; width:100%; height:auto; }
        .order { border-top:1px solid var(--line); padding-top:22px; margin-top:26px; }
        .order:first-of-type { border-top:none; padding-top:0; margin-top:0; }
        .removal { display:grid; grid-template-columns:1.15fr .85fr; gap:20px; align-items:start; border-top:1px dashed var(--line); padding-top:18px; margin-top:18px; }
        .removal-text { min-width:0; }
        footer { max-width:1180px; margin:0 auto; padding:26px 24px 50px; color:#64748b; font-size:12px; }
        footer p { margin:4px 0; }
        code { background:#f1f5f9; border:1px solid #e2e8f0; border-radius:4px; padding:1px 5px; font-family:Consolas,monospace; font-size:.92em; }
        @media (max-width:960px) {
          .two, .grid2, .removal { grid-template-columns:1fr; }
          .hero h1 { font-size:28px; }
          section.card { padding:20px 18px; }
          main { padding:10px 12px 40px; }
        }
        @media print {
          body { background:#fff; }
          .hero { padding:22px; }
          nav.toc { display:none; }
          section.card { box-shadow:none; border-color:#cbd5e1; break-inside:avoid; }
          figure, .removal, .order { break-inside:avoid; }
          a { color:inherit; text-decoration:none; }
        }
        """;
}
