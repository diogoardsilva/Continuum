using System.Text;
using Continuum.Core.Backup;
using Continuum.Core.Contacts;
using Continuum.Core.Messages;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Continuum.Core.Tests;

public sealed class BackupMessagesReaderTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "continuum-sms-" + Guid.NewGuid().ToString("N"));

    public BackupMessagesReaderTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void ReadsConversationsMessagesAndAttachments()
    {
        const string smsId = "3d0d7e5fb2ce288813306e4d4636395e047a3d28";
        const string photoId = "aabbccddeeff00112233445566778899aabbccdd";
        Exec(Path.Combine(_dir, "Manifest.db"),
            "CREATE TABLE Files (fileID TEXT, domain TEXT, relativePath TEXT, flags INTEGER, file BLOB)",
            $"INSERT INTO Files VALUES ('{smsId}', 'HomeDomain', 'Library/SMS/sms.db', 1, NULL)",
            $"INSERT INTO Files VALUES ('{photoId}', 'MediaDomain', 'Library/SMS/Attachments/ab/01/IMG_1.HEIC', 1, NULL)");
        File.WriteAllBytes(BackupFile(photoId), new byte[] { 1 });

        var nowNs = (long)(new DateTimeOffset(2026, 10, 29, 20, 0, 0, TimeSpan.Zero) - new DateTimeOffset(2001, 1, 1, 0, 0, 0, TimeSpan.Zero)).TotalSeconds * 1_000_000_000L;
        var attributed = TypedStream("Olá do iOS 27");
        Exec(BackupFile(smsId),
            "CREATE TABLE chat (ROWID INTEGER PRIMARY KEY, chat_identifier TEXT, display_name TEXT)",
            "CREATE TABLE handle (ROWID INTEGER PRIMARY KEY, id TEXT, service TEXT)",
            "CREATE TABLE chat_handle_join (chat_id INTEGER, handle_id INTEGER)",
            "CREATE TABLE message (ROWID INTEGER PRIMARY KEY, text TEXT, attributedBody BLOB, handle_id INTEGER, date INTEGER, is_from_me INTEGER, service TEXT)",
            "CREATE TABLE chat_message_join (chat_id INTEGER, message_id INTEGER)",
            "CREATE TABLE attachment (ROWID INTEGER PRIMARY KEY, filename TEXT, mime_type TEXT, transfer_name TEXT)",
            "CREATE TABLE message_attachment_join (message_id INTEGER, attachment_id INTEGER)",
            "INSERT INTO chat VALUES (1, '+351912345678', ''), (2, 'chat123', 'Família')",
            "INSERT INTO handle VALUES (1, '+351912345678', 'iMessage'), (2, 'mae@example.com', 'iMessage')",
            "INSERT INTO chat_handle_join VALUES (1, 1), (2, 1), (2, 2)",
            $"INSERT INTO message VALUES (1, 'Olá!', NULL, 1, {nowNs - 60_000_000_000}, 0, 'iMessage')",
            $"INSERT INTO message VALUES (2, NULL, X'{Convert.ToHexString(attributed)}', 0, {nowNs}, 1, 'iMessage')",
            $"INSERT INTO message VALUES (3, '￼', NULL, 2, {nowNs - 3600_000_000_000}, 0, 'iMessage')",
            "INSERT INTO chat_message_join VALUES (1, 1), (1, 2), (2, 3)",
            "INSERT INTO attachment VALUES (1, '~/Library/SMS/Attachments/ab/01/IMG_1.HEIC', 'image/heic', 'IMG_1.HEIC')",
            "INSERT INTO message_attachment_join VALUES (3, 1)");

        var backup = new IosBackup(_dir);
        var conversations = BackupMessagesReader.Read(backup);

        Assert.Equal(new long[] { 1, 2 }, conversations.Select(c => c.Id));
        var direct = conversations[0];
        Assert.False(direct.IsGroup);
        Assert.Equal(new[] { "Olá!", "Olá do iOS 27" }, direct.Messages.Select(m => m.Text));
        Assert.Equal("+351912345678", direct.Messages[0].Sender);
        Assert.True(direct.Messages[1].IsFromMe);
        Assert.Equal(new DateTimeOffset(2026, 10, 29, 20, 0, 0, TimeSpan.Zero), direct.Messages[1].Date);

        var group = conversations[1];
        Assert.True(group.IsGroup);
        Assert.Equal("Família", group.DisplayName);
        var attachment = group.Messages.Single().Attachments.Single();
        Assert.Equal("image/heic", attachment.MimeType);
        Assert.Equal(BackupFile(photoId), BackupMessagesReader.ResolveAttachment(backup, attachment));
        Assert.Equal("", group.Messages.Single().Text);
    }

    [Theory]
    [InlineData(0L, "2001-01-01T00:00:00+00:00")]
    [InlineData(783_000_000L, "2025-10-24T12:00:00+00:00")]
    [InlineData(783_000_000_000_000_000L, "2025-10-24T12:00:00+00:00")]
    public void ConvertsAppleTimestamps(long value, string expected)
    {
        Assert.Equal(DateTimeOffset.Parse(expected), BackupMessagesReader.FromAppleTime(value));
    }

    [Fact]
    public void ExtractsLongAttributedText()
    {
        var text = new string('a', 300);
        Assert.Equal(text, BackupMessagesReader.ExtractAttributedText(TypedStream(text)));
    }

    [Fact]
    public void MatchesHandlesToContactsInAnyFormat()
    {
        var ana = new Contact { Id = "1", FirstName = "Ana" };
        ana.Phones.Add(new ContactField("telemóvel", "912 345 678"));
        ana.Emails.Add(new ContactField("casa", "Ana@Example.com"));
        var directory = new ContactDirectory(new[] { ana });

        Assert.Same(ana, directory.Find("+351912345678"));
        Assert.Same(ana, directory.Find("00351 912-345-678"));
        Assert.Same(ana, directory.Find("ana@example.com"));
        Assert.Null(directory.Find("+351913000000"));
    }

    // Minimal typedstream: what iOS writes for an NSAttributedString's string part.
    static byte[] TypedStream(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var head = new List<byte>();
        head.AddRange("\u0004\u000bstreamtyped\u0081è\u0003\u0084\u0001@\u0084\u0084\u0084\u0012NSAttributedString\u0000\u0084\u0084\u0008NSObject\u0000\u0085\u0092\u0084\u0084\u0084\u0008"u8.ToArray());
        head.AddRange("NSString"u8.ToArray());
        head.AddRange(new byte[] { 0x01, 0x94, 0x84, 0x01, (byte)'+' });
        if (bytes.Length < 0x80) head.Add((byte)bytes.Length);
        else { head.Add(0x81); head.Add((byte)(bytes.Length & 0xFF)); head.Add((byte)(bytes.Length >> 8)); }
        head.AddRange(bytes);
        head.AddRange(new byte[] { 0x86, 0x84, 0x02 });
        return head.ToArray();
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
