namespace Continuum.Core.Contacts;

public sealed record ContactField(string Label, string Value);

public sealed class Contact
{
    public required string Id { get; init; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Organization { get; set; }
    public string? Nickname { get; set; }
    public List<ContactField> Phones { get; } = new();
    public List<ContactField> Emails { get; } = new();
    public byte[]? Photo { get; set; }

    // iCloud sometimes sends the photo as a link that needs the same credentials.
    public string? PhotoUri { get; set; }

    public string DisplayName
    {
        get
        {
            var name = string.Join(' ', new[] { FirstName, LastName }.Where(s => !string.IsNullOrWhiteSpace(s)));
            if (name.Length > 0) return name;
            if (!string.IsNullOrWhiteSpace(Organization)) return Organization;
            if (!string.IsNullOrWhiteSpace(Nickname)) return Nickname;
            return Phones.FirstOrDefault()?.Value ?? Emails.FirstOrDefault()?.Value ?? "(sem nome)";
        }
    }

    public override string ToString() => DisplayName;
}
