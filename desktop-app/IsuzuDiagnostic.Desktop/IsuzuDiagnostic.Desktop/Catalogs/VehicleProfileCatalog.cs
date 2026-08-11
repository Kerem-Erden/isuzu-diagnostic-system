using System;
using System.Collections.Generic;
using System.Windows.Controls.Primitives;

namespace IsuzuDiagnostic.Desktop.Catalogs;

public static class VehicleProfileCatolog
{
    public const string Manufacturer = "Isuzu";

    /*
     * These are temporary prototype options.
     * They do not yet represent a verified model-engine-year
     * compatibility matrix.
     */

    public static IReadOnlyList<string> Models { get; } = Array.AsReadOnly(new[]
    {
        "N-Series",
        "F-Series",
        "D-Max"
    });

    public static IReadOnlyList<int> ModelYears { get; } = Array.AsReadOnly(new[]
    {
        2012,
        2013,
        2014,
        2015,
        2016,
        2017,
        2018,
        2019,
        2020,
        2021,
        2022,
        2023,
        2024,
        2025
    });

    public static IReadOnlyList<string> EngineCodes { get; } = Array.AsReadOnly(new[]
    {
        "4JJ1-TC",
        "4HK1-TC",
        "4JZ1-TC",
    });

    public static IReadOnlyList<string> EcuTypes { get; } = Array.AsReadOnly(new[]
    {
        "ECU-1",
        "ECU-2",
        "ECU-3"
    });
}
