using Microsoft.Data.Sqlite;

namespace Continuum.Core.Live;

/// <summary>A message seen in an iPhone notification (incoming only, text only).</summary>
public sealed record LiveMessage(DateTimeOffset Date, string Title, string? Subtitle, string Body);

/// <summary>
/// Keeps every Messages notification the PC receives over Bluetooth, so the
/// history grows between cable syncs.
/// </summary>
public sealed class LiveMessageStore
{
    readonly string _connectionString;

    public LiveMessageStore(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString();
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS live_message (
                date INTEGER NOT NULL,
                title TEXT NOT NULL,
                subtitle TEXT,
                body TEXT NOT NULL,
                UNIQUE(date, title, body))
            """;
        cmd.ExecuteNonQuery();
    }

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Continuum", "live.db");

    /// <summary>Returns false when the same notification was already stored (the iPhone resends on reconnect).</summary>
    public bool Add(LiveMessage message)
    {
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = "INSERT OR IGNORE INTO live_message VALUES ($date, $title, $subtitle, $body)";
        cmd.Parameters.AddWithValue("$date", message.Date.ToUnixTimeSeconds());
        cmd.Parameters.AddWithValue("$title", message.Title);
        cmd.Parameters.AddWithValue("$subtitle", (object?)message.Subtitle ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$body", message.Body);
        return cmd.ExecuteNonQuery() > 0;
    }

    public List<LiveMessage> All()
    {
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT date, title, subtitle, body FROM live_message ORDER BY date";
        using var r = cmd.ExecuteReader();
        var list = new List<LiveMessage>();
        while (r.Read())
        {
            list.Add(new LiveMessage(DateTimeOffset.FromUnixTimeSeconds(r.GetInt64(0)), r.GetString(1),
                r.IsDBNull(2) ? null : r.GetString(2), r.GetString(3)));
        }
        return list;
    }

    SqliteConnection Open()
    {
        var c = new SqliteConnection(_connectionString);
        c.Open();
        return c;
    }
}
