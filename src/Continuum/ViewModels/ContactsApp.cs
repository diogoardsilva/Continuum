using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Continuum.Core.Contacts;
using Continuum.Services;

namespace Continuum.ViewModels;

public sealed record ContactItem(Contact Contact, Avatar Avatar)
{
    public string Name => Contact.DisplayName;
}

/// <summary>Letter heading between contacts in the sidebar; not selectable.</summary>
public sealed record ContactHeader(string Letter);

public sealed record ContactRow(string Label, string Value);

public sealed class ContactCard
{
    public ContactCard(Contact contact)
    {
        Name = contact.DisplayName;
        Organization = contact.FirstName is null && contact.LastName is null ? null : contact.Organization;
        Avatar = new Avatar(contact.DisplayName, contact.Photo);
        Rows = contact.Phones.Select(p => new ContactRow(p.Label, p.Value))
            .Concat(contact.Emails.Select(e => new ContactRow(e.Label, e.Value)))
            .ToList();
    }

    public string Name { get; }
    public string? Organization { get; }
    public bool HasOrganization => Organization is not null;
    public Avatar Avatar { get; }
    public IReadOnlyList<ContactRow> Rows { get; }
}

/// <summary>Contactos: alphabetical list in the sidebar, the card on the right.</summary>
public sealed partial class ContactsAppViewModel : ObservableObject
{
    readonly List<ContactItem> _all;

    public ContactsAppViewModel(AppServices services)
    {
        _all = services.Library.Contacts
            .OrderBy(c => c.DisplayName, StringComparer.Create(CultureInfo.GetCultureInfo("pt-PT"), true))
            .Select(c => new ContactItem(c, Avatar.For(c, c.DisplayName)))
            .ToList();
        _items = WithHeaders(_all);
        Selected = _all.FirstOrDefault();
    }

    public int Count => _all.Count;

    [ObservableProperty]
    private IReadOnlyList<object> _items;

    [ObservableProperty]
    private string _search = "";

    [ObservableProperty]
    private object? _selected;

    [ObservableProperty]
    private ContactCard? _card;

    partial void OnSelectedChanged(object? oldValue, object? newValue)
    {
        if (newValue is ContactHeader)
        {
            // Headings cannot be selected; keep the previous contact.
            Avalonia.Threading.Dispatcher.UIThread.Post(() => Selected = oldValue);
            return;
        }
        Card = newValue is ContactItem item ? new ContactCard(item.Contact) : null;
    }

    partial void OnSearchChanged(string value)
    {
        var q = value.Trim();
        Items = WithHeaders(q.Length == 0
            ? _all
            : _all.Where(i => i.Name.Contains(q, StringComparison.CurrentCultureIgnoreCase)
                              || i.Contact.Phones.Any(p => p.Value.Replace(" ", "").Contains(q.Replace(" ", "")))));
    }

    static IReadOnlyList<object> WithHeaders(IEnumerable<ContactItem> items) =>
        items.GroupBy(i => SectionLetter(i.Name))
            .OrderBy(g => g.Key == "#" ? "\uffff" : g.Key)
            .SelectMany(g => g.Cast<object>().Prepend(new ContactHeader(g.Key)))
            .ToList();

    static string SectionLetter(string name)
    {
        var first = name.Normalize(System.Text.NormalizationForm.FormD).FirstOrDefault(char.IsLetterOrDigit);
        return char.IsLetter(first) ? char.ToUpperInvariant(first).ToString() : "#";
    }
}
