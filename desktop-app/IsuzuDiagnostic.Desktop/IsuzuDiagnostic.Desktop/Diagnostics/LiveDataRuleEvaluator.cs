
namespace IsuzuDiagnostic.Desktop.Diagnostics;

public sealed class LiveDataRuleEvaluator
{
    private sealed class ParameterState
    {
        public DateTimeOffset? LastSampleAt { get; set; }

        public DiagnosticSeverity CurrentSeverity { get; set; } = DiagnosticSeverity.Normal;

        public DiagnosticSeverity? CandidateSeverity { get; set; }

        public DateTimeOffset? CandidateSince { get; set; }
    }

    private readonly Dictionary<string, ParameterState> _states = new(StringComparer.OrdinalIgnoreCase);

    private readonly object _syncRoot = new();

    public LiveDataEvaluation Evaluate(LiveReferenceRule rule, double value, DateTimeOffset timestamp)
    {
        ArgumentNullException.ThrowIfNull(rule);
        if (!double.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(value));

        lock (_syncRoot)
        {
            ParameterState state = GetOrCreateState(rule.ParameterKey);

            if (state.LastSampleAt is { } last && (timestamp < last || timestamp - last > TimeSpan.FromSeconds(2)))
            { state.CandidateSeverity = null; state.CandidateSince = null; }
            state.LastSampleAt = timestamp;
            DiagnosticSeverity previousSeverity = state.CurrentSeverity;

            DiagnosticSeverity detectedSeverity = DetermineSeverity(rule, value, state.CurrentSeverity);

            // Nothing changed. Any pending transition becomes invalid.
            if (detectedSeverity == state.CurrentSeverity)
            {
                state.CandidateSeverity = null;
                state.CandidateSince = null;

                return new LiveDataEvaluation(rule.ParameterKey, value, state.CurrentSeverity, previousSeverity, stateChanged: false, timestamp);
            }

            // A different candidate appeared.
            // Start its confirmation timer.
            if (state.CandidateSeverity != detectedSeverity)
            {
                state.CandidateSeverity = detectedSeverity;
                state.CandidateSince = timestamp;

                if (rule.ConfirmationDuration > TimeSpan.Zero)
                {
                    return new LiveDataEvaluation(rule.ParameterKey, value, state.CurrentSeverity, previousSeverity, stateChanged: false, timestamp);
                }
            }

            DateTimeOffset candidateSince = state.CandidateSince ?? timestamp;

            TimeSpan candidateDuration = timestamp - candidateSince;

            if (candidateDuration < rule.ConfirmationDuration)
            {
                return new LiveDataEvaluation(rule.ParameterKey, value, state.CurrentSeverity, previousSeverity, stateChanged: false, timestamp);
            }

            state.CurrentSeverity = detectedSeverity;

            state.CandidateSeverity = null;
            state.CandidateSince = null;

            return new LiveDataEvaluation(rule.ParameterKey, value, state.CurrentSeverity, previousSeverity, stateChanged: previousSeverity != state.CurrentSeverity, timestamp);
        }
    }

    public void Reset()
    {
        lock (_syncRoot)
        {
            _states.Clear();
        }
    }

    private ParameterState GetOrCreateState(string parameterKey)
    {
        if (_states.TryGetValue(parameterKey, out ParameterState? existing))
        {
            return existing;
        }

        ParameterState created = new();

        _states.Add(parameterKey, created);

        return created;
    }

    private static DiagnosticSeverity DetermineSeverity(LiveReferenceRule rule, double value, DiagnosticSeverity currentSeverity)
    {
        if (IsCritical(rule, value, currentSeverity))
        {
            return DiagnosticSeverity.Critical;
        }

        if (IsWarning(rule, value, currentSeverity))
        {
            return DiagnosticSeverity.Warning;
        }

        return DiagnosticSeverity.Normal;
    }

    private static bool IsCritical(LiveReferenceRule rule, double value, DiagnosticSeverity currentSeverity)
    {
        if (rule.CriticalMinimum.HasValue)
        {
            double minimum = rule.CriticalMinimum.Value;

            // While already critical on the low side,
            // require additional recovery before leaving CRITICAL.
            if (currentSeverity == DiagnosticSeverity.Critical && value < minimum + rule.Hysteresis)
            {
                return true;
            }

            if (value < minimum)
            {
                return true;
            }
        }

        if (rule.CriticalMaximum.HasValue)
        {
            double maximum = rule.CriticalMaximum.Value;

            if (currentSeverity == DiagnosticSeverity.Critical && value > maximum - rule.Hysteresis)
            {
                return true;
            }

            if (value > maximum)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsWarning(LiveReferenceRule rule, double value, DiagnosticSeverity currentSeverity)
    {
        if (rule.WarningMinimum.HasValue)
        {
            double minimum = rule.WarningMinimum.Value;

            if (currentSeverity == DiagnosticSeverity.Warning && value < minimum + rule.Hysteresis)
            {
                return true;
            }

            if (value < minimum)
            {
                return true;
            }
        }

        if (rule.WarningMaximum.HasValue)
        {
            double maximum = rule.WarningMaximum.Value;

            if (currentSeverity == DiagnosticSeverity.Warning && value > maximum - rule.Hysteresis)
            {
                return true;
            }

            if (value > maximum)
            {
                return true;
            }
        }

        return false;
    }

}
