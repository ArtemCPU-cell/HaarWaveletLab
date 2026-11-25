using System.Globalization;
using HaarWaveletLab.App;
using HaarWaveletLab.UI;

namespace HaarWaveletLab;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // Единый формат чисел во всём интерфейсе и отчёте (точка как разделитель).
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

        CommandLineOptions options;
        try
        {
            options = CommandLineOptions.Parse(args);
        }
        catch (Exception ex)
        {
            ConsoleHost.Prepare(false, null);
            Console.WriteLine("Ошибка параметров командной строки: " + ex.Message);
            Console.WriteLine();
            Console.WriteLine(CommandLineOptions.HelpText);
            return 2;
        }

        if (options.Mode == RunMode.Help)
        {
            ConsoleHost.Prepare(false, null);
            Console.WriteLine(CommandLineOptions.HelpText);
            return 0;
        }

        if (options.Mode is RunMode.Console or RunMode.Report or RunMode.SelfTest)
        {
            string logPath = Path.Combine(AppContext.BaseDirectory, "cli_output.log");
            ConsoleHost.Prepare(options.Mode == RunMode.Console, logPath);
            try
            {
                return options.Mode switch
                {
                    RunMode.Console => ConsoleMenu.Run(options),
                    RunMode.Report => HeadlessRunner.Run(options),
                    RunMode.SelfTest => SelfTest.Run(),
                    _ => 0
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine("ОШИБКА: " + ex.Message);
                Console.WriteLine(ex.StackTrace);
                return 1;
            }
        }

        try
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm(options));
            return 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Не удалось запустить программу:\n\n" + ex.Message,
                "Вейвлет-преобразование Хаара", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }
}
