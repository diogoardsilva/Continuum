using Continuum.Core.Calls;
using Continuum.Core.Contacts;
using Continuum.Core.Messages;

namespace Continuum.Services;

/// <summary>Everything the iPhone-style apps show, wherever it came from.</summary>
public interface IPhoneLibrary
{
    string Source { get; }
    IReadOnlyList<Contact> Contacts { get; }
    IReadOnlyList<Conversation> Conversations { get; }
    IReadOnlyList<CallRecord> Calls { get; }
    ContactDirectory Directory { get; }

    /// <summary>Bytes of a message attachment, or null when it is not available locally.</summary>
    byte[]? LoadAttachment(Attachment attachment);
}
