using HaarWaveletLab.Models;

namespace HaarWaveletLab.Core;

/// <summary>Сравнение восстановленного изображения с исходным.</summary>
public static class Metrics
{
    private const double C1 = 6.5025;   // (0.01 · 255)^2
    private const double C2 = 58.5225;  // (0.03 · 255)^2
    private const int SsimBlock = 8;

    public static QualityMetrics Compare(double[,] original, double[,] reconstructed)
    {
        int height = original.GetLength(0);
        int width = original.GetLength(1);
        int count = height * width;

        double mse = 0.0;
        double mae = 0.0;
        double maxError = 0.0;
        double sumA = 0.0;
        double sumB = 0.0;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                double a = PixelMath.Clamp(original[y, x]);
                double b = PixelMath.Clamp(reconstructed[y, x]);
                double d = a - b;
                mse += d * d;
                mae += Math.Abs(d);
                maxError = Math.Max(maxError, Math.Abs(d));
                sumA += a;
                sumB += b;
            }
        }

        mse /= count;
        mae /= count;
        double meanA = sumA / count;
        double meanB = sumB / count;

        double varA = 0.0;
        double varB = 0.0;
        double cov = 0.0;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                double da = PixelMath.Clamp(original[y, x]) - meanA;
                double db = PixelMath.Clamp(reconstructed[y, x]) - meanB;
                varA += da * da;
                varB += db * db;
                cov += da * db;
            }
        }

        varA /= count;
        varB /= count;
        cov /= count;

        double correlation = 1.0;
        double denominator = Math.Sqrt(varA * varB);
        if (denominator > 1e-12)
            correlation = Math.Clamp(cov / denominator, -1.0, 1.0);

        double psnr = mse <= 1e-10 ? double.PositiveInfinity : 10.0 * Math.Log10(255.0 * 255.0 / mse);
        double ssim = ComputeSsim(original, reconstructed);

        return new QualityMetrics(mse, psnr, ssim, mae, maxError, correlation);
    }

    /// <summary>Блочная оценка SSIM: среднее по неперекрывающимся блокам 8×8.</summary>
    private static double ComputeSsim(double[,] original, double[,] reconstructed)
    {
        int height = original.GetLength(0);
        int width = original.GetLength(1);
        double total = 0.0;
        int blocks = 0;

        for (int by = 0; by < height; by += SsimBlock)
        {
            for (int bx = 0; bx < width; bx += SsimBlock)
            {
                int h = Math.Min(SsimBlock, height - by);
                int w = Math.Min(SsimBlock, width - bx);
                int count = h * w;
                if (count == 0) continue;

                double meanA = 0.0;
                double meanB = 0.0;
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        meanA += PixelMath.Clamp(original[by + y, bx + x]);
                        meanB += PixelMath.Clamp(reconstructed[by + y, bx + x]);
                    }
                }

                meanA /= count;
                meanB /= count;

                double varA = 0.0;
                double varB = 0.0;
                double cov = 0.0;
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        double da = PixelMath.Clamp(original[by + y, bx + x]) - meanA;
                        double db = PixelMath.Clamp(reconstructed[by + y, bx + x]) - meanB;
                        varA += da * da;
                        varB += db * db;
                        cov += da * db;
                    }
                }

                varA /= count;
                varB /= count;
                cov /= count;

                double numerator = (2.0 * meanA * meanB + C1) * (2.0 * cov + C2);
                double denominator = (meanA * meanA + meanB * meanB + C1) * (varA + varB + C2);
                double ssim = denominator > 1e-12 ? numerator / denominator : 1.0;
                total += Math.Clamp(ssim, -1.0, 1.0);
                blocks++;
            }
        }

        return blocks > 0 ? total / blocks : 1.0;
    }
}
