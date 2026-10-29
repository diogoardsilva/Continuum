using System.Buffers.Binary;
using System.Text;

namespace Continuum.Core.Live;

/// <summary>
/// Apple Notification Center Service (ANCS): the Bluetooth LE service an
/// iPhone exposes to paired accessories (watches, car kits) so they can read
/// its notifications. Packet formats follow Apple's ANCS specification.
/// </summary>
public static class Ancs
{
    public static readonly Guid Service = new("7905F431-B5CE-4E99-A40F-4B1E122D00D0");
    public static readonly Guid NotificationSource = new("9FBF120D-6301-42D9-8C58-25E699A21DBD");
    public static readonly Guid ControlPoint = new("69D1D8F3-45E1-49A8-9821-9BBDFDAAD9D9");
    public static readonly Guid DataSource = new("22EAC6E9-24D6-4BB5-BE44-B36ACE7C7BFB");

    public const string MessagesAppId = "com.apple.MobileSMS";

    public enum EventId : byte { Added = 0, Modified = 1, Removed = 2 }

    public enum Category : byte
    {
        Other = 0, IncomingCall = 1, MissedCall = 2, Voicemail = 3, Social = 4, Schedule = 5,
        Email = 6, News = 7, HealthAndFitness = 8, BusinessAndFinance = 9, Location = 10, Entertainment = 11,
    }

    public enum Attribute : byte
    {
        AppIdentifier = 0, Title = 1, Subtitle = 2, Message = 3, MessageSize = 4, Date = 5,
        PositiveActionLabel = 6, NegativeActionLabel = 7,
    }

    [Flags]
    public enum EventFlags : byte { Silent = 1, Important = 2, PreExisting = 4, PositiveAction = 8, NegativeAction = 16 }

    public sealed record NotificationEvent(EventId Event, EventFlags Flags, Category Category, byte CategoryCount, uint Uid);

    /// <summary>Notification Source packets are always 8 bytes.</summary>
    public static NotificationEvent? ParseNotificationSource(ReadOnlySpan<byte> data)
    {
        if (data.Length < 8) return null;
        return new NotificationEvent((EventId)data[0], (EventFlags)data[1], (Category)data[2], data[3],
            BinaryPrimitives.ReadUInt32LittleEndian(data[4..8]));
    }

    /// <summary>Get Notification Attributes command (CommandID 0) for the Control Point.</summary>
    public static byte[] GetNotificationAttributes(uint uid, ushort maxTextLength = 1000)
    {
        var buffer = new List<byte> { 0 };
        Span<byte> u = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(u, uid);
        buffer.AddRange(u.ToArray());
        buffer.Add((byte)Attribute.AppIdentifier);
        foreach (var attr in new[] { Attribute.Title, Attribute.Subtitle, Attribute.Message })
        {
            buffer.Add((byte)attr);
            buffer.Add((byte)(maxTextLength & 0xFF));
            buffer.Add((byte)(maxTextLength >> 8));
        }
        buffer.Add((byte)Attribute.Date);
        return buffer.ToArray();
    }

    /// <summary>Number of attributes requested by <see cref="GetNotificationAttributes"/>.</summary>
    public const int RequestedAttributeCount = 5;

    public sealed record NotificationAttributes(uint Uid, IReadOnlyDictionary<Attribute, string> Values)
    {
        public string? Get(Attribute a) => Values.TryGetValue(a, out var v) && v.Length > 0 ? v : null;

        /// <summary>ANCS dates are local time "yyyyMMdd'T'HHmmSS".</summary>
        public DateTimeOffset? Date =>
            Get(Attribute.Date) is { } d && DateTime.TryParseExact(d, "yyyyMMdd'T'HHmmss",
                System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeLocal, out var t)
                ? new DateTimeOffset(t)
                : null;
    }

    /// <summary>
    /// Data Source responses can arrive split over several GATT notifications;
    /// feed every chunk in and a complete response comes out.
    /// </summary>
    public sealed class DataSourceAssembler
    {
        readonly List<byte> _buffer = new();

        public NotificationAttributes? Add(ReadOnlySpan<byte> chunk)
        {
            _buffer.AddRange(chunk.ToArray());
            var result = TryParse(_buffer.ToArray(), RequestedAttributeCount, out var consumed);
            if (result is not null) _buffer.RemoveRange(0, consumed);
            return result;
        }

        public void Reset() => _buffer.Clear();
    }

    public static NotificationAttributes? TryParse(byte[] data, int expectedAttributes, out int consumed)
    {
        consumed = 0;
        if (data.Length < 5 || data[0] != 0) return null;
        var uid = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(1, 4));
        var values = new Dictionary<Attribute, string>();
        var i = 5;
        for (var n = 0; n < expectedAttributes; n++)
        {
            if (i + 3 > data.Length) return null;
            var id = (Attribute)data[i];
            var length = data[i + 1] | (data[i + 2] << 8);
            if (i + 3 + length > data.Length) return null;
            values[id] = Encoding.UTF8.GetString(data, i + 3, length);
            i += 3 + length;
        }
        consumed = i;
        return new NotificationAttributes(uid, values);
    }
}
