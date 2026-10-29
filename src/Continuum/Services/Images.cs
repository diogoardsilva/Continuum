using Avalonia.Media.Imaging;

namespace Continuum.Services;

public static class Images
{
    /// <summary>Decodes JPEG/PNG bytes; returns null for formats Avalonia cannot show (e.g. HEIC).</summary>
    public static Bitmap? TryDecode(byte[]? data, int? decodeWidth = null)
    {
        if (data is null || data.Length == 0) return null;
        try
        {
            using var stream = new MemoryStream(data);
            return decodeWidth is { } w ? Bitmap.DecodeToWidth(stream, w) : new Bitmap(stream);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
