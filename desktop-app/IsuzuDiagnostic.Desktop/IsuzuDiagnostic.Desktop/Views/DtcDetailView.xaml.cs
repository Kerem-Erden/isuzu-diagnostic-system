using IsuzuDiagnostic.Desktop.Models;
using System;
using System.Windows;
using System.Windows.Controls;

using IsuzuDiagnostic.Desktop.Models;

namespace IsuzuDiagnostic.Desktop.Views;

public partial class DtcDetailView : UserControl
{
    public event EventHandler? BackRequested;

    public readonly DiagnosticTroubleCode _dtc;

    public DtcDetailView(DiagnosticTroubleCode dtc)
    {
        InitializeComponent();

        _dtc = dtc ?? throw new ArgumentNullException(nameof(dtc));

        DataContext = _dtc;
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

    private void ShowRelatedLiveDataButton_Click( object sender, RoutedEventArgs e )
    {
        MessageBox.Show("The related live-data parameters for this DTC are listed above.\n\n" +
                        "Filtered live-data navigation will be enabled when real ECU/CAN " +
                        "parameter reading is implemented.",
                        "Related Live Data",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
    }
}