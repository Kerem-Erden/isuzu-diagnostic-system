namespace IsuzuDiagnostic.Desktop.Models;

public sealed class DtcKnowledgeDetails
{
    public string Code { get; init; } = "";
    public string Title { get; init; } = "";
    public string? System { get; init; }

    public string? Description { get; init; }
    public string? ApplicationKey { get; init; }

    public IReadOnlyList<string> PossibleCauses { get; init; } =
        Array.Empty<string>();

    public IReadOnlyList<string> SolutionRecommendations { get; init; } =
        Array.Empty<string>();

    public IReadOnlyList<string> DiagnosticSteps { get; init; } =
        Array.Empty<string>();

    public IReadOnlyList<string> ConfirmationSteps { get; init; } =
        Array.Empty<string>();

    public IReadOnlyList<DtcRelatedLiveParameter> RelatedLiveData { get; init; } =
        Array.Empty<DtcRelatedLiveParameter>();
}

public sealed class DtcRelatedLiveParameter
{
    public string ParameterKey { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string? Unit { get; init; }

    public string? Role { get; init; }
    public string? Reason { get; init; }
}