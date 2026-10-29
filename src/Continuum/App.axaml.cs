using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Continuum.Services;
using Continuum.ViewModels;
using Continuum.Views;

namespace Continuum;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            IPhoneLibrary library = BackupPhoneLibrary.TryOpenDefault() as IPhoneLibrary ?? new SamplePhoneLibrary();
            var main = new MainViewModel(library);
            main.GoHome();
            desktop.MainWindow = new MainWindow { DataContext = main };
        }
        base.OnFrameworkInitializationCompleted();
    }
}
