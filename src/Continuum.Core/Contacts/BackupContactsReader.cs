using Continuum.Core.Backup;
using Microsoft.Data.Sqlite;

namespace Continuum.Core.Contacts;

/// <summary>
/// Reads the iPhone's own address book (AddressBook.sqlitedb) and contact
/// photos (AddressBookImages.sqlitedb) from a local backup.
/// </summary>
public static class BackupContactsReader
{
    const string Domain = "HomeDomain";
    const string AddressBookPath = "Library/AddressBook/AddressBook.sqlitedb";
    const string ImagesPath = "Library/AddressBook/AddressBookImages.sqlitedb";

    // ABMultiValue.property values used by iOS.
    const int PhoneProperty = 3;
    const int EmailProperty = 4;

    public static List<Contact> Read(IosBackup backup)
    {
        var addressBook = backup.GetFilePath(Domain, AddressBookPath)
            ?? throw new FileNotFoundException("O backup não tem os contactos (AddressBook.sqlitedb).");
        var contacts = ReadAddressBook(addressBook);

        var images = backup.GetFilePath(Domain, ImagesPath);
        if (images is not null) AttachPhotos(contacts, images);

        return contacts.Values.ToList();
    }

    public static Dictionary<long, Contact> ReadAddressBook(string path)
    {
        var contacts = new Dictionary<long, Contact>();
        using var db = IosBackup.OpenReadOnly(path);

        using (var cmd = db.CreateCommand())
        {
            cmd.CommandText = "SELECT ROWID, First, Last, Organization, Nickname FROM ABPerson";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var id = r.GetInt64(0);
                contacts[id] = new Contact
                {
                    Id = $"ab-{id}",
                    FirstName = NullableString(r, 1),
                    LastName = NullableString(r, 2),
                    Organization = NullableString(r, 3),
                    Nickname = NullableString(r, 4),
                };
            }
        }

        using (var cmd = db.CreateCommand())
        {
            cmd.CommandText = """
                SELECT mv.record_id, mv.property, l.value, mv.value
                FROM ABMultiValue mv
                LEFT JOIN ABMultiValueLabel l ON l.ROWID = mv.label
                WHERE mv.property IN (3, 4) AND mv.value IS NOT NULL
                ORDER BY mv.record_id, mv.UID
                """;
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                if (!contacts.TryGetValue(r.GetInt64(0), out var contact)) continue;
                var field = new ContactField(ContactLabels.Clean(NullableString(r, 2)), r.GetString(3));
                if (r.GetInt32(1) == PhoneProperty) contact.Phones.Add(field);
                else if (r.GetInt32(1) == EmailProperty) contact.Emails.Add(field);
            }
        }

        return contacts;
    }

    public static void AttachPhotos(Dictionary<long, Contact> contacts, string imagesPath)
    {
        using var db = IosBackup.OpenReadOnly(imagesPath);
        // Prefer the full-size image; fall back to the largest thumbnail.
        foreach (var query in new[]
        {
            "SELECT record_id, data FROM ABFullSizeImage",
            "SELECT record_id, data FROM ABThumbnailImage ORDER BY format DESC",
        })
        {
            try
            {
                using var cmd = db.CreateCommand();
                cmd.CommandText = query;
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    if (r.IsDBNull(1) || !contacts.TryGetValue(r.GetInt64(0), out var contact) || contact.Photo is not null) continue;
                    contact.Photo = (byte[])r.GetValue(1);
                }
            }
            catch (SqliteException)
            {
                // Table missing on this iOS version; try the next one.
            }
        }
    }

    static string? NullableString(SqliteDataReader r, int i) =>
        r.IsDBNull(i) || string.IsNullOrWhiteSpace(r.GetString(i)) ? null : r.GetString(i);
}
