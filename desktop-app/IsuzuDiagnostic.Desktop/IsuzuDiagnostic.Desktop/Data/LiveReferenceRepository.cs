using Microsoft.Data.Sqlite;
using IsuzuDiagnostic.Desktop.Diagnostics;

namespace IsuzuDiagnostic.Desktop.Data;

public sealed class LiveReferenceRepository
{
    public IReadOnlyList<LiveReferenceValue> FindByParameter(string parameterKey, string? applicationKey = null)
    {
        if (string.IsNullOrWhiteSpace(parameterKey))
        {
            throw new ArgumentException("Parameter key cannot be empty.", nameof(parameterKey));
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
            """
        SELECT
            lp.parameter_key,
            da.application_key,

            lrv.operating_condition_key,
            lrv.operating_condition_name,

            lrv.reference_kind,
            lrv.comparison_operator,

            lrv.minimum_value,
            lrv.maximum_value,
            lrv.nominal_value,

            lrv.text_value,
            lrv.unit,

            lrv.approximate,
            lrv.notes

        FROM live_reference_values lrv

        INNER JOIN live_parameters lp
            ON lp.id = lrv.live_parameter_id

        INNER JOIN diagnostic_applications da
            ON da.id = lrv.diagnostic_application_id

        WHERE lp.parameter_key = @parameterKey COLLATE NOCASE
          AND (
                @applicationKey IS NULL
                OR da.application_key = @applicationKey COLLATE NOCASE
              )

        ORDER BY
            da.application_key,
            lrv.operating_condition_key,
            lrv.id;
        """;

        command.Parameters.AddWithValue("@applicationKey", applicationKey is null ? DBNull.Value : applicationKey.Trim());
        command.Parameters.AddWithValue("@parameterKey", parameterKey.Trim());

        List<LiveReferenceValue> results = new();

        using SqliteDataReader reader = command.ExecuteReader();

        while (reader.Read())
        {
            results.Add(new LiveReferenceValue
            {
                ParameterKey = reader.GetString(0),
                ApplicationKey = reader.GetString(1),

                OperatingConditionKey = GetNullableString(reader, 2),
                OperatingConditionName = GetNullableString(reader, 3),
                ReferenceKind = GetNullableString(reader, 4),
                ComparisonOperator = GetNullableString(reader, 5),
                MinimumValue = GetNullableDouble(reader, 6),
                MaximumValue = GetNullableDouble(reader, 7),
                NominalValue = GetNullableDouble(reader, 8),
                TextValue = GetNullableString(reader, 9),
                Unit = GetNullableString(reader, 10),
                Approximate = reader.GetInt64(11) != 0,
                Notes = GetNullableString(reader, 12),

            });
        }

        return results;
    }

    private static string? GetNullableString(SqliteDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static double? GetNullableDouble(SqliteDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? null : reader.GetDouble(ordinal);
    }
}