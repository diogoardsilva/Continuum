using System.Globalization;

namespace Continuum.Services;

/// <summary>Dates and durations written the way the iPhone writes them in Portuguese.</summary>
public static class Format
{
    static readonly CultureInfo Pt = CultureInfo.GetCultureInfo("pt-PT");

    public static string ListDate(DateTimeOffset date, DateTimeOffset? now = null)
    {
        var today = (now ?? DateTimeOffset.Now).LocalDateTime.Date;
        var local = date.LocalDateTime;
        if (local.Date == today) return local.ToString("HH:mm", Pt);
        if (local.Date == today.AddDays(-1)) return "Ontem";
        if (local.Date > today.AddDays(-7)) return Capitalize(local.ToString("dddd", Pt));
        return local.ToString("dd/MM/yy", Pt);
    }

    public static string ChatHeader(DateTimeOffset date, DateTimeOffset? now = null)
    {
        var today = (now ?? DateTimeOffset.Now).LocalDateTime.Date;
        var local = date.LocalDateTime;
        var time = local.ToString("HH:mm", Pt);
        if (local.Date == today) return $"Hoje {time}";
        if (local.Date == today.AddDays(-1)) return $"Ontem {time}";
        if (local.Date > today.AddDays(-7)) return $"{Capitalize(local.ToString("dddd", Pt))} {time}";
        return local.ToString("d 'de' MMMM 'de' yyyy, HH:mm", Pt);
    }

    public static string LongDay(DateTime date) => Capitalize(date.ToString("dddd, d 'de' MMMM", Pt));

    public static string Duration(TimeSpan d) =>
        d.TotalHours >= 1 ? d.ToString(@"h\:mm\:ss") : d.ToString(@"m\:ss");

    public static string Initials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => char.IsLetter(p[0]))
            .ToArray();
        if (parts.Length == 0) return "";
        var first = char.ToUpperInvariant(parts[0][0]).ToString();
        return parts.Length > 1 ? first + char.ToUpperInvariant(parts[^1][0]) : first;
    }

    static string Capitalize(string s) => s.Length == 0 ? s : char.ToUpper(s[0], Pt) + s[1..];
}
