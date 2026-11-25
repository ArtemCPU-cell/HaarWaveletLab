using System.Globalization;

namespace HaarWaveletLab.Models;

/// <summary>Метрики качества восстановленного изображения относительно исходного.</summary>
public readonly record struct QualityMetrics(
    double Mse,
    double Psnr,
    double Ssim,
    double Mae,
    double MaxError,
    double Correlation)
{
    public static readonly QualityMetrics Perfect = new(0, double.PositiveInfinity, 1, 0, 0, 1);

    public bool IsPerfect => Mse <= 1e-9;

    public string PsnrText => double.IsPositiveInfinity(Psnr)
        ? "∞"
        : Psnr.ToString("F2", CultureInfo.InvariantCulture);

    /// <summary>Качественная оценка по PSNR — используется в автоматических выводах.</summary>
    public string QualityPhrase
    {
        get
        {
            if (IsPerfect) return "восстановление без потерь";
            if (Psnr >= 40) return "визуально неотличимо от исходного";
            if (Psnr >= 32) return "различие заметно только при внимательном сравнении";
            if (Psnr >= 26) return "заметная потеря мелких деталей";
            if (Psnr >= 20) return "существенная деградация деталей";
            if (Psnr >= 14) return "сильная деградация изображения";
            return "почти полная потеря изображения";
        }
    }
}
