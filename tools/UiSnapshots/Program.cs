// Renders each screen of the app with sample data to PNG files, so the UI can
// be reviewed without a Windows machine: dotnet run -- <output-dir> [dark]

using Avalonia;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Threading;
using Continuum;
using Continuum.Services;
using Continuum.ViewModels;
using Continuum.Views;

var output = args.Length > 0 ? args[0] : "snapshots";
var dark = args.Contains("dark");
Directory.CreateDirectory(output);

AppBuilder.Configure<App>()
    .UseSkia()
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
    .WithInterFont()
    .SetupWithoutStarting();

Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;

var main = new MainViewModel(new SamplePhoneLibrary());
var window = new MainWindow { DataContext = main, Width = 412, Height = 872 };
window.Show();

void Snap(string name)
{
    Dispatcher.UIThread.RunJobs();
    AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    // Let the cross-fade finish.
    for (var i = 0; i < 20; i++) { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Thread.Sleep(15); }
    var frame = window.CaptureRenderedFrame();
    var path = Path.Combine(output, $"{name}{(dark ? "-dark" : "")}.png");
    frame?.Save(path);
    Console.WriteLine(path);
}

main.GoHome();
Snap("1-home");

main.Home.Apps.First(a => a.Name == "Mensagens").Open.Execute(null);
Snap("2-mensagens");

((MessagesViewModel)main.Current).Items.First(i => i.IsGroup).Open.Execute(null);
Snap("3-conversa");

main.Home.Apps.First(a => a.Name == "Contactos").Open.Execute(null);
Snap("4-contactos");

((ContactsViewModel)main.Current).Sections[0].Items[0].Open.Execute(null);
Snap("5-contacto");

main.Home.Apps.First(a => a.Name == "Telefone").Open.Execute(null);
Snap("6-telefone");
