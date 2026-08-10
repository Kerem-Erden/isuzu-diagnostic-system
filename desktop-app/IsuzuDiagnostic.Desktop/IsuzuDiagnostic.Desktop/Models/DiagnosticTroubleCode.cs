using System;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace IsuzuDiagnostic.Desktop.Models
{
    public sealed class DiagnosticTroubleCode
    {
        public string Code { get; }

        public string Description { get; }

        public string Status { get; }

        public IReadOnlyList<DtcCause> PossibleCauses { get; }

        public IReadOnlyList<DiagnosticStep> DiagnosticSteps { get; }

        public IReadOnlyList<RelatedLiveDataItem> RelatedLiveData { get; }

        public IReadOnlyList<DtcSolution> PossibleSolutions { get; }


        public DiagnosticTroubleCode(string code, string description, string status, IReadOnlyList<DtcCause> possibleCauses, IReadOnlyList<DiagnosticStep> diagnosticSteps, IReadOnlyList<RelatedLiveDataItem> relatedLiveData, IReadOnlyList<DtcSolution> possibleSolutions)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ArgumentException("DTC code cannot be empty.", nameof(code));
            }

            if (string.IsNullOrWhiteSpace(description))
            {
                throw new ArgumentException("DTC description cannot be empty.", nameof(description));
            }

            if (string.IsNullOrWhiteSpace(status))
            {
                throw new ArgumentException("DTC status cannot be empty", nameof(status));
            }


            Code = code;
            Description = description;
            Status = status;

            PossibleCauses = possibleCauses ?? throw new ArgumentNullException(nameof(possibleCauses));
            DiagnosticSteps = diagnosticSteps ?? throw new ArgumentNullException(nameof(diagnosticSteps));
            RelatedLiveData = relatedLiveData ?? throw new ArgumentNullException(nameof(relatedLiveData));
            PossibleSolutions = possibleSolutions ?? throw new ArgumentNullException(nameof(possibleSolutions));
        }
    }
}
