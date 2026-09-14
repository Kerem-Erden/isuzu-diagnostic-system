using System;
using System.Collections.Generic;

namespace IsuzuDiagnostic.Desktop.Models
{
    public sealed class DiagnosticTroubleCode
    {
        public string Code { get; }

        // Short canonical title shown in lists and the detail header.
        public string Description { get; }

        // Longer OEM explanation, kept separate so it does not dominate the UI.
        public string DetailedDescription { get; }

        public string Status { get; }

        public IReadOnlyList<DtcCause> PossibleCauses { get; }

        public IReadOnlyList<DiagnosticStep> DiagnosticSteps { get; }

        public IReadOnlyList<DiagnosticStep> ConfirmationSteps { get; }

        public IReadOnlyList<RelatedLiveDataItem> RelatedLiveData { get; }

        public IReadOnlyList<DtcSolution> PossibleSolutions { get; }

        public DiagnosticTroubleCode(
            string code,
            string description,
            string status,
            IReadOnlyList<DtcCause> possibleCauses,
            IReadOnlyList<DiagnosticStep> diagnosticSteps,
            IReadOnlyList<RelatedLiveDataItem> relatedLiveData,
            IReadOnlyList<DtcSolution> possibleSolutions,
            IReadOnlyList<DiagnosticStep>? confirmationSteps = null,
            string? detailedDescription = null)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ArgumentException(
                    "DTC code cannot be empty.",
                    nameof(code));
            }

            if (string.IsNullOrWhiteSpace(description))
            {
                throw new ArgumentException(
                    "DTC description cannot be empty.",
                    nameof(description));
            }

            if (string.IsNullOrWhiteSpace(status))
            {
                throw new ArgumentException(
                    "DTC status cannot be empty.",
                    nameof(status));
            }

            Code = code;
            Description = description;
            DetailedDescription = detailedDescription ?? string.Empty;
            Status = status;

            PossibleCauses =
                possibleCauses
                ?? throw new ArgumentNullException(nameof(possibleCauses));

            DiagnosticSteps =
                diagnosticSteps
                ?? throw new ArgumentNullException(nameof(diagnosticSteps));

            ConfirmationSteps =
                confirmationSteps ?? Array.Empty<DiagnosticStep>();

            RelatedLiveData =
                relatedLiveData
                ?? throw new ArgumentNullException(nameof(relatedLiveData));

            PossibleSolutions =
                possibleSolutions
                ?? throw new ArgumentNullException(nameof(possibleSolutions));
        }
    }
}
