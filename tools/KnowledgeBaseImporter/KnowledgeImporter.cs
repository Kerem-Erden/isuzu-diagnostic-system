using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace KnowledgeBaseImporter;

public sealed record ImportSummary(
    int SourceDocuments,
    int Applications,
    int DtcCodes,
    int DtcProfiles,
    int Causes,
    int Solutions,
    int DiagnosticSteps,
    int ConfirmationSteps,
    int Mappings
);

public sealed partial class KnowledgeImporter
{
    private readonly SqliteConnection _connection;
    private readonly string _knowledgeDirectory;

    private SqliteTransaction? _transaction;

    private readonly Dictionary<string, long> _sourceDocumentIds =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, long> _sourceReferenceIds =
        new(StringComparer.Ordinal);

    private readonly Dictionary<string, long> _applicationIds =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, long> _dtcIds =
        new(StringComparer.OrdinalIgnoreCase);

    private int _causeCount;
    private int _solutionCount;
    private int _diagnosticStepCount;
    private int _confirmationStepCount;

    public KnowledgeImporter(
        SqliteConnection connection,
        string knowledgeDirectory)
    {
        _connection = connection
            ?? throw new ArgumentNullException(nameof(connection));

        _knowledgeDirectory = knowledgeDirectory
            ?? throw new ArgumentNullException(nameof(knowledgeDirectory));
    }

    public ImportSummary ImportCoreDtcKnowledge()
    {
        _transaction = _connection.BeginTransaction();

        try
        {
            int sourceCount = ImportSources();
            int applicationCount = ImportApplications();
            int dtcCount = ImportDtcs();
            int profileCount = ImportDtcProfiles();
            int mappingCount = ImportDtcMappings();

            SetMetadata("dataset_version", "v2");
            SetMetadata("core_dtc_import_complete", "true");
            SetMetadata(
                "generated_at_utc",
                DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
            );

            _transaction.Commit();
            _transaction.Dispose();
            _transaction = null;

            return new ImportSummary(
                sourceCount,
                applicationCount,
                dtcCount,
                profileCount,
                _causeCount,
                _solutionCount,
                _diagnosticStepCount,
                _confirmationStepCount,
                mappingCount
            );
        }
        catch
        {
            _transaction?.Rollback();
            _transaction?.Dispose();
            _transaction = null;
            throw;
        }
    }

    private int ImportSources()
    {
        using JsonDocument document = LoadJson("sources.json");

        int count = 0;

        foreach (JsonElement item in document.RootElement.EnumerateArray())
        {
            string documentKey = RequiredString(item, "document_key");

            using SqliteCommand command = CreateCommand(
                """
                INSERT INTO source_documents
                (
                    document_key,
                    file_name,
                    title,
                    publisher,
                    publication_year,
                    document_code,
                    role,
                    canonical,
                    canonical_source_key,
                    size_bytes,
                    sha256,
                    page_count
                )
                VALUES
                (
                    $documentKey,
                    $fileName,
                    $title,
                    $publisher,
                    $publicationYear,
                    $documentCode,
                    $role,
                    $canonical,
                    $canonicalSourceKey,
                    $sizeBytes,
                    $sha256,
                    $pageCount
                );

                SELECT last_insert_rowid();
                """
            );

            Add(command, "$documentKey", documentKey);
            Add(command, "$fileName", OptionalString(item, "file_name"));
            Add(command, "$title", RequiredString(item, "title"));
            Add(command, "$publisher", OptionalString(item, "publisher"));
            Add(command, "$publicationYear", OptionalInt64(item, "publication_year"));
            Add(command, "$documentCode", OptionalString(item, "document_code"));
            Add(command, "$role", OptionalString(item, "role"));
            Add(command, "$canonical", OptionalBool(item, "canonical") is false ? 0 : 1);
            Add(command, "$canonicalSourceKey", OptionalString(item, "canonical_source_key"));
            Add(command, "$sizeBytes", OptionalInt64(item, "size_bytes"));
            Add(command, "$sha256", OptionalString(item, "sha256"));
            Add(command, "$pageCount", OptionalInt64(item, "page_count"));

            long id = Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);

            _sourceDocumentIds.Add(documentKey, id);
            count++;
        }

        Console.WriteLine($"Imported {count} source documents.");
        return count;
    }

    private int ImportApplications()
    {
        using JsonDocument document = LoadJson("applications.json");

        int count = 0;

        foreach (JsonElement item in document.RootElement.EnumerateArray())
        {
            string applicationKey = RequiredString(item, "application_key");
            long? sourceReferenceId = ResolveSourceReference(item, "source");

            using SqliteCommand command = CreateCommand(
                """
                INSERT INTO diagnostic_applications
                (
                    application_key,
                    manufacturer,
                    vehicle_family,
                    model_year_from,
                    model_year_to,
                    production_start,
                    engine_family,
                    engine_variant,
                    emissions_generation,
                    system_generation,
                    notes,
                    source_reference_id
                )
                VALUES
                (
                    $applicationKey,
                    $manufacturer,
                    $vehicleFamily,
                    $modelYearFrom,
                    $modelYearTo,
                    $productionStart,
                    $engineFamily,
                    $engineVariant,
                    $emissionsGeneration,
                    $systemGeneration,
                    $notes,
                    $sourceReferenceId
                );

                SELECT last_insert_rowid();
                """
            );

            Add(command, "$applicationKey", applicationKey);
            Add(command, "$manufacturer", RequiredString(item, "manufacturer"));
            Add(command, "$vehicleFamily", OptionalString(item, "vehicle_family"));
            Add(command, "$modelYearFrom", OptionalInt64(item, "model_year_from"));
            Add(command, "$modelYearTo", OptionalInt64(item, "model_year_to"));
            Add(command, "$productionStart", OptionalString(item, "production_start"));
            Add(command, "$engineFamily", OptionalString(item, "engine_family"));
            Add(command, "$engineVariant", OptionalString(item, "engine_variant"));
            Add(command, "$emissionsGeneration", OptionalString(item, "emissions_generation"));
            Add(command, "$systemGeneration", OptionalString(item, "system_generation"));
            Add(command, "$notes", OptionalString(item, "notes"));
            Add(command, "$sourceReferenceId", sourceReferenceId);

            long applicationId =
                Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);

            _applicationIds.Add(applicationKey, applicationId);

            InsertStringArray(
                item,
                "vehicle_models",
                """
                INSERT OR IGNORE INTO application_vehicle_models
                (diagnostic_application_id, vehicle_model)
                VALUES ($applicationId, $value);
                """,
                applicationId
            );

            InsertStringArray(
                item,
                "engine_variants",
                """
                INSERT OR IGNORE INTO application_engine_variants
                (diagnostic_application_id, engine_variant)
                VALUES ($applicationId, $value);
                """,
                applicationId
            );

            InsertStringArray(
                item,
                "markets",
                """
                INSERT OR IGNORE INTO application_markets
                (diagnostic_application_id, market)
                VALUES ($applicationId, $value);
                """,
                applicationId
            );

            count++;
        }

        Console.WriteLine($"Imported {count} diagnostic applications.");
        return count;
    }

    private int ImportDtcs()
    {
        using JsonDocument document = LoadJson("dtcs.json");

        int count = 0;

        foreach (JsonElement item in document.RootElement.EnumerateArray())
        {
            string code = RequiredString(item, "code").ToUpperInvariant();

            using SqliteCommand command = CreateCommand(
                """
                INSERT INTO dtc_codes
                (
                    code,
                    title,
                    system,
                    knowledge_status
                )
                VALUES
                (
                    $code,
                    $title,
                    $system,
                    $knowledgeStatus
                );

                SELECT last_insert_rowid();
                """
            );

            Add(command, "$code", code);
            Add(command, "$title", RequiredString(item, "title"));
            Add(command, "$system", OptionalString(item, "system"));
            Add(command, "$knowledgeStatus", OptionalString(item, "knowledge_status"));

            long dtcId = Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            _dtcIds.Add(code, dtcId);

            if (TryGetArray(item, "alternate_titles", out JsonElement alternateTitles))
            {
                foreach (JsonElement titleElement in alternateTitles.EnumerateArray())
                {
                    string? title = ElementString(titleElement);

                    if (string.IsNullOrWhiteSpace(title))
                    {
                        continue;
                    }

                    using SqliteCommand titleCommand = CreateCommand(
                        """
                        INSERT OR IGNORE INTO dtc_alternate_titles
                        (dtc_code_id, title)
                        VALUES ($dtcId, $title);
                        """
                    );

                    Add(titleCommand, "$dtcId", dtcId);
                    Add(titleCommand, "$title", title);
                    titleCommand.ExecuteNonQuery();
                }
            }

            if (TryGetArray(item, "source_titles", out JsonElement sourceTitles))
            {
                foreach (JsonElement sourceTitle in sourceTitles.EnumerateArray())
                {
                    if (!sourceTitle.TryGetProperty("source", out JsonElement source))
                    {
                        continue;
                    }

                    long sourceReferenceId = ResolveSourceReference(source);

                    using SqliteCommand sourceTitleCommand = CreateCommand(
                        """
                        INSERT INTO dtc_source_titles
                        (dtc_code_id, title, source_reference_id)
                        VALUES ($dtcId, $title, $sourceReferenceId);
                        """
                    );

                    Add(sourceTitleCommand, "$dtcId", dtcId);
                    Add(sourceTitleCommand, "$title", RequiredString(sourceTitle, "title"));
                    Add(sourceTitleCommand, "$sourceReferenceId", sourceReferenceId);
                    sourceTitleCommand.ExecuteNonQuery();
                }
            }

            if (TryGetArray(item, "source_references", out JsonElement sourceReferences))
            {
                foreach (JsonElement source in sourceReferences.EnumerateArray())
                {
                    long sourceReferenceId = ResolveSourceReference(source);

                    using SqliteCommand sourceCommand = CreateCommand(
                        """
                        INSERT OR IGNORE INTO dtc_source_references
                        (dtc_code_id, source_reference_id)
                        VALUES ($dtcId, $sourceReferenceId);
                        """
                    );

                    Add(sourceCommand, "$dtcId", dtcId);
                    Add(sourceCommand, "$sourceReferenceId", sourceReferenceId);
                    sourceCommand.ExecuteNonQuery();
                }
            }

            count++;
        }

        Console.WriteLine($"Imported {count} global DTC codes.");
        return count;
    }

    private int ImportDtcProfiles()
    {
        using JsonDocument document = LoadJson("dtc_profiles.json");

        int count = 0;

        foreach (JsonElement item in document.RootElement.EnumerateArray())
        {
            string code = RequiredString(item, "code").ToUpperInvariant();
            long dtcId = GetDtcId(code);

            long? applicationId = ResolveApplicationId(
                OptionalString(item, "application_key")
            );

            long? sourceReferenceId = ResolveSourceReference(item, "source");
            long? diagnosticSourceReferenceId =
                ResolveSourceReferenceNullable(item, "diagnostic_source");

            string? manufacturerNotes = JoinStringArray(
                item,
                "manufacturer_notes"
            );

            using SqliteCommand command = CreateCommand(
                """
                INSERT INTO dtc_profiles
                (
                    profile_key,
                    dtc_code_id,
                    diagnostic_application_id,
                    module_key,
                    title,
                    description,
                    flash_code,
                    mil_status,
                    condition_for_running,
                    condition_for_setting,
                    action_taken_when_set,
                    manufacturer_notes,
                    data_quality,
                    source_reference_id,
                    diagnostic_source_reference_id
                )
                VALUES
                (
                    $profileKey,
                    $dtcId,
                    $applicationId,
                    $moduleKey,
                    $title,
                    $description,
                    $flashCode,
                    $milStatus,
                    $conditionForRunning,
                    $conditionForSetting,
                    $actionTakenWhenSet,
                    $manufacturerNotes,
                    $dataQuality,
                    $sourceReferenceId,
                    $diagnosticSourceReferenceId
                );

                SELECT last_insert_rowid();
                """
            );

            Add(command, "$profileKey", RequiredString(item, "profile_key"));
            Add(command, "$dtcId", dtcId);
            Add(command, "$applicationId", applicationId);
            Add(command, "$moduleKey", OptionalString(item, "module_key"));
            Add(command, "$title", OptionalString(item, "title"));
            Add(command, "$description", OptionalString(item, "description"));
            Add(command, "$flashCode", OptionalString(item, "flash_code"));
            Add(command, "$milStatus", OptionalString(item, "mil_status"));
            Add(command, "$conditionForRunning", OptionalString(item, "condition_for_running"));
            Add(command, "$conditionForSetting", OptionalString(item, "condition_for_setting"));
            Add(command, "$actionTakenWhenSet", OptionalString(item, "action_taken_when_set"));
            Add(command, "$manufacturerNotes", manufacturerNotes);
            Add(command, "$dataQuality", OptionalString(item, "data_quality"));
            Add(command, "$sourceReferenceId", sourceReferenceId);
            Add(command, "$diagnosticSourceReferenceId", diagnosticSourceReferenceId);

            long profileId = Convert.ToInt64(
                command.ExecuteScalar(),
                CultureInfo.InvariantCulture
            );

            ImportProfileConditions(profileId, item);
            ImportProfileCauses(profileId, item);
            ImportProfileSolutions(profileId, item);
            ImportProfileActions(profileId, item);
            ImportProfileRelations(profileId, item);
            ImportProcedureSteps(profileId, item, "oem_diagnostic_steps", "DIAGNOSIS");
            ImportProcedureSteps(profileId, item, "oem_confirmation_steps", "CONFIRMATION");

            count++;
        }

        Console.WriteLine($"Imported {count} DTC profiles.");
        return count;
    }

    private void ImportProfileConditions(long profileId, JsonElement item)
    {
        if (!TryGetArray(item, "conditions", out JsonElement conditions))
        {
            return;
        }

        int order = 1;

        foreach (JsonElement condition in conditions.EnumerateArray())
        {
            using SqliteCommand command = CreateCommand(
                """
                INSERT INTO dtc_profile_conditions
                (
                    dtc_profile_id,
                    sort_order,
                    condition_text,
                    comparison_operator,
                    comparison_value,
                    unit,
                    operating_condition_key
                )
                VALUES
                (
                    $profileId,
                    $sortOrder,
                    $conditionText,
                    $operator,
                    $value,
                    $unit,
                    $operatingConditionKey
                );
                """
            );

            Add(command, "$profileId", profileId);
            Add(command, "$sortOrder", order++);
            Add(command, "$conditionText", RequiredString(condition, "text"));
            Add(command, "$operator", OptionalString(condition, "operator"));
            Add(command, "$value", OptionalDouble(condition, "value"));
            Add(command, "$unit", OptionalString(condition, "unit"));
            Add(command, "$operatingConditionKey", OptionalString(condition, "operating_condition_key"));
            command.ExecuteNonQuery();
        }
    }

    private void ImportProfileCauses(long profileId, JsonElement item)
    {
        if (!TryGetArray(item, "possible_causes", out JsonElement causes))
        {
            return;
        }

        int order = 1;

        foreach (JsonElement cause in causes.EnumerateArray())
        {
            using SqliteCommand command = CreateCommand(
                """
                INSERT INTO dtc_causes
                (dtc_profile_id, sort_order, description, basis)
                VALUES ($profileId, $sortOrder, $description, $basis);
                """
            );

            Add(command, "$profileId", profileId);
            Add(command, "$sortOrder", order++);
            Add(command, "$description", RequiredString(cause, "text"));
            Add(command, "$basis", OptionalString(cause, "basis"));
            command.ExecuteNonQuery();
            _causeCount++;
        }
    }

    private void ImportProfileSolutions(long profileId, JsonElement item)
    {
        if (!TryGetArray(item, "derived_solution_recommendations", out JsonElement solutions))
        {
            return;
        }

        int order = 1;

        foreach (JsonElement solution in solutions.EnumerateArray())
        {
            using SqliteCommand command = CreateCommand(
                """
                INSERT INTO dtc_solution_recommendations
                (
                    dtc_profile_id,
                    sort_order,
                    description,
                    basis,
                    oem_verbatim,
                    derived_from_cause
                )
                VALUES
                (
                    $profileId,
                    $sortOrder,
                    $description,
                    $basis,
                    $oemVerbatim,
                    $derivedFromCause
                );
                """
            );

            Add(command, "$profileId", profileId);
            Add(command, "$sortOrder", order++);
            Add(command, "$description", RequiredString(solution, "text"));
            Add(command, "$basis", OptionalString(solution, "basis"));
            Add(command, "$oemVerbatim", OptionalBool(solution, "oem_verbatim") is true ? 1 : 0);
            Add(command, "$derivedFromCause", OptionalString(solution, "derived_from_cause"));
            command.ExecuteNonQuery();
            _solutionCount++;
        }
    }

    private void ImportProfileActions(long profileId, JsonElement item)
    {
        if (!TryGetArray(item, "ecu_actions", out JsonElement actions))
        {
            return;
        }

        int order = 1;

        foreach (JsonElement actionElement in actions.EnumerateArray())
        {
            string? action = ElementString(actionElement);

            if (string.IsNullOrWhiteSpace(action))
            {
                continue;
            }

            using SqliteCommand command = CreateCommand(
                """
                INSERT INTO dtc_ecu_actions
                (dtc_profile_id, sort_order, action_text)
                VALUES ($profileId, $sortOrder, $actionText);
                """
            );

            Add(command, "$profileId", profileId);
            Add(command, "$sortOrder", order++);
            Add(command, "$actionText", action);
            command.ExecuteNonQuery();
        }
    }

    private void ImportProfileRelations(long profileId, JsonElement item)
    {
        ImportRelationArray(profileId, item, "priority_dtc_codes", "PRIORITY");
        ImportRelationArray(profileId, item, "related_dtc_codes", "RELATED");
    }

    private void ImportRelationArray(
        long profileId,
        JsonElement item,
        string propertyName,
        string relationType)
    {
        if (!TryGetArray(item, propertyName, out JsonElement relations))
        {
            return;
        }

        foreach (JsonElement relationElement in relations.EnumerateArray())
        {
            string? relatedCode = ElementString(relationElement)?.ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(relatedCode))
            {
                continue;
            }

            using SqliteCommand command = CreateCommand(
                """
                INSERT OR IGNORE INTO dtc_relations
                (dtc_profile_id, related_code, relation_type)
                VALUES ($profileId, $relatedCode, $relationType);
                """
            );

            Add(command, "$profileId", profileId);
            Add(command, "$relatedCode", relatedCode);
            Add(command, "$relationType", relationType);
            command.ExecuteNonQuery();
        }
    }

    private void ImportProcedureSteps(
        long profileId,
        JsonElement item,
        string propertyName,
        string procedureType)
    {
        if (!TryGetArray(item, propertyName, out JsonElement steps))
        {
            return;
        }

        foreach (JsonElement step in steps.EnumerateArray())
        {
            long stepOrder =
                OptionalInt64(step, "step_order")
                ?? throw new InvalidDataException(
                    $"{propertyName} contains a step without step_order."
                );

            using SqliteCommand command = CreateCommand(
                """
                INSERT INTO dtc_procedure_steps
                (
                    dtc_profile_id,
                    procedure_type,
                    step_order,
                    instruction
                )
                VALUES
                (
                    $profileId,
                    $procedureType,
                    $stepOrder,
                    $instruction
                );
                """
            );

            Add(command, "$profileId", profileId);
            Add(command, "$procedureType", procedureType);
            Add(command, "$stepOrder", stepOrder);
            Add(command, "$instruction", RequiredString(step, "instruction"));
            command.ExecuteNonQuery();

            if (procedureType == "DIAGNOSIS")
            {
                _diagnosticStepCount++;
            }
            else if (procedureType == "CONFIRMATION")
            {
                _confirmationStepCount++;
            }
        }
    }

    private int ImportDtcMappings()
    {
        using JsonDocument document = LoadJson("dtc_mappings.json");

        int count = 0;

        foreach (JsonElement item in document.RootElement.EnumerateArray())
        {
            string code = RequiredString(item, "code").ToUpperInvariant();
            long dtcId = GetDtcId(code);
            long? sourceReferenceId = ResolveSourceReference(item, "source");

            using SqliteCommand command = CreateCommand(
                """
                INSERT INTO dtc_mappings
                (
                    dtc_code_id,
                    flash_code,
                    spn,
                    fmi,
                    description,
                    source_reference_id
                )
                VALUES
                (
                    $dtcId,
                    $flashCode,
                    $spn,
                    $fmi,
                    $description,
                    $sourceReferenceId
                );
                """
            );

            Add(command, "$dtcId", dtcId);
            Add(command, "$flashCode", OptionalString(item, "flash_code"));
            Add(command, "$spn", OptionalInt64(item, "spn"));
            Add(command, "$fmi", OptionalInt64(item, "fmi"));
            Add(command, "$description", OptionalString(item, "description"));
            Add(command, "$sourceReferenceId", sourceReferenceId);
            command.ExecuteNonQuery();
            count++;
        }

        Console.WriteLine($"Imported {count} DTC mappings.");
        return count;
    }

    private long ResolveSourceReference(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out JsonElement source)
            || source.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            throw new InvalidDataException(
                $"Missing required source object '{propertyName}'."
            );
        }

        return ResolveSourceReference(source);
    }

    private long? ResolveSourceReferenceNullable(
        JsonElement parent,
        string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out JsonElement source)
            || source.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return ResolveSourceReference(source);
    }

    private long ResolveSourceReference(JsonElement source)
    {
        string documentKey = RequiredString(source, "document_key");

        if (!_sourceDocumentIds.TryGetValue(documentKey, out long sourceDocumentId))
        {
            throw new InvalidDataException(
                $"Unknown source document key: {documentKey}"
            );
        }

        long? pdfPage = OptionalInt64(source, "pdf_page");
        long? pdfPageStart = OptionalInt64(source, "pdf_page_start");
        long? pdfPageEnd = OptionalInt64(source, "pdf_page_end");
        string? printedPage = OptionalString(source, "printed_page");
        string? section = OptionalString(source, "section");

        string cacheKey = string.Join(
            "|",
            documentKey,
            pdfPage?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            pdfPageStart?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            pdfPageEnd?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            printedPage ?? string.Empty,
            section ?? string.Empty
        );

        if (_sourceReferenceIds.TryGetValue(cacheKey, out long existingId))
        {
            return existingId;
        }

        using SqliteCommand command = CreateCommand(
            """
            INSERT INTO source_references
            (
                source_document_id,
                pdf_page,
                pdf_page_start,
                pdf_page_end,
                printed_page,
                section
            )
            VALUES
            (
                $sourceDocumentId,
                $pdfPage,
                $pdfPageStart,
                $pdfPageEnd,
                $printedPage,
                $section
            );

            SELECT last_insert_rowid();
            """
        );

        Add(command, "$sourceDocumentId", sourceDocumentId);
        Add(command, "$pdfPage", pdfPage);
        Add(command, "$pdfPageStart", pdfPageStart);
        Add(command, "$pdfPageEnd", pdfPageEnd);
        Add(command, "$printedPage", printedPage);
        Add(command, "$section", section);

        long sourceReferenceId = Convert.ToInt64(
            command.ExecuteScalar(),
            CultureInfo.InvariantCulture
        );

        _sourceReferenceIds.Add(cacheKey, sourceReferenceId);
        return sourceReferenceId;
    }

    private long? ResolveApplicationId(string? applicationKey)
    {
        if (string.IsNullOrWhiteSpace(applicationKey))
        {
            return null;
        }

        if (!_applicationIds.TryGetValue(applicationKey, out long applicationId))
        {
            throw new InvalidDataException(
                $"Unknown application key: {applicationKey}"
            );
        }

        return applicationId;
    }

    private long GetDtcId(string code)
    {
        if (!_dtcIds.TryGetValue(code, out long dtcId))
        {
            throw new InvalidDataException(
                $"DTC profile/mapping references unknown DTC code: {code}"
            );
        }

        return dtcId;
    }

    private void InsertStringArray(
        JsonElement item,
        string propertyName,
        string sql,
        long applicationId)
    {
        if (!TryGetArray(item, propertyName, out JsonElement values))
        {
            return;
        }

        foreach (JsonElement valueElement in values.EnumerateArray())
        {
            string? value = ElementString(valueElement);

            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            using SqliteCommand command = CreateCommand(sql);
            Add(command, "$applicationId", applicationId);
            Add(command, "$value", value);
            command.ExecuteNonQuery();
        }
    }

    private string? JoinStringArray(JsonElement item, string propertyName)
    {
        if (!TryGetArray(item, propertyName, out JsonElement values))
        {
            return null;
        }

        List<string> lines = new();

        foreach (JsonElement valueElement in values.EnumerateArray())
        {
            string? value = ElementString(valueElement);

            if (!string.IsNullOrWhiteSpace(value))
            {
                lines.Add(value);
            }
        }

        return lines.Count == 0
            ? null
            : string.Join(Environment.NewLine, lines);
    }

    private void SetMetadata(string key, string value)
    {
        using SqliteCommand command = CreateCommand(
            """
            INSERT OR REPLACE INTO kb_metadata(key, value)
            VALUES ($key, $value);
            """
        );

        Add(command, "$key", key);
        Add(command, "$value", value);
        command.ExecuteNonQuery();
    }

    private JsonDocument LoadJson(string fileName)
    {
        string path = Path.Combine(_knowledgeDirectory, fileName);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Knowledge-base JSON file was not found: {path}",
                path
            );
        }

        return JsonDocument.Parse(
            File.ReadAllText(path),
            new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            }
        );
    }

    private SqliteCommand CreateCommand(string sql)
    {
        SqliteCommand command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = sql;
        return command;
    }

    private static void Add(
        SqliteCommand command,
        string parameterName,
        object? value)
    {
        command.Parameters.AddWithValue(
            parameterName,
            value ?? DBNull.Value
        );
    }

    private static bool TryGetArray(
        JsonElement item,
        string propertyName,
        out JsonElement array)
    {
        if (item.TryGetProperty(propertyName, out array)
            && array.ValueKind == JsonValueKind.Array)
        {
            return true;
        }

        array = default;
        return false;
    }

    private static string RequiredString(
        JsonElement item,
        string propertyName)
    {
        string? value = OptionalString(item, propertyName);

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException(
                $"Required JSON property '{propertyName}' is missing or empty."
            );
        }

        return value;
    }

    private static string? OptionalString(
        JsonElement item,
        string propertyName)
    {
        if (!item.TryGetProperty(propertyName, out JsonElement value)
            || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return ElementString(value);
    }

    private static string? ElementString(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private static long? OptionalInt64(
        JsonElement item,
        string propertyName)
    {
        if (!item.TryGetProperty(propertyName, out JsonElement value)
            || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number
            && value.TryGetInt64(out long result))
        {
            return result;
        }

        if (value.ValueKind == JsonValueKind.String
            && long.TryParse(
                value.GetString(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out result))
        {
            return result;
        }

        return null;
    }

    private static double? OptionalDouble(
        JsonElement item,
        string propertyName)
    {
        if (!item.TryGetProperty(propertyName, out JsonElement value)
            || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number
            && value.TryGetDouble(out double result))
        {
            return result;
        }

        if (value.ValueKind == JsonValueKind.String
            && double.TryParse(
                value.GetString(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out result))
        {
            return result;
        }

        return null;
    }

    private static bool? OptionalBool(
        JsonElement item,
        string propertyName)
    {
        if (!item.TryGetProperty(propertyName, out JsonElement value)
            || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(value.GetString(), out bool result)
                => result,
            _ => null
        };
    }
}
