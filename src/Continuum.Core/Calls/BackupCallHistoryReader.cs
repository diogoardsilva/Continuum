using System.Text;
using Continuum.Core.Backup;
using Continuum.Core.Messages;

namespace Continuum.Core.Calls;

public enum CallKind { Phone, FaceTimeVideo, FaceTimeAudio, Other }

public sealed record CallRecord(
    DateTimeOffset Date,
    TimeSpan Duration,
    string? Address,
    string? Name,
    bool Outgoing,
    bool Answered,
    CallKind Kind)
{
    public bool Missed => !Outgoing && !Answered;
}

/// <summary>Reads the Phone app's recents (calls and FaceTime) from CallHistory.storedata.</summary>
public static class BackupCallHistoryReader
{
    const string Domain = "HomeDomain";
    const string CallHistoryPath = "Library/CallHistoryDB/CallHistory.storedata";

    public static List<CallRecord> Read(IosBackup backup)
    {
        var path = backup.GetFilePath(Domain, CallHistoryPath)
            ?? throw new FileNotFoundException("O backup não tem o histórico de chamadas (CallHistory.storedata).");
        return ReadDatabase(path);
    }

    public static List<CallRecord> ReadDatabase(string path)
    {
        using var db = IosBackup.OpenReadOnly(path);
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT ZDATE, ZDURATION, ZADDRESS, ZNAME, ZORIGINATED, ZANSWERED, ZCALLTYPE
            FROM ZCALLRECORD
            ORDER BY ZDATE DESC
            """;
        using var r = cmd.ExecuteReader();
        var calls = new List<CallRecord>();
        while (r.Read())
        {
            calls.Add(new CallRecord(
                BackupMessagesReader.FromAppleTime((long)(r.IsDBNull(0) ? 0 : r.GetDouble(0))),
                TimeSpan.FromSeconds(r.IsDBNull(1) ? 0 : r.GetDouble(1)),
                ReadAddress(r.GetValue(2)),
                r.IsDBNull(3) || r.GetString(3).Length == 0 ? null : r.GetString(3),
                !r.IsDBNull(4) && r.GetInt64(4) != 0,
                !r.IsDBNull(5) && r.GetInt64(5) != 0,
                (r.IsDBNull(6) ? 0 : r.GetInt64(6)) switch
                {
                    1 => CallKind.Phone,
                    8 => CallKind.FaceTimeVideo,
                    16 => CallKind.FaceTimeAudio,
                    _ => CallKind.Other,
                }));
        }
        return calls;
    }

    // ZADDRESS is text on recent iOS and a UTF-8 blob on older versions.
    static string? ReadAddress(object value) => value switch
    {
        string s when s.Length > 0 => s,
        byte[] b when b.Length > 0 => Encoding.UTF8.GetString(b),
        _ => null,
    };
}
