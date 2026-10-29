// Continuum notifications: listens to the iPhone's notifications over
// Bluetooth (ANCS, the same service Apple Watch-style accessories use) and
// stores every incoming Messages notification, so the Messages app shows new
// messages without a cable sync.
//
// The iPhone must be paired with this PC once in Windows Settings > Bluetooth.

using System.Runtime.InteropServices.WindowsRuntime;
using Continuum.Core.Live;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;

Console.OutputEncoding = System.Text.Encoding.UTF8;
var quiet = args.Contains("--quiet");
void Log(string text) { if (!quiet) Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {text}"); }

var store = new LiveMessageStore(LiveMessageStore.DefaultPath);
Log("Continuum - notificações do iPhone por Bluetooth");
Log($"A guardar mensagens em {LiveMessageStore.DefaultPath}");

while (true)
{
    try
    {
        await RunOnce();
    }
    catch (Exception ex)
    {
        Log($"Erro: {ex.Message}");
    }
    Log("A tentar novamente daqui a 10 segundos…");
    await Task.Delay(TimeSpan.FromSeconds(10));
}

async Task RunOnce()
{
    var (device, service) = await FindIPhone();
    if (device is null || service is null)
    {
        Log("Não encontrei um iPhone emparelhado com o serviço de notificações.");
        Log("Emparelha o iPhone em Definições > Bluetooth e dispositivos > Adicionar dispositivo, e mantém-no desbloqueado.");
        return;
    }
    Log($"Ligado a {device.Name}.");

    var chars = await service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
    if (chars.Status != GattCommunicationStatus.Success) throw new InvalidOperationException($"características: {chars.Status}");
    GattCharacteristic Char(Guid id) => chars.Characteristics.First(c => c.Uuid == id);
    var notificationSource = Char(Ancs.NotificationSource);
    var controlPoint = Char(Ancs.ControlPoint);
    var dataSource = Char(Ancs.DataSource);

    var assembler = new Ancs.DataSourceAssembler();
    var pending = new Queue<uint>();
    var gate = new SemaphoreSlim(1);
    var disconnected = new TaskCompletionSource();

    device.ConnectionStatusChanged += (d, _) =>
    {
        if (d.ConnectionStatus == BluetoothConnectionStatus.Disconnected) disconnected.TrySetResult();
    };

    dataSource.ValueChanged += (_, e) =>
    {
        var attributes = assembler.Add(e.CharacteristicValue.ToArray());
        if (attributes is null) return;
        if (attributes.Get(Ancs.Attribute.AppIdentifier) == Ancs.MessagesAppId
            && attributes.Get(Ancs.Attribute.Title) is { } title
            && attributes.Get(Ancs.Attribute.Message) is { } body)
        {
            var message = new LiveMessage(attributes.Date ?? DateTimeOffset.Now, title, attributes.Get(Ancs.Attribute.Subtitle), body);
            if (store.Add(message)) Log($"Mensagem de {title}: {body}");
        }
        gate.Release();
    };

    notificationSource.ValueChanged += async (_, e) =>
    {
        var ev = Ancs.ParseNotificationSource(e.CharacteristicValue.ToArray());
        if (ev is null || ev.Event != Ancs.EventId.Added || ev.Category != Ancs.Category.Social) return;
        // One attribute request at a time: Data Source replies are not tagged by request.
        await gate.WaitAsync();
        assembler.Reset();
        var result = await controlPoint.WriteValueWithResultAsync(Ancs.GetNotificationAttributes(ev.Uid).AsBuffer());
        if (result.Status != GattCommunicationStatus.Success) gate.Release();
    };

    // Apple asks for the Data Source subscription first, then the Notification Source.
    await Subscribe(dataSource);
    await Subscribe(notificationSource);
    Log("À espera de mensagens. Podes minimizar esta janela.");

    await disconnected.Task;
    Log("O iPhone desligou-se.");
}

async Task Subscribe(GattCharacteristic c)
{
    var status = await c.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);
    if (status != GattCommunicationStatus.Success)
    {
        throw new InvalidOperationException(status == GattCommunicationStatus.AccessDenied
            ? "o iPhone recusou o acesso às notificações. Em Definições > Bluetooth no iPhone, toca no (i) do PC e ativa \"Partilhar notificações do sistema\"."
            : $"subscrição falhou: {status}");
    }
}

async Task<(BluetoothLEDevice? Device, GattDeviceService? Service)> FindIPhone()
{
    var paired = await DeviceInformation.FindAllAsync(BluetoothLEDevice.GetDeviceSelectorFromPairingState(true));
    foreach (var info in paired)
    {
        var device = await BluetoothLEDevice.FromIdAsync(info.Id);
        if (device is null) continue;
        var services = await device.GetGattServicesForUuidAsync(Ancs.Service, BluetoothCacheMode.Uncached);
        if (services.Status == GattCommunicationStatus.Success && services.Services.Count > 0)
        {
            return (device, services.Services[0]);
        }
        device.Dispose();
    }
    return (null, null);
}
