using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Continuum.Services;

namespace Continuum.ViewModels;

public abstract class PageViewModel : ObservableObject
{
    protected PageViewModel(MainViewModel main) => Main = main;
    public MainViewModel Main { get; }
    public abstract string Title { get; }
    /// <summary>Label of the iOS back button ("Mensagens", "Contactos"...).</summary>
    public string? BackLabel { get; set; }
}

public sealed partial class MainViewModel : ObservableObject
{
    readonly ObservableCollection<PageViewModel> _stack = new();

    public MainViewModel(IPhoneLibrary library)
    {
        Library = library;
        Home = new HomeViewModel(this);
    }

    public IPhoneLibrary Library { get; }
    public HomeViewModel Home { get; }
    public Settings Settings { get; } = Settings.Load();

    Task<ICloudBridge>? _bridge;

    /// <summary>Starts Continuum.iCloud.exe the first time an app needs iCloud.com.</summary>
    public Task<ICloudBridge> GetICloudAsync() => _bridge ??= ICloudBridge.StartAsync();

    public async ValueTask ShutdownAsync()
    {
        if (_bridge is { IsCompletedSuccessfully: true }) await _bridge.Result.DisposeAsync();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHome), nameof(CanGoBack))]
    private ObservableObject _current = null!;

    public bool IsHome => Current is HomeViewModel;
    public bool CanGoBack => _stack.Count > 1;

    public void Open(PageViewModel page, bool asRoot = false)
    {
        if (asRoot) _stack.Clear();
        page.BackLabel = _stack.LastOrDefault()?.Title;
        _stack.Add(page);
        Current = page;
        OnPropertyChanged(nameof(CanGoBack));
    }

    [RelayCommand]
    public void Back()
    {
        if (_stack.Count <= 1) { GoHome(); return; }
        _stack.RemoveAt(_stack.Count - 1);
        Current = _stack[^1];
        OnPropertyChanged(nameof(CanGoBack));
    }

    [RelayCommand]
    public void GoHome()
    {
        _stack.Clear();
        Current = Home;
        OnPropertyChanged(nameof(CanGoBack));
    }
}

public sealed record AppTile(string Name, string Icon, string Color, IRelayCommand Open);

public sealed class HomeViewModel : ObservableObject
{
    public HomeViewModel(MainViewModel main)
    {
        Apps = new List<AppTile>
        {
            new("Telefone", Icons.Phone, "#34C759", new RelayCommand(() => main.Open(new PhoneViewModel(main), asRoot: true))),
            new("Mensagens", Icons.Message, "#30D158", new RelayCommand(() => main.Open(new MessagesViewModel(main), asRoot: true))),
            new("Contactos", Icons.Person, "#8E8E93", new RelayCommand(() => main.Open(new ContactsViewModel(main), asRoot: true))),
            new("Fotos", Icons.Flower, "#FF9F0A", new RelayCommand(() => main.Open(new PhotosViewModel(main), asRoot: true))),
        };
        Source = main.Library.Source;
    }

    public IReadOnlyList<AppTile> Apps { get; }
    public string Source { get; }
    public string Today => Format.LongDay(DateTime.Now);
    public string Time => DateTime.Now.ToString("HH:mm");
}
