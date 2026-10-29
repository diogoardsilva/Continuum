using Continuum.Core.Calls;
using Continuum.Services;

namespace Continuum.ViewModels;

public sealed record CallItem(string Name, string Detail, string Date, bool Missed, bool Outgoing, Avatar Avatar);

public sealed class PhoneViewModel : PageViewModel
{
    public PhoneViewModel(MainViewModel main) : base(main)
    {
        Recents = main.Library.Calls.Select(call =>
        {
            var contact = main.Library.Directory.Find(call.Address);
            var name = contact?.DisplayName ?? call.Name ?? call.Address ?? "Desconhecido";
            var detail = call.Kind switch
            {
                CallKind.FaceTimeVideo => "Vídeo FaceTime",
                CallKind.FaceTimeAudio => "Áudio FaceTime",
                _ => contact?.Phones.FirstOrDefault(p => ContactDirectoryKeyEquals(p.Value, call.Address))?.Label ?? "telefone",
            };
            return new CallItem(name, detail, Format.ListDate(call.Date), call.Missed, call.Outgoing, Avatar.For(contact, name));
        }).ToList();
    }

    public override string Title => "Recentes";
    public IReadOnlyList<CallItem> Recents { get; }

    static bool ContactDirectoryKeyEquals(string a, string? b) =>
        b is not null && Core.Contacts.ContactDirectory.Key(a) == Core.Contacts.ContactDirectory.Key(b);
}

public sealed class PhotosViewModel : PageViewModel
{
    public PhotosViewModel(MainViewModel main) : base(main) { }
    public override string Title => "Fotos";
}
