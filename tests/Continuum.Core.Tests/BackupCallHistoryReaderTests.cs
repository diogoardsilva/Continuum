using Continuum.Core.Calls;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Continuum.Core.Tests;

public sealed class BackupCallHistoryReaderTests : IDisposable
{
    readonly string _db = Path.Combine(Path.GetTempPath(), "continuum-calls-" + Guid.NewGuid().ToString("N") + ".db");

    public void Dispose() => File.Delete(_db);

    [Fact]
    public void ReadsRecentsNewestFirst()
    {
        using (var db = new SqliteConnection($"Data Source={_db};Pooling=False"))
        {
            db.Open();
            foreach (var sql in new[]
            {
                "CREATE TABLE ZCALLRECORD (Z_PK INTEGER PRIMARY KEY, ZDATE TIMESTAMP, ZDURATION FLOAT, ZADDRESS VARCHAR, ZNAME VARCHAR, ZORIGINATED INTEGER, ZANSWERED INTEGER, ZCALLTYPE INTEGER)",
                "INSERT INTO ZCALLRECORD VALUES (1, 783000000.5, 65.2, '+351912345678', NULL, 1, 1, 1)",
                "INSERT INTO ZCALLRECORD VALUES (2, 783000100.0, 0, X'6d616540657861', '', 0, 0, 8)",
            })
            {
                using var cmd = db.CreateCommand();
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }
        }

        var calls = BackupCallHistoryReader.ReadDatabase(_db);

        Assert.Equal(2, calls.Count);
        Assert.Equal("mae@exa", calls[0].Address);
        Assert.Equal(CallKind.FaceTimeVideo, calls[0].Kind);
        Assert.True(calls[0].Missed);
        Assert.Equal("+351912345678", calls[1].Address);
        Assert.True(calls[1].Outgoing);
        Assert.Equal(65, (int)calls[1].Duration.TotalSeconds);
        Assert.Equal(CallKind.Phone, calls[1].Kind);
    }
}
