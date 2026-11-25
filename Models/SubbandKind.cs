namespace HaarWaveletLab.Models;

/// <summary>Четыре матрицы одного порядка разложения.</summary>
public enum SubbandKind
{
    /// <summary>Приближение: низкие частоты по обеим осям (ALL).</summary>
    All = 0,

    /// <summary>Низкие частоты по X, высокие по Y (ALH) — горизонтальные перепады.</summary>
    Alh = 1,

    /// <summary>Высокие частоты по X, низкие по Y (AHL) — вертикальные перепады.</summary>
    Ahl = 2,

    /// <summary>Высокие частоты по обеим осям (AHH) — диагональные детали.</summary>
    Ahh = 3
}

/// <summary>Подписи и пояснения к матрицам. Используются в интерфейсе, отчёте и CSV.</summary>
public static class Subbands
{
    public static readonly SubbandKind[] All = { SubbandKind.All, SubbandKind.Alh, SubbandKind.Ahl, SubbandKind.Ahh };

    public static readonly SubbandKind[] Details = { SubbandKind.Alh, SubbandKind.Ahl, SubbandKind.Ahh };

    public static string Code(this SubbandKind kind) => kind switch
    {
        SubbandKind.All => "ALL",
        SubbandKind.Alh => "ALH",
        SubbandKind.Ahl => "AHL",
        SubbandKind.Ahh => "AHH",
        _ => "?"
    };

    /// <summary>Краткое название для интерфейса.</summary>
    public static string Title(this SubbandKind kind) => kind switch
    {
        SubbandKind.All => "Приближение (ALL)",
        SubbandKind.Alh => "Детали ALH",
        SubbandKind.Ahl => "Детали AHL",
        SubbandKind.Ahh => "Детали AHH",
        _ => "?"
    };

    /// <summary>Какие частоты содержит матрица.</summary>
    public static string Axes(this SubbandKind kind) => kind switch
    {
        SubbandKind.All => "НЧ по X, НЧ по Y",
        SubbandKind.Alh => "НЧ по X, ВЧ по Y",
        SubbandKind.Ahl => "ВЧ по X, НЧ по Y",
        SubbandKind.Ahh => "ВЧ по X, ВЧ по Y",
        _ => "?"
    };

    /// <summary>Что именно теряется при удалении матрицы.</summary>
    public static string Meaning(this SubbandKind kind) => kind switch
    {
        SubbandKind.All =>
            "средняя яркость и крупные формы изображения; в этой матрице сосредоточена основная энергия",
        SubbandKind.Alh =>
            "горизонтальные перепады яркости: горизонтальные линии и границы, вытянутые по горизонтали",
        SubbandKind.Ahl =>
            "вертикальные перепады яркости: вертикальные линии и границы, вытянутые по вертикали",
        SubbandKind.Ahh =>
            "самые мелкие диагональные детали, текстуры и шум",
        _ => ""
    };

    /// <summary>Краткая роль матрицы одной строкой.</summary>
    public static string Role(this SubbandKind kind) => kind switch
    {
        SubbandKind.All => "низкочастотное приближение",
        SubbandKind.Alh => "горизонтальные детали",
        SubbandKind.Ahl => "вертикальные детали",
        SubbandKind.Ahh => "диагональные детали",
        _ => ""
    };

    /// <summary>Доля полосы частот (в единицах Найквиста) для матрицы уровня level.</summary>
    public static (double Low, double High) FrequencyBand(this SubbandKind kind, int level)
    {
        double scale = Math.Pow(2, -level);
        return kind == SubbandKind.All
            ? (0.0, scale)
            : (scale, 2.0 * scale);
    }
}

/// <summary>Встроенные тестовые изображения.</summary>
public enum TestImageKind
{
    TestCard = 0,
    ColorBars = 1,
    Geometry = 2,
    Portrait = 3,
    Texture = 4
}

public static class TestImagesInfo
{
    public static readonly TestImageKind[] All =
    {
        TestImageKind.TestCard, TestImageKind.ColorBars, TestImageKind.Geometry,
        TestImageKind.Portrait, TestImageKind.Texture
    };

    public static string Title(this TestImageKind kind) => kind switch
    {
        TestImageKind.TestCard => "Тестовая таблица",
        TestImageKind.ColorBars => "Цветные полосы (яркость)",
        TestImageKind.Geometry => "Геометрия и линии",
        TestImageKind.Portrait => "Схематичный портрет",
        TestImageKind.Texture => "Градиент и текстура",
        _ => "Изображение"
    };

    public static string Description(this TestImageKind kind) => kind switch
    {
        TestImageKind.TestCard => "испытательная таблица: сетка, круги, миры разной частоты, градации серого",
        TestImageKind.ColorBars => "восемь вертикальных полос со стандартными уровнями яркости Y",
        TestImageKind.Geometry => "крупные фигуры, тонкие линии и точки — разный пространственный масштаб",
        TestImageKind.Portrait => "мягкие градиенты и мелкие детали лица",
        TestImageKind.Texture => "плавный градиент с периодической текстурой и шумом",
        _ => ""
    };

    public static string CommandId(this TestImageKind kind) => kind switch
    {
        TestImageKind.TestCard => "testcard",
        TestImageKind.ColorBars => "bars",
        TestImageKind.Geometry => "geometry",
        TestImageKind.Portrait => "portrait",
        TestImageKind.Texture => "texture",
        _ => "testcard"
    };

    public static bool TryParseCommandId(string? value, out TestImageKind kind)
    {
        foreach (var item in All)
        {
            if (string.Equals(item.CommandId(), value, StringComparison.OrdinalIgnoreCase))
            {
                kind = item;
                return true;
            }
        }

        kind = TestImageKind.TestCard;
        return false;
    }
}
