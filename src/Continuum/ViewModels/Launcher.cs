using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Continuum.Services;

namespace Continuum.ViewModels;

public sealed record LauncherItem(AppInfo App, Bitmap Icon, IRelayCommand Launch)
{
    public string Name => App.Name;
}

/// <summary>Opening Continuum.exe without arguments: a Launchpad-like grid of the apps.</summary>
public sealed partial class LauncherViewModel : ObservableObject
{
    public LauncherViewModel(AppServices services)
    {
        Apps = AppInfo.All
            .Select(a => new LauncherItem(a, AppIcons.Render(a, 128), new RelayCommand(() => WindowsShell.Launch(a))))
            .ToList();
        Source = services.Library is SamplePhoneLibrary
            ? "A mostrar dados de exemplo. Liga o iPhone e abre o Continuum.iPhone.exe para copiar os teus."
            : "Dados do iPhone: " + services.Library.Source;
    }

    public IReadOnlyList<LauncherItem> Apps { get; }
    public string Source { get; }

    [ObservableProperty] private string? _status;

    [RelayCommand]
    void InstallShortcuts()
    {
        try
        {
            WindowsShell.CreateStartMenuShortcuts(AppIcons.RenderPng);
            Status = "Atalhos criados no menu Iniciar, na pasta Continuum.";
        }
        catch (Exception ex)
        {
            Status = "Não foi possível criar os atalhos: " + ex.Message;
        }
    }
}
