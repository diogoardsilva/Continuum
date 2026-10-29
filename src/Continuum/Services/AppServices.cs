namespace Continuum.Services;

/// <summary>What every app window shares: the phone's data, settings and the iCloud.com bridge.</summary>
public sealed class AppServices : IAsyncDisposable
{
    Task<ICloudBridge>? _bridge;

    public AppServices(IPhoneLibrary library, Settings settings)
    {
        Library = library;
        Settings = settings;
    }

    public IPhoneLibrary Library { get; }
    public Settings Settings { get; }

    /// <summary>Starts Continuum.iCloud.exe the first time an app needs iCloud.com.</summary>
    public Task<ICloudBridge> GetICloudAsync() => _bridge ??= ICloudBridge.StartAsync();

    public static AppServices LoadDefault() =>
        new(BackupPhoneLibrary.TryOpenDefault() as IPhoneLibrary ?? new SamplePhoneLibrary(), Settings.Load());

    public async ValueTask DisposeAsync()
    {
        if (_bridge is { IsCompletedSuccessfully: true }) await _bridge.Result.DisposeAsync();
    }
}
