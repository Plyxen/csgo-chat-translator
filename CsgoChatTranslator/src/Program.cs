namespace CsgoChatTranslator;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var directory = Settings.DefaultDirectory;
        var configIndex = Array.IndexOf(args, "--config");
        if (configIndex >= 0 && configIndex + 1 < args.Length) directory = args[configIndex + 1];

        using var instance = new Mutex(true, @"Local\CsgoChatTranslator", out var isFirstInstance);
        if (!isFirstInstance) return;

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApp(Settings.Load(directory), args.Contains("--quiet")));
    }
}
