using System;
using System.Collections.Generic;
using IsuzuDiagnostic.Desktop.Models;

namespace IsuzuDiagnostic.Desktop.Catalogs
{
    public sealed class MockDtcCatalog
    {
        public static IReadOnlyList<DiagnosticTroubleCode> Items { get; } = Array.AsReadOnly(new[]
        {
            new DiagnosticTroubleCode(
                code: "P003A",
                description: "Turbocharger boost control position exceeded learning limit",
                status: "Active",
                possibleCauses: new[]
                {
                     new DtcCause("VGT actuator malfunction"),
                     new DtcCause("Turbocharger mechanism sticking"),
                     new DtcCause("Wiring or connector fault"),
                     new DtcCause("Adaptation limit exceeded")
                },
                diagnosticSteps: new[]
                {
                    new DiagnosticStep(1, "Inspect the turbocharger actuator and connector."),
                    new DiagnosticStep(2, "Check the turbocharger mechanism for sticking." ),
                    new DiagnosticStep(3, "Compare desired and actual turbocharger position." ),
                    new DiagnosticStep(4, "Inspect wiring before replacing components." )
                },
                relatedLiveData: new[]
                {
                    new RelatedLiveDataItem("TURBO_DESIRED_POSITION", "Desired Turbo Position", "%"),
                    new RelatedLiveDataItem("TURBO_ACTUAL_POSITION", "Actual Turbo Position", "%"),
                    new RelatedLiveDataItem("BOOST_PRESSURE", "Boost Pressure", "kPa"),
                    new RelatedLiveDataItem("RPM", "Engine RPM", "rpm")
                }),

            new DiagnosticTroubleCode(
                    code: "P2080",
                    description:
                        "Exhaust gas temperature sensor circuit range/performance",
                    status: "Stored",
                    possibleCauses: new[]
                    {
                        new DtcCause("Exhaust gas temperature sensor fault"),
                        new DtcCause("Wiring or connector problem"),
                        new DtcCause("Implausible sensor reading")
                    },
                    diagnosticSteps: new[]
                    {
                        new DiagnosticStep(1, "Inspect the exhaust gas temperature sensor connector."),
                        new DiagnosticStep(2, "Check sensor wiring for open or short circuits."),
                        new DiagnosticStep(3, "Compare the reported temperature with expected conditions.")
                    },
                    relatedLiveData: new[]
                    {
                        new RelatedLiveDataItem("EGT", "Exhaust Gas Temperature", "°C" ),
                        new RelatedLiveDataItem("RPM", "Engine RPM", "rpm"),
                        new RelatedLiveDataItem("COOLANT_TEMP", "Coolant Temperature", "°C" )
                    }
                )
        });

        public static DiagnosticTroubleCode? FindByCode(string code)
        {
            foreach (DiagnosticTroubleCode dtc in Items)
            {
                if (string.Equals(dtc.Code, code, StringComparison.OrdinalIgnoreCase))
                {
                    return dtc;
                }
            }

            return null;
        }
    }
}
