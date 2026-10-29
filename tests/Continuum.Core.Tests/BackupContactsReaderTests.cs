using Continuum.Core.Backup;
using Continuum.Core.Contacts;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Continuum.Core.Tests;

public sealed class BackupContactsReaderTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "continuum-backup-" + Guid.NewGuid().ToString("N"));

    public BackupContactsReaderTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void ReadsContactsPhonesEmailsAndPhotos()
    {
        const string bookId = "31bb7ba8914766d4ba40d6dfb6113c8b614be442";
        const string imagesId = "cd6702cea29fe89cf280a76794405adb17f9a0ee";

        Exec(Path.Combine(_dir, "Manifest.db"),
            "CREATE TABLE Files (fileID TEXT, domain TEXT, relativePath TEXT, flags INTEGER, file BLOB)",
            $"INSERT INTO Files VALUES ('{bookId}', 'HomeDomain', 'Library/AddressBook/AddressBook.sqlitedb', 1, NULL)",
            $"INSERT INTO Files VALUES ('{imagesId}', 'HomeDomain', 'Library/AddressBook/AddressBookImages.sqlitedb', 1, NULL)");

        Exec(BackupFile(bookId),
            "CREATE TABLE ABPerson (ROWID INTEGER PRIMARY KEY, First TEXT, Last TEXT, Organization TEXT, Nickname TEXT)",
            "CREATE TABLE ABMultiValue (UID INTEGER PRIMARY KEY, record_id INTEGER, property INTEGER, identifier INTEGER, label INTEGER, value TEXT)",
            "CREATE TABLE ABMultiValueLabel (ROWID INTEGER PRIMARY KEY, value TEXT)",
            "INSERT INTO ABPerson VALUES (1, 'Diogo', 'Silva', NULL, NULL), (2, NULL, NULL, 'Farmácia', NULL)",
            "INSERT INTO ABMultiValueLabel VALUES (1, '_$!<Mobile>!$_'), (2, 'Trabalho Lisboa')",
            "INSERT INTO ABMultiValue VALUES (1, 1, 3, 0, 1, '+351912345678'), (2, 1, 4, 0, 2, 'diogo@example.com'), (3, 2, 3, 0, NULL, '213000000')");

        Exec(BackupFile(imagesId),
            "CREATE TABLE ABFullSizeImage (ROWID INTEGER PRIMARY KEY, record_id INTEGER, crop_x INTEGER, data BLOB)",
            "INSERT INTO ABFullSizeImage VALUES (1, 1, 0, X'FFD8FFE0')");

        var contacts = BackupContactsReader.Read(new IosBackup(_dir)).OrderBy(c => c.Id).ToList();

        Assert.Equal(new[] { "Diogo Silva", "Farmácia" }, contacts.Select(c => c.DisplayName));
        Assert.Equal(new ContactField("telemóvel", "+351912345678"), contacts[0].Phones.Single());
        Assert.Equal(new ContactField("Trabalho Lisboa", "diogo@example.com"), contacts[0].Emails.Single());
        Assert.Equal(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, contacts[0].Photo);
        Assert.Equal("outro", contacts[1].Phones.Single().Label);
        Assert.Null(contacts[1].Photo);
    }

    [Fact]
    public void RejectsFoldersThatAreNotBackups()
    {
        Assert.Throws<FileNotFoundException>(() => new IosBackup(_dir));
    }

    string BackupFile(string fileId)
    {
        var dir = Path.Combine(_dir, fileId[..2]);
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, fileId);
    }

    static void Exec(string path, params string[] statements)
    {
        using var db = new SqliteConnection($"Data Source={path};Pooling=False");
        db.Open();
        foreach (var sql in statements)
        {
            using var cmd = db.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }
    }
}
