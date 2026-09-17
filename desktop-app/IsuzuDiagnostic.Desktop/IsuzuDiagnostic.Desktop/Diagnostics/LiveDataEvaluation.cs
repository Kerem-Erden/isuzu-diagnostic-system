
namespace IsuzuDiagnostic.Desktop.Diagnostics;

public sealed class LiveDataEvaluation
{
    public string ParameterKey { get; }
    public double Value { get; }
    public DiagnosticSeverity Severity { get; }
    public DiagnosticSeverity PreviousSeverity { get; }
    public bool StateChanged { get; }
    public DateTimeOffset Timestamp { get; }

    public LiveDataEvaluation(string parameterKey, double value, DiagnosticSeverity severity, DiagnosticSeverity previousSeverity, bool stateChanged, DateTimeOffset timestamp)
    {
        ParameterKey = parameterKey;
        Value = value;
        Severity = severity;
        PreviousSeverity = previousSeverity;
        StateChanged = stateChanged;
        Timestamp = timestamp;
    }
}