using Avalonia;
using Continuum.Services;

namespace Continuum;

internal static class Program
{
    /// <summary>
    /// "Continuum.exe --app mensagens" opens Mensagens as its own app (own window,
    /// taskbar button and icon); with no arguments it opens the launcher.
    /// </summary>
    [STAThread]
    public static void Main(string[] args)
    {
        var app = AppInfo.FromArguments(args);
        WindowsShell.SetAppId(app?.AppUserModelId ?? "Continuum");
        App.StartupApp = app;
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
