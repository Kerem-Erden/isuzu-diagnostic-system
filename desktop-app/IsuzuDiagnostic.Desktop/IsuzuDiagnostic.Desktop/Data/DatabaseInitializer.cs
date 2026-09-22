using System.IO;
using Microsoft.Data.Sqlite;

namespace IsuzuDiagnostic.Desktop.Data;

public static class DatabaseInitializer
{
    public static void Initialize()
    {
        ValidateKnowledgeDatabase();
        Directory.CreateDirectory(DatabasePaths.DatabaseDirectory);

        using SqliteConnection connection = new($"Data Source={DatabasePaths.DatabaseFile}");

        connection.Open();

        using SqliteCommand command = connection.CreateCommand();

        command.CommandText =
        """
        CREATE TABLE IF NOT EXISTS diagnostic_events (
            id INTEGER PRIMARY KEY AUTOINCREMENT, session_id TEXT NOT NULL,
            source TEXT NOT NULL, created_at_utc TEXT NOT NULL, kind TEXT NOT NULL, payload_json TEXT NOT NULL
        );
        CREATE TABLE IF NOT EXISTS vehicle_profiles
        (
            id INTEGER PRIMARY KEY AUTOINCREMENT,

            manufacturer TEXT NOT NULL,
            model TEXT NOT NULL,
            model_year INTEGER NOT NULL,
            engine_code TEXT NOT NULL,
            ecu_type TEXT NOT NULL,

            created_at_utc TEXT NOT NULL,

            UNIQUE (
                manufacturer,
                model,
                model_year,
                engine_code,
                ecu_type
            )
        );
        """;

        command.ExecuteNonQuery();
    }

    private static void ValidateKnowledgeDatabase()
    {
        if (!File.Exists(DatabasePaths.KnowledgeDatabaseFilePath))
        {
            throw new FileNotFoundException(
                "Knowledge database missing. Restore Data/isuzu-knowledge.db beside the application.",
                DatabasePaths.KnowledgeDatabaseFilePath);
        }

        SqliteConnectionStringBuilder builder = new()
        {
            DataSource = DatabasePaths.KnowledgeDatabaseFilePath,
            Mode = SqliteOpenMode.ReadOnly
        };
        using SqliteConnection connection = new(builder.ConnectionString);
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN " +
            "('dtc_codes','dtc_profiles','live_reference_values','live_parameters','diagnostic_applications')";
        long tableCount = (long)(command.ExecuteScalar() ?? 0L);
        if (tableCount != 5)
            throw new InvalidDataException("Knowledge database is incomplete or uses an unsupported schema.");
    }
}
