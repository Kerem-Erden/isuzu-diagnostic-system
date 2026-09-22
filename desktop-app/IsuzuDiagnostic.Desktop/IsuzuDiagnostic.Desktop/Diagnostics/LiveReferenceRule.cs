
namespace IsuzuDiagnostic.Desktop.Diagnostics;

public sealed class LiveReferenceRule
{
    public string ParameterKey { get; }

    public double? WarningMinimum { get; }
    public double? WarningMaximum { get; }

    public double? CriticalMinimum { get; }
    public double? CriticalMaximum { get; }

    public double Hysteresis { get; }

    public TimeSpan ConfirmationDuration { get; }

    public LiveReferenceRule(string parameterKey, double? warningMinimum, double? warningMaximum, double? criticalMinimum, double? criticalMaximum, double hysteresis, TimeSpan confirmationDuration)
    {
        if (string.IsNullOrWhiteSpace(parameterKey))
        {
            throw new ArgumentException("Parameter key cannot be empty.", nameof(parameterKey));
        }

        if (!double.IsFinite(hysteresis) || hysteresis < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(hysteresis));
        }

        if (confirmationDuration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(confirmationDuration));
        }

        double?[] limits = [warningMinimum, warningMaximum, criticalMinimum, criticalMaximum];
        if (limits.Any(v => v.HasValue && !double.IsFinite(v.Value)) ||
            warningMinimum > warningMaximum || criticalMinimum > criticalMaximum ||
            criticalMinimum > warningMinimum || criticalMaximum < warningMaximum)
            throw new ArgumentException("Invalid diagnostic threshold ordering.");
        ParameterKey = parameterKey.Trim();

        WarningMinimum = warningMinimum;
        WarningMaximum = warningMaximum;

        CriticalMinimum = criticalMinimum;
        CriticalMaximum = criticalMaximum;

        Hysteresis = hysteresis;
        ConfirmationDuration = confirmationDuration;
    }
}
