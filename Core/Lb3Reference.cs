using HaarWaveletLab.Models;

namespace HaarWaveletLab.Core;

/// <summary>Один эксперимент из Lb3_Haar.xlsx: обнуление части коэффициентов и обратное преобразование.</summary>
public sealed class Lb3Case
{
    public required string Title { get; init; }
    public required int[] ZeroedIndices { get; init; }
    public required double[,] Coefficients { get; init; }
    public required double[,] Reconstructed { get; init; }
    public required double OutputPower { get; init; }
    public required double ReferenceOutputPower { get; init; }
}

/// <summary>
/// Проверка программы по данным файла Lb3_Haar.xlsx: базис Хаара 8×8,
/// два окна по 8 отсчётов и три варианта обнуления коэффициентов.
/// </summary>
/// <remarks>
/// В файле Lb3_Haar.xlsx базисная и обратная матрицы записаны с округлением до 4 знаков.
/// Поэтому «эталонные» значения мощности Pвыхср немного отличаются от точного
/// ортонормированного преобразования — это объясняется в отчёте.
/// </remarks>
public sealed class Lb3Check
{
    public const double InputPower = 77.5;

    // Базисная матрица Хаара 8×8 из Lb3_Haar.xlsx (строки 1–8).
    private static readonly double[,] BasisMatrix =
    {
        { 0.3536, 0.3536, 0.3536, 0.3536, 0.3536, 0.3536, 0.3536, 0.3536 },
        { 0.3536, 0.3536, 0.3536, 0.3536, -0.3536, -0.3536, -0.3536, -0.3536 },
        { 0.5000, 0.5000, -0.5000, -0.5000, 0.0000, 0.0000, 0.0000, 0.0000 },
        { 0.0000, 0.0000, 0.0000, 0.0000, 0.5000, 0.5000, -0.5000, -0.5000 },
        { 0.7071, -0.7071, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000 },
        { 0.0000, 0.0000, 0.7071, -0.7071, 0.0000, 0.0000, 0.0000, 0.0000 },
        { 0.0000, 0.0000, 0.0000, 0.0000, 0.7071, -0.7071, 0.0000, 0.0000 },
        { 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.0000, 0.7071, -0.7071 }
    };

    // Обратная матрица Хаара из Lb3_Haar.xlsx (строки 19–26): транспонированный базис.
    private static readonly double[,] InverseMatrix =
    {
        { 0.3536, 0.3536, 0.5000, 0.0000, 0.7071, 0.0000, 0.0000, 0.0000 },
        { 0.3536, 0.3536, 0.5000, 0.0000, -0.7071, 0.0000, 0.0000, 0.0000 },
        { 0.3536, 0.3536, -0.5000, 0.0000, 0.0000, 0.7071, 0.0000, 0.0000 },
        { 0.3536, 0.3536, -0.5000, 0.0000, 0.0000, -0.7071, 0.0000, 0.0000 },
        { 0.3536, -0.3536, 0.0000, 0.5000, 0.0000, 0.0000, 0.7071, 0.0000 },
        { 0.3536, -0.3536, 0.0000, 0.5000, 0.0000, 0.0000, -0.7071, 0.0000 },
        { 0.3536, -0.3536, 0.0000, -0.5000, 0.0000, 0.0000, 0.0000, 0.7071 },
        { 0.3536, -0.3536, 0.0000, -0.5000, 0.0000, 0.0000, 0.0000, -0.7071 }
    };

    private static readonly double[,] SampleData =
    {
        { 0, 3, 1, 2, 7, 9, 4, 5 },
        { 6, 8, 15, 11, 13, 12, 10, 14 }
    };

    private Lb3Check()
    {
    }

    public static Lb3Check Create()
    {
        var check = new Lb3Check
        {
            Basis = (double[,])BasisMatrix.Clone(),
            InverseBasis = (double[,])InverseMatrix.Clone(),
            Samples = (double[,])SampleData.Clone()
        };

        var coefficients = new double[2, 8];
        for (int row = 0; row < 2; row++)
            for (int column = 0; column < 8; column++)
                for (int i = 0; i < 8; i++)
                    coefficients[row, column] += BasisMatrix[column, i] * SampleData[row, i];

        check.Coefficients = coefficients;
        check.ExactCoefficients = check.ComputeExactCoefficients();
        check.ReferenceCoefficients = new double[2, 8]
        {
            { 10.9616, -6.7184, 0.0, 3.5, -2.1213, -0.7071, -1.4142, -0.7071 },
            { 31.4704, -3.1824, -6.0, 0.5, -1.4142, 2.8284, 0.7071, -2.8284 }
        };

        check.Cases = new List<Lb3Case>
        {
            BuildCase(check, "Все коэффициенты", Array.Empty<int>(), 77.5383576102156),
            BuildCase(check, "Без 1 и 2 коэффициентов", new[] { 0, 1 }, 4.656187665597792),
            BuildCase(check, "Без 4 и 5 коэффициентов", new[] { 3, 4 }, 76.35087319381614),
            BuildCase(check, "Без последних двух коэффициентов", new[] { 6, 7 }, 76.85088398246269)
        };

        return check;
    }

    public double[,] Basis { get; private set; } = new double[8, 8];
    public double[,] InverseBasis { get; private set; } = new double[8, 8];
    public double[,] Samples { get; private set; } = new double[2, 8];
    public double[,] Coefficients { get; private set; } = new double[2, 8];
    public double[,] ExactCoefficients { get; private set; } = new double[2, 8];

    /// <summary>Коэффициенты, записанные в Lb3_Haar.xlsx (округлены до 4 знаков).</summary>
    public double[,] ReferenceCoefficients { get; private set; } = new double[2, 8];
    public IReadOnlyList<Lb3Case> Cases { get; private set; } = Array.Empty<Lb3Case>();

    /// <summary>Коэффициенты точного ортонормированного преобразования (без округления базиса).</summary>
    private double[,] ComputeExactCoefficients()
    {
        var result = new double[2, 8];
        for (int row = 0; row < 2; row++)
        {
            var values = new double[8];
            for (int i = 0; i < 8; i++) values[i] = SampleData[row, i];
            HaarTransform.Forward1D(values);
            for (int i = 0; i < 8; i++) result[row, i] = values[i];
        }

        return result;
    }

    private static Lb3Case BuildCase(Lb3Check check, string title, int[] zeroed, double referencePower)
    {
        var coeff = (double[,])check.Coefficients.Clone();
        foreach (int index in zeroed)
        {
            for (int row = 0; row < 2; row++) coeff[row, index] = 0.0;
        }

        var reconstructed = new double[2, 8];
        for (int row = 0; row < 2; row++)
            for (int sample = 0; sample < 8; sample++)
                for (int k = 0; k < 8; k++)
                    reconstructed[row, sample] += InverseMatrix[sample, k] * coeff[row, k];

        double power = 0.0;
        for (int row = 0; row < 2; row++)
            for (int sample = 0; sample < 8; sample++)
                power += reconstructed[row, sample] * reconstructed[row, sample];
        power /= 16.0;

        return new Lb3Case
        {
            Title = title,
            ZeroedIndices = zeroed,
            Coefficients = coeff,
            Reconstructed = reconstructed,
            OutputPower = power,
            ReferenceOutputPower = referencePower
        };
    }
}
