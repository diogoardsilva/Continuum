using System.Diagnostics;
using Continuum.Core.Calls;
using Continuum.Core.Contacts;
using Continuum.Core.Live;
using Continuum.Core.Messages;

namespace Continuum.Services;

/// <summary>
/// The synced library plus the messages Continuum.Notificacoes.exe has heard
/// from the iPhone's notifications since the last cable sync.
/// </summary>
public sealed class LivePhoneLibrary : IPhoneLibrary, IDisposable
{
    const string ListenerExe = "Continuum.Notificacoes.exe";

    readonly IPhoneLibrary _synced;
    readonly string _path;
    readonly FileSystemWatcher? _watcher;
    readonly Timer _debounce;

    public LivePhoneLibrary(IPhoneLibrary synced, string path)
    {
        _synced = synced;
        _path = path;
        Conversations = Load();
        _debounce = new Timer(_ => Reload());
        var dir = Path.GetDirectoryName(path)!;
        if (System.IO.Directory.Exists(dir))
        {
            _watcher = new FileSystemWatcher(dir, Path.GetFileName(path) + "*") { EnableRaisingEvents = true };
            _watcher.Changed += (_, _) => _debounce.Change(500, Timeout.Infinite);
            _watcher.Created += (_, _) => _debounce.Change(500, Timeout.Infinite);
        }
    }

    /// <summary>Raised (off the UI thread) when new live messages arrived.</summary>
    public event Action? Changed;

    public string Source => _synced.Source;
    public IReadOnlyList<Contact> Contacts => _synced.Contacts;
    public IReadOnlyList<Conversation> Conversations { get; private set; }
    public IReadOnlyList<CallRecord> Calls => _synced.Calls;
    public ContactDirectory Directory => _synced.Directory;
    public byte[]? LoadAttachment(Attachment attachment) => _synced.LoadAttachment(attachment);

    List<Conversation> Load()
    {
        if (!File.Exists(_path)) return _synced.Conversations.ToList();
        try
        {
            return LiveMessageMerger.Merge(_synced.Conversations, new LiveMessageStore(_path).All(), _synced.Directory, _synced.Contacts);
        }
        catch (Exception ex) when (ex is IOException or Microsoft.Data.Sqlite.SqliteException)
        {
            return _synced.Conversations.ToList();
        }
    }

    void Reload()
    {
        Conversations = Load();
        Changed?.Invoke();
    }

    /// <summary>Starts the notifications listener in the background if it ships next to the app and isn't running.</summary>
    public static void StartListener()
    {
        var exe = Path.Combine(AppContext.BaseDirectory, ListenerExe);
        if (!File.Exists(exe) || Process.GetProcessesByName(Path.GetFileNameWithoutExtension(ListenerExe)).Length > 0) return;
        try
        {
            Process.Start(new ProcessStartInfo(exe, "--quiet") { UseShellExecute = false, CreateNoWindow = true })?.Dispose();
        }
        catch (System.ComponentModel.Win32Exception) { }
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _debounce.Dispose();
    }
}
