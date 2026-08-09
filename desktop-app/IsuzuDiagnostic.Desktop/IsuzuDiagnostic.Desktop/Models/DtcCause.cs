using System;

namespace IsuzuDiagnostic.Desktop.Models
{
    public sealed class DtcCause
    {
        public string Description { get; }

        public DtcCause(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                throw new ArgumentException("DTC cause description cannot be empty.", nameof(description));
            }

            Description = description;
        }
    }
}
