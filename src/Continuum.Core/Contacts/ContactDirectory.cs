namespace Continuum.Core.Contacts;

/// <summary>
/// Finds the contact behind an iMessage/SMS handle (a phone number in any
/// format, or an email address).
/// </summary>
public sealed class ContactDirectory
{
    // Matching on the last 9 digits ignores country codes and formatting:
    // "+351 912 345 678", "00351912345678" and "912345678" are the same number.
    const int SignificantDigits = 9;

    readonly Dictionary<string, Contact> _byKey = new(StringComparer.OrdinalIgnoreCase);

    public ContactDirectory(IEnumerable<Contact> contacts)
    {
        foreach (var contact in contacts)
        {
            foreach (var phone in contact.Phones) _byKey.TryAdd(Key(phone.Value), contact);
            foreach (var email in contact.Emails) _byKey.TryAdd(Key(email.Value), contact);
        }
    }

    public Contact? Find(string? handle) =>
        handle is not null && _byKey.TryGetValue(Key(handle), out var contact) ? contact : null;

    public static string Key(string handle)
    {
        handle = handle.Trim();
        if (handle.Contains('@')) return handle.ToLowerInvariant();
        var digits = new string(handle.Where(char.IsDigit).ToArray());
        return digits.Length > SignificantDigits ? digits[^SignificantDigits..] : digits;
    }
}
