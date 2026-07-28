using System.Runtime.InteropServices;
using System.Text;

namespace SpiritVale.ChinesePatch.Installer;

internal static class StartupDiagnostics
{
    private const uint MbIconError = 0x00000010;
    private const uint MbOk = 0x00000000;
    private static readonly object LogLock = new();
    private static int _fatalReportStarted;
    private static int _uiThreadFatalOccurred;
    private static string? _logPath;

    internal static bool UiThreadFatalOccurred =>
        Volatile.Read(ref _uiThreadFatalOccurred) != 0;

    internal static void Initialize(string[] args)
    {
        try
        {
            _logPath = PrepareLogPath();
            Write("------------------------------------------------------------");
            Write($"Installer {PatchInfo.Version} managed entry reached.");
            Write($"Executable: {Environment.ProcessPath ?? "<unknown>"}");
            Write($"Base directory: {AppContext.BaseDirectory}");
            Write($"Current directory: {Environment.CurrentDirectory}");
            Write($"OS: {RuntimeInformation.OSDescription}");
            Write($"Framework: {RuntimeInformation.FrameworkDescription}");
            Write(
                $"Architecture: process={RuntimeInformation.ProcessArchitecture}; " +
                $"OS={RuntimeInformation.OSArchitecture}; " +
                $"64-bit process={Environment.Is64BitProcess}; " +
                $"64-bit OS={Environment.Is64BitOperatingSystem}");
            Write($"Processor: {Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "<unknown>"}");
            Write($"Temp directory: {Path.GetTempPath()}");
            Write($"Argument count: {args.Length}");

            AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
            {
                var exception = eventArgs.ExceptionObject as Exception ??
                    new InvalidOperationException(
                        $"Unhandled non-Exception object: {eventArgs.ExceptionObject}");
                ReportFatal("An unhandled installer error occurred.", exception, showDialog: true);
            };

            TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
                WriteException("An unobserved task exception was reported.", eventArgs.Exception);
        }
        catch
        {
            // Diagnostics must never prevent the installer from starting.
        }
    }

    internal static void Write(string message)
    {
        var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} " +
            $"[PID {Environment.ProcessId}] {message}{Environment.NewLine}";

        lock (LogLock)
        {
            if (string.IsNullOrWhiteSpace(_logPath)) return;

            try
            {
                using var stream = new FileStream(
                    _logPath,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.ReadWrite);
                using var writer = new StreamWriter(stream, new UTF8Encoding(false));
                writer.Write(line);
            }
            catch
            {
                // A logging failure must not mask the original startup failure.
            }
        }
    }

    internal static void ReportUiThreadFatal(Exception exception)
    {
        Interlocked.Exchange(ref _uiThreadFatalOccurred, 1);
        ReportFatal("The installer interface encountered a fatal error.", exception, showDialog: true);

        try
        {
            Application.Exit();
        }
        catch
        {
            // The UI may already be tearing down.
        }
    }

    internal static void ReportFatal(string context, Exception exception, bool showDialog)
    {
        WriteException(context, exception);
        if (!showDialog || Interlocked.Exchange(ref _fatalReportStarted, 1) != 0) return;

        var logLocation = string.IsNullOrWhiteSpace(_logPath)
            ? "启动日志无法写入。"
            : $"启动日志：\r\n{_logPath}";
        var message =
            "SpiritVale 汉化安装器无法启动。\r\n\r\n" +
            "请把启动日志发送给汉化补丁维护者。" +
            "安装器需要 64 位 Windows 10 或 Windows 11。\r\n\r\n" +
            $"{logLocation}\r\n\r\n" +
            $"错误：{exception.GetType().Name}: {exception.Message}";

        try
        {
            MessageBoxW(IntPtr.Zero, message, "SpiritVale Chinese Patch", MbOk | MbIconError);
        }
        catch
        {
            // There is no safer user-visible fallback in a WinExe process.
        }
    }

    private static void WriteException(string context, Exception exception)
    {
        Write(context);
        Write(exception.ToString());
    }

    private static string? PrepareLogPath()
    {
        var candidates = new List<string>();
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            candidates.Add(Path.Combine(
                localAppData,
                "auryx",
                "SpiritValeChinesePatch",
                "Logs",
                "installer-startup.log"));
        }

        try
        {
            candidates.Add(Path.Combine(Path.GetTempPath(), "SpiritVale_Chinese_Patch_startup.log"));
        }
        catch
        {
            // The process may have an invalid or inaccessible temporary directory.
        }

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var directory = Path.GetDirectoryName(candidate);
                if (string.IsNullOrWhiteSpace(directory)) continue;
                Directory.CreateDirectory(directory);
                using var stream = new FileStream(
                    candidate,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.ReadWrite);
                return candidate;
            }
            catch
            {
                // Try the next per-user writable location.
            }
        }

        return null;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int MessageBoxW(
        IntPtr windowHandle,
        string text,
        string caption,
        uint type);
}
