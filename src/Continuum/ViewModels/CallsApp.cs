using CommunityToolkit.Mvvm.ComponentModel;
using Continuum.Core.Calls;
using Continuum.Core.Contacts;
using Continuum.Services;

namespace Continuum.ViewModels;

public sealed record CallItem(CallRecord Call, Contact? Contact, string Name, string Detail, string Date, Avatar Avatar)
{
    public bool Missed => Call.Missed;
    public bool Outgoing => Call.Outgoing;
    public bool IsVideo => Call.Kind == CallKind.FaceTimeVideo;
}

public sealed record CallDetail(CallItem Item, string When, string What, ContactCard? Card)
{
    public bool HasCard => Card is not null;
}

/// <summary>Shared by Telefone and FaceTime: a list of calls with filters and a detail pane.</summary>
public abstract partial class CallsAppViewModel : ObservableObject
{
    readonly List<CallItem> _all;

    protected CallsAppViewModel(AppServices services, Func<CallRecord, bool> include)
    {
        var library = services.Library;
        _all = library.Calls.Where(include).Select(call =>
        {
            var contact = library.Directory.Find(call.Address);
            var name = contact?.DisplayName ?? call.Name ?? Format.Phone(call.Address) ?? "Desconhecido";
            var detail = call.Kind switch
            {
                CallKind.FaceTimeVideo => "Vídeo FaceTime",
                CallKind.FaceTimeAudio => "Áudio FaceTime",
                _ => contact?.Phones.FirstOrDefault(p => call.Address is not null
                        && ContactDirectory.Key(p.Value) == ContactDirectory.Key(call.Address))?.Label ?? "telefone",
            };
            return new CallItem(call, contact, name, detail, Format.ListDate(call.Date), Avatar.For(contact, name));
        }).ToList();
        _items = _all;
        Selected = _all.FirstOrDefault();
    }

    public abstract string AppName { get; }

    [ObservableProperty]
    private IReadOnlyList<CallItem> _items;

    [ObservableProperty]
    private bool _missedOnly;

    [ObservableProperty]
    private CallItem? _selected;

    [ObservableProperty]
    private CallDetail? _detail;

    partial void OnMissedOnlyChanged(bool value) => Items = value ? _all.Where(i => i.Missed).ToList() : _all;

    partial void OnSelectedChanged(CallItem? value)
    {
        if (value is null) { Detail = null; return; }
        var call = value.Call;
        var direction = call.Missed ? "Chamada perdida" : call.Outgoing ? "Chamada efetuada" : "Chamada recebida";
        var what = call.Duration > TimeSpan.Zero ? $"{direction} · {Format.Duration(call.Duration)}" : direction;
        Detail = new CallDetail(value, Format.ChatHeader(call.Date), $"{value.Detail} · {what}",
            value.Contact is null ? null : new ContactCard(value.Contact));
    }
}

public sealed class PhoneAppViewModel : CallsAppViewModel
{
    public PhoneAppViewModel(AppServices services) : base(services, _ => true) { }
    public override string AppName => "Telefone";
}

public sealed class FaceTimeAppViewModel : CallsAppViewModel
{
    public FaceTimeAppViewModel(AppServices services)
        : base(services, c => c.Kind is CallKind.FaceTimeVideo or CallKind.FaceTimeAudio) { }
    public override string AppName => "FaceTime";
}
