
namespace IsuzuDiagnostic.Desktop.Diagnostics;

public sealed class LiveDiagnosticContext
{
    public string? OperatingConditionKey { get; private set; }

    public void SetOperatingCondition(string? operatingConditionKey)
    {
        OperatingConditionKey = string.IsNullOrWhiteSpace(operatingConditionKey) ? null : operatingConditionKey.Trim();
    }

    public void ClearOperatingCondition()
    {
        OperatingConditionKey = null;
    }
}