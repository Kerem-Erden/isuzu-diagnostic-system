using Microsoft.Data.Sqlite;
using KnowledgeBaseImporter;

static string? FindRepoRoot(string startDirectory)
{
    DirectoryInfo? current = new DirectoryInfo(startDirectory);

    while (current is not null)
    {
        string candidate = Path.Combine(
            current.FullName,
            "Data",
            "knowledge-base",
            "dtcs.json"
        );

        if (File.Exists(candidate))
        {
            return current.FullName;
        }

        current = current.Parent;
    }

    return null;
}

string? repoRoot =
    FindRepoRoot(Directory.GetCurrentDirectory())
    ?? FindRepoRoot(AppContext.BaseDirectory);

if (repoRoot is null)
{
    Console.Error.WriteLine(
        "ERROR: Could not locate the repository root. " +
        "Expected Data/knowledge-base/dtcs.json."
    );

    return 1;
}

string knowledgeDirectory = Path.Combine(
    repoRoot,
    "Data",
    "knowledge-base"
);

string databasePath = Path.Combine(
    knowledgeDirectory,
    "isuzu-knowledge.db"
);

Console.WriteLine($"Repository: {repoRoot}");
Console.WriteLine($"Knowledge data: {knowledgeDirectory}");
Console.WriteLine($"Output database: {databasePath}");
Console.WriteLine();

/*
 * This database is generated from the versioned JSON knowledge data.
 * Rebuilding it must never touch isuzu-diagnostic.db, which contains
 * user/session data.
 */
if (File.Exists(databasePath))
{
    File.Delete(databasePath);
    Console.WriteLine("Removed previous generated isuzu-knowledge.db.");
}

using SqliteConnection connection =
    new($"Data Source={databasePath}");

connection.Open();

DatabaseSchema.Create(connection);

KnowledgeImporter importer = new(
    connection,
    knowledgeDirectory
);

ImportSummary core = importer.ImportCoreDtcKnowledge();
ExtendedImportSummary extended = importer.ImportExtendedKnowledge();

Console.WriteLine();
Console.WriteLine("CORE DTC IMPORT COMPLETE");
Console.WriteLine($"Sources:                 {core.SourceDocuments}");
Console.WriteLine($"Applications:            {core.Applications}");
Console.WriteLine($"DTC codes:               {core.DtcCodes}");
Console.WriteLine($"DTC profiles:            {core.DtcProfiles}");
Console.WriteLine($"Possible causes:          {core.Causes}");
Console.WriteLine($"Solution suggestions:     {core.Solutions}");
Console.WriteLine($"OEM diagnostic steps:     {core.DiagnosticSteps}");
Console.WriteLine($"OEM confirmation steps:   {core.ConfirmationSteps}");
Console.WriteLine($"DTC mappings:             {core.Mappings}");

Console.WriteLine();
Console.WriteLine("EXTENDED KNOWLEDGE IMPORT COMPLETE");
Console.WriteLine($"Control modules:          {extended.ControlModules}");
Console.WriteLine($"Vehicle networks:         {extended.Networks}");
Console.WriteLine($"Network ECU pins:         {extended.NetworkEcuPins}");
Console.WriteLine($"Control-module pins:      {extended.ControlModulePins}");
Console.WriteLine($"Live parameters:          {extended.LiveParameters}");
Console.WriteLine($"Live parameter aliases:   {extended.LiveParameterAliases}");
Console.WriteLine($"Live parameter sources:   {extended.LiveParameterSources}");
Console.WriteLine($"Live fallback behaviors:  {extended.LiveParameterFallbacks}");
Console.WriteLine($"Operating conditions:     {extended.OperatingConditions}");
Console.WriteLine($"Live reference values:    {extended.LiveReferenceValues}");
Console.WriteLine($"DTC/live-data links:      {extended.DtcRelatedLiveData}");
Console.WriteLine($"Service specifications:   {extended.ServiceSpecifications}");
Console.WriteLine($"Service spec values:      {extended.ServiceSpecificationValues}");
Console.WriteLine($"Actuator tests:            {extended.ActuatorTests}");
Console.WriteLine($"Actuator/live links:       {extended.ActuatorLiveParameters}");
Console.WriteLine($"Special functions:         {extended.SpecialFunctions}");
Console.WriteLine($"DTC lifecycle rules:       {extended.DtcLifecycleRules}");
Console.WriteLine($"Lifecycle behaviors:       {extended.DtcLifecycleBehaviors}");
Console.WriteLine($"Fault classes:             {extended.FaultClasses}");
Console.WriteLine($"Symptom troubleshooting:   {extended.SymptomTroubleshooting}");

Console.WriteLine();
Console.WriteLine("FULL KNOWLEDGE DATABASE GENERATED SUCCESSFULLY.");
Console.WriteLine(databasePath);

return 0;
