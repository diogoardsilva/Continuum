using Continuum.Core.Backup;
using Continuum.Core.Calls;
using Continuum.Core.Contacts;
using Continuum.Core.Messages;

namespace Continuum.Services;

/// <summary>Data copied from the iPhone by Continuum.iPhone.exe (or any local backup).</summary>
public sealed class BackupPhoneLibrary : IPhoneLibrary
{
    readonly IosBackup _backup;

    BackupPhoneLibrary(IosBackup backup)
    {
        _backup = backup;
        Contacts = Try(() => BackupContactsReader.Read(backup)) ?? new List<Contact>();
        Conversations = Try(() => BackupMessagesReader.Read(backup)) ?? new List<Conversation>();
        Calls = Try(() => BackupCallHistoryReader.Read(backup)) ?? new List<CallRecord>();
        Directory = new ContactDirectory(Contacts);
    }

    public string Source => _backup.Directory;
    public IReadOnlyList<Contact> Contacts { get; }
    public IReadOnlyList<Conversation> Conversations { get; }
    public IReadOnlyList<CallRecord> Calls { get; }
    public ContactDirectory Directory { get; }

    public byte[]? LoadAttachment(Attachment attachment)
    {
        var path = BackupMessagesReader.ResolveAttachment(_backup, attachment);
        return path is null ? null : File.ReadAllBytes(path);
    }

    /// <summary>Continuum's own sync folder first, then the Apple Devices / iTunes backups.</summary>
    public static BackupPhoneLibrary? TryOpenDefault()
    {
        var own = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Continuum", "backup");
        var candidates = (System.IO.Directory.Exists(own)
                ? System.IO.Directory.EnumerateDirectories(own).Where(d => File.Exists(Path.Combine(d, "Manifest.db")))
                : Enumerable.Empty<string>())
            .Concat(IosBackup.FindBackups());
        var first = candidates.FirstOrDefault();
        return first is null ? null : new BackupPhoneLibrary(new IosBackup(first));
    }

    static T? Try<T>(Func<T> read) where T : class
    {
        try { return read(); }
        catch (Exception) { return null; }
    }
}
