// Renders each app window with sample data to PNG files, so the UI can be
// reviewed without a Windows machine: dotnet run -- <output-dir> [dark]

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Threading;
using Continuum;
using Continuum.Services;
using Continuum.ViewModels;

var output = args.Length > 0 ? args[0] : "snapshots";
var dark = args.Contains("dark");
Directory.CreateDirectory(output);

AppBuilder.Configure<App>()
    .UseSkia()
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
    .WithInterFont()
    .SetupWithoutStarting();

Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
var services = new AppServices(new SamplePhoneLibrary(), new Settings());

void Snap(Window window, string name)
{
    window.Show();
    for (var i = 0; i < 10; i++) { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Thread.Sleep(10); }
    var path = Path.Combine(output, $"{name}{(dark ? "-dark" : "")}.png");
    window.CaptureRenderedFrame()?.Save(path);
    window.Close();
    Console.WriteLine(path);
}

Snap(App.CreateWindow(null, services), "0-launcher");
foreach (var app in AppInfo.All)
{
    var window = App.CreateWindow(app, services);
    if (window.DataContext is PhotosAppViewModel photos)
    {
        // There is no bridge on the build machine; show the sign-in form anyway.
        photos.State = PhotosState.SignedOut;
        photos.Message = null;
    }
    Snap(window, app.Argument);
}
