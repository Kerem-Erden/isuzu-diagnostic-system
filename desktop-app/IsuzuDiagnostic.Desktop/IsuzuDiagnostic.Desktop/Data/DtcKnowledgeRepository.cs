using IsuzuDiagnostic.Desktop.Models;
using Microsoft.Data.Sqlite;
using System.IO;

namespace IsuzuDiagnostic.Desktop.Data;

public sealed class DtcKnowledgeRepository
{
    private readonly string _databasePath;

    public DtcKnowledgeRepository()
        : this(DatabasePaths.KnowledgeDatabaseFilePath)
    {
    }

    public DtcKnowledgeRepository(string databasePath)
    {
        _databasePath = databasePath;
    }

    public DtcKnowledgeDetails? FindByCode(
        string code,
        string? applicationKey = null)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException(
                "DTC code cannot be empty.",
                nameof(code));
        }

        code = code.Trim().ToUpperInvariant();

        if (!File.Exists(_databasePath))
        {
            throw new FileNotFoundException(
                "Isuzu knowledge database could not be found.",
                _databasePath);
        }

        using SqliteConnection connection = new(
            $"Data Source={_databasePath};Mode=ReadOnly");

        connection.Open();

        DtcHeader? header = ReadDtcHeader(connection, code);

        if (header is null)
        {
            return null;
        }

        DtcProfile? profile = FindBestProfile(
            connection,
            header.Id,
            applicationKey);

        /*
         * A single DTC can be documented differently across Isuzu manuals.
         *
         * Example:
         * - one source can contain the complete OEM diagnostic procedure,
         * - another source can contain the OEM suspected-cause table.
         *
         * Therefore causes/solutions are collected at DTC level instead of
         * being limited to the one profile selected for description/procedure.
         */
        IReadOnlyList<string> causes =
            ReadCauses(connection, header.Id, applicationKey);

        IReadOnlyList<string> solutions =
            ReadSolutions(connection, header.Id, applicationKey);

        return new DtcKnowledgeDetails
        {
            Code = header.Code,
            Title = header.Title,
            System = header.System,

            Description = profile?.Description,
            ApplicationKey = profile?.ApplicationKey,

            PossibleCauses = causes,

            SolutionRecommendations = solutions,

            DiagnosticSteps = profile is null
                ? []
                : ReadProcedureSteps(
                    connection,
                    profile.Id,
                    "DIAGNOSIS"),

            ConfirmationSteps = profile is null
                ? []
                : ReadProcedureSteps(
                    connection,
                    profile.Id,
                    "CONFIRMATION"),

            RelatedLiveData = ReadRelatedLiveData(
                connection,
                header.Id,
                applicationKey)
        };
    }

    private static DtcHeader? ReadDtcHeader(
        SqliteConnection connection,
        string code)
    {
        using SqliteCommand command = connection.CreateCommand();

        command.CommandText =
            """
            SELECT
                id,
                code,
                title,
                system
            FROM dtc_codes
            WHERE code = $code
            LIMIT 1;
            """;

        command.Parameters.AddWithValue("$code", code);

        using SqliteDataReader reader = command.ExecuteReader();

        if (!reader.Read())
        {
            return null;
        }

        return new DtcHeader(
            reader.GetInt64(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3));
    }

    private static DtcProfile? FindBestProfile(
        SqliteConnection connection,
        long dtcId,
        string? applicationKey)
    {
        using SqliteCommand command = connection.CreateCommand();

        command.CommandText =
            """
            SELECT
                p.id,
                p.description,
                a.application_key
            FROM dtc_profiles p
            LEFT JOIN diagnostic_applications a
                ON a.id = p.diagnostic_application_id
            WHERE p.dtc_code_id = $dtcId
            ORDER BY
                CASE
                    WHEN $applicationKey IS NOT NULL
                         AND a.application_key = $applicationKey
                    THEN 0
                    ELSE 1
                END,

                (
                    SELECT COUNT(*)
                    FROM dtc_procedure_steps ps
                    WHERE ps.dtc_profile_id = p.id
                ) DESC,

                CASE
                    WHEN p.description IS NOT NULL
                         AND LENGTH(TRIM(p.description)) > 0
                    THEN 0
                    ELSE 1
                END,

                (
                    SELECT COUNT(*)
                    FROM dtc_causes c
                    WHERE c.dtc_profile_id = p.id
                ) DESC
            LIMIT 1;
            """;

        command.Parameters.AddWithValue("$dtcId", dtcId);

        command.Parameters.AddWithValue(
            "$applicationKey",
            (object?)applicationKey ?? DBNull.Value);

        using SqliteDataReader reader = command.ExecuteReader();

        if (!reader.Read())
        {
            return null;
        }

        return new DtcProfile(
            reader.GetInt64(0),
            reader.IsDBNull(1) ? null : reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2));
    }

    private static IReadOnlyList<string> ReadCauses(
        SqliteConnection connection,
        long dtcId,
        string? applicationKey)
    {
        List<string> values = ReadDtcLevelStrings(
            connection,
            dtcId,
            applicationKey,
            """
            SELECT
                c.description,
                MIN(
                    CASE
                        WHEN $applicationKey IS NOT NULL
                             AND a.application_key = $applicationKey
                        THEN 0
                        WHEN p.diagnostic_application_id IS NULL
                        THEN 1
                        ELSE 2
                    END
                ) AS source_rank,
                MIN(c.sort_order) AS item_order
            FROM dtc_causes c
            INNER JOIN dtc_profiles p
                ON p.id = c.dtc_profile_id
            LEFT JOIN diagnostic_applications a
                ON a.id = p.diagnostic_application_id
            WHERE p.dtc_code_id = $dtcId
              AND
              (
                  $applicationKey IS NULL
                  OR p.diagnostic_application_id IS NULL
                  OR a.application_key = $applicationKey
              )
            GROUP BY c.description
            ORDER BY source_rank, item_order, c.description;
            """
        );

        // Global Isuzu fallback: never hide known DTC knowledge just because
        // a profile-specific enrichment is unavailable.
        if (values.Count == 0 && applicationKey is not null)
        {
            values = ReadDtcLevelStrings(
                connection,
                dtcId,
                null,
                """
                SELECT
                    c.description,
                    0 AS source_rank,
                    MIN(c.sort_order) AS item_order
                FROM dtc_causes c
                INNER JOIN dtc_profiles p
                    ON p.id = c.dtc_profile_id
                WHERE p.dtc_code_id = $dtcId
                GROUP BY c.description
                ORDER BY item_order, c.description;
                """
            );
        }

        return values;
    }

    private static IReadOnlyList<string> ReadSolutions(
        SqliteConnection connection,
        long dtcId,
        string? applicationKey)
    {
        List<string> values = ReadDtcLevelStrings(
            connection,
            dtcId,
            applicationKey,
            """
            SELECT
                s.description,
                MIN(
                    CASE
                        WHEN $applicationKey IS NOT NULL
                             AND a.application_key = $applicationKey
                        THEN 0
                        WHEN p.diagnostic_application_id IS NULL
                        THEN 1
                        ELSE 2
                    END
                ) AS source_rank,
                MIN(s.sort_order) AS item_order
            FROM dtc_solution_recommendations s
            INNER JOIN dtc_profiles p
                ON p.id = s.dtc_profile_id
            LEFT JOIN diagnostic_applications a
                ON a.id = p.diagnostic_application_id
            WHERE p.dtc_code_id = $dtcId
              AND
              (
                  $applicationKey IS NULL
                  OR p.diagnostic_application_id IS NULL
                  OR a.application_key = $applicationKey
              )
            GROUP BY s.description
            ORDER BY source_rank, item_order, s.description;
            """
        );

        if (values.Count == 0 && applicationKey is not null)
        {
            values = ReadDtcLevelStrings(
                connection,
                dtcId,
                null,
                """
                SELECT
                    s.description,
                    0 AS source_rank,
                    MIN(s.sort_order) AS item_order
                FROM dtc_solution_recommendations s
                INNER JOIN dtc_profiles p
                    ON p.id = s.dtc_profile_id
                WHERE p.dtc_code_id = $dtcId
                GROUP BY s.description
                ORDER BY item_order, s.description;
                """
            );
        }

        return values;
    }

    private static List<string> ReadDtcLevelStrings(
        SqliteConnection connection,
        long dtcId,
        string? applicationKey,
        string sql)
    {
        List<string> values = [];

        using SqliteCommand command = connection.CreateCommand();

        command.CommandText = sql;
        command.Parameters.AddWithValue("$dtcId", dtcId);
        command.Parameters.AddWithValue(
            "$applicationKey",
            (object?)applicationKey ?? DBNull.Value);

        using SqliteDataReader reader = command.ExecuteReader();

        while (reader.Read())
        {
            values.Add(reader.GetString(0));
        }

        return values;
    }

    private static IReadOnlyList<string> ReadProcedureSteps(
        SqliteConnection connection,
        long profileId,
        string procedureType)
    {
        List<string> steps = [];

        using SqliteCommand command = connection.CreateCommand();

        command.CommandText =
            """
            SELECT instruction
            FROM dtc_procedure_steps
            WHERE dtc_profile_id = $profileId
              AND procedure_type = $procedureType
            ORDER BY step_order;
            """;

        command.Parameters.AddWithValue("$profileId", profileId);
        command.Parameters.AddWithValue(
            "$procedureType",
            procedureType);

        using SqliteDataReader reader = command.ExecuteReader();

        while (reader.Read())
        {
            steps.Add(reader.GetString(0));
        }

        return steps;
    }

    private static IReadOnlyList<DtcRelatedLiveParameter>
        ReadRelatedLiveData(
            SqliteConnection connection,
            long dtcId,
            string? applicationKey)
    {
        List<DtcRelatedLiveParameter> items = [];

        using SqliteCommand command = connection.CreateCommand();

        command.CommandText =
            """
            SELECT
                lp.parameter_key,
                lp.display_name,
                lp.default_unit,
                r.relation_role,
                r.reason
            FROM dtc_related_live_data r
            INNER JOIN live_parameters lp
                ON lp.id = r.live_parameter_id
            LEFT JOIN diagnostic_applications a
                ON a.id = r.diagnostic_application_id
            WHERE r.dtc_code_id = $dtcId
              AND
              (
                  r.diagnostic_application_id IS NULL
                  OR $applicationKey IS NULL
                  OR a.application_key = $applicationKey
              )
            ORDER BY
                r.display_priority,
                lp.display_name;
            """;

        command.Parameters.AddWithValue("$dtcId", dtcId);

        command.Parameters.AddWithValue(
            "$applicationKey",
            (object?)applicationKey ?? DBNull.Value);

        using SqliteDataReader reader = command.ExecuteReader();

        while (reader.Read())
        {
            items.Add(
                new DtcRelatedLiveParameter
                {
                    ParameterKey = reader.GetString(0),
                    DisplayName = reader.GetString(1),

                    Unit = reader.IsDBNull(2)
                        ? null
                        : reader.GetString(2),

                    Role = reader.IsDBNull(3)
                        ? null
                        : reader.GetString(3),

                    Reason = reader.IsDBNull(4)
                        ? null
                        : reader.GetString(4)
                });
        }

        return items;
    }

    private sealed record DtcHeader(
        long Id,
        string Code,
        string Title,
        string? System);

    private sealed record DtcProfile(
        long Id,
        string? Description,
        string? ApplicationKey);
}
