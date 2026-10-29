using Continuum.Core.Contacts;
using Continuum.Core.Messages;

namespace Continuum.Core.Live;

/// <summary>
/// Adds notification messages to the synced conversations. A notification
/// only carries what the lock screen shows: the title is the contact's name
/// (as saved on the iPhone), the group name, or the number.
/// </summary>
public static class LiveMessageMerger
{
    public static List<Conversation> Merge(IReadOnlyList<Conversation> synced, IEnumerable<LiveMessage> live, ContactDirectory directory, IEnumerable<Contact> contacts)
    {
        var result = synced.ToList();
        var byName = contacts
            .GroupBy(c => c.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.CurrentCultureIgnoreCase);
        long nextId = -1;

        foreach (var message in live.OrderBy(m => m.Date))
        {
            var (conversation, sender) = Find(result, message, directory, byName);
            if (conversation is null)
            {
                conversation = new Conversation { Id = nextId--, Identifier = message.Title, DisplayName = message.Title };
                conversation.Participants.Add(sender ?? message.Title);
                result.Add(conversation);
            }

            // Skip what the last cable sync already brought in.
            var last = conversation.Messages.Count == 0 ? (DateTimeOffset?)null : conversation.Messages[^1].Date;
            if (last is not null && message.Date <= last) continue;

            conversation.Messages.Add(new Message
            {
                Id = -message.Date.ToUnixTimeMilliseconds(),
                Date = message.Date,
                Text = message.Body,
                IsFromMe = false,
                Sender = sender ?? message.Subtitle ?? message.Title,
                Service = "iMessage",
            });
        }

        return result.OrderByDescending(c => c.LastActivity).ToList();
    }

    static (Conversation? Conversation, string? Sender) Find(List<Conversation> conversations, LiveMessage message,
        ContactDirectory directory, Dictionary<string, Contact> byName)
    {
        // Group chats: title is the group name, subtitle the sender.
        var group = conversations.FirstOrDefault(c => c.IsGroup && string.Equals(c.DisplayName, message.Title, StringComparison.CurrentCultureIgnoreCase));
        if (group is not null)
        {
            var senderHandle = message.Subtitle is not null && byName.TryGetValue(message.Subtitle, out var s)
                ? group.Participants.FirstOrDefault(p => directory.Find(p) == s)
                : null;
            return (group, senderHandle ?? message.Subtitle);
        }

        // One-to-one: title is the contact name or the raw number/email.
        var contact = byName.GetValueOrDefault(message.Title) ?? directory.Find(message.Title);
        var direct = conversations.FirstOrDefault(c => !c.IsGroup && c.Participants.Count > 0 && (
            (contact is not null && directory.Find(c.Participants[0]) == contact)
            || ContactDirectory.Key(c.Participants[0]) == ContactDirectory.Key(message.Title)));
        return (direct, direct?.Participants.FirstOrDefault());
    }
}
