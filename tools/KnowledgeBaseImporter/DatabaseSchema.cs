using Microsoft.Data.Sqlite;

namespace KnowledgeBaseImporter;

public static class DatabaseSchema
{
    public const int SchemaVersion = 1;

    public static void Create(SqliteConnection connection)
    {
        using SqliteCommand command = connection.CreateCommand();

        command.CommandText =
        """
        PRAGMA foreign_keys = ON;

        /*
         * Knowledge-base metadata
         */
        CREATE TABLE IF NOT EXISTS kb_metadata
        (
            key TEXT PRIMARY KEY,
            value TEXT NOT NULL
        );

        /*
         * ============================================================
         * SOURCES / PROVENANCE
         * ============================================================
         */

        CREATE TABLE IF NOT EXISTS source_documents
        (
            id INTEGER PRIMARY KEY AUTOINCREMENT,

            document_key TEXT NOT NULL UNIQUE,

            file_name TEXT NULL,
            title TEXT NOT NULL,
            publisher TEXT NULL,
            publication_year INTEGER NULL,
            document_code TEXT NULL,

            role TEXT NULL,

            canonical INTEGER NOT NULL DEFAULT 1,
            canonical_source_key TEXT NULL,

            size_bytes INTEGER NULL,
            sha256 TEXT NULL,
            page_count INTEGER NULL
        );

        CREATE TABLE IF NOT EXISTS source_references
        (
            id INTEGER PRIMARY KEY AUTOINCREMENT,

            source_document_id INTEGER NOT NULL,

            pdf_page INTEGER NULL,
            pdf_page_start INTEGER NULL,
            pdf_page_end INTEGER NULL,

            printed_page TEXT NULL,
            section TEXT NULL,

            FOREIGN KEY (source_document_id)
                REFERENCES source_documents(id)
                ON DELETE CASCADE
        );

        CREATE UNIQUE INDEX IF NOT EXISTS
            idx_source_reference_unique
        ON source_references
        (
            source_document_id,
            IFNULL(pdf_page, -1),
            IFNULL(pdf_page_start, -1),
            IFNULL(pdf_page_end, -1),
            IFNULL(printed_page, ''),
            IFNULL(section, '')
        );

        /*
         * ============================================================
         * VEHICLE / ENGINE APPLICATIONS
         * ============================================================
         */

        CREATE TABLE IF NOT EXISTS diagnostic_applications
        (
            id INTEGER PRIMARY KEY AUTOINCREMENT,

            application_key TEXT NOT NULL UNIQUE,

            manufacturer TEXT NOT NULL,

            vehicle_family TEXT NULL,

            model_year_from INTEGER NULL,
            model_year_to INTEGER NULL,

            production_start TEXT NULL,

            engine_family TEXT NULL,
            engine_variant TEXT NULL,

            emissions_generation TEXT NULL,
            system_generation TEXT NULL,

            notes TEXT NULL,

            source_reference_id INTEGER NULL,

            FOREIGN KEY (source_reference_id)
                REFERENCES source_references(id)
                ON DELETE SET NULL
        );

        CREATE TABLE IF NOT EXISTS application_vehicle_models
        (
            id INTEGER PRIMARY KEY AUTOINCREMENT,

            diagnostic_application_id INTEGER NOT NULL,
            vehicle_model TEXT NOT NULL,

            UNIQUE (
                diagnostic_application_id,
                vehicle_model
            ),

            FOREIGN KEY (diagnostic_application_id)
                REFERENCES diagnostic_applications(id)
                ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS application_engine_variants
        (
            id INTEGER PRIMARY KEY AUTOINCREMENT,

            diagnostic_application_id INTEGER NOT NULL,
            engine_variant TEXT NOT NULL,

            UNIQUE (
                diagnostic_application_id,
                engine_variant
            ),

            FOREIGN KEY (diagnostic_application_id)
                REFERENCES diagnostic_applications(id)
                ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS application_markets
        (
            id INTEGER PRIMARY KEY AUTOINCREMENT,

            diagnostic_application_id INTEGER NOT NULL,
            market TEXT NOT NULL,

            UNIQUE (
                diagnostic_application_id,
                market
            ),

            FOREIGN KEY (diagnostic_application_id)
                REFERENCES diagnostic_applications(id)
                ON DELETE CASCADE
        );

        /*
         * ============================================================
         * CONTROL MODULES / NETWORK
         * ============================================================
         */

        CREATE TABLE IF NOT EXISTS control_modules
        (
            id INTEGER PRIMARY KEY AUTOINCREMENT,

            diagnostic_application_id INTEGER NOT NULL,

            module_key TEXT NOT NULL,
            module_name TEXT NULL,

            manufacturer TEXT NULL,
            connector_summary TEXT NULL,

            source_reference_id INTEGER NULL,

            UNIQUE (
                diagnostic_application_id,
                module_key
            ),

            FOREIGN KEY (diagnostic_application_id)
                REFERENCES diagnostic_applications(id)
                ON DELETE CASCADE,

            FOREIGN KEY (source_reference_id)
                REFERENCES source_references(id)
                ON DELETE SET NULL
        );

        CREATE TABLE IF NOT EXISTS vehicle_networks
        (
            id INTEGER PRIMARY KEY AUTOINCREMENT,

            diagnostic_application_id INTEGER NOT NULL,

            network_key TEXT NOT NULL,

            bitrate_bps INTEGER NULL,
            diagnostic INTEGER NULL,

            dlc_high_pin INTEGER NULL,
            dlc_low_pin INTEGER NULL,

            source_reference_id INTEGER NULL,

            UNIQUE (
                diagnostic_application_id,
                network_key
            ),

            FOREIGN KEY (diagnostic_application_id)
                REFERENCES diagnostic_applications(id)
                ON DELETE CASCADE,

            FOREIGN KEY (source_reference_id)
                REFERENCES source_references(id)
                ON DELETE SET NULL
        );

        CREATE TABLE IF NOT EXISTS network_ecu_pins
        (
            id INTEGER PRIMARY KEY AUTOINCREMENT,

            vehicle_network_id INTEGER NOT NULL,

            pin_description TEXT NOT NULL,

            UNIQUE (
                vehicle_network_id,
                pin_description
            ),

            FOREIGN KEY (vehicle_network_id)
                REFERENCES vehicle_networks(id)
                ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS control_module_pins
        (
            id INTEGER PRIMARY KEY AUTOINCREMENT,

            diagnostic_application_id INTEGER NOT NULL,

            module_key TEXT NOT NULL,

            connector TEXT NULL,
            pin TEXT NOT NULL,

            terminal_name TEXT NULL,
            description TEXT NULL,

            source_reference_id INTEGER NULL,

            FOREIGN KEY (diagnostic_application_id)
                REFERENCES diagnostic_applications(id)
                ON DELETE CASCADE,

            FOREIGN KEY (source_reference_id)
                REFERENCES source_references(id)
                ON DELETE SET NULL
        );

        /*
         * ============================================================
         * GLOBAL DTC INDEX
         *
         * IMPORTANT:
         * DTC lookup is GLOBAL by code.
         * Engine/application does NOT decide whether a DTC exists.
         * ============================================================
         */

        CREATE TABLE IF NOT EXISTS dtc_codes
        (
            id INTEGER PRIMARY KEY AUTOINCREMENT,

            code TEXT NOT NULL COLLATE NOCASE UNIQUE,

            title TEXT NOT NULL,
            system TEXT NULL,

            knowledge_status TEXT NULL
        );

        CREATE TABLE IF NOT EXISTS dtc_alternate_titles
        (
            id INTEGER PRIMARY KEY AUTOINCREMENT,

            dtc_code_id INTEGER NOT NULL,
            title TEXT NOT NULL,

            UNIQUE (
                dtc_code_id,
                title
            ),

            FOREIGN KEY (dtc_code_id)
                REFERENCES dtc_codes(id)
                ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS dtc_source_titles
        (
            id INTEGER PRIMARY KEY AUTOINCREMENT,

            dtc_code_id INTEGER NOT NULL,

            title TEXT NOT NULL,
            source_reference_id INTEGER NOT NULL,

            FOREIGN KEY (dtc_code_id)
                REFERENCES dtc_codes(id)
                ON DELETE CASCADE,

            FOREIGN KEY (source_reference_id)
                REFERENCES source_references(id)
                ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS dtc_source_references
        (
            id INTEGER PRIMARY KEY AUTOINCREMENT,

            dtc_code_id INTEGER NOT NULL,
            source_reference_id INTEGER NOT NULL,

            UNIQUE (
                dtc_code_id,
                source_reference_id
            ),

            FOREIGN KEY (dtc_code_id)
                REFERENCES dtc_codes(id)
                ON DELETE CASCADE,

            FOREIGN KEY (source_reference_id)
                REFERENCES source_references(id)
                ON DELETE CASCADE
        );

        /*
         * ============================================================
         * DTC KNOWLEDGE PROFILES
         * ============================================================
         */

        CREATE TABLE IF NOT EXISTS dtc_profiles
        (
            id INTEGER PRIMARY KEY AUTOINCREMENT,

            profile_key TEXT NOT NULL UNIQUE,

            dtc_code_id INTEGER NOT NULL,

            diagnostic_application_id INTEGER NULL,

            module_key TEXT NULL,

            title TEXT NULL,
            description TEXT NULL,

            flash_code TEXT NULL,

            mil_status TEXT NULL,

            condition_for_running TEXT NULL,
            condition_for_setting TEXT NULL,

            action_taken_when_set TEXT NULL,

            manufacturer_notes TEXT NULL,

            data_quality TEXT NULL,

            source_reference_id INTEGER NULL,
            diagnostic_source_reference_id INTEGER NULL,

            FOREIGN KEY (dtc_code_id)
                REFERENCES dtc_codes(id)
                ON DELETE CASCADE,

            FOREIGN KEY (diagnostic_application_id)
                REFERENCES diagnostic_applications(id)
                ON DELETE SET NULL,

            FOREIGN KEY (source_reference_id)
                REFERENCES source_references(id)
                ON DELETE SET NULL,

            FOREIGN KEY (diagnostic_source_reference_id)
                REFERENCES source_references(id)
                ON DELETE SET NULL
        );

        CREATE TABLE IF NOT EXISTS dtc_profile_conditions
        (
            id INTEGER PRIMARY KEY AUTOINCREMENT,

            dtc_profile_id INTEGER NOT NULL,

            sort_order INTEGER NOT NULL,

            condition_text TEXT NOT NULL,

            comparison_operator TEXT NULL,
            comparison_value REAL NULL,
            unit TEXT NULL,

            operating_condition_key TEXT NULL,

            FOREIGN KEY (dtc_profile_id)
                REFERENCES dtc_profiles(id)
                ON DELETE CASCADE
        );

        /*
         * Actual OEM possible/suspected causes.
         */
        CREATE TABLE IF NOT EXISTS dtc_causes
        (
            id INTEGER PRIMARY KEY AUTOINCREMENT,

            dtc_profile_id INTEGER NOT NULL,

            sort_order INTEGER NOT NULL,

            description TEXT NOT NULL,
            basis TEXT NULL,

            FOREIGN KEY (dtc_profile_id)
                REFERENCES dtc_profiles(id)
                ON DELETE CASCADE,

            UNIQUE (
                dtc_profile_id,
                sort_order
            )
        );

        /*
         * Recommendations DERIVED from OEM causes.
         *
         * They are intentionally separate from OEM diagnostic steps.
         */
        CREATE TABLE IF NOT EXISTS dtc_solution_recommendations
        (
            id INTEGER PRIMARY KEY AUTOINCREMENT,

            dtc_profile_id INTEGER NOT NULL,

            sort_order INTEGER NOT NULL,

            description TEXT NOT NULL,

            basis TEXT NULL,
            oem_verbatim INTEGER NOT NULL DEFAULT 0,

            derived_from_cause TEXT NULL,

            FOREIGN KEY (dtc_profile_id)
                REFERENCES dtc_profiles(id)
                ON DELETE CASCADE,

            UNIQUE (
                dtc_profile_id,
                sort_order
            )
        );

        CREATE TABLE IF NOT EXISTS dtc_ecu_actions
        (
            id INTEGER PRIMARY KEY AUTOINCREMENT,

            dtc_profile_id INTEGER NOT NULL,

            sort_order INTEGER NOT NULL,

            action_text TEXT NOT NULL,

            FOREIGN KEY (dtc_profile_id)
                REFERENCES dtc_profiles(id)
                ON DELETE CASCADE,

            UNIQUE (
                dtc_profile_id,
                sort_order
            )
        );

        CREATE TABLE IF NOT EXISTS dtc_relations
        (
            id INTEGER PRIMARY KEY AUTOINCREMENT,

            dtc_profile_id INTEGER NOT NULL,

            related_code TEXT NOT NULL COLLATE NOCASE,

            relation_type TEXT NOT NULL,

            UNIQUE (
                dtc_profile_id,
                related_code,
                relation_type
            ),

            FOREIGN KEY (dtc_profile_id)
                REFERENCES dtc_profiles(id)
                ON DELETE CASCADE
        );

        /*
         * REAL OEM diagnostic procedure.
         */
        CREATE TABLE IF NOT EXISTS dtc_procedure_steps
        (
            id INTEGER PRIMARY KEY AUTOINCREMENT,

            dtc_profile_id INTEGER NOT NULL,

            procedure_type TEXT NOT NULL,

            step_order INTEGER NOT NULL,

            instruction TEXT NOT NULL,

            UNIQUE (
                dtc_profile_id,
                procedure_type,
                step_order
            ),

            FOREIGN KEY (dtc_profile_id)
                REFERENCES dtc_profiles(id)
                ON DELETE CASCADE
        );

        /*
         * ============================================================
         * FLASH CODE / SPN / FMI
         * ============================================================
         */

        CREATE TABLE IF NOT EXISTS dtc_mappings
        (
            id INTEGER PRIMARY KEY AUTOINCREMENT,

            dtc_code_id INTEGER NOT NULL,

            flash_code TEXT NULL,

            spn INTEGER NULL,
            fmi INTEGER NULL,

            description TEXT NULL,

            source_reference_id INTEGER NULL,

            FOREIGN KEY (dtc_code_id)
                REFERENCES dtc_codes(id)
                ON DELETE CASCADE,

            FOREIGN KEY (source_reference_id)
                REFERENCES source_references(id)
                ON DELETE SET NULL
        );

        /*
         * ============================================================
         * LIVE DATA
         * ============================================================
         */

        CREATE TABLE IF NOT EXISTS live_parameters
        (
            id INTEGER PRIMARY KEY AUTOINCREMENT,

            parameter_key TEXT NOT NULL COLLATE NOCASE UNIQUE,

            display_name TEXT NOT NULL,

            quantity_type TEXT NULL,
            default_unit TEXT NULL
        );

        CREATE TABLE IF NOT EXISTS live_parameter_aliases
        (
            id INTEGER PRIMARY KEY AUTOINCREMENT,

            live_parameter_id INTEGER NOT NULL,

            alias TEXT NOT NULL,

            UNIQUE (
                live_parameter_id,
                alias
            ),

            FOREIGN KEY (live_parameter_id)
                REFERENCES live_parameters(id)
                ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS live_parameter_sources
        (
            id INTEGER PRIMARY KEY AUTOINCREMENT,

            live_parameter_id INTEGER NOT NULL,
            source_reference_id INTEGER NOT NULL,

            UNIQUE (
                live_parameter_id,
                source_reference_id
            ),

            FOREIGN KEY (live_parameter_id)
                REFERENCES live_parameters(id)
                ON DELETE CASCADE,

            FOREIGN KEY (source_reference_id)
                REFERENCES source_references(id)
                ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS live_parameter_fallback_behaviors
        (
            id INTEGER PRIMARY KEY AUTOINCREMENT,

            live_parameter_id INTEGER NOT NULL,
            diagnostic_application_id INTEGER NULL,

            behavior_text TEXT NOT NULL,

            FOREIGN KEY (live_parameter_id)
                REFERENCES live_parameters(id)
                ON DELETE CASCADE,

            FOREIGN KEY (diagnostic_application_id)
                REFERENCES diagnostic_applications(id)
                ON DELETE SET NULL
        );

        /*
         * ============================================================
         * OPERATING CONDITIONS / REFERENCE VALUES
         * ============================================================
         */

        CREATE TABLE IF NOT EXISTS operating_conditions
        (
            id INTEGER PRIMARY KEY AUTOINCREMENT,

            operating_condition_key TEXT NOT NULL,

            diagnostic_application_id INTEGER NOT NULL,

            name TEXT NOT NULL,
            description TEXT NULL,

            source_reference_id INTEGER NULL,

            UNIQUE (
                operating_condition_key,
                diagnostic_application_id
            ),

            FOREIGN KEY (diagnostic_application_id)
                REFERENCES diagnostic_applications(id)
                ON DELETE CASCADE,

            FOREIGN KEY (source_reference_id)
                REFERENCES source_references(id)
                ON DELETE SET NULL
        );

        CREATE TABLE IF NOT EXISTS live_reference_values
        (
            id INTEGER PRIMARY KEY AUTOINCREMENT,

            live_parameter_id INTEGER NOT NULL,

            diagnostic_application_id INTEGER NOT NULL,

            operating_condition_key TEXT NULL,
            operating_condition_name TEXT NULL,

            reference_kind TEXT NULL,

            comparison_operator TEXT NULL,

            minimum_value REAL NULL,
            maximum_value REAL NULL,
            nominal_value REAL NULL,

            text_value TEXT NULL,

            unit TEXT NULL,

            approximate INTEGER NOT NULL DEFAULT 0,

            axis_name TEXT NULL,
            axis_value REAL NULL,
            axis_unit TEXT NULL,

            notes TEXT NULL,

            source_reference_id INTEGER NULL,

            FOREIGN KEY (live_parameter_id)
                REFERENCES live_parameters(id)
                ON DELETE CASCADE,

            FOREIGN KEY (diagnostic_application_id)
                REFERENCES diagnostic_applications(id)
                ON DELETE CASCADE,

            FOREIGN KEY (source_reference_id)
                REFERENCES source_references(id)
                ON DELETE SET NULL
        );

        /*
         * ============================================================
         * DTC -> RELATED LIVE DATA
         * ============================================================
         */

        CREATE TABLE IF NOT EXISTS dtc_related_live_data
        (
            id INTEGER PRIMARY KEY AUTOINCREMENT,

            dtc_code_id INTEGER NOT NULL,

            live_parameter_id INTEGER NOT NULL,

            diagnostic_application_id INTEGER NULL,

            display_priority INTEGER NOT NULL DEFAULT 100,

            relation_role TEXT NULL,
            relation_basis TEXT NULL,

            reason TEXT NULL,

            source_reference_id INTEGER NULL,

            FOREIGN KEY (dtc_code_id)
                REFERENCES dtc_codes(id)
                ON DELETE CASCADE,

            FOREIGN KEY (live_parameter_id)
                REFERENCES live_parameters(id)
                ON DELETE CASCADE,

            FOREIGN KEY (diagnostic_application_id)
                REFERENCES diagnostic_applications(id)
                ON DELETE SET NULL,

            FOREIGN KEY (source_reference_id)
                REFERENCES source_references(id)
                ON DELETE SET NULL
        );

        /*
         * ============================================================
         * SERVICE / MECHANICAL SPECIFICATIONS
         * ============================================================
         */

        CREATE TABLE IF NOT EXISTS service_specifications
        (
            id INTEGER PRIMARY KEY AUTOINCREMENT,

            diagnostic_application_id INTEGER NOT NULL,

            category TEXT NOT NULL,
            specification_name TEXT NOT NULL,

            value_text TEXT NULL,
            unit TEXT NULL,

            condition_text TEXT NULL,
            notes TEXT NULL,

            source_reference_id INTEGER NULL,

            FOREIGN KEY (diagnostic_application_id)
                REFERENCES diagnostic_applications(id)
                ON DELETE CASCADE,

            FOREIGN KEY (source_reference_id)
                REFERENCES source_references(id)
                ON DELETE SET NULL
        );

        CREATE TABLE IF NOT EXISTS service_specification_values
        (
            id INTEGER PRIMARY KEY AUTOINCREMENT,

            service_specification_id INTEGER NOT NULL,

            sort_order INTEGER NOT NULL,

            value_text TEXT NULL,

            FOREIGN KEY (service_specification_id)
                REFERENCES service_specifications(id)
                ON DELETE CASCADE
        );

        /*
         * ============================================================
         * ACTUATOR TESTS / SPECIAL FUNCTIONS
         * ============================================================
         */

        CREATE TABLE IF NOT EXISTS actuator_tests
        (
            id INTEGER PRIMARY KEY AUTOINCREMENT,

            diagnostic_application_id INTEGER NOT NULL,

            module_key TEXT NULL,

            test_key TEXT NOT NULL,
            display_name TEXT NOT NULL,

            description TEXT NULL,

            source_reference_id INTEGER NULL,

            UNIQUE (
                diagnostic_application_id,
                test_key
            ),

            FOREIGN KEY (diagnostic_application_id)
                REFERENCES diagnostic_applications(id)
                ON DELETE CASCADE,

            FOREIGN KEY (source_reference_id)
                REFERENCES source_references(id)
                ON DELETE SET NULL
        );

        CREATE TABLE IF NOT EXISTS actuator_test_live_parameters
        (
            id INTEGER PRIMARY KEY AUTOINCREMENT,

            actuator_test_id INTEGER NOT NULL,
            parameter_key TEXT NOT NULL,

            UNIQUE (
                actuator_test_id,
                parameter_key
            ),

            FOREIGN KEY (actuator_test_id)
                REFERENCES actuator_tests(id)
                ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS special_functions
        (
            id INTEGER PRIMARY KEY AUTOINCREMENT,

            diagnostic_application_id INTEGER NOT NULL,

            module_key TEXT NULL,

            function_key TEXT NOT NULL,
            name TEXT NOT NULL,

            notes TEXT NULL,

            source_reference_id INTEGER NULL,

            UNIQUE (
                diagnostic_application_id,
                module_key,
                function_key
            ),

            FOREIGN KEY (diagnostic_application_id)
                REFERENCES diagnostic_applications(id)
                ON DELETE CASCADE,

            FOREIGN KEY (source_reference_id)
                REFERENCES source_references(id)
                ON DELETE SET NULL
        );

        /*
         * ============================================================
         * DTC LIFECYCLE / EURO VI FAULT CLASSES
         * ============================================================
         */

        CREATE TABLE IF NOT EXISTS dtc_lifecycle_rules
        (
            id INTEGER PRIMARY KEY AUTOINCREMENT,

            diagnostic_application_id INTEGER NOT NULL,

            fault_cycle_type INTEGER NOT NULL,

            emissions_related INTEGER NOT NULL,

            indicator TEXT NULL,

            source_reference_id INTEGER NULL,

            UNIQUE (
                diagnostic_application_id,
                fault_cycle_type
            ),

            FOREIGN KEY (diagnostic_application_id)
                REFERENCES diagnostic_applications(id)
                ON DELETE CASCADE,

            FOREIGN KEY (source_reference_id)
                REFERENCES source_references(id)
                ON DELETE SET NULL
        );

        CREATE TABLE IF NOT EXISTS dtc_lifecycle_behaviors
        (
            id INTEGER PRIMARY KEY AUTOINCREMENT,

            dtc_lifecycle_rule_id INTEGER NOT NULL,

            behavior_type TEXT NOT NULL,

            sort_order INTEGER NOT NULL,

            behavior_text TEXT NOT NULL,

            FOREIGN KEY (dtc_lifecycle_rule_id)
                REFERENCES dtc_lifecycle_rules(id)
                ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS fault_classes
        (
            id INTEGER PRIMARY KEY AUTOINCREMENT,

            diagnostic_application_id INTEGER NOT NULL,

            fault_class TEXT NOT NULL,

            description TEXT NOT NULL,

            source_reference_id INTEGER NULL,

            UNIQUE (
                diagnostic_application_id,
                fault_class
            ),

            FOREIGN KEY (diagnostic_application_id)
                REFERENCES diagnostic_applications(id)
                ON DELETE CASCADE,

            FOREIGN KEY (source_reference_id)
                REFERENCES source_references(id)
                ON DELETE SET NULL
        );

        /*
         * ============================================================
         * SYMPTOM TROUBLESHOOTING
         * ============================================================
         */

        CREATE TABLE IF NOT EXISTS symptom_troubleshooting
        (
            id INTEGER PRIMARY KEY AUTOINCREMENT,

            diagnostic_application_id INTEGER NOT NULL,

            condition_text TEXT NULL,

            possible_cause TEXT NOT NULL,
            correction TEXT NULL,

            source_reference_id INTEGER NULL,

            FOREIGN KEY (diagnostic_application_id)
                REFERENCES diagnostic_applications(id)
                ON DELETE CASCADE,

            FOREIGN KEY (source_reference_id)
                REFERENCES source_references(id)
                ON DELETE SET NULL
        );

        /*
         * ============================================================
         * INDEXES
         * ============================================================
         */

        CREATE INDEX IF NOT EXISTS idx_dtc_code
            ON dtc_codes(code);

        CREATE INDEX IF NOT EXISTS idx_dtc_profile_code
            ON dtc_profiles(dtc_code_id);

        CREATE INDEX IF NOT EXISTS idx_dtc_profile_application
            ON dtc_profiles(diagnostic_application_id);

        CREATE INDEX IF NOT EXISTS idx_dtc_mapping_flash
            ON dtc_mappings(flash_code);

        CREATE INDEX IF NOT EXISTS idx_dtc_mapping_spn_fmi
            ON dtc_mappings(spn, fmi);

        CREATE INDEX IF NOT EXISTS idx_dtc_causes_profile
            ON dtc_causes(dtc_profile_id);

        CREATE INDEX IF NOT EXISTS idx_dtc_procedure_profile
            ON dtc_procedure_steps(
                dtc_profile_id,
                procedure_type,
                step_order
            );

        CREATE INDEX IF NOT EXISTS idx_live_parameter_key
            ON live_parameters(parameter_key);

        CREATE INDEX IF NOT EXISTS idx_live_reference_lookup
            ON live_reference_values(
                live_parameter_id,
                diagnostic_application_id,
                operating_condition_key
            );

        CREATE INDEX IF NOT EXISTS idx_dtc_related_live
            ON dtc_related_live_data(
                dtc_code_id,
                display_priority
            );

        INSERT OR REPLACE INTO kb_metadata(key, value)
        VALUES ('schema_version', '1');
        """;

        command.ExecuteNonQuery();
    }
}