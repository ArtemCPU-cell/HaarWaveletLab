using System.Runtime.InteropServices;
using System.Text;

namespace HaarWaveletLab.App;

/// <summary>
/// Подключение к консоли для программы с графической подсистемой Windows.
/// Позволяет запускать --console и --report из командной строки, хотя exe собран как WinExe.
/// </summary>
public static class ConsoleHost
{
    private const uint AttachParentProcess = 0xFFFFFFFF;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AllocConsole();

    public static void Prepare(bool allocateIfMissing, string? logPath)
    {
        bool attached = AttachConsole(AttachParentProcess);
        if (!attached && allocateIfMissing && !Console.IsOutputRedirected)
        {
            try { AllocConsole(); } catch { }
        }

        if (attached)
        {
            try
            {
                var stdout = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = true };
                Console.SetOut(stdout);
                var stderr = new StreamWriter(Console.OpenStandardError(), new UTF8Encoding(false)) { AutoFlush = true };
                Console.SetError(stderr);
                Console.SetIn(new StreamReader(Console.OpenStandardInput(), Encoding.UTF8));
            }
            catch
            {
                // Если стандартные потоки недоступны, остаёмся с тем, что есть.
            }
        }

        try
        {
            Console.OutputEncoding = new UTF8Encoding(false);
            Console.InputEncoding = Encoding.UTF8;
        }
        catch
        {
            // Кодировка может быть недоступна при перенаправленных потоках.
        }

        if (!string.IsNullOrWhiteSpace(logPath))
            InstallLog(logPath);
    }

    private static void InstallLog(string logPath)
    {
        try
        {
            string? folder = Path.GetDirectoryName(Path.GetFullPath(logPath));
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
            var file = new StreamWriter(logPath, append: false, new UTF8Encoding(false)) { AutoFlush = true };
            Console.SetOut(new TeeWriter(Console.Out, file));
        }
        catch
        {
            // Если лог создать не удалось, программа продолжает работать без него.
        }
    }

    /// <summary>Пишет одновременно в консоль и в файл журнала.</summary>
    private sealed class TeeWriter : TextWriter
    {
        private readonly TextWriter _console;
        private readonly TextWriter _file;
        private readonly object _lock = new();

        public TeeWriter(TextWriter console, TextWriter file)
        {
            _console = console;
            _file = file;
        }

        public override Encoding Encoding => _console.Encoding;

        public override void Write(char value)
        {
            lock (_lock)
            {
                Try(() => _console.Write(value));
                Try(() => _file.Write(value));
            }
        }

        public override void Write(string? value)
        {
            lock (_lock)
            {
                Try(() => _console.Write(value));
                Try(() => _file.Write(value));
            }
        }

        public override void WriteLine(string? value)
        {
            lock (_lock)
            {
                Try(() => _console.WriteLine(value));
                Try(() => _file.WriteLine(value));
            }
        }

        public override void Flush()
        {
            lock (_lock)
            {
                Try(_console.Flush);
                Try(_file.Flush);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Try(_console.Flush);
                Try(_file.Dispose);
            }

            base.Dispose(disposing);
        }

        private static void Try(Action action)
        {
            try { action(); } catch { }
        }
    }
}
