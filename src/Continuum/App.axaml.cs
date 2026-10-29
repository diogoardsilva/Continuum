using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Continuum.Services;
using Continuum.ViewModels;
using Continuum.Views;

namespace Continuum;

public partial class App : Application
{
    public static AppInfo? StartupApp { get; set; }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var services = AppServices.LoadDefault();
            desktop.MainWindow = CreateWindow(StartupApp, services);
            desktop.Exit += (_, _) => services.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(2));
        }
        base.OnFrameworkInitializationCompleted();
    }

    public static Window CreateWindow(AppInfo? app, AppServices services)
    {
        if (app is null) return new LauncherWindow { DataContext = new LauncherViewModel(services) };

        object content = app.Kind switch
        {
            AppKind.Messages => new MessagesAppViewModel(services),
            AppKind.Contacts => new ContactsAppViewModel(services),
            AppKind.Photos => new PhotosAppViewModel(services),
            AppKind.Phone => new PhoneAppViewModel(services),
            AppKind.FaceTime => new FaceTimeAppViewModel(services),
            _ => throw new ArgumentOutOfRangeException(nameof(app)),
        };
        return new AppWindow
        {
            Title = app.Name,
            Icon = new WindowIcon(AppIcons.Render(app, 64)),
            DataContext = content,
        };
    }
}
