namespace QuickTranslator;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(true, @"Local\QuickTranslator.SingleInstance", out bool isFirstInstance);
        using var showRequest = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\QuickTranslator.Show");

        if (!isFirstInstance)
        {
            showRequest.Set(); // ask the running instance to show its window
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.SetColorMode(SystemColorMode.System); // follow the Windows light/dark theme
        Application.Run(new TrayApp(showRequest));
    }
}
