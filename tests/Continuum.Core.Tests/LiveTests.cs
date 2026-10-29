using Continuum.Core.Contacts;
using Continuum.Core.Live;
using Continuum.Core.Messages;
using Xunit;

namespace Continuum.Core.Tests;

public sealed class LiveTests : IDisposable
{
    readonly string _db = Path.Combine(Path.GetTempPath(), "continuum-live-" + Guid.NewGuid().ToString("N") + ".db");

    public void Dispose() => File.Delete(_db);

    [Fact]
    public void ParsesNotificationSource()
    {
        var e = Ancs.ParseNotificationSource(new byte[] { 0, 0x02, 4, 3, 0x2A, 0, 0, 0 })!;
        Assert.Equal(Ancs.EventId.Added, e.Event);
        Assert.Equal(Ancs.Category.Social, e.Category);
        Assert.True(e.Flags.HasFlag(Ancs.EventFlags.Important));
        Assert.Equal(42u, e.Uid);
    }

    [Fact]
    public void BuildsAttributeRequest()
    {
        var cmd = Ancs.GetNotificationAttributes(7, 500);
        Assert.Equal(new byte[] { 0, 7, 0, 0, 0, 0, 1, 0xF4, 0x01, 2, 0xF4, 0x01, 3, 0xF4, 0x01, 5 }, cmd);
    }

    [Fact]
    public void AssemblesFragmentedDataSourceResponse()
    {
        var response = new List<byte> { 0, 7, 0, 0, 0 };
        void Attr(Ancs.Attribute a, string v)
        {
            var b = System.Text.Encoding.UTF8.GetBytes(v);
            response.Add((byte)a); response.Add((byte)b.Length); response.Add(0); response.AddRange(b);
        }
        Attr(Ancs.Attribute.AppIdentifier, Ancs.MessagesAppId);
        Attr(Ancs.Attribute.Title, "Mãe");
        Attr(Ancs.Attribute.Subtitle, "");
        Attr(Ancs.Attribute.Message, "Já chegaste?");
        Attr(Ancs.Attribute.Date, "20261029T213000");
        var bytes = response.ToArray();

        var assembler = new Ancs.DataSourceAssembler();
        Assert.Null(assembler.Add(bytes.AsSpan(0, 20)));
        var result = assembler.Add(bytes.AsSpan(20))!;

        Assert.Equal(7u, result.Uid);
        Assert.Equal(Ancs.MessagesAppId, result.Get(Ancs.Attribute.AppIdentifier));
        Assert.Equal("Mãe", result.Get(Ancs.Attribute.Title));
        Assert.Null(result.Get(Ancs.Attribute.Subtitle));
        Assert.Equal("Já chegaste?", result.Get(Ancs.Attribute.Message));
        Assert.Equal(new DateTime(2026, 10, 29, 21, 30, 0), result.Date!.Value.DateTime);
    }

    [Fact]
    public void StoreIgnoresDuplicates()
    {
        var store = new LiveMessageStore(_db);
        var m = new LiveMessage(DateTimeOffset.FromUnixTimeSeconds(1_800_000_000), "Mãe", null, "Olá");
        Assert.True(store.Add(m));
        Assert.False(store.Add(m));
        Assert.Equal(m, store.All().Single());
    }

    [Fact]
    public void MergesIntoExistingAndNewConversations()
    {
        var mae = new Contact { Id = "1", FirstName = "Mãe" };
        mae.Phones.Add(new ContactField("telemóvel", "+351917000001"));
        var directory = new ContactDirectory(new[] { mae });
        var t0 = new DateTimeOffset(2026, 10, 29, 20, 0, 0, TimeSpan.Zero);

        var direct = new Conversation { Id = 1, Identifier = "+351917000001" };
        direct.Participants.Add("+351917000001");
        direct.Messages.Add(new Message { Id = 1, Date = t0, Text = "Antigo" });
        var group = new Conversation { Id = 2, Identifier = "chat1", DisplayName = "Família" };
        group.Participants.AddRange(new[] { "+351917000001", "+351912000000" });
        group.Messages.Add(new Message { Id = 2, Date = t0.AddDays(-1), Text = "x" });

        var live = new[]
        {
            new LiveMessage(t0.AddMinutes(-5), "Mãe", null, "Já sincronizada"),
            new LiveMessage(t0.AddMinutes(10), "Mãe", null, "Nova"),
            new LiveMessage(t0.AddMinutes(11), "Família", "Mãe", "Jantar às 20h"),
            new LiveMessage(t0.AddMinutes(12), "+351 966 000 000", null, "Quem é?"),
        };

        var merged = LiveMessageMerger.Merge(new[] { direct, group }, live, directory, new[] { mae });

        Assert.Equal(new[] { "Antigo", "Nova" }, direct.Messages.Select(m => m.Text));
        Assert.Equal("+351917000001", group.Messages[^1].Sender);
        Assert.Equal("+351 966 000 000", merged[0].Identifier);
        Assert.Equal(3, merged.Count);
    }
}
