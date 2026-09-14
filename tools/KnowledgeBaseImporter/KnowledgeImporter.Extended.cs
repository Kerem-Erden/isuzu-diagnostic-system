using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace KnowledgeBaseImporter;

public sealed record ExtendedImportSummary(
    int ControlModules,
    int Networks,
    int NetworkEcuPins,
    int ControlModulePins,
    int LiveParameters,
    int LiveParameterAliases,
    int LiveParameterSources,
    int LiveParameterFallbacks,
    int OperatingConditions,
    int LiveReferenceValues,
    int DtcRelatedLiveData,
    int ServiceSpecifications,
    int ServiceSpecificationValues,
    int ActuatorTests,
    int ActuatorLiveParameters,
    int SpecialFunctions,
    int DtcLifecycleRules,
    int DtcLifecycleBehaviors,
    int FaultClasses,
    int SymptomTroubleshooting
);

public sealed partial class KnowledgeImporter
{
    private readonly Dictionary<string, long> _liveParameterIds =
        new(StringComparer.OrdinalIgnoreCase);

    private int _networkEcuPinCount;
    private int _liveParameterAliasCount;
    private int _liveParameterSourceCount;
    private int _liveParameterFallbackCount;
    private int _serviceSpecificationValueCount;
    private int _actuatorLiveParameterCount;
    private int _dtcLifecycleBehaviorCount;

    public ExtendedImportSummary ImportExtendedKnowledge()
    {
        _transaction = _connection.BeginTransaction();

        try
        {
            int controlModuleCount = ImportControlModules();
            int networkCount = ImportNetworks();
            int controlModulePinCount = ImportControlModulePins();

            int liveParameterCount = ImportLiveParameters();
            int operatingConditionCount = ImportOperatingConditions();
            int liveReferenceValueCount = ImportLiveReferenceValues();
            int dtcRelatedLiveDataCount = ImportDtcRelatedLiveData();

            int serviceSpecificationCount = ImportServiceSpecifications();

            int actuatorTestCount = ImportActuatorTests();
            int specialFunctionCount = ImportSpecialFunctions();

            int lifecycleRuleCount = ImportDtcLifecycleRules();
            int faultClassCount = ImportFaultClasses();

            int symptomTroubleshootingCount = ImportSymptomTroubleshooting();

            SetMetadata("extended_knowledge_import_complete", "true");
            SetMetadata(
                "full_import_generated_at_utc",
                DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
            );

            _transaction.Commit();
            _transaction.Dispose();
            _transaction = null;

            return new ExtendedImportSummary(
                controlModuleCount,
                networkCount,
                _networkEcuPinCount,
                controlModulePinCount,
                liveParameterCount,
                _liveParameterAliasCount,
                _liveParameterSourceCount,
                _liveParameterFallbackCount,
                operatingConditionCount,
                liveReferenceValueCount,
                dtcRelatedLiveDataCount,
                serviceSpecificationCount,
                _serviceSpecificationValueCount,
                actuatorTestCount,
                _actuatorLiveParameterCount,
                specialFunctionCount,
                lifecycleRuleCount,
                _dtcLifecycleBehaviorCount,
                faultClassCount,
                symptomTroubleshootingCount
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

    private int ImportControlModules()
    {
        using JsonDocument document = LoadJson("control_modules.json");

        int count = 0;

        foreach (JsonElement item in document.RootElement.EnumerateArray())
        {
            long applicationId = RequiredApplicationId(item);
            long? sourceReferenceId = ResolveSourceReferenceNullable(item, "source");

            using SqliteCommand command = CreateCommand(
                """
                INSERT INTO control_modules
                (
                    diagnostic_application_id,
                    module_key,
                    module_name,
                    manufacturer,
                    connector_summary,
                    source_reference_id
                )
                VALUES
                (
                    $applicationId,
                    $moduleKey,
                    $moduleName,
                    $manufacturer,
                    $connectorSummary,
                    $sourceReferenceId
                );
                """
            );

            Add(command, "$applicationId", applicationId);
            Add(command, "$moduleKey", RequiredString(item, "module_key"));
            Add(command, "$moduleName", OptionalString(item, "module_name"));
            Add(command, "$manufacturer", OptionalString(item, "manufacturer"));
            Add(command, "$connectorSummary", OptionalString(item, "connector_summary"));
            Add(command, "$sourceReferenceId", sourceReferenceId);

            command.ExecuteNonQuery();
            count++;
        }

        Console.WriteLine($"Imported {count} control modules.");
        return count;
    }

    private int ImportNetworks()
    {
        using JsonDocument document = LoadJson("networks.json");

        int count = 0;

        foreach (JsonElement item in document.RootElement.EnumerateArray())
        {
            long applicationId = RequiredApplicationId(item);
            long? sourceReferenceId = ResolveSourceReferenceNullable(item, "source");

            bool? diagnostic = OptionalBool(item, "diagnostic");

            using SqliteCommand command = CreateCommand(
                """
                INSERT INTO vehicle_networks
                (
                    diagnostic_application_id,
                    network_key,
                    bitrate_bps,
                    diagnostic,
                    dlc_high_pin,
                    dlc_low_pin,
                    source_reference_id
                )
                VALUES
                (
                    $applicationId,
                    $networkKey,
                    $bitrateBps,
                    $diagnostic,
                    $dlcHighPin,
                    $dlcLowPin,
                    $sourceReferenceId
                );

                SELECT last_insert_rowid();
                """
            );

            Add(command, "$applicationId", applicationId);
            Add(command, "$networkKey", RequiredString(item, "network_key"));
            Add(command, "$bitrateBps", OptionalInt64(item, "bitrate_bps"));
            Add(
                command,
                "$diagnostic",
                diagnostic.HasValue ? (diagnostic.Value ? 1 : 0) : null
            );
            Add(command, "$dlcHighPin", OptionalInt64(item, "dlc_high_pin"));
            Add(command, "$dlcLowPin", OptionalInt64(item, "dlc_low_pin"));
            Add(command, "$sourceReferenceId", sourceReferenceId);

            long networkId = Convert.ToInt64(
                command.ExecuteScalar(),
                CultureInfo.InvariantCulture
            );

            if (TryGetArray(item, "ecu_pins", out JsonElement ecuPins))
            {
                foreach (JsonElement pinElement in ecuPins.EnumerateArray())
                {
                    string? pinDescription = ElementString(pinElement);

                    if (string.IsNullOrWhiteSpace(pinDescription))
                    {
                        continue;
                    }

                    using SqliteCommand pinCommand = CreateCommand(
                        """
                        INSERT OR IGNORE INTO network_ecu_pins
                        (
                            vehicle_network_id,
                            pin_description
                        )
                        VALUES
                        (
                            $networkId,
                            $pinDescription
                        );
                        """
                    );

                    Add(pinCommand, "$networkId", networkId);
                    Add(pinCommand, "$pinDescription", pinDescription);
                    pinCommand.ExecuteNonQuery();

                    _networkEcuPinCount++;
                }
            }

            count++;
        }

        Console.WriteLine(
            $"Imported {count} vehicle networks " +
            $"({_networkEcuPinCount} ECU pin references)."
        );

        return count;
    }

    private int ImportControlModulePins()
    {
        using JsonDocument document = LoadJson("control_module_pins.json");

        int count = 0;

        foreach (JsonElement item in document.RootElement.EnumerateArray())
        {
            long applicationId = RequiredApplicationId(item);
            long? sourceReferenceId = ResolveSourceReferenceNullable(item, "source");

            using SqliteCommand command = CreateCommand(
                """
                INSERT INTO control_module_pins
                (
                    diagnostic_application_id,
                    module_key,
                    connector,
                    pin,
                    terminal_name,
                    description,
                    source_reference_id
                )
                VALUES
                (
                    $applicationId,
                    $moduleKey,
                    $connector,
                    $pin,
                    $terminalName,
                    $description,
                    $sourceReferenceId
                );
                """
            );

            Add(command, "$applicationId", applicationId);
            Add(command, "$moduleKey", RequiredString(item, "module_key"));
            Add(command, "$connector", OptionalString(item, "connector"));
            Add(command, "$pin", RequiredString(item, "pin"));
            Add(command, "$terminalName", OptionalString(item, "terminal_name"));
            Add(command, "$description", OptionalString(item, "description"));
            Add(command, "$sourceReferenceId", sourceReferenceId);

            command.ExecuteNonQuery();
            count++;
        }

        Console.WriteLine($"Imported {count} control-module pins.");
        return count;
    }

    private int ImportLiveParameters()
    {
        using JsonDocument document = LoadJson("live_parameters.json");

        int count = 0;

        foreach (JsonElement item in document.RootElement.EnumerateArray())
        {
            string parameterKey = RequiredString(item, "parameter_key");

            using SqliteCommand command = CreateCommand(
                """
                INSERT INTO live_parameters
                (
                    parameter_key,
                    display_name,
                    quantity_type,
                    default_unit
                )
                VALUES
                (
                    $parameterKey,
                    $displayName,
                    $quantityType,
                    $defaultUnit
                );

                SELECT last_insert_rowid();
                """
            );

            Add(command, "$parameterKey", parameterKey);
            Add(command, "$displayName", RequiredString(item, "display_name"));
            Add(command, "$quantityType", OptionalString(item, "quantity_type"));
            Add(command, "$defaultUnit", OptionalString(item, "default_unit"));

            long liveParameterId = Convert.ToInt64(
                command.ExecuteScalar(),
                CultureInfo.InvariantCulture
            );

            _liveParameterIds.Add(parameterKey, liveParameterId);

            if (TryGetArray(item, "aliases", out JsonElement aliases))
            {
                foreach (JsonElement aliasElement in aliases.EnumerateArray())
                {
                    string? alias = ElementString(aliasElement);

                    if (string.IsNullOrWhiteSpace(alias))
                    {
                        continue;
                    }

                    using SqliteCommand aliasCommand = CreateCommand(
                        """
                        INSERT OR IGNORE INTO live_parameter_aliases
                        (
                            live_parameter_id,
                            alias
                        )
                        VALUES
                        (
                            $liveParameterId,
                            $alias
                        );
                        """
                    );

                    Add(aliasCommand, "$liveParameterId", liveParameterId);
                    Add(aliasCommand, "$alias", alias);
                    aliasCommand.ExecuteNonQuery();

                    _liveParameterAliasCount++;
                }
            }

            if (TryGetArray(item, "sources", out JsonElement sources))
            {
                foreach (JsonElement source in sources.EnumerateArray())
                {
                    long sourceReferenceId = ResolveSourceReference(source);

                    using SqliteCommand sourceCommand = CreateCommand(
                        """
                        INSERT OR IGNORE INTO live_parameter_sources
                        (
                            live_parameter_id,
                            source_reference_id
                        )
                        VALUES
                        (
                            $liveParameterId,
                            $sourceReferenceId
                        );
                        """
                    );

                    Add(sourceCommand, "$liveParameterId", liveParameterId);
                    Add(sourceCommand, "$sourceReferenceId", sourceReferenceId);
                    sourceCommand.ExecuteNonQuery();

                    _liveParameterSourceCount++;
                }
            }

            if (TryGetArray(item, "fallback_behaviors", out JsonElement fallbacks))
            {
                foreach (JsonElement fallback in fallbacks.EnumerateArray())
                {
                    long? applicationId = ResolveApplicationId(
                        OptionalString(fallback, "application_key")
                    );

                    using SqliteCommand fallbackCommand = CreateCommand(
                        """
                        INSERT INTO live_parameter_fallback_behaviors
                        (
                            live_parameter_id,
                            diagnostic_application_id,
                            behavior_text
                        )
                        VALUES
                        (
                            $liveParameterId,
                            $applicationId,
                            $behaviorText
                        );
                        """
                    );

                    Add(fallbackCommand, "$liveParameterId", liveParameterId);
                    Add(fallbackCommand, "$applicationId", applicationId);
                    Add(fallbackCommand, "$behaviorText", RequiredString(fallback, "text"));
                    fallbackCommand.ExecuteNonQuery();

                    _liveParameterFallbackCount++;
                }
            }

            count++;
        }

        Console.WriteLine(
            $"Imported {count} live parameters " +
            $"({_liveParameterAliasCount} aliases, " +
            $"{_liveParameterSourceCount} source links, " +
            $"{_liveParameterFallbackCount} fallback behaviors)."
        );

        return count;
    }

    private int ImportOperatingConditions()
    {
        using JsonDocument document = LoadJson("operating_conditions.json");

        int count = 0;

        foreach (JsonElement item in document.RootElement.EnumerateArray())
        {
            long applicationId = RequiredApplicationId(item);
            long? sourceReferenceId = ResolveSourceReferenceNullable(item, "source");

            using SqliteCommand command = CreateCommand(
                """
                INSERT INTO operating_conditions
                (
                    operating_condition_key,
                    diagnostic_application_id,
                    name,
                    description,
                    source_reference_id
                )
                VALUES
                (
                    $conditionKey,
                    $applicationId,
                    $name,
                    $description,
                    $sourceReferenceId
                );
                """
            );

            Add(
                command,
                "$conditionKey",
                RequiredString(item, "operating_condition_key")
            );
            Add(command, "$applicationId", applicationId);
            Add(command, "$name", RequiredString(item, "name"));
            Add(command, "$description", OptionalString(item, "description"));
            Add(command, "$sourceReferenceId", sourceReferenceId);

            command.ExecuteNonQuery();
            count++;
        }

        Console.WriteLine($"Imported {count} operating conditions.");
        return count;
    }

    private int ImportLiveReferenceValues()
    {
        using JsonDocument document = LoadJson("live_reference_values.json");

        int count = 0;

        foreach (JsonElement item in document.RootElement.EnumerateArray())
        {
            long liveParameterId = GetLiveParameterId(
                RequiredString(item, "parameter_key")
            );

            long applicationId = RequiredApplicationId(item);
            long? sourceReferenceId = ResolveSourceReferenceNullable(item, "source");

            bool approximate = OptionalBool(item, "approximate") ?? false;

            using SqliteCommand command = CreateCommand(
                """
                INSERT INTO live_reference_values
                (
                    live_parameter_id,
                    diagnostic_application_id,
                    operating_condition_key,
                    operating_condition_name,
                    reference_kind,
                    comparison_operator,
                    minimum_value,
                    maximum_value,
                    nominal_value,
                    text_value,
                    unit,
                    approximate,
                    axis_name,
                    axis_value,
                    axis_unit,
                    notes,
                    source_reference_id
                )
                VALUES
                (
                    $liveParameterId,
                    $applicationId,
                    $operatingConditionKey,
                    $operatingConditionName,
                    $referenceKind,
                    $comparisonOperator,
                    $minimumValue,
                    $maximumValue,
                    $nominalValue,
                    $textValue,
                    $unit,
                    $approximate,
                    $axisName,
                    $axisValue,
                    $axisUnit,
                    $notes,
                    $sourceReferenceId
                );
                """
            );

            Add(command, "$liveParameterId", liveParameterId);
            Add(command, "$applicationId", applicationId);
            Add(
                command,
                "$operatingConditionKey",
                OptionalString(item, "operating_condition_key")
            );
            Add(
                command,
                "$operatingConditionName",
                OptionalString(item, "operating_condition_name")
            );
            Add(command, "$referenceKind", OptionalString(item, "reference_kind"));
            Add(command, "$comparisonOperator", OptionalString(item, "operator"));
            Add(command, "$minimumValue", OptionalDouble(item, "minimum"));
            Add(command, "$maximumValue", OptionalDouble(item, "maximum"));
            Add(command, "$nominalValue", OptionalDouble(item, "nominal"));
            Add(command, "$textValue", OptionalString(item, "text_value"));
            Add(command, "$unit", OptionalString(item, "unit"));
            Add(command, "$approximate", approximate ? 1 : 0);
            Add(command, "$axisName", OptionalString(item, "axis_name"));
            Add(command, "$axisValue", OptionalDouble(item, "axis_value"));
            Add(command, "$axisUnit", OptionalString(item, "axis_unit"));
            Add(command, "$notes", OptionalString(item, "notes"));
            Add(command, "$sourceReferenceId", sourceReferenceId);

            command.ExecuteNonQuery();
            count++;
        }

        Console.WriteLine($"Imported {count} live-data reference values.");
        return count;
    }

    private int ImportDtcRelatedLiveData()
    {
        using JsonDocument document = LoadJson("dtc_related_live_data.json");

        int count = 0;

        foreach (JsonElement item in document.RootElement.EnumerateArray())
        {
            long dtcId = GetDtcId(
                RequiredString(item, "code").ToUpperInvariant()
            );

            long liveParameterId = GetLiveParameterId(
                RequiredString(item, "parameter_key")
            );

            long? applicationId = ResolveApplicationId(
                OptionalString(item, "application_key")
            );

            long? sourceReferenceId = ResolveSourceReferenceNullable(item, "source");

            using SqliteCommand command = CreateCommand(
                """
                INSERT INTO dtc_related_live_data
                (
                    dtc_code_id,
                    live_parameter_id,
                    diagnostic_application_id,
                    display_priority,
                    relation_role,
                    relation_basis,
                    reason,
                    source_reference_id
                )
                VALUES
                (
                    $dtcId,
                    $liveParameterId,
                    $applicationId,
                    $displayPriority,
                    $relationRole,
                    $relationBasis,
                    $reason,
                    $sourceReferenceId
                );
                """
            );

            Add(command, "$dtcId", dtcId);
            Add(command, "$liveParameterId", liveParameterId);
            Add(command, "$applicationId", applicationId);
            Add(
                command,
                "$displayPriority",
                OptionalInt64(item, "display_priority") ?? 100
            );
            Add(command, "$relationRole", OptionalString(item, "relation_role"));
            Add(command, "$relationBasis", OptionalString(item, "relation_basis"));
            Add(command, "$reason", OptionalString(item, "reason"));
            Add(command, "$sourceReferenceId", sourceReferenceId);

            command.ExecuteNonQuery();
            count++;
        }

        Console.WriteLine($"Imported {count} DTC/live-data links.");
        return count;
    }

    private int ImportServiceSpecifications()
    {
        using JsonDocument document = LoadJson("service_specifications.json");

        int count = 0;

        foreach (JsonElement item in document.RootElement.EnumerateArray())
        {
            long applicationId = RequiredApplicationId(item);
            long? sourceReferenceId = ResolveSourceReferenceNullable(item, "source");

            using SqliteCommand command = CreateCommand(
                """
                INSERT INTO service_specifications
                (
                    diagnostic_application_id,
                    category,
                    specification_name,
                    value_text,
                    unit,
                    condition_text,
                    notes,
                    source_reference_id
                )
                VALUES
                (
                    $applicationId,
                    $category,
                    $specificationName,
                    $valueText,
                    $unit,
                    $conditionText,
                    $notes,
                    $sourceReferenceId
                );

                SELECT last_insert_rowid();
                """
            );

            Add(command, "$applicationId", applicationId);
            Add(command, "$category", RequiredString(item, "category"));
            Add(
                command,
                "$specificationName",
                RequiredString(item, "specification_name")
            );
            Add(command, "$valueText", OptionalString(item, "value_text"));
            Add(command, "$unit", OptionalString(item, "unit"));
            Add(command, "$conditionText", OptionalString(item, "condition_text"));
            Add(command, "$notes", OptionalString(item, "notes"));
            Add(command, "$sourceReferenceId", sourceReferenceId);

            long specificationId = Convert.ToInt64(
                command.ExecuteScalar(),
                CultureInfo.InvariantCulture
            );

            if (TryGetArray(item, "values", out JsonElement values))
            {
                int sortOrder = 1;

                foreach (JsonElement value in values.EnumerateArray())
                {
                    using SqliteCommand valueCommand = CreateCommand(
                        """
                        INSERT INTO service_specification_values
                        (
                            service_specification_id,
                            sort_order,
                            value_text
                        )
                        VALUES
                        (
                            $specificationId,
                            $sortOrder,
                            $valueText
                        );
                        """
                    );

                    Add(valueCommand, "$specificationId", specificationId);
                    Add(valueCommand, "$sortOrder", sortOrder++);
                    Add(valueCommand, "$valueText", ElementString(value));
                    valueCommand.ExecuteNonQuery();

                    _serviceSpecificationValueCount++;
                }
            }

            count++;
        }

        Console.WriteLine(
            $"Imported {count} service specifications " +
            $"({_serviceSpecificationValueCount} child values)."
        );

        return count;
    }

    private int ImportActuatorTests()
    {
        using JsonDocument document = LoadJson("actuator_tests.json");

        int count = 0;

        foreach (JsonElement item in document.RootElement.EnumerateArray())
        {
            long applicationId = RequiredApplicationId(item);
            long? sourceReferenceId = ResolveSourceReferenceNullable(item, "source");

            using SqliteCommand command = CreateCommand(
                """
                INSERT INTO actuator_tests
                (
                    diagnostic_application_id,
                    module_key,
                    test_key,
                    display_name,
                    description,
                    source_reference_id
                )
                VALUES
                (
                    $applicationId,
                    $moduleKey,
                    $testKey,
                    $displayName,
                    $description,
                    $sourceReferenceId
                );

                SELECT last_insert_rowid();
                """
            );

            Add(command, "$applicationId", applicationId);
            Add(command, "$moduleKey", OptionalString(item, "module_key"));
            Add(command, "$testKey", RequiredString(item, "test_key"));
            Add(command, "$displayName", RequiredString(item, "display_name"));
            Add(command, "$description", OptionalString(item, "description"));
            Add(command, "$sourceReferenceId", sourceReferenceId);

            long actuatorTestId = Convert.ToInt64(
                command.ExecuteScalar(),
                CultureInfo.InvariantCulture
            );

            if (TryGetArray(
                item,
                "related_live_parameters",
                out JsonElement relatedLiveParameters))
            {
                foreach (JsonElement parameterElement
                    in relatedLiveParameters.EnumerateArray())
                {
                    string? parameterKey = ElementString(parameterElement);

                    if (string.IsNullOrWhiteSpace(parameterKey))
                    {
                        continue;
                    }

                    // Validate the key even though the table intentionally stores
                    // the stable string key rather than a numeric FK.
                    _ = GetLiveParameterId(parameterKey);

                    using SqliteCommand parameterCommand = CreateCommand(
                        """
                        INSERT OR IGNORE INTO actuator_test_live_parameters
                        (
                            actuator_test_id,
                            parameter_key
                        )
                        VALUES
                        (
                            $actuatorTestId,
                            $parameterKey
                        );
                        """
                    );

                    Add(parameterCommand, "$actuatorTestId", actuatorTestId);
                    Add(parameterCommand, "$parameterKey", parameterKey);
                    parameterCommand.ExecuteNonQuery();

                    _actuatorLiveParameterCount++;
                }
            }

            count++;
        }

        Console.WriteLine(
            $"Imported {count} actuator tests " +
            $"({_actuatorLiveParameterCount} live-parameter links)."
        );

        return count;
    }

    private int ImportSpecialFunctions()
    {
        using JsonDocument document = LoadJson("special_functions.json");

        int count = 0;

        foreach (JsonElement item in document.RootElement.EnumerateArray())
        {
            long applicationId = RequiredApplicationId(item);
            long? sourceReferenceId = ResolveSourceReferenceNullable(item, "source");

            using SqliteCommand command = CreateCommand(
                """
                INSERT INTO special_functions
                (
                    diagnostic_application_id,
                    module_key,
                    function_key,
                    name,
                    notes,
                    source_reference_id
                )
                VALUES
                (
                    $applicationId,
                    $moduleKey,
                    $functionKey,
                    $name,
                    $notes,
                    $sourceReferenceId
                );
                """
            );

            Add(command, "$applicationId", applicationId);
            Add(command, "$moduleKey", OptionalString(item, "module_key"));
            Add(command, "$functionKey", RequiredString(item, "function_key"));
            Add(command, "$name", RequiredString(item, "name"));
            Add(command, "$notes", OptionalString(item, "notes"));
            Add(command, "$sourceReferenceId", sourceReferenceId);

            command.ExecuteNonQuery();
            count++;
        }

        Console.WriteLine($"Imported {count} special functions.");
        return count;
    }

    private int ImportDtcLifecycleRules()
    {
        using JsonDocument document = LoadJson("dtc_lifecycle_rules.json");

        int count = 0;

        foreach (JsonElement item in document.RootElement.EnumerateArray())
        {
            long applicationId = RequiredApplicationId(item);
            long? sourceReferenceId = ResolveSourceReferenceNullable(item, "source");

            long faultCycleType =
                OptionalInt64(item, "fault_cycle_type")
                ?? throw new InvalidDataException(
                    "DTC lifecycle rule is missing fault_cycle_type."
                );

            bool emissionsRelated =
                OptionalBool(item, "emissions_related")
                ?? throw new InvalidDataException(
                    "DTC lifecycle rule is missing emissions_related."
                );

            using SqliteCommand command = CreateCommand(
                """
                INSERT INTO dtc_lifecycle_rules
                (
                    diagnostic_application_id,
                    fault_cycle_type,
                    emissions_related,
                    indicator,
                    source_reference_id
                )
                VALUES
                (
                    $applicationId,
                    $faultCycleType,
                    $emissionsRelated,
                    $indicator,
                    $sourceReferenceId
                );

                SELECT last_insert_rowid();
                """
            );

            Add(command, "$applicationId", applicationId);
            Add(command, "$faultCycleType", faultCycleType);
            Add(command, "$emissionsRelated", emissionsRelated ? 1 : 0);
            Add(command, "$indicator", OptionalString(item, "indicator"));
            Add(command, "$sourceReferenceId", sourceReferenceId);

            long ruleId = Convert.ToInt64(
                command.ExecuteScalar(),
                CultureInfo.InvariantCulture
            );

            ImportLifecycleBehaviorArray(
                ruleId,
                item,
                "set_behavior",
                "SET"
            );

            ImportLifecycleBehaviorArray(
                ruleId,
                item,
                "clear_behavior",
                "CLEAR"
            );

            count++;
        }

        Console.WriteLine(
            $"Imported {count} DTC lifecycle rules " +
            $"({_dtcLifecycleBehaviorCount} behavior rows)."
        );

        return count;
    }

    private void ImportLifecycleBehaviorArray(
        long ruleId,
        JsonElement item,
        string propertyName,
        string behaviorType)
    {
        if (!TryGetArray(item, propertyName, out JsonElement behaviors))
        {
            return;
        }

        int sortOrder = 1;

        foreach (JsonElement behaviorElement in behaviors.EnumerateArray())
        {
            string? behaviorText = ElementString(behaviorElement);

            if (string.IsNullOrWhiteSpace(behaviorText))
            {
                continue;
            }

            using SqliteCommand command = CreateCommand(
                """
                INSERT INTO dtc_lifecycle_behaviors
                (
                    dtc_lifecycle_rule_id,
                    behavior_type,
                    sort_order,
                    behavior_text
                )
                VALUES
                (
                    $ruleId,
                    $behaviorType,
                    $sortOrder,
                    $behaviorText
                );
                """
            );

            Add(command, "$ruleId", ruleId);
            Add(command, "$behaviorType", behaviorType);
            Add(command, "$sortOrder", sortOrder++);
            Add(command, "$behaviorText", behaviorText);

            command.ExecuteNonQuery();
            _dtcLifecycleBehaviorCount++;
        }
    }

    private int ImportFaultClasses()
    {
        using JsonDocument document = LoadJson("fault_classes.json");

        int count = 0;

        foreach (JsonElement item in document.RootElement.EnumerateArray())
        {
            long applicationId = RequiredApplicationId(item);
            long? sourceReferenceId = ResolveSourceReferenceNullable(item, "source");

            using SqliteCommand command = CreateCommand(
                """
                INSERT INTO fault_classes
                (
                    diagnostic_application_id,
                    fault_class,
                    description,
                    source_reference_id
                )
                VALUES
                (
                    $applicationId,
                    $faultClass,
                    $description,
                    $sourceReferenceId
                );
                """
            );

            Add(command, "$applicationId", applicationId);
            Add(command, "$faultClass", RequiredString(item, "fault_class"));
            Add(command, "$description", RequiredString(item, "description"));
            Add(command, "$sourceReferenceId", sourceReferenceId);

            command.ExecuteNonQuery();
            count++;
        }

        Console.WriteLine($"Imported {count} fault classes.");
        return count;
    }

    private int ImportSymptomTroubleshooting()
    {
        using JsonDocument document = LoadJson("symptom_troubleshooting.json");

        int count = 0;

        foreach (JsonElement item in document.RootElement.EnumerateArray())
        {
            long applicationId = RequiredApplicationId(item);
            long? sourceReferenceId = ResolveSourceReferenceNullable(item, "source");

            using SqliteCommand command = CreateCommand(
                """
                INSERT INTO symptom_troubleshooting
                (
                    diagnostic_application_id,
                    condition_text,
                    possible_cause,
                    correction,
                    source_reference_id
                )
                VALUES
                (
                    $applicationId,
                    $conditionText,
                    $possibleCause,
                    $correction,
                    $sourceReferenceId
                );
                """
            );

            Add(command, "$applicationId", applicationId);
            Add(command, "$conditionText", OptionalString(item, "condition"));
            Add(command, "$possibleCause", RequiredString(item, "possible_cause"));
            Add(command, "$correction", OptionalString(item, "correction"));
            Add(command, "$sourceReferenceId", sourceReferenceId);

            command.ExecuteNonQuery();
            count++;
        }

        Console.WriteLine($"Imported {count} symptom troubleshooting rows.");
        return count;
    }

    private long RequiredApplicationId(JsonElement item)
    {
        string applicationKey = RequiredString(item, "application_key");

        return ResolveApplicationId(applicationKey)
            ?? throw new InvalidDataException(
                $"Unknown required application key: {applicationKey}"
            );
    }

    private long GetLiveParameterId(string parameterKey)
    {
        if (!_liveParameterIds.TryGetValue(parameterKey, out long id))
        {
            throw new InvalidDataException(
                $"Unknown live parameter key: {parameterKey}"
            );
        }

        return id;
    }
}
