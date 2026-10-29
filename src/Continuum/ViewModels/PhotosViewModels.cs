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
    public PhotoTile(ICloudPhoto photo, IRelayCommand open)
    {
        Photo = photo;
        Open = open;
    }

    public ICloudPhoto Photo { get; }
    public IRelayCommand Open { get; }
    public bool IsVideo => Photo.Type == "movie";

    [ObservableProperty]
    private Bitmap? _thumbnail;
}

public sealed partial class PhotosViewModel : PageViewModel
{
    const int PageSize = 120;
    const int ParallelThumbnails = 4;

    public PhotosViewModel(MainViewModel main) : base(main)
    {
        _appleId = main.Settings.AppleId ?? "";
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
                if (status.IsOk) await LoadAsync(bridge);
                else State = PhotosState.SignedOut;
            });
        }
    }

    public override string Title => "Fotos";
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
            Main.Settings.AppleId = AppleId.Trim();
            Main.Settings.Save();
            await LoadAsync(bridge);
        }
        else
        {
            State = PhotosState.SignedOut;
            Message = status.Message;
        }
    }

    async Task LoadAsync(ICloudBridge bridge)
    {
        State = PhotosState.Busy;
        Message = "A carregar a fototeca…";
        var photos = await bridge.GetPhotosAsync(0, PageSize);
        Photos.Clear();
        foreach (var p in photos)
        {
            Photos.Add(new PhotoTile(p, new RelayCommand(() => Main.Open(new PhotoDetailViewModel(Main, p)))));
        }
        State = PhotosState.Ready;
        Message = null;

        using var gate = new SemaphoreSlim(ParallelThumbnails);
        await Task.WhenAll(Photos.ToList().Select(async tile =>
        {
            await gate.WaitAsync();
            try
            {
                var bytes = await bridge.GetPhotoAsync(tile.Photo.Id, "thumb");
                var bitmap = Images.TryDecode(bytes, 240);
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
            await action(await Main.GetICloudAsync());
        }
        catch (Exception ex)
        {
            State = previous == PhotosState.Busy ? PhotosState.SignedOut : previous;
            Message = "Não foi possível falar com o iCloud: " + ex.Message;
        }
    }
}

public sealed partial class PhotoDetailViewModel : PageViewModel
{
    public PhotoDetailViewModel(MainViewModel main, ICloudPhoto photo) : base(main)
    {
        Photo = photo;
        _ = LoadAsync();
    }

    public ICloudPhoto Photo { get; }
    public override string Title => Photo.Created is { } d ? Format.ChatHeader(d) : Photo.FileName ?? "";

    [ObservableProperty] private Bitmap? _image;

    async Task LoadAsync()
    {
        var bridge = await Main.GetICloudAsync();
        // "medium" is a JPEG; originals are often HEIC, which Windows apps cannot decode without extensions.
        var version = Photo.Versions.Contains("medium") ? "medium" : "thumb";
        Image = Images.TryDecode(await bridge.GetPhotoAsync(Photo.Id, version));
    }
}
