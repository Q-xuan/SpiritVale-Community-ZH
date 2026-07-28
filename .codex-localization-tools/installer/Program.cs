namespace SpiritVale.ChinesePatch.Installer;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var isSelfTest = args.Length >= 1 &&
            args[0].Equals("--self-test", StringComparison.OrdinalIgnoreCase);
        StartupDiagnostics.Initialize(args);

        try
        {
            if (isSelfTest)
            {
                var result = SelfTest.Run(args.Length >= 2 ? args[1] : null);
                StartupDiagnostics.Write($"Self-test finished with exit code {result}.");
                return result;
            }

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, eventArgs) =>
                StartupDiagnostics.ReportUiThreadFatal(eventArgs.Exception);

            StartupDiagnostics.Write("Initializing Windows Forms.");
            ApplicationConfiguration.Initialize();
            StartupDiagnostics.Write("Creating the main window.");
            Application.Run(new MainForm());
            StartupDiagnostics.Write("The main window closed normally.");
            return StartupDiagnostics.UiThreadFatalOccurred ? 1 : 0;
        }
        catch (Exception exception)
        {
            StartupDiagnostics.ReportFatal(
                "The installer could not start.",
                exception,
                showDialog: !isSelfTest);
            return 1;
        }
    }
}
