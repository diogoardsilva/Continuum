using Avalonia.Media.Imaging;
using Continuum.Core.Contacts;
using Continuum.Services;

namespace Continuum.ViewModels;

public sealed class Avatar
{
    public Avatar(string name, byte[]? photo)
    {
        Initials = Format.Initials(name);
        Photo = Images.TryDecode(photo, 160);
    }

    public string Initials { get; }
    public Bitmap? Photo { get; }
    public bool HasPhoto => Photo is not null;
    public bool HasInitials => Photo is null && Initials.Length > 0;
    public bool IsAnonymous => Photo is null && Initials.Length == 0;

    public static Avatar For(Contact? contact, string fallbackName) =>
        new(contact?.DisplayName ?? fallbackName, contact?.Photo);
}
