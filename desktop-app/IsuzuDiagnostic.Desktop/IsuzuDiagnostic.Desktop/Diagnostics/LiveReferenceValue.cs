
namespace IsuzuDiagnostic.Desktop.Diagnostics;

public sealed class LiveReferenceValue
{
    public string ParameterKey { get; init; } = string.Empty;
    public string ApplicationKey { get; init; } = string.Empty;

    public string? OperatingConditionKey { get; init; }
    public string? OperatingConditionName { get; init; }

    public string? ReferenceKind { get; init; }
    public string? ComparisonOperator { get; init; }

    public double? MinimumValue { get; init; }
    public double? MaximumValue { get; init; }
    public double? NominalValue { get; init; }
    
    public string? TextValue { get; init; }
    public string? Unit { get; init; }

    public bool Approximate { get; init; }

    public string? Notes { get; init; }
}
