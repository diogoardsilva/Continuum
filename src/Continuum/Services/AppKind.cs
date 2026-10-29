namespace Continuum.Services;

public enum AppKind { Launcher, Messages, Contacts, Photos, Phone, FaceTime }

public sealed record AppInfo(AppKind Kind, string Name, string Argument, string Icon, string Color)
{
    /// <summary>Separate taskbar button per app, as if each were its own program.</summary>
    public string AppUserModelId => "Continuum." + Argument;

    public static readonly IReadOnlyList<AppInfo> All = new[]
    {
        new AppInfo(AppKind.Messages, "Mensagens", "mensagens", Icons.Message, "#30D158"),
        new AppInfo(AppKind.FaceTime, "FaceTime", "facetime", Icons.Video, "#30D158"),
        new AppInfo(AppKind.Phone, "Telefone", "telefone", Icons.Phone, "#34C759"),
        new AppInfo(AppKind.Contacts, "Contactos", "contactos", Icons.Person, "#8E8E93"),
        new AppInfo(AppKind.Photos, "Fotos", "fotos", Icons.Flower, "#FF9F0A"),
    };

    public static AppInfo? FromArguments(string[] args)
    {
        var i = Array.FindIndex(args, a => a is "--app" or "-a");
        var name = i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        return All.FirstOrDefault(a => string.Equals(a.Argument, name, StringComparison.OrdinalIgnoreCase));
    }
}
