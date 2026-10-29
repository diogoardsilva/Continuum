// Continuum contacts test: reads every contact (with photos) from iCloud or
// from a local iPhone backup, shows a summary and exports them to a folder.

using System.Text;
using Continuum.Core.Backup;
using Continuum.Core.Contacts;

Console.OutputEncoding = Encoding.UTF8;
Console.WriteLine("Continuum - teste de contactos");
Console.WriteLine("==============================");
Console.WriteLine();
Console.WriteLine("De onde queres ler os contactos?");
Console.WriteLine("  1. iCloud (Apple ID + palavra-passe para apps)");
Console.WriteLine("  2. Backup local do iPhone (feito na app Apple Devices ou no iTunes)");
Console.Write("> ");

try
{
    var contacts = Console.ReadLine()?.Trim() switch
    {
        "1" => await FromICloud(),
        "2" => FromBackup(),
        _ => throw new InvalidOperationException("Escolhe 1 ou 2."),
    };
    Report(contacts);
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine();
    Console.WriteLine("RESULTADO: falhou - " + ex.Message);
    Console.ResetColor();
}

Console.WriteLine();
Console.WriteLine("Copia o texto desta janela e envia-mo. Enter para sair.");
Console.ReadLine();

static async Task<List<Contact>> FromICloud()
{
    Console.WriteLine();
    Console.WriteLine("Cria uma palavra-passe para apps em account.apple.com > Início de sessão e segurança >");
    Console.WriteLine("Palavras-passe para apps. Não uses a palavra-passe normal da Apple ID.");
    Console.WriteLine();
    Console.Write("Apple ID (email): ");
    var appleId = Console.ReadLine()?.Trim() ?? "";
    Console.Write("Palavra-passe para apps (xxxx-xxxx-xxxx-xxxx): ");
    var password = ReadHidden();

    Console.WriteLine();
    Console.WriteLine("A ligar ao iCloud...");
    using var client = new ICloudContactsClient(appleId, password);
    return await client.GetAllContactsAsync();
}

static List<Contact> FromBackup()
{
    var backups = IosBackup.FindBackups().ToList();
    Console.WriteLine();
    string folder;
    if (backups.Count == 0)
    {
        Console.WriteLine("Não encontrei backups nas pastas habituais. Escreve o caminho da pasta do backup:");
        Console.Write("> ");
        folder = Console.ReadLine()?.Trim().Trim('"') ?? "";
    }
    else
    {
        for (var i = 0; i < backups.Count; i++)
        {
            var when = File.GetLastWriteTime(Path.Combine(backups[i], "Manifest.db"));
            Console.WriteLine($"  {i + 1}. {Path.GetFileName(backups[i])} ({when:dd/MM/yyyy HH:mm})");
        }
        Console.Write("Qual? [1] ");
        var choice = int.TryParse(Console.ReadLine(), out var n) && n >= 1 && n <= backups.Count ? n : 1;
        folder = backups[choice - 1];
    }

    Console.WriteLine("A ler o backup...");
    return BackupContactsReader.Read(new IosBackup(folder));
}

static void Report(List<Contact> contacts)
{
    var withPhoto = contacts.Count(c => c.Photo is not null);
    var withPhone = contacts.Count(c => c.Phones.Count > 0);

    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"RESULTADO: {contacts.Count} contactos, {withPhone} com telefone, {withPhoto} com foto.");
    Console.ResetColor();
    Console.WriteLine();

    foreach (var c in contacts.OrderBy(c => c.DisplayName, StringComparer.CurrentCultureIgnoreCase).Take(15))
    {
        Console.WriteLine($"  {c.DisplayName,-30} {c.Phones.Count} tel  {c.Emails.Count} email  {(c.Photo is null ? "" : "foto")}");
    }
    if (contacts.Count > 15) Console.WriteLine($"  ... e mais {contacts.Count - 15}");

    var outDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Continuum", "Contactos");
    var photosDir = Path.Combine(outDir, "fotos");
    Directory.CreateDirectory(photosDir);

    var csv = new StringBuilder("nome;telefones;emails;foto\n");
    for (var i = 0; i < contacts.Count; i++)
    {
        var c = contacts[i];
        string? photoFile = null;
        if (c.Photo is not null)
        {
            photoFile = $"{i + 1:0000}-{SafeFileName(c.DisplayName)}.jpg";
            File.WriteAllBytes(Path.Combine(photosDir, photoFile), c.Photo);
        }
        csv.Append(Csv(c.DisplayName)).Append(';')
           .Append(Csv(string.Join(" | ", c.Phones.Select(p => $"{p.Label}: {p.Value}")))).Append(';')
           .Append(Csv(string.Join(" | ", c.Emails.Select(e => $"{e.Label}: {e.Value}")))).Append(';')
           .Append(photoFile ?? "").Append('\n');
    }
    File.WriteAllText(Path.Combine(outDir, "contactos.csv"), csv.ToString(), new UTF8Encoding(true));

    Console.WriteLine();
    Console.WriteLine($"Exportado para {outDir} (contactos.csv e pasta fotos).");
}

static string Csv(string value) => value.Contains(';') || value.Contains('"') ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;

static string SafeFileName(string name)
{
    var invalid = Path.GetInvalidFileNameChars();
    var clean = new string(name.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray()).Trim();
    return clean.Length > 40 ? clean[..40] : clean;
}

static string ReadHidden()
{
    var sb = new StringBuilder();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter) break;
        if (key.Key == ConsoleKey.Backspace) { if (sb.Length > 0) sb.Length--; continue; }
        if (!char.IsControl(key.KeyChar)) sb.Append(key.KeyChar);
    }
    Console.WriteLine();
    return sb.ToString();
}
