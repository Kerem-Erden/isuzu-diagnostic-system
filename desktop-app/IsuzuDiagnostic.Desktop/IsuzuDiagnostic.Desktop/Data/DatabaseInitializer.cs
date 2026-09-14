using System.IO;
using Microsoft.Data.Sqlite;

namespace IsuzuDiagnostic.Desktop.Data;

public static class DatabaseInitializer
{
    public static void Initialize()
    {
        Directory.CreateDirectory(DatabasePaths.DatabaseDirectory);

        using SqliteConnection connection = new($"Data Source={DatabasePaths.DatabaseFile}");

        connection.Open();

        using SqliteCommand command = connection.CreateCommand();

        command.CommandText =
        """
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
}