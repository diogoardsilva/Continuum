using System.Text;

namespace Continuum.Core.Contacts;

/// <summary>
/// Parses the vCard 3.0 cards that iCloud and iOS produce, including Apple's
/// grouped labels (item1.TEL + item1.X-ABLabel) and inline JPEG photos.
/// </summary>
public static class VCardParser
{
    sealed record Line(string? Group, string Name, Dictionary<string, List<string>> Params, string Value);

    public static List<Contact> ParseAll(string text)
    {
        var contacts = new List<Contact>();
        List<Line>? card = null;
        foreach (var line in Unfold(text).Select(ParseLine).OfType<Line>())
        {
            if (line.Name == "BEGIN" && line.Value.Equals("VCARD", StringComparison.OrdinalIgnoreCase))
            {
                card = new List<Line>();
            }
            else if (line.Name == "END" && line.Value.Equals("VCARD", StringComparison.OrdinalIgnoreCase))
            {
                if (card is not null) contacts.Add(Build(card));
                card = null;
            }
            else
            {
                card?.Add(line);
            }
        }
        return contacts;
    }

    public static Contact? Parse(string text) => ParseAll(text).FirstOrDefault();

    static Contact Build(List<Line> lines)
    {
        var uid = lines.FirstOrDefault(l => l.Name == "UID")?.Value;
        var contact = new Contact { Id = string.IsNullOrEmpty(uid) ? Guid.NewGuid().ToString() : uid };

        // Apple puts custom labels on a separate line in the same group.
        var groupLabels = lines
            .Where(l => l.Group is not null && l.Name == "X-ABLABEL")
            .GroupBy(l => l.Group!)
            .ToDictionary(g => g.Key, g => ContactLabels.Clean(Unescape(g.First().Value)));

        foreach (var line in lines)
        {
            switch (line.Name)
            {
                case "N":
                    var parts = SplitStructured(line.Value);
                    contact.LastName = NullIfEmpty(parts.ElementAtOrDefault(0));
                    contact.FirstName = NullIfEmpty(parts.ElementAtOrDefault(1));
                    break;
                case "FN" when contact.FirstName is null && contact.LastName is null:
                    contact.FirstName = NullIfEmpty(Unescape(line.Value));
                    break;
                case "ORG":
                    contact.Organization = NullIfEmpty(SplitStructured(line.Value).FirstOrDefault());
                    break;
                case "NICKNAME":
                    contact.Nickname = NullIfEmpty(Unescape(line.Value));
                    break;
                case "TEL":
                    contact.Phones.Add(new ContactField(LabelFor(line, groupLabels), Unescape(line.Value).Trim()));
                    break;
                case "EMAIL":
                    contact.Emails.Add(new ContactField(LabelFor(line, groupLabels), Unescape(line.Value).Trim()));
                    break;
                case "PHOTO":
                    ReadPhoto(line, contact);
                    break;
            }
        }

        return contact;
    }

    static void ReadPhoto(Line line, Contact contact)
    {
        var value = line.Value.Trim();
        var encoding = ParamValues(line, "ENCODING").FirstOrDefault()?.ToLowerInvariant();
        var valueType = ParamValues(line, "VALUE").FirstOrDefault()?.ToLowerInvariant();

        if (value.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var comma = value.IndexOf(',');
            if (comma > 0) contact.Photo = TryBase64(value[(comma + 1)..]);
        }
        else if (encoding is "b" or "base64")
        {
            contact.Photo = TryBase64(value);
        }
        else if (valueType == "uri" || value.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            contact.PhotoUri = value;
        }
    }

    static string LabelFor(Line line, Dictionary<string, string> groupLabels)
    {
        if (line.Group is not null && groupLabels.TryGetValue(line.Group, out var custom))
        {
            return custom;
        }

        var types = ParamValues(line, "TYPE")
            .Select(t => t.ToLowerInvariant())
            .Where(t => t is not ("pref" or "voice" or "internet"))
            .ToList();
        return ContactLabels.Clean(types.FirstOrDefault());
    }

    static IEnumerable<string> ParamValues(Line line, string name) =>
        line.Params.TryGetValue(name, out var values) ? values : Enumerable.Empty<string>();

    static IEnumerable<string> Unfold(string text)
    {
        var current = new StringBuilder();
        foreach (var raw in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            if (raw.Length > 0 && (raw[0] == ' ' || raw[0] == '\t'))
            {
                current.Append(raw, 1, raw.Length - 1);
                continue;
            }
            if (current.Length > 0) yield return current.ToString();
            current.Clear().Append(raw);
        }
        if (current.Length > 0) yield return current.ToString();
    }

    static Line? ParseLine(string raw)
    {
        // The value starts at the first colon that is not inside a quoted parameter.
        var inQuotes = false;
        var colon = -1;
        for (var i = 0; i < raw.Length; i++)
        {
            if (raw[i] == '"') inQuotes = !inQuotes;
            else if (raw[i] == ':' && !inQuotes) { colon = i; break; }
        }
        if (colon <= 0) return null;

        var head = raw[..colon].Split(';');
        var value = raw[(colon + 1)..];

        var nameWithGroup = head[0];
        string? group = null;
        var dot = nameWithGroup.IndexOf('.');
        if (dot > 0)
        {
            group = nameWithGroup[..dot];
            nameWithGroup = nameWithGroup[(dot + 1)..];
        }

        var parameters = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in head.Skip(1))
        {
            var eq = p.IndexOf('=');
            // vCard 2.1 style bare parameters ("TEL;CELL:...") are types.
            var key = eq > 0 ? p[..eq] : "TYPE";
            var values = (eq > 0 ? p[(eq + 1)..] : p).Trim('"').Split(',');
            if (!parameters.TryGetValue(key, out var list)) parameters[key] = list = new List<string>();
            list.AddRange(values);
        }

        return new Line(group, nameWithGroup.ToUpperInvariant(), parameters, value);
    }

    static List<string> SplitStructured(string value)
    {
        var parts = new List<string>();
        var current = new StringBuilder();
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '\\' && i + 1 < value.Length)
            {
                current.Append(value[i]).Append(value[++i]);
            }
            else if (value[i] == ';')
            {
                parts.Add(Unescape(current.ToString()));
                current.Clear();
            }
            else
            {
                current.Append(value[i]);
            }
        }
        parts.Add(Unescape(current.ToString()));
        return parts;
    }

    static string Unescape(string value)
    {
        if (!value.Contains('\\')) return value;
        var sb = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '\\' && i + 1 < value.Length)
            {
                var next = value[++i];
                sb.Append(next is 'n' or 'N' ? '\n' : next);
            }
            else
            {
                sb.Append(value[i]);
            }
        }
        return sb.ToString();
    }

    static byte[]? TryBase64(string value)
    {
        try { return Convert.FromBase64String(value.Trim()); }
        catch (FormatException) { return null; }
    }

    static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
