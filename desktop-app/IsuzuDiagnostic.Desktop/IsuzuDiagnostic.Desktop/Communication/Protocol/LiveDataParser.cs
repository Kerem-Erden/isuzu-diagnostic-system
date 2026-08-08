using System;
using System.Globalization;

namespace IsuzuDiagnostic.Desktop.Communication.Protocol
{
    public static class LiveDataParser
    {
        public static bool TryParse(string line, out LiveDataMessage? message)
        {
            message = null;

            if (string.IsNullOrWhiteSpace(line))
            {
                return false;
            }

            if (!line.StartsWith("LIVE:",StringComparison.Ordinal))
            {
                return false;
            }

            string[] fields = line.Split(':');

            if (fields.Length !=3)
            {
                return false;
            }

            string parameter = fields[1];

            if (string.IsNullOrWhiteSpace(parameter))
            {
                return false;
            }

            if (!double.TryParse(fields[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            {
                return false;
            }

            message = new LiveDataMessage(parameter, value);

            return true;
        }
    }
}
