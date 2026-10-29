using Microsoft.Data.Sqlite;

namespace Continuum.Core.Backup;

public sealed class EncryptedBackupException : Exception
{
    public EncryptedBackupException()
        : base("Este backup está encriptado. Por agora o Continuum só lê backups sem encriptação.") { }
}

/// <summary>
/// A local iPhone backup as written by Apple Devices / iTunes (or idevicebackup2):
/// a folder with Manifest.db plus every file stored under its SHA-1 file ID.
/// </summary>
public sealed class IosBackup
{
    public string Directory { get; }

    public IosBackup(string directory)
    {
        if (!File.Exists(Path.Combine(directory, "Manifest.db")))
        {
            throw new FileNotFoundException("Não encontrei o Manifest.db: esta pasta não parece um backup do iPhone.", directory);
        }
        Directory = directory;
    }

    /// <summary>Default backup folders used by the Apple Devices app and by iTunes on Windows.</summary>
    public static IEnumerable<string> FindBackups()
    {
        var roots = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Apple", "MobileSync", "Backup"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Apple Computer", "MobileSync", "Backup"),
        };
        return roots
            .Where(System.IO.Directory.Exists)
            .SelectMany(System.IO.Directory.EnumerateDirectories)
            .Where(d => File.Exists(Path.Combine(d, "Manifest.db")))
            .OrderByDescending(d => File.GetLastWriteTimeUtc(Path.Combine(d, "Manifest.db")));
    }

    /// <summary>Returns the on-disk path of a file from the phone, or null if the backup does not have it.</summary>
    public string? GetFilePath(string domain, string relativePath)
    {
        string? fileId;
        try
        {
            using var db = OpenReadOnly(Path.Combine(Directory, "Manifest.db"));
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT fileID FROM Files WHERE domain = $domain AND relativePath = $path LIMIT 1";
            cmd.Parameters.AddWithValue("$domain", domain);
            cmd.Parameters.AddWithValue("$path", relativePath);
            fileId = cmd.ExecuteScalar() as string;
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 26) // SQLITE_NOTADB
        {
            throw new EncryptedBackupException();
        }

        if (fileId is null) return null;
        var path = Path.Combine(Directory, fileId[..2], fileId);
        return File.Exists(path) ? path : null;
    }

    internal static SqliteConnection OpenReadOnly(string path)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
        }.ToString());
        connection.Open();
        return connection;
    }
}
