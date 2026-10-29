namespace Continuum.Core.Contacts;

internal static class ContactLabels
{
    /// <summary>Turns Apple's stored labels ("_$!&lt;Mobile&gt;!$_", "Home", ...) into Portuguese display labels.</summary>
    public static string Clean(string? label)
    {
        if (string.IsNullOrWhiteSpace(label)) return "outro";
        if (label.StartsWith("_$!<") && label.EndsWith(">!$_")) label = label[4..^4];
        return label.ToLowerInvariant() switch
        {
            "mobile" or "cell" => "telemóvel",
            "iphone" => "iPhone",
            "home" => "casa",
            "work" => "trabalho",
            "main" => "principal",
            "fax" => "fax",
            "other" => "outro",
            _ => label,
        };
    }
}
