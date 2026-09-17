
namespace IsuzuDiagnostic.Desktop.Diagnostics;

public static class LiveReferenceSelector
{
    
    public static LiveReferenceValue? Select (IReadOnlyList<LiveReferenceValue> references, string operatingConditionKey)
    {
        ArgumentNullException.ThrowIfNull(references);

        if (string.IsNullOrWhiteSpace(operatingConditionKey))
        {
            throw new ArgumentException("Operating condition key cannot be empty.", nameof(operatingConditionKey));
        }

        return references.FirstOrDefault(
            reference =>
                string.Equals(reference.OperatingConditionKey, operatingConditionKey, StringComparison.OrdinalIgnoreCase));
    }

    public static LiveReferenceValue? Select(IReadOnlyList<LiveReferenceValue> references, LiveDiagnosticContext context)
    {
        ArgumentNullException.ThrowIfNull(references);
        ArgumentNullException.ThrowIfNull(context);

        if (references.Count == 0)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(context.OperatingConditionKey))
        {
            return references.Count == 1 ? references[0] : null;
        }

        return Select(references, context.OperatingConditionKey);
    }
}