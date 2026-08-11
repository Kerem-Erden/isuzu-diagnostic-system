using System;

namespace IsuzuDiagnostic.Desktop.Models
{
    public sealed class DtcSolution
    {
        public string Description { get; }

        public DtcSolution(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                throw new ArgumentNullException("DTC solution description cannot be empty.", nameof(description));
            }

            Description = description;
        }
    }
}
