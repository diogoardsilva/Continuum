using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Continuum.Services;

namespace Continuum.ViewModels;

public enum PhotosState { SignedOut, Code, Busy, Ready, Unavailable }

public sealed partial class PhotoTile : ObservableObject
{
    public PhotoTile(ICloudPhoto photo) => Photo = photo;

    public ICloudPhoto Photo { get; }
    public bool IsVideo => Photo.Type == "movie";

    [ObservableProperty]
    private Bitmap? _thumbnail;
}

/// <summary>Fotos: signs in to iCloud.com through the bridge and shows the library.</summary>
public sealed partial class PhotosAppViewModel : ObservableObject
{
    const int PageSize = 200;
    const int ParallelThumbnails = 4;

    readonly AppServices _services;

    public PhotosAppViewModel(AppServices services)
    {
        _services = services;
        _appleId = services.Settings.AppleId ?? "";
        if (!ICloudBridge.IsAvailable)
        {
            State = PhotosState.Unavailable;
            Message = "Falta o Continuum.iCloud.exe ao lado do Continuum.exe.";
        }
        else if (_appleId.Length > 0)
        {
            _ = RunAsync(async bridge =>
            {
                var status = await bridge.ResumeAsync(_appleId);
                if (status.IsOk) await LoadAsync(bridge, status.Name);
                else State = PhotosState.SignedOut;
            });
        }
    }

    public ObservableCollection<PhotoTile> Photos { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSignedOut), nameof(NeedsCode), nameof(IsBusy), nameof(IsReady), nameof(IsUnavailable))]
    private PhotosState _state = PhotosState.SignedOut;

    public bool IsSignedOut => State == PhotosState.SignedOut;
    public bool NeedsCode => State == PhotosState.Code;
    public bool IsBusy => State == PhotosState.Busy;
    public bool IsReady => State == PhotosState.Ready;
    public bool IsUnavailable => State == PhotosState.Unavailable;

    [ObservableProperty] private string _appleId;
    [ObservableProperty] private string _password = "";
    [ObservableProperty] private string _code = "";
    [ObservableProperty] private string? _message;
    [ObservableProperty] private string? _accountName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsViewing))]
    private PhotoTile? _viewing;

    [ObservableProperty] private Bitmap? _viewingImage;

    public bool IsViewing => Viewing is not null;

    [RelayCommand]
    Task SignIn() => RunAsync(async bridge =>
    {
        var status = await bridge.LoginAsync(AppleId.Trim(), Password);
        Password = "";
        await Handle(bridge, status);
    });

    [RelayCommand]
    Task SubmitCode() => RunAsync(async bridge =>
    {
        var status = await bridge.SubmitCodeAsync(Code.Trim());
        Code = "";
        await Handle(bridge, status);
    });

    [RelayCommand]
    async Task Open(PhotoTile tile)
    {
        Viewing = tile;
        ViewingImage = tile.Thumbnail;
        var bridge = await _services.GetICloudAsync();
        // "medium" is a JPEG; originals are often HEIC, which Windows cannot decode without extensions.
        var version = tile.Photo.Versions.Contains("medium") ? "medium" : "thumb";
        var image = Images.TryDecode(await bridge.GetPhotoAsync(tile.Photo.Id, version));
        if (Viewing == tile && image is not null) ViewingImage = image;
    }

    [RelayCommand]
    void Close()
    {
        Viewing = null;
        ViewingImage = null;
    }

    async Task Handle(ICloudBridge bridge, BridgeStatus status)
    {
        if (status.NeedsCode)
        {
            State = PhotosState.Code;
            Message = status.Delivery == "sms"
                ? "Escreve o código que recebeste por SMS."
                : "Escreve o código de 6 dígitos que apareceu no teu iPhone.";
        }
        else if (status.IsOk)
        {
            _services.Settings.AppleId = AppleId.Trim();
            _services.Settings.Save();
            await LoadAsync(bridge, status.Name);
        }
        else
        {
            State = PhotosState.SignedOut;
            Message = status.Message;
        }
    }

    async Task LoadAsync(ICloudBridge bridge, string? name)
    {
        AccountName = name;
        State = PhotosState.Busy;
        Message = "A carregar a fototeca…";
        var photos = await bridge.GetPhotosAsync(0, PageSize);
        Photos.Clear();
        foreach (var p in photos) Photos.Add(new PhotoTile(p));
        State = PhotosState.Ready;
        Message = null;

        using var gate = new SemaphoreSlim(ParallelThumbnails);
        await Task.WhenAll(Photos.ToList().Select(async tile =>
        {
            await gate.WaitAsync();
            try
            {
                var bitmap = Images.TryDecode(await bridge.GetPhotoAsync(tile.Photo.Id, "thumb"), 320);
                await Dispatcher.UIThread.InvokeAsync(() => tile.Thumbnail = bitmap);
            }
            finally
            {
                gate.Release();
            }
        }));
    }

    async Task RunAsync(Func<ICloudBridge, Task> action)
    {
        var previous = State;
        State = PhotosState.Busy;
        try
        {
            await action(await _services.GetICloudAsync());
        }
        catch (Exception ex)
        {
            State = previous == PhotosState.Busy ? PhotosState.SignedOut : previous;
            Message = "Não foi possível falar com o iCloud: " + ex.Message;
        }
    }
}
