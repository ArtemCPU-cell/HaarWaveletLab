using System.Globalization;
using System.Text;
using HaarWaveletLab.Models;

namespace HaarWaveletLab.Reporting;

/// <summary>Векторные иллюстрации и графики для HTML-отчёта (чистый SVG, без внешних библиотек).</summary>
public static class SvgCharts
{
    private static readonly CultureInfo CI = CultureInfo.InvariantCulture;
    private const string Font = "Segoe UI, Arial, sans-serif";

    private static readonly string[] SeriesColors = { "#2563eb", "#16a34a", "#ea580c", "#dc2626" };

    public static string Histogram(double[,] pixels, int bins = 64)
    {
        bins = Math.Clamp(bins, 8, 128);
        var counts = new int[bins];
        int height = pixels.GetLength(0);
        int width = pixels.GetLength(1);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int bin = (int)Math.Clamp(pixels[y, x] / 256.0 * bins, 0, bins - 1);
                counts[bin]++;
            }
        }

        int max = Math.Max(1, counts.Max());
        const int w = 1000, h = 340, left = 80, right = 30, top = 55, bottom = 55;
        double plotW = w - left - right;
        double plotH = h - top - bottom;
        var sb = Begin(w, h, "Гистограмма яркости");

        sb.AppendLine($"<text x=\"{left}\" y=\"30\" font-family=\"{Font}\" font-size=\"16\" font-weight=\"700\" fill=\"#0f172a\">Гистограмма яркости исходного изображения</text>");
        AppendAxes(sb, left, top, plotW, plotH, w);

        for (int tick = 0; tick <= 4; tick++)
        {
            double value = max * tick / 4.0;
            double y = top + plotH - plotH * tick / 4.0;
            sb.AppendLine($"<line x1=\"{left}\" y1=\"{y:F1}\" x2=\"{w - right}\" y2=\"{y:F1}\" stroke=\"#e8edf5\"/>");
            sb.AppendLine($"<text x=\"{left - 10}\" y=\"{y + 4:F1}\" text-anchor=\"end\" font-family=\"{Font}\" font-size=\"11\" fill=\"#64748b\">{value.ToString("N0", CI).Replace(",", " ")}</text>");
        }

        double barW = plotW / bins;
        for (int i = 0; i < bins; i++)
        {
            double barH = counts[i] / (double)max * plotH;
            double x = left + i * barW;
            sb.AppendLine($"<rect x=\"{x:F1}\" y=\"{top + plotH - barH:F1}\" width=\"{Math.Max(1, barW - 1):F1}\" height=\"{barH:F1}\" fill=\"#3b82f6\"/>");
        }

        for (int tick = 0; tick <= 4; tick++)
        {
            double value = 255.0 * tick / 4.0;
            double x = left + plotW * tick / 4.0;
            sb.AppendLine($"<text x=\"{x:F1}\" y=\"{top + plotH + 22}\" text-anchor=\"middle\" font-family=\"{Font}\" font-size=\"11\" fill=\"#64748b\">{value:F0}</text>");
        }

        sb.AppendLine($"<text x=\"{left + plotW / 2:F0}\" y=\"{h - 14}\" text-anchor=\"middle\" font-family=\"{Font}\" font-size=\"12\" fill=\"#475569\">яркость Y (0…255)</text>");
        sb.AppendLine($"<text x=\"18\" y=\"{top + plotH / 2:F0}\" transform=\"rotate(-90 18 {top + plotH / 2:F0})\" text-anchor=\"middle\" font-family=\"{Font}\" font-size=\"12\" fill=\"#475569\">число пикселей</text>");
        return End(sb);
    }

    /// <summary>PSNR после удаления каждой матрицы в зависимости от порядка разложения.</summary>
    public static string PsnrByOrder(ReportModel model)
    {
        var series = Subbands.All.Select((kind, index) => (
            Name: kind.Code(),
            Color: SeriesColors[index],
            Values: model.Orders.Select(o => o.GetRemoval(kind).Metrics.Psnr).ToArray()
        )).ToList();

        var finite = series.SelectMany(s => s.Values).Where(v => !double.IsPositiveInfinity(v)).ToList();
        double max = finite.Count > 0 ? finite.Max() : 40.0;
        double yMax = Math.Ceiling(Math.Max(10, max) / 10.0) * 10.0;
        return LineChart(
            "PSNR после удаления одной матрицы",
            model.Orders.Select(o => o.Order.ToString(CI)).ToArray(),
            series,
            0, yMax, "дБ");
    }

    /// <summary>Качество восстановления только по приближению ALL_k.</summary>
    public static string ApproximationCurve(ReportModel model)
    {
        var series = new List<(string Name, string Color, double[] Values)>
        {
            ("PSNR", "#2563eb", model.Approximation.Select(p => p.Metrics.Psnr).ToArray())
        };

        double max = model.Approximation
            .Where(p => !double.IsPositiveInfinity(p.Metrics.Psnr))
            .Select(p => p.Metrics.Psnr)
            .DefaultIfEmpty(40.0)
            .Max();
        double yMax = Math.Ceiling(Math.Max(10, max) / 10.0) * 10.0;
        return LineChart(
            "Сжатие до одного приближения ALL_k",
            model.Approximation.Select(p => p.Order.ToString(CI)).ToArray(),
            series,
            0, yMax, "дБ");
    }

    /// <summary>Доля энергии матриц по порядкам (логарифмическая шкала).</summary>
    public static string EnergyShareByOrder(ReportModel model)
    {
        var series = Subbands.All.Select((kind, index) => (
            Name: kind.Code(),
            Color: SeriesColors[index],
            Values: model.Orders.Select(o => Math.Max(0.001, o.GetRemoval(kind).RemovedEnergyPercent)).ToArray()
        )).ToList();

        return LogLineChart(
            "Доля энергии, приходящаяся на каждую матрицу",
            model.Orders.Select(o => o.Order.ToString(CI)).ToArray(),
            series);
    }

    /// <summary>Кривая «сжатие — качество» при отбрасывании малых коэффициентов.</summary>
    public static string CompressionCurve(ThresholdStudy study)
    {
        const int w = 1000, h = 420, left = 85, right = 40, top = 65, bottom = 65;
        double plotW = w - left - right;
        double plotH = h - top - bottom;
        var points = study.Points.OrderBy(p => p.CompressionRatio).ToList();
        double maxRatio = Math.Max(2.0, points.Max(p => p.CompressionRatio) * 1.08);
        var finitePsnrs = points
            .Where(p => !double.IsPositiveInfinity(p.Metrics.Psnr))
            .Select(p => p.Metrics.Psnr)
            .ToList();
        double maxFinite = finitePsnrs.Count > 0 ? finitePsnrs.Max() : 40.0;
        double maxPsnr = Math.Min(80.0, Math.Max(35.0, Math.Ceiling(maxFinite / 5.0) * 5.0));

        var sb = Begin(w, h, "Кривая сжатие — качество");
        sb.AppendLine($"<text x=\"{left}\" y=\"30\" font-family=\"{Font}\" font-size=\"16\" font-weight=\"700\" fill=\"#0f172a\">Компромисс «сжатие — качество»</text>");
        sb.AppendLine($"<text x=\"{left}\" y=\"48\" font-family=\"{Font}\" font-size=\"11\" fill=\"#64748b\">Порядок разложения {study.Order}; сохранена доля наибольших коэффициентов; PSNR = ∞ показан у верхней границы</text>");
        AppendAxes(sb, left, top, plotW, plotH, w);

        for (int tick = 0; tick <= 4; tick++)
        {
            double value = maxPsnr * tick / 4.0;
            double y = top + plotH - plotH * tick / 4.0;
            sb.AppendLine($"<line x1=\"{left}\" y1=\"{y:F1}\" x2=\"{w - right}\" y2=\"{y:F1}\" stroke=\"#e8edf5\"/>");
            sb.AppendLine($"<text x=\"{left - 10}\" y=\"{y + 4:F1}\" text-anchor=\"end\" font-family=\"{Font}\" font-size=\"11\" fill=\"#64748b\">{value:F0}</text>");
        }

        double thresholdY = top + plotH - 30.0 / maxPsnr * plotH;
        sb.AppendLine($"<line x1=\"{left}\" y1=\"{thresholdY:F1}\" x2=\"{w - right}\" y2=\"{thresholdY:F1}\" stroke=\"#94a3b8\" stroke-dasharray=\"6 5\"/>");
        sb.AppendLine($"<text x=\"{w - right}\" y=\"{thresholdY - 6:F1}\" text-anchor=\"end\" font-family=\"{Font}\" font-size=\"10\" fill=\"#64748b\">30 дБ — визуально высокое качество</text>");

        var polyline = new StringBuilder();
        foreach (var point in points)
        {
            double x = left + (point.CompressionRatio - 1.0) / (maxRatio - 1.0) * plotW;
            double psnr = double.IsPositiveInfinity(point.Metrics.Psnr) ? maxPsnr : Math.Clamp(point.Metrics.Psnr, 0, maxPsnr);
            double y = top + plotH - psnr / maxPsnr * plotH;
            polyline.Append($"{x:F1},{y:F1} ");
            sb.AppendLine($"<circle cx=\"{x:F1}\" cy=\"{y:F1}\" r=\"5\" fill=\"#0ea5e9\" stroke=\"#fff\" stroke-width=\"1.5\"/>");
            sb.AppendLine($"<text x=\"{x:F1}\" y=\"{y - 10:F1}\" text-anchor=\"middle\" font-family=\"{Font}\" font-size=\"10\" fill=\"#0369a1\">{point.ActualPercent:F1}%</text>");
        }

        sb.AppendLine($"<polyline points=\"{polyline.ToString().Trim()}\" fill=\"none\" stroke=\"#0ea5e9\" stroke-width=\"2.5\" stroke-linejoin=\"round\"/>");

        for (int tick = 0; tick <= 4; tick++)
        {
            double value = 1.0 + (maxRatio - 1.0) * tick / 4.0;
            double x = left + plotW * tick / 4.0;
            sb.AppendLine($"<text x=\"{x:F1}\" y=\"{top + plotH + 22}\" text-anchor=\"middle\" font-family=\"{Font}\" font-size=\"11\" fill=\"#64748b\">{value:F1}×</text>");
        }

        sb.AppendLine($"<text x=\"{left + plotW / 2:F0}\" y=\"{h - 14}\" text-anchor=\"middle\" font-family=\"{Font}\" font-size=\"12\" fill=\"#475569\">условное сжатие по числу коэффициентов</text>");
        sb.AppendLine($"<text x=\"18\" y=\"{top + plotH / 2:F0}\" transform=\"rotate(-90 18 {top + plotH / 2:F0})\" text-anchor=\"middle\" font-family=\"{Font}\" font-size=\"12\" fill=\"#475569\">PSNR, дБ</text>");
        return End(sb);
    }

    private static string LineChart(
        string title,
        string[] categories,
        IReadOnlyList<(string Name, string Color, double[] Values)> series,
        double yMin,
        double yMax,
        string unit)
    {
        const int w = 1000, h = 400, left = 85, right = 40, top = 78, bottom = 65;
        double plotW = w - left - right;
        double plotH = h - top - bottom;
        var sb = Begin(w, h, title);
        sb.AppendLine($"<text x=\"{left}\" y=\"30\" font-family=\"{Font}\" font-size=\"16\" font-weight=\"700\" fill=\"#0f172a\">{Escape(title)}</text>");
        AppendLegend(sb, left, 52, series.Select(s => (s.Name, s.Color)).ToArray(), Font);
        AppendAxes(sb, left, top, plotW, plotH, w);

        for (int tick = 0; tick <= 5; tick++)
        {
            double value = yMin + (yMax - yMin) * tick / 5.0;
            double y = top + plotH - plotH * tick / 5.0;
            sb.AppendLine($"<line x1=\"{left}\" y1=\"{y:F1}\" x2=\"{w - right}\" y2=\"{y:F1}\" stroke=\"#e8edf5\"/>");
            sb.AppendLine($"<text x=\"{left - 10}\" y=\"{y + 4:F1}\" text-anchor=\"end\" font-family=\"{Font}\" font-size=\"11\" fill=\"#64748b\">{value.ToString("F0", CI)}</text>");
        }

        int n = categories.Length;
        for (int s = 0; s < series.Count; s++)
        {
            var values = series[s].Values;
            var points = new StringBuilder();
            for (int i = 0; i < n; i++)
            {
                double x = n <= 1 ? left + plotW / 2 : left + i * plotW / (n - 1.0);
                double y = top + plotH - Math.Clamp(values[i], yMin, yMax) / (yMax - yMin) * plotH;
                points.Append($"{x:F1},{y:F1} ");
                sb.AppendLine($"<circle cx=\"{x:F1}\" cy=\"{y:F1}\" r=\"4.5\" fill=\"{series[s].Color}\" stroke=\"#fff\" stroke-width=\"1.5\"/>");
                string valueLabel = double.IsPositiveInfinity(values[i])
                    ? "∞"
                    : values[i].ToString("F1", CI);
                sb.AppendLine($"<text x=\"{x:F1}\" y=\"{y - 10:F1}\" text-anchor=\"middle\" font-family=\"{Font}\" font-size=\"10\" fill=\"{series[s].Color}\">{valueLabel}</text>");
            }

            sb.AppendLine($"<polyline points=\"{points.ToString().Trim()}\" fill=\"none\" stroke=\"{series[s].Color}\" stroke-width=\"2.5\" stroke-linejoin=\"round\"/>");
        }

        for (int i = 0; i < n; i++)
        {
            double x = n <= 1 ? left + plotW / 2 : left + i * plotW / (n - 1.0);
            sb.AppendLine($"<text x=\"{x:F1}\" y=\"{top + plotH + 24}\" text-anchor=\"middle\" font-family=\"{Font}\" font-size=\"12\" fill=\"#334155\">{Escape(categories[i])}</text>");
        }

        sb.AppendLine($"<text x=\"{left + plotW / 2:F0}\" y=\"{h - 14}\" text-anchor=\"middle\" font-family=\"{Font}\" font-size=\"12\" fill=\"#475569\">порядок разложения</text>");
        sb.AppendLine($"<text x=\"18\" y=\"{top + plotH / 2:F0}\" transform=\"rotate(-90 18 {top + plotH / 2:F0})\" text-anchor=\"middle\" font-family=\"{Font}\" font-size=\"12\" fill=\"#475569\">{Escape(unit)}</text>");
        return End(sb);
    }

    private static string LogLineChart(
        string title,
        string[] categories,
        IReadOnlyList<(string Name, string Color, double[] Values)> series)
    {
        const int w = 1000, h = 400, left = 95, right = 40, top = 78, bottom = 65;
        double plotW = w - left - right;
        double plotH = h - top - bottom;
        const double logMin = -3.0;
        const double logMax = 2.0;
        var sb = Begin(w, h, title);
        sb.AppendLine($"<text x=\"{left}\" y=\"30\" font-family=\"{Font}\" font-size=\"16\" font-weight=\"700\" fill=\"#0f172a\">{Escape(title)}</text>");
        sb.AppendLine($"<text x=\"{left}\" y=\"48\" font-family=\"{Font}\" font-size=\"11\" fill=\"#64748b\">логарифмическая шкала, % энергии</text>");
        AppendLegend(sb, w - 330, 52, series.Select(s => (s.Name, s.Color)).ToArray(), Font);
        AppendAxes(sb, left, top, plotW, plotH, w);

        for (int tick = 0; tick <= 5; tick++)
        {
            double logValue = logMin + (logMax - logMin) * tick / 5.0;
            double y = top + plotH - plotH * tick / 5.0;
            double percent = Math.Pow(10, logValue);
            sb.AppendLine($"<line x1=\"{left}\" y1=\"{y:F1}\" x2=\"{w - right}\" y2=\"{y:F1}\" stroke=\"#e8edf5\"/>");
            sb.AppendLine($"<text x=\"{left - 10}\" y=\"{y + 4:F1}\" text-anchor=\"end\" font-family=\"{Font}\" font-size=\"11\" fill=\"#64748b\">{percent.ToString(percent < 0.1 ? "0.000" : "0.###", CI)}%</text>");
        }

        int n = categories.Length;
        for (int s = 0; s < series.Count; s++)
        {
            var values = series[s].Values;
            var points = new StringBuilder();
            for (int i = 0; i < n; i++)
            {
                double logValue = Math.Clamp(Math.Log10(Math.Max(values[i], 0.001)), logMin, logMax);
                double x = n <= 1 ? left + plotW / 2 : left + i * plotW / (n - 1.0);
                double y = top + plotH - (logValue - logMin) / (logMax - logMin) * plotH;
                points.Append($"{x:F1},{y:F1} ");
                sb.AppendLine($"<circle cx=\"{x:F1}\" cy=\"{y:F1}\" r=\"4.5\" fill=\"{series[s].Color}\" stroke=\"#fff\" stroke-width=\"1.5\"/>");
            }

            sb.AppendLine($"<polyline points=\"{points.ToString().Trim()}\" fill=\"none\" stroke=\"{series[s].Color}\" stroke-width=\"2.5\" stroke-linejoin=\"round\"/>");
        }

        for (int i = 0; i < n; i++)
        {
            double x = n <= 1 ? left + plotW / 2 : left + i * plotW / (n - 1.0);
            sb.AppendLine($"<text x=\"{x:F1}\" y=\"{top + plotH + 24}\" text-anchor=\"middle\" font-family=\"{Font}\" font-size=\"12\" fill=\"#334155\">{Escape(categories[i])}</text>");
        }

        sb.AppendLine($"<text x=\"{left + plotW / 2:F0}\" y=\"{h - 14}\" text-anchor=\"middle\" font-family=\"{Font}\" font-size=\"12\" fill=\"#475569\">порядок разложения</text>");
        return End(sb);
    }

    /// <summary>Базисные функции Хаара для 8 отсчётов — строки матрицы H из Lb3_Haar.xlsx.</summary>
    public static string HaarBasis8()
    {
        double inv = 1.0 / Math.Sqrt(8);
        double[,] basis =
        {
            { inv, inv, inv, inv, inv, inv, inv, inv },
            { inv, inv, inv, inv, -inv, -inv, -inv, -inv },
            { 0.5, 0.5, -0.5, -0.5, 0, 0, 0, 0 },
            { 0, 0, 0, 0, 0.5, 0.5, -0.5, -0.5 },
            { 0.70710678, -0.70710678, 0, 0, 0, 0, 0, 0 },
            { 0, 0, 0.70710678, -0.70710678, 0, 0, 0, 0 },
            { 0, 0, 0, 0, 0.70710678, -0.70710678, 0, 0 },
            { 0, 0, 0, 0, 0, 0, 0.70710678, -0.70710678 }
        };

        const int w = 1000, h = 430;
        var sb = Begin(w, h, "Базисные функции Хаара");
        sb.AppendLine($"<text x=\"30\" y=\"28\" font-family=\"{Font}\" font-size=\"16\" font-weight=\"700\" fill=\"#0f172a\">Базисные функции Хаара (8 отсчётов)</text>");
        sb.AppendLine($"<text x=\"30\" y=\"47\" font-family=\"{Font}\" font-size=\"11\" fill=\"#64748b\">Строки 1–8 базисной матрицы H из Lb3_Haar.xlsx</text>");

        const int columns = 4;
        int panelW = 230;
        int panelH = 160;
        int startX = 30;
        int startY = 65;

        for (int k = 0; k < 8; k++)
        {
            int col = k % columns;
            int row = k / columns;
            int px = startX + col * (panelW + 10);
            int py = startY + row * (panelH + 15);

            sb.AppendLine($"<rect x=\"{px}\" y=\"{py}\" width=\"{panelW}\" height=\"{panelH}\" rx=\"8\" fill=\"#f8fafc\" stroke=\"#e2e8f0\"/>");
            sb.AppendLine($"<text x=\"{px + 10}\" y=\"{py + 20}\" font-family=\"{Font}\" font-size=\"12\" font-weight=\"700\" fill=\"#0f172a\">ψ{k + 1}(x)</text>");

            double cx = px + 18;
            double baseY = py + panelH / 2.0 + 8;
            double usableW = panelW - 36;
            double step = usableW / 8.0;
            double scale = 52.0 / 0.70710678;

            sb.AppendLine($"<line x1=\"{cx}\" y1=\"{baseY:F1}\" x2=\"{cx + usableW:F1}\" y2=\"{baseY:F1}\" stroke=\"#cbd5e1\"/>");
            sb.AppendLine($"<line x1=\"{cx + usableW:F1}\" y1=\"{baseY - 6}\" x2=\"{cx + usableW:F1}\" y2=\"{baseY + 6}\" stroke=\"#94a3b8\"/>");

            double previousY = baseY;
            for (int i = 0; i < 8; i++)
            {
                double value = basis[k, i];
                double y = baseY - value * scale;
                double x0 = cx + i * step;
                double x1 = x0 + step;
                if (i > 0)
                    sb.AppendLine($"<line x1=\"{x0:F1}\" y1=\"{previousY:F1}\" x2=\"{x0:F1}\" y2=\"{y:F1}\" stroke=\"#2563eb\" stroke-width=\"2\"/>");
                sb.AppendLine($"<line x1=\"{x0:F1}\" y1=\"{y:F1}\" x2=\"{x1:F1}\" y2=\"{y:F1}\" stroke=\"#2563eb\" stroke-width=\"2\"/>");
                previousY = y;
            }
        }

        return End(sb);
    }

    /// <summary>Структурная схема прямого и обратного преобразования (рисунок 1 методички).</summary>
    public static string StructuralScheme()
    {
        const int w = 1000, h = 390;
        var sb = Begin(w, h, "Структурная схема вейвлет-преобразования");
        sb.AppendLine($"<text x=\"30\" y=\"28\" font-family=\"{Font}\" font-size=\"16\" font-weight=\"700\" fill=\"#0f172a\">Структурная схема прямого и обратного преобразования</text>");
        sb.AppendLine($"<text x=\"30\" y=\"47\" font-family=\"{Font}\" font-size=\"11\" fill=\"#64748b\">WTx, WTy — прямое преобразование по осям X и Y; IWTx, IWTy — обратное</text>");

        // Верхний ряд: прямое преобразование.
        Box(sb, 20, 80, 120, 66, "A", "N × N", "#eff6ff", "#2563eb");
        Arrow(sb, 140, 113, 180, 113);
        Box(sb, 180, 80, 150, 66, "WTx", "по строкам (X)", "#f0fdf4", "#16a34a");
        Arrow(sb, 330, 113, 370, 113);
        Box(sb, 370, 80, 120, 66, "AL | AH", "N/2 × N", "#f8fafc", "#94a3b8");
        Arrow(sb, 490, 113, 530, 113);
        Box(sb, 530, 80, 150, 66, "WTy", "по столбцам (Y)", "#f0fdf4", "#16a34a");
        Arrow(sb, 680, 113, 720, 113);
        Box(sb, 720, 66, 250, 94, "ALL  AHL", "ALH  AHH", "#fef2f2", "#dc2626", extra: "(N/2) × (N/2)");
        sb.AppendLine($"<text x=\"845\" y=\"178\" text-anchor=\"middle\" font-family=\"{Font}\" font-size=\"10\" fill=\"#64748b\">четыре матрицы уровня</text>");

        // Стрелка вниз к обратному преобразованию.
        Arrow(sb, 845, 160, 845, 215);
        sb.AppendLine($"<rect x=\"730\" y=\"222\" width=\"230\" height=\"34\" rx=\"8\" fill=\"#fffbeb\" stroke=\"#f59e0b\"/>");
        sb.AppendLine($"<text x=\"845\" y=\"244\" text-anchor=\"middle\" font-family=\"{Font}\" font-size=\"11\" fill=\"#92400e\">удаление матриц → нули</text>");

        // Нижний ряд: обратное преобразование (справа налево).
        Arrow(sb, 845, 256, 845, 296);
        Box(sb, 710, 296, 135, 66, "IWTy", "по столбцам", "#eff6ff", "#2563eb");
        Arrow(sb, 710, 329, 674, 329);
        Box(sb, 540, 296, 134, 66, "IWTx", "по строкам", "#eff6ff", "#2563eb");
        Arrow(sb, 540, 329, 504, 329);
        Box(sb, 380, 296, 124, 66, "A′", "N × N", "#f0fdf4", "#16a34a");
        sb.AppendLine($"<text x=\"442\" y=\"386\" text-anchor=\"middle\" font-family=\"{Font}\" font-size=\"10\" fill=\"#64748b\">восстановленное изображение</text>");

        sb.AppendLine($"<text x=\"40\" y=\"330\" font-family=\"{Font}\" font-size=\"11\" fill=\"#475569\">Суммарное число коэффициентов</text>");
        sb.AppendLine($"<text x=\"40\" y=\"348\" font-family=\"{Font}\" font-size=\"11\" fill=\"#475569\">на каждом шаге остаётся равным N².</text>");
        return End(sb);
    }

    /// <summary>Раскладка четырёх матриц и оси координат.</summary>
    public static string QuadrantScheme()
    {
        const int w = 1000, h = 480;
        var sb = Begin(w, h, "Раскладка матриц");
        sb.AppendLine($"<text x=\"30\" y=\"28\" font-family=\"{Font}\" font-size=\"16\" font-weight=\"700\" fill=\"#0f172a\">Раскладка четырёх матриц уровня</text>");
        sb.AppendLine($"<text x=\"30\" y=\"47\" font-family=\"{Font}\" font-size=\"11\" fill=\"#64748b\">Вторая буква кода — ось X, третья — ось Y; L — низкие частоты, H — высокие</text>");

        int x0 = 190, y0 = 80, size = 340, half = size / 2;
        sb.AppendLine($"<rect x=\"{x0}\" y=\"{y0}\" width=\"{size}\" height=\"{size}\" fill=\"#f8fafc\" stroke=\"#334155\" stroke-width=\"2\"/>");
        sb.AppendLine($"<line x1=\"{x0 + half}\" y1=\"{y0}\" x2=\"{x0 + half}\" y2=\"{y0 + size}\" stroke=\"#334155\" stroke-width=\"1.5\"/>");
        sb.AppendLine($"<line x1=\"{x0}\" y1=\"{y0 + half}\" x2=\"{x0 + size}\" y2=\"{y0 + half}\" stroke=\"#334155\" stroke-width=\"1.5\"/>");

        Quadrant(sb, x0, y0, half, "ALL", "НЧ по X, НЧ по Y", "#dbeafe", "#1d4ed8");
        Quadrant(sb, x0 + half, y0, half, "AHL", "ВЧ по X, НЧ по Y", "#ffedd5", "#c2410c");
        Quadrant(sb, x0, y0 + half, half, "ALH", "НЧ по X, ВЧ по Y", "#dcfce7", "#15803d");
        Quadrant(sb, x0 + half, y0 + half, half, "AHH", "ВЧ по X, ВЧ по Y", "#fee2e2", "#b91c1c");

        // Оси.
        Arrow(sb, x0, y0 - 22, x0 + size, y0 - 22);
        sb.AppendLine($"<text x=\"{x0 + size}\" y=\"{y0 - 30}\" text-anchor=\"end\" font-family=\"{Font}\" font-size=\"12\" fill=\"#475569\">X →</text>");
        Arrow(sb, x0 - 26, y0, x0 - 26, y0 + size);
        sb.AppendLine($"<text x=\"{x0 - 34}\" y=\"{y0 + 12}\" text-anchor=\"end\" font-family=\"{Font}\" font-size=\"12\" fill=\"#475569\">↓ Y</text>");

        sb.AppendLine($"<text x=\"{x0 + size / 2}\" y=\"{y0 + size + 30}\" text-anchor=\"middle\" font-family=\"{Font}\" font-size=\"11\" fill=\"#64748b\">каждая матрица (N/2) × (N/2); всего N² коэффициентов</text>");
        sb.AppendLine($"<text x=\"610\" y=\"130\" font-family=\"{Font}\" font-size=\"12\" font-weight=\"700\" fill=\"#0f172a\">Что означает код</text>");
        sb.AppendLine($"<text x=\"610\" y=\"156\" font-family=\"{Font}\" font-size=\"11\" fill=\"#475569\">A — approximation (приближение)</text>");
        sb.AppendLine($"<text x=\"610\" y=\"178\" font-family=\"{Font}\" font-size=\"11\" fill=\"#475569\">L — low-pass (низкие частоты)</text>");
        sb.AppendLine($"<text x=\"610\" y=\"200\" font-family=\"{Font}\" font-size=\"11\" fill=\"#475569\">H — high-pass (высокие частоты)</text>");
        sb.AppendLine($"<text x=\"610\" y=\"238\" font-family=\"{Font}\" font-size=\"11\" fill=\"#475569\">ALL — крупные формы, яркость</text>");
        sb.AppendLine($"<text x=\"610\" y=\"260\" font-family=\"{Font}\" font-size=\"11\" fill=\"#475569\">AHL — вертикальные перепады</text>");
        sb.AppendLine($"<text x=\"610\" y=\"282\" font-family=\"{Font}\" font-size=\"11\" fill=\"#475569\">ALH — горизонтальные перепады</text>");
        sb.AppendLine($"<text x=\"610\" y=\"304\" font-family=\"{Font}\" font-size=\"11\" fill=\"#475569\">AHH — диагональные детали и шум</text>");
        return End(sb);
    }

    private static void Quadrant(StringBuilder sb, int x, int y, int size, string title, string subtitle, string fill, string stroke)
    {
        sb.AppendLine($"<rect x=\"{x + 4}\" y=\"{y + 4}\" width=\"{size - 8}\" height=\"{size - 8}\" rx=\"6\" fill=\"{fill}\"/>");
        sb.AppendLine($"<text x=\"{x + size / 2}\" y=\"{y + size / 2 - 8}\" text-anchor=\"middle\" font-family=\"{Font}\" font-size=\"24\" font-weight=\"700\" fill=\"{stroke}\">{title}</text>");
        sb.AppendLine($"<text x=\"{x + size / 2}\" y=\"{y + size / 2 + 16}\" text-anchor=\"middle\" font-family=\"{Font}\" font-size=\"10\" fill=\"{stroke}\">{subtitle}</text>");
    }

    /// <summary>Частотная лестница: полосы, соответствующие уровням разложения.</summary>
    public static string FrequencyLadder(int order)
    {
        order = Math.Clamp(order, 1, 6);
        const int w = 1000, h = 220;
        var sb = Begin(w, h, "Частотные полосы");
        sb.AppendLine($"<text x=\"30\" y=\"28\" font-family=\"{Font}\" font-size=\"16\" font-weight=\"700\" fill=\"#0f172a\">Частотные полосы уровней разложения</text>");
        sb.AppendLine($"<text x=\"30\" y=\"47\" font-family=\"{Font}\" font-size=\"11\" fill=\"#64748b\">1.0 — частота Найквиста; каждый уровень делит полосу пополам</text>");

        int left = 60, top = 80, width = 880, barH = 46;
        sb.AppendLine($"<rect x=\"{left}\" y=\"{top}\" width=\"{width}\" height=\"{barH}\" fill=\"#f8fafc\" stroke=\"#cbd5e1\"/>");

        double allHigh = Math.Pow(2, -order);
        int allWidth = (int)Math.Round(width * allHigh);
        sb.AppendLine($"<rect x=\"{left}\" y=\"{top}\" width=\"{allWidth}\" height=\"{barH}\" fill=\"#bfdbfe\"/>");
        if (allWidth >= 44)
            sb.AppendLine($"<text x=\"{left + allWidth / 2}\" y=\"{top + 28}\" text-anchor=\"middle\" font-family=\"{Font}\" font-size=\"11\" font-weight=\"700\" fill=\"#1e3a8a\">ALL{order}</text>");

        for (int level = order; level >= 1; level--)
        {
            double low = Math.Pow(2, -level);
            double high = 2 * low;
            int x = left + (int)Math.Round(width * low);
            int segment = (int)Math.Round(width * (high - low));
            string color = level % 2 == 0 ? "#fed7aa" : "#fef3c7";
            sb.AppendLine($"<rect x=\"{x}\" y=\"{top}\" width=\"{segment}\" height=\"{barH}\" fill=\"{color}\" stroke=\"#f59e0b\"/>");
            if (segment >= 42)
                sb.AppendLine($"<text x=\"{x + segment / 2}\" y=\"{top + 28}\" text-anchor=\"middle\" font-family=\"{Font}\" font-size=\"11\" fill=\"#92400e\">L{level}</text>");
        }

        sb.AppendLine($"<text x=\"{left}\" y=\"{top + barH + 24}\" font-family=\"{Font}\" font-size=\"11\" fill=\"#475569\">0</text>");
        sb.AppendLine($"<text x=\"{left + width}\" y=\"{top + barH + 24}\" text-anchor=\"end\" font-family=\"{Font}\" font-size=\"11\" fill=\"#475569\">1.0 (Найквист)</text>");
        sb.AppendLine($"<text x=\"{left}\" y=\"{top + barH + 60}\" font-family=\"{Font}\" font-size=\"11\" fill=\"#475569\">ALL{order} — приближение (низкие частоты), L1…L{order} — детали соответствующих уровней.</text>");
        return End(sb);
    }

    private static void Box(StringBuilder sb, int x, int y, int w, int h, string title, string subtitle,
        string fill, string stroke, string? extra = null)
    {
        sb.AppendLine($"<rect x=\"{x}\" y=\"{y}\" width=\"{w}\" height=\"{h}\" rx=\"10\" fill=\"{fill}\" stroke=\"{stroke}\" stroke-width=\"1.5\"/>");
        sb.AppendLine($"<text x=\"{x + w / 2}\" y=\"{y + (extra is null ? h / 2 + 2 : h / 2 - 6)}\" text-anchor=\"middle\" font-family=\"{Font}\" font-size=\"14\" font-weight=\"700\" fill=\"{stroke}\">{title}</text>");
        sb.AppendLine($"<text x=\"{x + w / 2}\" y=\"{y + (extra is null ? h / 2 + 20 : h / 2 + 12)}\" text-anchor=\"middle\" font-family=\"{Font}\" font-size=\"10\" fill=\"#475569\">{subtitle}</text>");
        if (extra is not null)
            sb.AppendLine($"<text x=\"{x + w / 2}\" y=\"{y + h / 2 + 30}\" text-anchor=\"middle\" font-family=\"{Font}\" font-size=\"10\" fill=\"#64748b\">{extra}</text>");
    }

    private static void Arrow(StringBuilder sb, int x1, int y1, int x2, int y2)
    {
        sb.AppendLine($"<line x1=\"{x1}\" y1=\"{y1}\" x2=\"{x2}\" y2=\"{y2}\" stroke=\"#64748b\" stroke-width=\"2\"/>");
        double angle = Math.Atan2(y2 - y1, x2 - x1);
        double size = 8;
        double a = angle;
        double xa = x2 - size * Math.Cos(a - 0.4);
        double ya = y2 - size * Math.Sin(a - 0.4);
        double xb = x2 - size * Math.Cos(a + 0.4);
        double yb = y2 - size * Math.Sin(a + 0.4);
        sb.AppendLine($"<polygon points=\"{x2},{y2} {xa:F1},{ya:F1} {xb:F1},{yb:F1}\" fill=\"#64748b\"/>");
    }

    private static void AppendAxes(StringBuilder sb, int left, int top, double plotW, double plotH, int w)
    {
        sb.AppendLine($"<line x1=\"{left}\" y1=\"{top + plotH:F1}\" x2=\"{w - 30}\" y2=\"{top + plotH:F1}\" stroke=\"#334155\" stroke-width=\"1.5\"/>");
        sb.AppendLine($"<line x1=\"{left}\" y1=\"{top}\" x2=\"{left}\" y2=\"{top + plotH:F1}\" stroke=\"#334155\" stroke-width=\"1.5\"/>");
    }

    private static void AppendLegend(StringBuilder sb, int x, int y, IReadOnlyList<(string Name, string Color)> items, string font)
    {
        double currentX = x;
        foreach (var (name, color) in items)
        {
            sb.AppendLine($"<rect x=\"{currentX:F1}\" y=\"{y - 9}\" width=\"11\" height=\"11\" rx=\"2\" fill=\"{color}\"/>");
            sb.AppendLine($"<text x=\"{currentX + 16:F1}\" y=\"{y}\" font-family=\"{font}\" font-size=\"11\" fill=\"#334155\">{Escape(name)}</text>");
            currentX += 28 + name.Length * 7.4;
        }
    }

    private static StringBuilder Begin(int width, int height, string title)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {width} {height}\" role=\"img\" aria-label=\"{Escape(title)}\">");
        sb.AppendLine($"<rect width=\"{width}\" height=\"{height}\" fill=\"#ffffff\"/>");
        return sb;
    }

    private static string End(StringBuilder sb)
    {
        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    private static string Escape(string value) => value
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;")
        .Replace("\"", "&quot;");
}
