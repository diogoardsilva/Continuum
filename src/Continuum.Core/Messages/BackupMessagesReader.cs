using System.Text;
using Continuum.Core.Backup;

namespace Continuum.Core.Messages;

/// <summary>
/// Reads the full Messages history (iMessage, SMS, RCS) from sms.db in a
/// local iPhone backup, including group chats and attachment locations.
/// </summary>
public static class BackupMessagesReader
{
    const string Domain = "HomeDomain";
    const string SmsDbPath = "Library/SMS/sms.db";
    const string AttachmentsDomain = "MediaDomain";

    static readonly DateTimeOffset AppleEpoch = new(2001, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static List<Conversation> Read(IosBackup backup)
    {
        var path = backup.GetFilePath(Domain, SmsDbPath)
            ?? throw new FileNotFoundException("O backup não tem as mensagens (sms.db).");
        return ReadDatabase(path);
    }

    /// <summary>Finds an attachment's file inside the backup (looked up on demand, as there can be thousands).</summary>
    public static string? ResolveAttachment(IosBackup backup, Attachment attachment)
    {
        if (attachment.PathOnPhone is null) return null;
        // sms.db stores "~/Library/SMS/Attachments/..."; the backup keys it without "~/".
        var relative = attachment.PathOnPhone.StartsWith("~/") ? attachment.PathOnPhone[2..] : attachment.PathOnPhone;
        return backup.GetFilePath(AttachmentsDomain, relative);
    }

    public static List<Conversation> ReadDatabase(string smsDbPath)
    {
        using var db = IosBackup.OpenReadOnly(smsDbPath);
        var conversations = new Dictionary<long, Conversation>();

        using (var cmd = db.CreateCommand())
        {
            cmd.CommandText = "SELECT ROWID, chat_identifier, display_name FROM chat";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                conversations[r.GetInt64(0)] = new Conversation
                {
                    Id = r.GetInt64(0),
                    Identifier = r.IsDBNull(1) ? "" : r.GetString(1),
                    DisplayName = r.IsDBNull(2) || r.GetString(2).Length == 0 ? null : r.GetString(2),
                };
            }
        }

        using (var cmd = db.CreateCommand())
        {
            cmd.CommandText = """
                SELECT chj.chat_id, h.id
                FROM chat_handle_join chj JOIN handle h ON h.ROWID = chj.handle_id
                ORDER BY chj.chat_id, h.ROWID
                """;
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                if (conversations.TryGetValue(r.GetInt64(0), out var c) && !c.Participants.Contains(r.GetString(1)))
                {
                    c.Participants.Add(r.GetString(1));
                }
            }
        }

        var messages = new Dictionary<long, Message>();
        using (var cmd = db.CreateCommand())
        {
            cmd.CommandText = """
                SELECT m.ROWID, cmj.chat_id, m.date, m.text, m.attributedBody, m.is_from_me, h.id, m.service
                FROM message m
                JOIN chat_message_join cmj ON cmj.message_id = m.ROWID
                LEFT JOIN handle h ON h.ROWID = m.handle_id
                ORDER BY m.date, m.ROWID
                """;
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                if (!conversations.TryGetValue(r.GetInt64(1), out var conversation)) continue;
                var text = r.IsDBNull(3) ? null : r.GetString(3);
                if (string.IsNullOrEmpty(text) && !r.IsDBNull(4))
                {
                    text = ExtractAttributedText((byte[])r.GetValue(4));
                }
                var isFromMe = !r.IsDBNull(5) && r.GetInt64(5) != 0;
                var message = new Message
                {
                    Id = r.GetInt64(0),
                    Date = FromAppleTime(r.IsDBNull(2) ? 0 : r.GetInt64(2)),
                    Text = text?.Replace("￼", "").Trim(), // object replacement char marks inline attachments
                    IsFromMe = isFromMe,
                    Sender = isFromMe || r.IsDBNull(6) ? null : r.GetString(6),
                    Service = r.IsDBNull(7) ? null : r.GetString(7),
                };
                conversation.Messages.Add(message);
                messages[message.Id] = message;
            }
        }

        using (var cmd = db.CreateCommand())
        {
            cmd.CommandText = """
                SELECT maj.message_id, a.transfer_name, a.mime_type, a.filename
                FROM message_attachment_join maj JOIN attachment a ON a.ROWID = maj.attachment_id
                """;
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                if (!messages.TryGetValue(r.GetInt64(0), out var message)) continue;
                message.Attachments.Add(new Attachment(
                    r.IsDBNull(1) ? null : r.GetString(1),
                    r.IsDBNull(2) ? null : r.GetString(2),
                    r.IsDBNull(3) ? null : r.GetString(3)));
            }
        }

        return conversations.Values
            .Where(c => c.Messages.Count > 0)
            .OrderByDescending(c => c.LastActivity)
            .ToList();
    }

    /// <summary>
    /// Message dates are seconds (older iOS) or nanoseconds (iOS 11+) since 2001-01-01 UTC.
    /// </summary>
    public static DateTimeOffset FromAppleTime(long value)
    {
        var seconds = value > 1_000_000_000_000 ? value / 1_000_000_000.0 : value;
        return AppleEpoch.AddSeconds(seconds);
    }

    /// <summary>
    /// Newer iOS versions leave message.text empty and keep the text inside
    /// attributedBody, an NSAttributedString archived as a typedstream. The
    /// plain string follows the first "NSString" class name as
    /// 0x01 0x94/0x95 0x84 0x01 '+' then a length and the UTF-8 bytes.
    /// </summary>
    public static string? ExtractAttributedText(byte[] blob)
    {
        var marker = "NSString"u8;
        var start = blob.AsSpan().IndexOf(marker);
        if (start < 0) return null;

        var i = start + marker.Length;
        // Skip the class header up to the '+' that introduces the string.
        var plus = Array.IndexOf(blob, (byte)'+', i, Math.Min(8, blob.Length - i));
        if (plus < 0) return null;
        i = plus + 1;
        if (i >= blob.Length) return null;

        int length;
        if (blob[i] == 0x81 && i + 2 < blob.Length)
        {
            length = blob[i + 1] | (blob[i + 2] << 8);
            i += 3;
        }
        else if (blob[i] == 0x82 && i + 4 < blob.Length)
        {
            length = BitConverter.ToInt32(blob, i + 1);
            i += 5;
        }
        else
        {
            length = blob[i];
            i += 1;
        }

        if (length <= 0 || i + length > blob.Length) return null;
        return Encoding.UTF8.GetString(blob, i, length);
    }
}
