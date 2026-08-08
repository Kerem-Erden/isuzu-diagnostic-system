using System;
using System.Runtime.InteropServices;

namespace IsuzuDiagnostic.Desktop.Communication.Protocol
{
    public static class GatewayLineRouter
    {
        public static GatewayLineType Classify(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return GatewayLineType.Unknown;
            }

            if (line.StartsWith("RES|",StringComparison.Ordinal))
            {
                return GatewayLineType.Response;
            }

            if (line.StartsWith("LIVE:", StringComparison.Ordinal))
            {
                return GatewayLineType.LiveData;
            }

            if (line.StartsWith("SYS:", StringComparison.Ordinal))
            {
                return GatewayLineType.System;
            }

            return GatewayLineType.Unknown;
        }
    }
}
