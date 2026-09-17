
namespace IsuzuDiagnostic.Desktop.Diagnostics;

public static class LiveReferenceRuleFactory
{
    public static LiveReferenceRule CreateFromRange(
        LiveReferenceValue reference,
        double warningMargin,
        double criticalMargin,
        double hysteresis,
        TimeSpan confirmationDuration)
    {
        ArgumentNullException.ThrowIfNull(reference);

        if (!reference.MinimumValue.HasValue || !reference.MaximumValue.HasValue)
        {
            throw new InvalidOperationException("Reference must contain minimum and maximum values.");
        }

        if (warningMargin < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(warningMargin));
        }

        if (criticalMargin < warningMargin)
        {
            throw new ArgumentException("Critical margin must be greater than or equal to warning margin.", nameof(criticalMargin));
        }

        double minimum = reference.MinimumValue.Value;
        double maximum = reference.MaximumValue.Value;

        return new LiveReferenceRule(
            parameterKey: reference.ParameterKey,

            warningMinimum: minimum - warningMargin,
            warningMaximum: maximum + warningMargin,

            criticalMinimum: minimum - criticalMargin,
            criticalMaximum: maximum + criticalMargin,

            hysteresis: hysteresis,
            confirmationDuration: confirmationDuration);
    }
}