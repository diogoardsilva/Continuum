// Continuum Bluetooth HID probe.
//
// Answers one question: can this Windows PC present itself to an iPhone as a
// Bluetooth LE mouse + keyboard (HID over GATT) using only the built-in
// Bluetooth stack? If it can, Continuum controls the iPhone this way; if not,
// we fall back to WebDriverAgent or an external dongle.
//
// The probe publishes a HID service, advertises it, and once the iPhone has
// paired and subscribed it lets you move the iPhone's AssistiveTouch pointer
// from the console.

using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;

namespace Continuum.BtHidProbe;

internal static class Program
{
    const byte MouseReportId = 1;
    const byte KeyboardReportId = 2;
    const byte ConsumerReportId = 3;

    // Mouse (ID 1): 3 buttons, X, Y, wheel (relative, int8).
    // Keyboard (ID 2): standard 8-byte boot-style report + LED output.
    // Consumer (ID 3): one 16-bit usage, used for Home (AC Home 0x0223).
    static readonly byte[] ReportMap =
    {
        0x05, 0x01, 0x09, 0x02, 0xA1, 0x01, 0x85, MouseReportId,
        0x09, 0x01, 0xA1, 0x00,
        0x05, 0x09, 0x19, 0x01, 0x29, 0x03, 0x15, 0x00, 0x25, 0x01,
        0x95, 0x03, 0x75, 0x01, 0x81, 0x02,
        0x95, 0x01, 0x75, 0x05, 0x81, 0x03,
        0x05, 0x01, 0x09, 0x30, 0x09, 0x31, 0x09, 0x38,
        0x15, 0x81, 0x25, 0x7F, 0x75, 0x08, 0x95, 0x03, 0x81, 0x06,
        0xC0, 0xC0,

        0x05, 0x01, 0x09, 0x06, 0xA1, 0x01, 0x85, KeyboardReportId,
        0x05, 0x07, 0x19, 0xE0, 0x29, 0xE7, 0x15, 0x00, 0x25, 0x01,
        0x75, 0x01, 0x95, 0x08, 0x81, 0x02,
        0x95, 0x01, 0x75, 0x08, 0x81, 0x01,
        0x95, 0x05, 0x75, 0x01, 0x05, 0x08, 0x19, 0x01, 0x29, 0x05, 0x91, 0x02,
        0x95, 0x01, 0x75, 0x03, 0x91, 0x01,
        0x95, 0x06, 0x75, 0x08, 0x15, 0x00, 0x25, 0x65,
        0x05, 0x07, 0x19, 0x00, 0x29, 0x65, 0x81, 0x00,
        0xC0,

        0x05, 0x0C, 0x09, 0x01, 0xA1, 0x01, 0x85, ConsumerReportId,
        0x15, 0x00, 0x26, 0xFF, 0x03, 0x19, 0x00, 0x2A, 0xFF, 0x03,
        0x75, 0x10, 0x95, 0x01, 0x81, 0x00,
        0xC0,
    };

    static readonly Guid HidInformationUuid = GattCharacteristicUuids.HidInformation;
    static readonly Guid ReportMapUuid = GattCharacteristicUuids.ReportMap;
    static readonly Guid HidControlPointUuid = GattCharacteristicUuids.HidControlPoint;
    static readonly Guid ProtocolModeUuid = GattCharacteristicUuids.ProtocolMode;
    static readonly Guid ReportUuid = GattCharacteristicUuids.Report;
    static readonly Guid ReportReferenceUuid = BluetoothUuidHelper.FromShortId(0x2908);

    static GattLocalCharacteristic? _mouse;
    static GattLocalCharacteristic? _keyboard;
    static GattLocalCharacteristic? _consumer;

    static async Task<int> Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("Continuum - teste de rato/teclado Bluetooth");
        Console.WriteLine("===========================================");
        Console.WriteLine();

        var adapter = await BluetoothAdapter.GetDefaultAsync();
        if (adapter is null)
        {
            return Fail("Não foi encontrado nenhum adaptador Bluetooth neste PC.");
        }

        Console.WriteLine($"Adaptador: {adapter.DeviceId}");
        Console.WriteLine($"  Bluetooth LE:          {Yes(adapter.IsLowEnergySupported)}");
        Console.WriteLine($"  Papel de periférico:   {Yes(adapter.IsPeripheralRoleSupported)}");
        Console.WriteLine($"  Papel central:         {Yes(adapter.IsCentralRoleSupported)}");
        Console.WriteLine();

        if (!adapter.IsPeripheralRoleSupported)
        {
            return Fail("O adaptador Bluetooth não suporta o papel de periférico. "
                + "Sem isso o PC não consegue fazer de rato para o iPhone.");
        }

        Console.WriteLine("A publicar o serviço HID (0x1812)...");
        var created = await GattServiceProvider.CreateAsync(GattServiceUuids.HumanInterfaceDevice);
        if (created.Error != BluetoothError.Success)
        {
            return Fail($"O Windows recusou criar o serviço HID: {created.Error}. "
                + "Isto confirma que é preciso outro caminho (WebDriverAgent ou dongle).");
        }

        var provider = created.ServiceProvider;
        var service = provider.Service;

        try
        {
            await AddStatic(service, HidInformationUuid, new byte[] { 0x11, 0x01, 0x00, 0x02 });
            await AddStatic(service, ReportMapUuid, ReportMap);
            await AddProtocolMode(service);
            await AddControlPoint(service);
            _mouse = await AddInputReport(service, MouseReportId, 4);
            _keyboard = await AddInputReport(service, KeyboardReportId, 8);
            await AddOutputReport(service, KeyboardReportId);
            _consumer = await AddInputReport(service, ConsumerReportId, 2);
        }
        catch (Exception ex)
        {
            return Fail($"Falhou ao criar as características HID: {ex.Message}");
        }

        provider.AdvertisementStatusChanged += (s, e) =>
            Console.WriteLine($"[anúncio] {e.Status}{(e.Error != BluetoothError.Success ? $" ({e.Error})" : "")}");

        try
        {
            provider.StartAdvertising(new GattServiceProviderAdvertisingParameters
            {
                IsConnectable = true,
                IsDiscoverable = true,
            });
        }
        catch (Exception ex)
        {
            return Fail($"O Windows não deixou anunciar o serviço: {ex.Message}");
        }

        Console.WriteLine();
        Console.WriteLine("Serviço publicado. Agora no iPhone:");
        Console.WriteLine("  1. Definições > Acessibilidade > Toque > AssistiveTouch: ligar.");
        Console.WriteLine("  2. Definições > Bluetooth: procura este PC na lista e emparelha.");
        Console.WriteLine("     (Se não aparecer ao fim de 30 s, diz-me exatamente o que vês.)");
        Console.WriteLine();
        Console.WriteLine("Quando o iPhone estiver ligado, usa as teclas aqui:");
        Console.WriteLine("  Setas = mover o cursor   Espaço = clicar   H = ecrã principal");
        Console.WriteLine("  T = escrever \"ola\"       C = cursor ao canto superior esquerdo");
        Console.WriteLine("  Esc = sair");
        Console.WriteLine();

        await RunConsoleLoop();

        provider.StopAdvertising();
        return 0;
    }

    static async Task RunConsoleLoop()
    {
        var lastSubscribers = -1;
        while (true)
        {
            var subs = _mouse?.SubscribedClients.Count ?? 0;
            if (subs != lastSubscribers)
            {
                Console.WriteLine(subs > 0
                    ? $"[ligação] iPhone ligado e subscrito ({subs}). Experimenta as setas!"
                    : "[ligação] À espera que o iPhone emparelhe e subscreva...");
                lastSubscribers = subs;
            }

            if (!Console.KeyAvailable)
            {
                await Task.Delay(50);
                continue;
            }

            var key = Console.ReadKey(intercept: true).Key;
            switch (key)
            {
                case ConsoleKey.Escape:
                    return;
                case ConsoleKey.LeftArrow: await Move(-20, 0); break;
                case ConsoleKey.RightArrow: await Move(20, 0); break;
                case ConsoleKey.UpArrow: await Move(0, -20); break;
                case ConsoleKey.DownArrow: await Move(0, 20); break;
                case ConsoleKey.Spacebar: await Click(); break;
                case ConsoleKey.H: await Home(); break;
                case ConsoleKey.T: await TypeText("ola"); break;
                case ConsoleKey.C:
                    for (var i = 0; i < 40; i++) await Move(-127, -127);
                    break;
            }
        }
    }

    static async Task Move(int dx, int dy, byte buttons = 0)
    {
        await Notify(_mouse, new[] { buttons, (byte)(sbyte)dx, (byte)(sbyte)dy, (byte)0 }, "rato");
    }

    static async Task Click()
    {
        await Move(0, 0, 1);
        await Task.Delay(30);
        await Move(0, 0, 0);
    }

    static async Task Home()
    {
        await Notify(_consumer, new byte[] { 0x23, 0x02 }, "Início");
        await Task.Delay(30);
        await Notify(_consumer, new byte[] { 0x00, 0x00 }, "Início");
    }

    static async Task TypeText(string text)
    {
        foreach (var ch in text)
        {
            // HID usage IDs: 'a' = 0x04 ... 'z' = 0x1D.
            if (ch < 'a' || ch > 'z') continue;
            var usage = (byte)(0x04 + (ch - 'a'));
            await Notify(_keyboard, new byte[] { 0, 0, usage, 0, 0, 0, 0, 0 }, "teclado");
            await Task.Delay(20);
            await Notify(_keyboard, new byte[8], "teclado");
            await Task.Delay(20);
        }
    }

    static async Task Notify(GattLocalCharacteristic? characteristic, byte[] report, string what)
    {
        if (characteristic is null || characteristic.SubscribedClients.Count == 0)
        {
            Console.WriteLine($"(ainda sem iPhone ligado; {what} ignorado)");
            return;
        }

        var results = await characteristic.NotifyValueAsync(report.AsBuffer());
        foreach (var r in results)
        {
            if (r.ProtocolError.HasValue)
            {
                Console.WriteLine($"[erro] notificação {what}: protocolo {r.ProtocolError}");
            }
        }
    }

    static async Task AddStatic(GattLocalService service, Guid uuid, byte[] value)
    {
        var result = await service.CreateCharacteristicAsync(uuid, new GattLocalCharacteristicParameters
        {
            CharacteristicProperties = GattCharacteristicProperties.Read,
            ReadProtectionLevel = GattProtectionLevel.EncryptionRequired,
            StaticValue = value.AsBuffer(),
        });
        Ensure(result.Error, uuid);
    }

    static async Task AddProtocolMode(GattLocalService service)
    {
        var result = await service.CreateCharacteristicAsync(ProtocolModeUuid, new GattLocalCharacteristicParameters
        {
            CharacteristicProperties = GattCharacteristicProperties.Read | GattCharacteristicProperties.WriteWithoutResponse,
            ReadProtectionLevel = GattProtectionLevel.EncryptionRequired,
            WriteProtectionLevel = GattProtectionLevel.EncryptionRequired,
        });
        Ensure(result.Error, ProtocolModeUuid);
        var ch = result.Characteristic;
        ch.ReadRequested += async (s, e) =>
        {
            using var deferral = e.GetDeferral();
            var request = await e.GetRequestAsync();
            request?.RespondWithValue(new byte[] { 0x01 }.AsBuffer()); // report protocol
        };
        ch.WriteRequested += async (s, e) =>
        {
            using var deferral = e.GetDeferral();
            var request = await e.GetRequestAsync();
            if (request?.Option == GattWriteOption.WriteWithResponse) request.Respond();
        };
    }

    static async Task AddControlPoint(GattLocalService service)
    {
        var result = await service.CreateCharacteristicAsync(HidControlPointUuid, new GattLocalCharacteristicParameters
        {
            CharacteristicProperties = GattCharacteristicProperties.WriteWithoutResponse,
            WriteProtectionLevel = GattProtectionLevel.EncryptionRequired,
        });
        Ensure(result.Error, HidControlPointUuid);
        result.Characteristic.WriteRequested += async (s, e) =>
        {
            using var deferral = e.GetDeferral();
            var request = await e.GetRequestAsync();
            if (request?.Option == GattWriteOption.WriteWithResponse) request.Respond();
        };
    }

    static async Task<GattLocalCharacteristic> AddInputReport(GattLocalService service, byte reportId, int length)
    {
        var result = await service.CreateCharacteristicAsync(ReportUuid, new GattLocalCharacteristicParameters
        {
            CharacteristicProperties = GattCharacteristicProperties.Read | GattCharacteristicProperties.Notify,
            ReadProtectionLevel = GattProtectionLevel.EncryptionRequired,
        });
        Ensure(result.Error, ReportUuid);
        var ch = result.Characteristic;
        ch.ReadRequested += async (s, e) =>
        {
            using var deferral = e.GetDeferral();
            var request = await e.GetRequestAsync();
            request?.RespondWithValue(new byte[length].AsBuffer());
        };
        ch.SubscribedClientsChanged += (s, e) =>
            Console.WriteLine($"[ligação] relatório {reportId}: {s.SubscribedClients.Count} subscritor(es)");
        await AddReportReference(ch, reportId, 0x01);
        return ch;
    }

    static async Task AddOutputReport(GattLocalService service, byte reportId)
    {
        var result = await service.CreateCharacteristicAsync(ReportUuid, new GattLocalCharacteristicParameters
        {
            CharacteristicProperties = GattCharacteristicProperties.Read
                | GattCharacteristicProperties.Write
                | GattCharacteristicProperties.WriteWithoutResponse,
            ReadProtectionLevel = GattProtectionLevel.EncryptionRequired,
            WriteProtectionLevel = GattProtectionLevel.EncryptionRequired,
        });
        Ensure(result.Error, ReportUuid);
        var ch = result.Characteristic;
        ch.ReadRequested += async (s, e) =>
        {
            using var deferral = e.GetDeferral();
            var request = await e.GetRequestAsync();
            request?.RespondWithValue(new byte[1].AsBuffer());
        };
        ch.WriteRequested += async (s, e) =>
        {
            using var deferral = e.GetDeferral();
            var request = await e.GetRequestAsync();
            if (request?.Option == GattWriteOption.WriteWithResponse) request.Respond();
        };
        await AddReportReference(ch, reportId, 0x02);
    }

    static async Task AddReportReference(GattLocalCharacteristic ch, byte reportId, byte type)
    {
        var result = await ch.CreateDescriptorAsync(ReportReferenceUuid, new GattLocalDescriptorParameters
        {
            ReadProtectionLevel = GattProtectionLevel.EncryptionRequired,
            StaticValue = new byte[] { reportId, type }.AsBuffer(),
        });
        Ensure(result.Error, ReportReferenceUuid);
    }

    static void Ensure(BluetoothError error, Guid uuid)
    {
        if (error != BluetoothError.Success)
        {
            throw new InvalidOperationException($"{uuid}: {error}");
        }
    }

    static string Yes(bool value) => value ? "sim" : "não";

    static int Fail(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine();
        Console.WriteLine("RESULTADO: " + message);
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine("Copia este texto e envia-mo. Carrega numa tecla para sair.");
        Console.ReadKey(intercept: true);
        return 1;
    }
}
