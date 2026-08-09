using System;

namespace IsuzuDiagnostic.Desktop.Models
{
    public sealed class RelatedLiveDataItem
    {
        public string Parameter { get; }

        public string DisplayName { get; }

        public string Unit { get; }

        public RelatedLiveDataItem(string parameter, string displayName, string unit)
        {
            if (string.IsNullOrWhiteSpace(parameter))
            {
                throw new ArgumentException("Live-data parameter cannot be empty.", nameof(parameter));
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException("Live-data display name cannot be empty.", nameof(displayName));
            }

            Parameter = parameter;
            DisplayName = displayName;
            Unit = unit ?? string.Empty;
        }
    }
}
