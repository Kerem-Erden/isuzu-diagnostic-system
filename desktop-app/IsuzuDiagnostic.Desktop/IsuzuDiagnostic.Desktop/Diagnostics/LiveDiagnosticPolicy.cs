
namespace IsuzuDiagnostic.Desktop.Diagnostics;

public sealed class LiveDiagnosticPolicy
{
    public string ParameterKey { get; }
    
    public double WarningMargin { get; }
    public double CriticalMargin { get; }

    public double Hysteresis { get; }

    public TimeSpan ConfirmationDuration { get; }

    public LiveDiagnosticPolicy(string parameterKey, double warningMargin, double criticalMargin, double hysteresis, TimeSpan confirmationDuration)
    {
        if (string.IsNullOrWhiteSpace(parameterKey))
        {
            throw new ArgumentException("Parameter key cannot be empty.", nameof(parameterKey));
        }

        if (warningMargin < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(warningMargin));
        }

        if (criticalMargin < warningMargin)
            throw new ArgumentException("Critical margin must be greater than or equal to warning margin.", nameof(criticalMargin));
        
        
        ParameterKey = parameterKey.Trim();
        WarningMargin = warningMargin;
        CriticalMargin = criticalMargin;
        Hysteresis = hysteresis;
        ConfirmationDuration = confirmationDuration;
    }
}