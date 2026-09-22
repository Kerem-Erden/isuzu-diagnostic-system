using IsuzuDiagnostic.Desktop.Models;
using System;
using System.Windows;
using System.Windows.Controls;

namespace IsuzuDiagnostic.Desktop.Views;

public partial class DtcDetailView : UserControl
{
    public event EventHandler? BackRequested;
    public event Action<DiagnosticTroubleCode>? RelatedLiveDataRequested;

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
        RelatedLiveDataRequested?.Invoke(_dtc);
    }
}