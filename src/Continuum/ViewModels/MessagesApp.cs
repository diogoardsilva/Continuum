using CommunityToolkit.Mvvm.ComponentModel;
using Continuum.Core.Messages;
using Continuum.Services;

namespace Continuum.ViewModels;

public sealed record ConversationItem(Conversation Conversation, string Title, string Preview, string Date, Avatar Avatar);

public sealed class ConversationPane
{
    public ConversationPane(IPhoneLibrary library, Conversation conversation)
    {
        Title = Chat.Title(library, conversation);
        Avatar = Chat.Avatar(library, conversation);
        Rows = Chat.Rows(library, conversation);
        Subtitle = conversation.IsGroup ? $"{conversation.Participants.Count} pessoas" : conversation.Participants.FirstOrDefault();
    }

    public string Title { get; }
    public string? Subtitle { get; }
    public Avatar Avatar { get; }
    public IReadOnlyList<ChatRow> Rows { get; }
}

/// <summary>Mensagens: conversations in the sidebar, the selected one on the right.</summary>
public sealed partial class MessagesAppViewModel : ObservableObject
{
    readonly IPhoneLibrary _library;
    List<ConversationItem> _all;

    public MessagesAppViewModel(AppServices services)
    {
        _library = services.Library;
        _all = Build();
        _items = _all;
        Selected = _all.FirstOrDefault();
        if (_library is LivePhoneLibrary live)
        {
            live.Changed += () => Avalonia.Threading.Dispatcher.UIThread.Post(Refresh);
        }
    }

    List<ConversationItem> Build() => _library.Conversations
        .OrderByDescending(c => c.Messages[^1].Date)
        .Select(c => new ConversationItem(c, Chat.Title(_library, c), Chat.Preview(c.Messages[^1]),
            Format.ListDate(c.Messages[^1].Date), Chat.Avatar(_library, c)))
        .ToList();

    /// <summary>New messages arrived from the iPhone's notifications: rebuild, keeping the open conversation.</summary>
    void Refresh()
    {
        var open = Selected?.Conversation;
        _all = Build();
        OnSearchChanged(Search);
        Selected = _all.FirstOrDefault(i => open is not null && i.Conversation.Id == open.Id) ?? _all.FirstOrDefault();
        OnPropertyChanged(nameof(IsEmpty));
    }

    [ObservableProperty]
    private IReadOnlyList<ConversationItem> _items;

    [ObservableProperty]
    private string _search = "";

    [ObservableProperty]
    private ConversationItem? _selected;

    [ObservableProperty]
    private ConversationPane? _pane;

    public bool IsEmpty => _all.Count == 0;

    partial void OnSelectedChanged(ConversationItem? value) =>
        Pane = value is null ? null : new ConversationPane(_library, value.Conversation);

    partial void OnSearchChanged(string value)
    {
        var q = value.Trim();
        Items = q.Length == 0
            ? _all
            : _all.Where(i => i.Title.Contains(q, StringComparison.CurrentCultureIgnoreCase)
                              || i.Conversation.Messages.Any(m => m.Text?.Contains(q, StringComparison.CurrentCultureIgnoreCase) == true))
                  .ToList();
    }
}
