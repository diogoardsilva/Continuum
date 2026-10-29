using Continuum.Core.Calls;
using Continuum.Core.Contacts;
using Continuum.Core.Messages;

namespace Continuum.Services;

/// <summary>Demo data shown until the iPhone has been synced once.</summary>
public sealed class SamplePhoneLibrary : IPhoneLibrary
{
    public SamplePhoneLibrary(DateTimeOffset? now = null)
    {
        var t = now ?? DateTimeOffset.Now;
        var contacts = new List<Contact>
        {
            Person("1", "Ana", "Costa", "+351 912 345 678"),
            Person("2", "Bruno", "Ferreira", "+351 913 222 111"),
            Person("3", "Mãe", null, "+351 917 000 001"),
            Person("4", "Rita", "Almeida", "+351 918 765 432"),
            Person("5", "Tiago", "Santos", "+351 926 111 222"),
            Person("6", "Inês", "Moreira", "+351 935 444 555"),
            Person("7", "João", "Pereira", "+351 961 999 888"),
        };
        contacts[0].Emails.Add(new ContactField("casa", "ana.costa@example.com"));
        Contacts = contacts;
        Directory = new ContactDirectory(contacts);

        Conversations = new List<Conversation>
        {
            Chat(1, "+351917000001", null, new[] { "+351917000001" },
                (t.AddMinutes(-42), false, "+351917000001", "Já jantaste?"),
                (t.AddMinutes(-40), true, null, "Ainda não, vou agora"),
                (t.AddMinutes(-3), false, "+351917000001", "Não te esqueças de levar o casaco amanhã 😊")),
            Chat(2, "+351912345678", null, new[] { "+351912345678" },
                (t.AddHours(-3), false, "+351912345678", "Viste o jogo ontem?"),
                (t.AddHours(-3).AddMinutes(2), true, null, "Vi! Que golo no fim"),
                (t.AddHours(-2), false, "+351912345678", "Incrível. Sábado há mais")),
            Chat(3, "chat-amigos", "Amigos 🍻", new[] { "+351913222111", "+351926111222", "+351961999888" },
                (t.AddDays(-1).AddHours(-5), false, "+351913222111", "Bora jantar sexta?"),
                (t.AddDays(-1).AddHours(-5).AddMinutes(1), false, "+351926111222", "Bora!"),
                (t.AddDays(-1).AddHours(-4), true, null, "Contem comigo"),
                (t.AddDays(-1), false, "+351961999888", "Reservei para as 20h30")),
            Chat(4, "+351918765432", null, new[] { "+351918765432" },
                (t.AddDays(-3), true, null, "Obrigado pela ajuda!"),
                (t.AddDays(-3).AddMinutes(5), false, "+351918765432", "De nada, quando precisares diz")),
        };

        Calls = new List<CallRecord>
        {
            new(t.AddMinutes(-15), TimeSpan.Zero, "+351917000001", null, false, false, CallKind.Phone),
            new(t.AddHours(-2), TimeSpan.FromMinutes(12), "+351912345678", null, true, true, CallKind.FaceTimeVideo),
            new(t.AddHours(-6), TimeSpan.FromMinutes(3), "+351935444555", null, false, true, CallKind.Phone),
            new(t.AddDays(-1), TimeSpan.FromSeconds(40), "+351210000000", null, true, true, CallKind.Phone),
            new(t.AddDays(-2), TimeSpan.Zero, "+351926111222", null, false, false, CallKind.FaceTimeAudio),
        };
    }

    public string Source => "Dados de exemplo";
    public IReadOnlyList<Contact> Contacts { get; }
    public IReadOnlyList<Conversation> Conversations { get; }
    public IReadOnlyList<CallRecord> Calls { get; }
    public ContactDirectory Directory { get; }
    public byte[]? LoadAttachment(Attachment attachment) => null;

    static Contact Person(string id, string first, string? last, string phone)
    {
        var c = new Contact { Id = id, FirstName = first, LastName = last };
        c.Phones.Add(new ContactField("telemóvel", phone));
        return c;
    }

    static Conversation Chat(long id, string identifier, string? name, string[] participants,
        params (DateTimeOffset Date, bool FromMe, string? Sender, string Text)[] messages)
    {
        var c = new Conversation { Id = id, Identifier = identifier, DisplayName = name };
        c.Participants.AddRange(participants);
        var n = id * 100;
        foreach (var m in messages)
        {
            c.Messages.Add(new Message { Id = n++, Date = m.Date, IsFromMe = m.FromMe, Sender = m.Sender, Text = m.Text, Service = "iMessage" });
        }
        return c;
    }
}
