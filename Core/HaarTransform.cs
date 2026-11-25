using HaarWaveletLab.Models;

namespace HaarWaveletLab.Core;

/// <summary>Прямое и обратное двумерное преобразование Хаара (ортонормированный базис).</summary>
/// <remarks>
/// Одномерное преобразование пары (a, b):
///   приближение = (a + b) / √2,  детали = (a − b) / √2.
/// Двумерное преобразование выполняется разделимо: сначала по строкам (ось X),
/// затем по столбцам (ось Y). Многократное разложение применяется только к матрице
/// приближения ALL, поэтому в левом верхнем углу N×N остаётся «пирамида» коэффициентов.
/// </remarks>
public static class HaarTransform
{
    private const double InvSqrt2 = 0.70710678118654752440;

    /// <summary>Прямое одномерное преобразование на месте; результат — [приближения | детали].</summary>
    public static void Forward1D(Span<double> data)
    {
        int n = data.Length;
        if (n < 2) return;

        var temp = new double[n];
        for (int length = n; length >= 2; length >>= 1)
        {
            int half = length >> 1;
            for (int i = 0; i < half; i++)
            {
                double a = data[2 * i];
                double b = data[2 * i + 1];
                temp[i] = (a + b) * InvSqrt2;
                temp[half + i] = (a - b) * InvSqrt2;
            }

            for (int i = 0; i < length; i++) data[i] = temp[i];
        }
    }

    /// <summary>Обратное одномерное преобразование на месте.</summary>
    public static void Inverse1D(Span<double> data)
    {
        int n = data.Length;
        if (n < 2) return;

        var temp = new double[n];
        for (int length = 2; length <= n; length <<= 1)
        {
            int half = length >> 1;
            for (int i = 0; i < half; i++)
            {
                double approx = data[i];
                double detail = data[half + i];
                temp[2 * i] = (approx + detail) * InvSqrt2;
                temp[2 * i + 1] = (approx - detail) * InvSqrt2;
            }

            for (int i = 0; i < length; i++) data[i] = temp[i];
        }
    }

    /// <summary>Один уровень прямого преобразования блока size×size в левом верхнем углу.</summary>
    public static void ForwardLevel(double[,] coefficients, int imageSize, int level)
    {
        int block = imageSize >> (level - 1);
        if (block < 2) return;

        ForwardRows(coefficients, block);
        ForwardColumns(coefficients, block);
    }

    /// <summary>Один уровень обратного преобразования блока size×size в левом верхнем углу.</summary>
    public static void InverseLevel(double[,] coefficients, int imageSize, int level)
    {
        int block = imageSize >> (level - 1);
        if (block < 2) return;

        // Прямое преобразование: строки → столбцы. Обратное — в противоположном порядке.
        InverseColumns(coefficients, block);
        InverseRows(coefficients, block);
    }

    /// <summary>Полное обратное преобразование пирамиды, разложенной до order уровней.</summary>
    public static double[,] Reconstruct(double[,] coefficients, int order)
    {
        int n = coefficients.GetLength(0);
        var result = (double[,])coefficients.Clone();
        order = Math.Clamp(order, 1, Math.Max(1, SubbandGeometry.MaxOrder(n)));

        for (int level = order; level >= 1; level--)
            InverseLevel(result, n, level);

        return result;
    }

    private static void ForwardRows(double[,] m, int block)
    {
        var row = new double[block];
        for (int r = 0; r < block; r++)
        {
            for (int c = 0; c < block; c++) row[c] = m[r, c];
            ForwardStep(row);
            for (int c = 0; c < block; c++) m[r, c] = row[c];
        }
    }

    private static void ForwardColumns(double[,] m, int block)
    {
        var column = new double[block];
        for (int c = 0; c < block; c++)
        {
            for (int r = 0; r < block; r++) column[r] = m[r, c];
            ForwardStep(column);
            for (int r = 0; r < block; r++) m[r, c] = column[r];
        }
    }

    private static void InverseColumns(double[,] m, int block)
    {
        var column = new double[block];
        for (int c = 0; c < block; c++)
        {
            for (int r = 0; r < block; r++) column[r] = m[r, c];
            InverseStep(column);
            for (int r = 0; r < block; r++) m[r, c] = column[r];
        }
    }

    private static void InverseRows(double[,] m, int block)
    {
        var row = new double[block];
        for (int r = 0; r < block; r++)
        {
            for (int c = 0; c < block; c++) row[c] = m[r, c];
            InverseStep(row);
            for (int c = 0; c < block; c++) m[r, c] = row[c];
        }
    }

    /// <summary>Один шаг прямого преобразования: пары (a, b) → приближение и деталь.</summary>
    private static void ForwardStep(Span<double> data)
    {
        int half = data.Length / 2;
        var temp = new double[data.Length];
        for (int i = 0; i < half; i++)
        {
            double a = data[2 * i];
            double b = data[2 * i + 1];
            temp[i] = (a + b) * InvSqrt2;
            temp[half + i] = (a - b) * InvSqrt2;
        }

        for (int i = 0; i < data.Length; i++) data[i] = temp[i];
    }

    /// <summary>Один шаг обратного преобразования: приближение и деталь → пара (a, b).</summary>
    private static void InverseStep(Span<double> data)
    {
        int half = data.Length / 2;
        var temp = new double[data.Length];
        for (int i = 0; i < half; i++)
        {
            double approx = data[i];
            double detail = data[half + i];
            temp[2 * i] = (approx + detail) * InvSqrt2;
            temp[2 * i + 1] = (approx - detail) * InvSqrt2;
        }

        for (int i = 0; i < data.Length; i++) data[i] = temp[i];
    }
}
