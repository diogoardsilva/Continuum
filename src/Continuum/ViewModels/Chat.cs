using Avalonia.Media.Imaging;
using Continuum.Core.Messages;
using Continuum.Services;

namespace Continuum.ViewModels;

public abstract record ChatRow;
public sealed record TimeRow(string Text) : ChatRow;
public sealed record BubbleRow(string? Text, bool IsFromMe, bool IsSms, string? SenderName, Bitmap? Image, string? AttachmentName) : ChatRow
{
    public bool HasText => !string.IsNullOrEmpty(Text);
    public bool HasImage => Image is not null;
    public bool HasFile => Image is null && AttachmentName is not null;
    public bool ShowSender => SenderName is not null;
}

/// <summary>Turns a conversation into what Messages shows: names, avatars, bubbles and time headers.</summary>
public static class Chat
{
    // A new time header appears after an hour of silence, like on the iPhone.
    static readonly TimeSpan HeaderGap = TimeSpan.FromHours(1);
    const int MaxMessages = 400;

    public static string Title(IPhoneLibrary library, Conversation c)
    {
        if (!string.IsNullOrWhiteSpace(c.DisplayName)) return c.DisplayName!;
        var names = c.Participants.Select(p => Name(library, p, shortName: c.IsGroup)).ToList();
        return names.Count == 0 ? c.Identifier : string.Join(", ", names);
    }

    public static Avatar Avatar(IPhoneLibrary library, Conversation c) =>
        c.IsGroup
            ? new Avatar(Title(library, c), null)
            : ViewModels.Avatar.For(library.Directory.Find(c.Participants.FirstOrDefault()), Title(library, c));

    public static string Preview(Message last) =>
        string.IsNullOrWhiteSpace(last.Text) ? (last.Attachments.Count > 0 ? "Anexo" : "") : last.Text!;

    public static string Name(IPhoneLibrary library, string handle, bool shortName = false)
    {
        var contact = library.Directory.Find(handle);
        if (contact is null) return handle;
        return shortName ? contact.FirstName ?? contact.DisplayName : contact.DisplayName;
    }

    public static List<ChatRow> Rows(IPhoneLibrary library, Conversation conversation)
    {
        var rows = new List<ChatRow>();
        DateTimeOffset? previous = null;
        string? previousSender = null;
        foreach (var m in conversation.Messages.TakeLast(MaxMessages))
        {
            if (previous is null || m.Date - previous > HeaderGap)
            {
                rows.Add(new TimeRow(Format.ChatHeader(m.Date)));
                previousSender = null;
            }
            var sender = m.IsFromMe ? null : m.Sender;
            var senderName = conversation.IsGroup && sender is not null && sender != previousSender
                ? Name(library, sender)
                : null;
            var isSms = m.Service is "SMS" or "RCS";

            foreach (var a in m.Attachments)
            {
                var image = a.MimeType?.StartsWith("image/") == true ? Images.TryDecode(library.LoadAttachment(a), 480) : null;
                rows.Add(new BubbleRow(null, m.IsFromMe, isSms, senderName, image, a.FileName ?? "Anexo"));
                senderName = null;
            }
            if (!string.IsNullOrWhiteSpace(m.Text))
            {
                rows.Add(new BubbleRow(m.Text, m.IsFromMe, isSms, senderName, null, null));
            }
            previous = m.Date;
            previousSender = sender;
        }
        return rows;
    }
}
