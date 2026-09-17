using IsuzuDiagnostic.Desktop.Models;

namespace IsuzuDiagnostic.Desktop.Diagnostics;

public static class LiveDiagnosticApplicationResolver
{
    public static string? Resolve(VehicleProfile vehicle)
    {
        ArgumentNullException.ThrowIfNull(vehicle);

        if (string.Equals(vehicle.EngineCode, "4HK1", StringComparison.OrdinalIgnoreCase))
        {
            return "ISUZU_4HK1_TM_REFERENCE";
        }

        return null;
    }
}
