using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using IsuzuDiagnostic.Desktop.Models;

namespace IsuzuDiagnostic.Desktop.Data;

public sealed class VehicleProfileRepository
{
    private readonly string _connectionString;

    public VehicleProfileRepository()
    {
        _connectionString = $"Data Source={DatabasePaths.DatabaseFile}";
    }

    public void Save(VehicleProfile profile)
    {
        using SqliteConnection connection = new(_connectionString);

        connection.Open();

        using SqliteCommand command = connection.CreateCommand();

        command.CommandText =
            """
        INSERT OR IGNORE INTO vehicle_profiles
        (
            manufacturer,
            model,
            model_year,
            engine_code,
            ecu_type,
            created_at_utc
        )
        VALUES
        (
            $manufacturer,
            $model,
            $modelYear,
            $engineCode,
            $ecuType,
            $createdAtUtc
        );
        """;

        command.Parameters.AddWithValue("$manufacturer", profile.Manufacturer);
        command.Parameters.AddWithValue("$model", profile.Model);
        command.Parameters.AddWithValue("$modelYear", profile.ModelYear);
        command.Parameters.AddWithValue("$engineCode", profile.EngineCode);
        command.Parameters.AddWithValue("$ecuType", profile.EcuType);
        command.Parameters.AddWithValue("$createdAtUtc", DateTime.UtcNow.ToString("O"));

        command.ExecuteNonQuery();
    }

    public bool Exists(VehicleProfile profile)
    {
        using SqliteConnection connection = new(_connectionString);

        connection.Open();

        using SqliteCommand command = connection.CreateCommand();

        command.CommandText =
            """
        SELECT EXISTS
        (
            SELECT 1
            FROM vehicle_profiles WHERE manufacturer = $manufacturer
                AND model = $model
                AND model_year = $modelYear
                AND engine_code = $engineCode
                AND ecu_type = $ecuType
        );
        """;

        command.Parameters.AddWithValue("$manufacturer", profile.Manufacturer);
        command.Parameters.AddWithValue("$model", profile.Model);
        command.Parameters.AddWithValue("$modelYear", profile.ModelYear);
        command.Parameters.AddWithValue("$engineCode", profile.EngineCode);
        command.Parameters.AddWithValue("$ecuType", profile.EcuType);

        long result = (long)command.ExecuteScalar()!;

        return result == 1;
    }

    public List<VehicleProfile> GetAll()
    {
        List<VehicleProfile> profiles = new();

        using SqliteConnection connection = new(_connectionString);

        connection.Open();

        using SqliteCommand command = connection.CreateCommand();

        command.CommandText =
            """
        
        SELECT
            manufacturer,
            model,
            model_year,
            engine_code,
            ecu_type
        FROM vehicle_profiles
        ORDER BY manufacturer, model, model_year;
        """;

        using SqliteDataReader reader = command.ExecuteReader();

        while (reader.Read())
        {
            VehicleProfile profile = new(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt32(2),
                reader.GetString(3),
                reader.GetString(4)
                );

            profiles.Add(profile);
        }

        return profiles;
    }
}