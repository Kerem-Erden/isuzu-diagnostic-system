using System.Text.Json;
using Microsoft.Data.Sqlite;
using IsuzuDiagnostic.Desktop.Services;
namespace IsuzuDiagnostic.Desktop.Data;

public sealed class DiagnosticAuditLog : IDiagnosticAudit
{
    private readonly string _session;
    private readonly string _source;
    public DiagnosticAuditLog(string session, string source) { _session = session; _source = source; }
    public void Write(string kind, object payload)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = DatabasePaths.DatabaseFile }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO diagnostic_events(session_id,source,created_at_utc,kind,payload_json) VALUES($s,$source,$t,$k,$p)";
        command.Parameters.AddWithValue("$s", _session);
        command.Parameters.AddWithValue("$source", _source);
        command.Parameters.AddWithValue("$t", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$k", kind);
        command.Parameters.AddWithValue("$p", JsonSerializer.Serialize(payload));
        command.ExecuteNonQuery();
    }
    public static string ReadRecent()
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = DatabasePaths.DatabaseFile, Mode = SqliteOpenMode.ReadOnly }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT created_at_utc,source,kind,payload_json FROM diagnostic_events ORDER BY id DESC LIMIT 100";
        using var reader = command.ExecuteReader();
        List<string> lines = [];
        while (reader.Read()) lines.Add($"{reader.GetString(0)} [{reader.GetString(1)}] {reader.GetString(2)}\n{reader.GetString(3)}\n");
        return string.Join("\n", lines);
    }
}
