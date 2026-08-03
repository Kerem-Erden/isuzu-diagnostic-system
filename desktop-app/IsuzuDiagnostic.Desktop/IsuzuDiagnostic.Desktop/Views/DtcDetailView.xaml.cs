using System;
using System.Windows;
using System.Windows.Controls;

namespace IsuzuDiagnostic.Desktop.Views;

public partial class DtcDetailView : UserControl
{
    public event EventHandler? BackRequested;

    public DtcDetailView()
    {
        InitializeComponent();
    }

    public DtcDetailView(string dtcCode)
        : this()
    {
        LoadMockDtc(dtcCode);
    }

    private void LoadMockDtc(string dtcCode)
    {
        DtcCodeTextBlock.Text = dtcCode;

        switch (dtcCode)
        {
            case "P003A":
                DtcDescriptionTextBlock.Text =
                    "Turbocharger boost control position exceeded learning limit";

                DtcStatusTextBlock.Text =
                    "Active";

                PossibleCausesTextBlock.Text =
                    "• VGT actuator malfunction\n" +
                    "• Turbocharger mechanism sticking\n" +
                    "• Wiring or connector fault\n" +
                    "• Adaptation limit exceeded";

                RelatedLiveDataTextBlock.Text =
                    "• Desired turbo position\n" +
                    "• Actual turbo position\n" +
                    "• Boost pressure\n" +
                    "• Engine RPM\n" +
                    "• Engine load";

                break;

            case "P2080":
                DtcDescriptionTextBlock.Text =
                    "Exhaust gas temperature sensor circuit range/performance";

                DtcStatusTextBlock.Text =
                    "Stored";

                PossibleCausesTextBlock.Text =
                    "• Exhaust gas temperature sensor fault\n" +
                    "• Wiring or connector problem\n" +
                    "• Implausible sensor reading\n" +
                    "• Exhaust temperature outside expected range";

                RelatedLiveDataTextBlock.Text =
                    "• Exhaust gas temperature\n" +
                    "• Engine RPM\n" +
                    "• Engine load\n" +
                    "• Coolant temperature";

                break;

            default:
                DtcDescriptionTextBlock.Text =
                    "Diagnostic information is not available.";

                DtcStatusTextBlock.Text =
                    "Unknown";

                PossibleCausesTextBlock.Text =
                    "No verified cause information is available.";

                RelatedLiveDataTextBlock.Text =
                    "No related live-data mapping is available.";

                break;
        }
    }

    private void BackButton_Click(
        object sender,
        RoutedEventArgs e
    )
    {
        BackRequested?.Invoke(
            this,
            EventArgs.Empty
        );
    }
}