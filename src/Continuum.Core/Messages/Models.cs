namespace Continuum.Core.Messages;

public sealed record Attachment(string? FileName, string? MimeType, string? PathOnPhone, string? PathInBackup);

public sealed class Message
{
    public required long Id { get; init; }
    public required DateTimeOffset Date { get; init; }
    public string? Text { get; init; }
    public bool IsFromMe { get; init; }
    /// <summary>Phone number or email of the sender; null when IsFromMe.</summary>
    public string? Sender { get; init; }
    /// <summary>"iMessage", "SMS" or "RCS".</summary>
    public string? Service { get; init; }
    public List<Attachment> Attachments { get; } = new();
}

public sealed class Conversation
{
    public required long Id { get; init; }
    public required string Identifier { get; init; }
    public string? DisplayName { get; init; }
    public List<string> Participants { get; } = new();
    public bool IsGroup => Participants.Count > 1;
    public List<Message> Messages { get; } = new();
    public DateTimeOffset? LastActivity => Messages.Count == 0 ? null : Messages[^1].Date;
}
