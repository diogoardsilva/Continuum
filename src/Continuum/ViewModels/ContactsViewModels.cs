using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Continuum.Core.Contacts;

namespace Continuum.ViewModels;

public sealed record ContactItem(Contact Contact, Avatar Avatar, IRelayCommand Open)
{
    public string Name => Contact.DisplayName;
}

public sealed record ContactSection(string Letter, IReadOnlyList<ContactItem> Items);

public sealed partial class ContactsViewModel : PageViewModel
{
    readonly List<ContactItem> _all;

    public ContactsViewModel(MainViewModel main) : base(main)
    {
        _all = main.Library.Contacts
            .OrderBy(c => c.DisplayName, StringComparer.Create(CultureInfo.GetCultureInfo("pt-PT"), true))
            .Select(c => new ContactItem(c, Avatar.For(c, c.DisplayName),
                new RelayCommand(() => main.Open(new ContactDetailViewModel(main, c)))))
            .ToList();
        Sections = Group(_all);
    }

    public override string Title => "Contactos";
    public int Count => _all.Count;

    [ObservableProperty]
    private IReadOnlyList<ContactSection> _sections;

    [ObservableProperty]
    private string _search = "";

    partial void OnSearchChanged(string value)
    {
        var q = value.Trim();
        Sections = Group(q.Length == 0
            ? _all
            : _all.Where(i => i.Name.Contains(q, StringComparison.CurrentCultureIgnoreCase)
                              || i.Contact.Phones.Any(p => p.Value.Replace(" ", "").Contains(q.Replace(" ", "")))));
    }

    static IReadOnlyList<ContactSection> Group(IEnumerable<ContactItem> items) =>
        items.GroupBy(i => SectionLetter(i.Name))
            .OrderBy(g => g.Key == "#" ? "￿" : g.Key)
            .Select(g => new ContactSection(g.Key, g.ToList()))
            .ToList();

    static string SectionLetter(string name)
    {
        var first = name.Normalize(System.Text.NormalizationForm.FormD).FirstOrDefault(char.IsLetterOrDigit);
        return char.IsLetter(first) ? char.ToUpperInvariant(first).ToString() : "#";
    }
}

public sealed record ContactRow(string Label, string Value);

public sealed class ContactDetailViewModel : PageViewModel
{
    public ContactDetailViewModel(MainViewModel main, Contact contact) : base(main)
    {
        Contact = contact;
        Avatar = new Avatar(contact.DisplayName, contact.Photo);
        Rows = contact.Phones.Select(p => new ContactRow(p.Label, p.Value))
            .Concat(contact.Emails.Select(e => new ContactRow(e.Label, e.Value)))
            .ToList();
        OpenMessages = new RelayCommand(OpenConversation, () => FindConversation() is not null);
    }

    public Contact Contact { get; }
    public Avatar Avatar { get; }
    public override string Title => "";
    public string Name => Contact.DisplayName;
    public string? Organization => Contact.FirstName is null && Contact.LastName is null ? null : Contact.Organization;
    public IReadOnlyList<ContactRow> Rows { get; }
    public IRelayCommand OpenMessages { get; }

    Core.Messages.Conversation? FindConversation() =>
        Main.Library.Conversations.FirstOrDefault(c => !c.IsGroup
            && c.Participants.Any(p => Main.Library.Directory.Find(p) == Contact));

    void OpenConversation()
    {
        if (FindConversation() is { } c) Main.Open(new ConversationViewModel(Main, c));
    }
}
