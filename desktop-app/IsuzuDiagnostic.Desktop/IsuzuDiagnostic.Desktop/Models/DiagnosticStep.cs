using System;

namespace IsuzuDiagnostic.Desktop.Models
{
    public sealed class DiagnosticStep
    {
        public int Order { get; }

        public string Description { get; }

        public DiagnosticStep(int order, string description)
        {
            if (order <= 0 )
            {
                throw new ArgumentOutOfRangeException(nameof(order), "Diagnostic step order must be greater than zero.");
            }

            if (string.IsNullOrWhiteSpace(description))
            {
                throw new ArgumentException("Diagnostic step description cannot be empty.", nameof(description));
            }

            Order = order;
            Description = description;
        }
    }
}
