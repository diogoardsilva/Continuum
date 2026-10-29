using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Continuum.Core.Messages;
using Continuum.Services;

namespace Continuum.ViewModels;

public sealed record ConversationItem(string Title, string Preview, string Date, Avatar Avatar, bool IsGroup, IRelayCommand Open);

public sealed partial class MessagesViewModel : PageViewModel
{
    readonly List<ConversationItem> _all;

    public MessagesViewModel(MainViewModel main) : base(main)
    {
        _all = main.Library.Conversations
            .Select(c =>
            {
                var title = ConversationViewModel.TitleFor(main, c);
                var last = c.Messages[^1];
                var preview = string.IsNullOrWhiteSpace(last.Text)
                    ? (last.Attachments.Count > 0 ? "Anexo" : "")
                    : last.Text!;
                var avatar = c.IsGroup
                    ? new Avatar(title, null)
                    : Avatar.For(main.Library.Directory.Find(c.Participants.FirstOrDefault()), title);
                return new ConversationItem(title, preview, Format.ListDate(last.Date), avatar, c.IsGroup,
                    new RelayCommand(() => main.Open(new ConversationViewModel(main, c))));
            })
            .ToList();
        Items = _all;
    }

    public override string Title => "Mensagens";

    [ObservableProperty]
    private IReadOnlyList<ConversationItem> _items;

    [ObservableProperty]
    private string _search = "";

    partial void OnSearchChanged(string value)
    {
        var q = value.Trim();
        Items = q.Length == 0
            ? _all
            : _all.Where(i => i.Title.Contains(q, StringComparison.CurrentCultureIgnoreCase)
                              || i.Preview.Contains(q, StringComparison.CurrentCultureIgnoreCase)).ToList();
    }
}

public abstract record ChatRow;
public sealed record TimeRow(string Text) : ChatRow;
public sealed record BubbleRow(string? Text, bool IsFromMe, bool IsSms, string? SenderName, Bitmap? Image, string? AttachmentName) : ChatRow
{
    public bool HasText => !string.IsNullOrEmpty(Text);
    public bool HasImage => Image is not null;
    public bool HasFile => Image is null && AttachmentName is not null;
    public bool ShowSender => SenderName is not null;
}

public sealed class ConversationViewModel : PageViewModel
{
    // A new time header appears after an hour of silence, like on the iPhone.
    static readonly TimeSpan HeaderGap = TimeSpan.FromHours(1);
    const int MaxMessages = 400;

    public ConversationViewModel(MainViewModel main, Conversation conversation) : base(main)
    {
        Conversation = conversation;
        ConversationTitle = TitleFor(main, conversation);
        Avatar = conversation.IsGroup
            ? new Avatar(ConversationTitle, null)
            : Avatar.For(main.Library.Directory.Find(conversation.Participants.FirstOrDefault()), ConversationTitle);

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
                ? NameFor(main, sender)
                : null;
            var isSms = m.Service is "SMS" or "RCS";

            foreach (var a in m.Attachments)
            {
                var image = a.MimeType?.StartsWith("image/") == true ? Images.TryDecode(main.Library.LoadAttachment(a), 480) : null;
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
        Rows = rows;
    }

    public Conversation Conversation { get; }
    public string ConversationTitle { get; }
    public Avatar Avatar { get; }
    public override string Title => ConversationTitle;
    public IReadOnlyList<ChatRow> Rows { get; }

    public static string TitleFor(MainViewModel main, Conversation c)
    {
        if (!string.IsNullOrWhiteSpace(c.DisplayName)) return c.DisplayName!;
        var names = c.Participants.Select(p => NameFor(main, p, shortName: c.IsGroup)).ToList();
        return names.Count == 0 ? c.Identifier : string.Join(", ", names);
    }

    static string NameFor(MainViewModel main, string handle, bool shortName = false)
    {
        var contact = main.Library.Directory.Find(handle);
        if (contact is null) return handle;
        return shortName ? contact.FirstName ?? contact.DisplayName : contact.DisplayName;
    }
}
