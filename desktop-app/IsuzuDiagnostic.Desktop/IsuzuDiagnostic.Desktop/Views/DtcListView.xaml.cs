using System;
using System.Windows;
using System.Windows.Controls;

using IsuzuDiagnostic.Desktop.Data;
using IsuzuDiagnostic.Desktop.Models;

namespace IsuzuDiagnostic.Desktop.Views;

public partial class DtcListView : UserControl
{
    private readonly DtcKnowledgeRepository _knowledgeRepository = new();

    /*
     * TEMPORARY:
     *
     * These represent DTC codes returned by a simulated scan.
     * The knowledge itself is NOT mocked anymore.
     *
     * When desktop Mode 03 communication is connected,
     * this array disappears and the ECU response supplies the codes.
     */
    private readonly string[] _simulatedDetectedCodes =
    {
        "P1093",
        "P0087"
    };

    public event EventHandler? BackRequested;

    public event Action<DiagnosticTroubleCode>? DtcDetailsRequested;

    public DtcListView()
    {
        InitializeComponent();

        LoadDetectedDtcs();
    }

    private void DetailsButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is Button button &&
            button.Tag is DiagnosticTroubleCode dtc)
        {
            DtcDetailsRequested?.Invoke(dtc);
        }
    }

    private void RescanButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        IReadOnlyList<DiagnosticTroubleCode> dtcs =
            LoadDetectedDtcs();

        MessageBox.Show(
            $"{dtcs.Count} simulated DTC(s) detected.\n\n" +
            "Diagnostic information is loaded from isuzu-knowledge.db.",
            "DTC Scan",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void ClearDtcMemoryButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        MessageBoxResult result = MessageBox.Show(
            "Clear diagnostic trouble code memory?\n\n" +
            "This is currently a simulated operation. " +
            "No command will be sent to the vehicle ECU.",
            "Clear DTC memory",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        MessageBox.Show(
            "DTC clearing is not implemented yet.\n\n" +
            "No command was sent to the vehicle ECU.",
            "Clear DTC memory",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        LoadDetectedDtcs();
    }

    private void BackButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        BackRequested?.Invoke(this, EventArgs.Empty);
    }

    private IReadOnlyList<DiagnosticTroubleCode> LoadDetectedDtcs()
    {
        List<DiagnosticTroubleCode> dtcs = [];

        foreach (string code in _simulatedDetectedCodes)
        {
            DtcKnowledgeDetails? knowledge =
                _knowledgeRepository.FindByCode(code);

            if (knowledge is null)
            {
                dtcs.Add(CreateUnknownDtc(code));
                continue;
            }

            /*
             * Mode 03 represents stored/confirmed emission-related DTCs.
             * For this simulated scan we therefore use "Stored".
             */
            dtcs.Add(
                DtcKnowledgeMapper.ToDiagnosticTroubleCode(
                    knowledge,
                    "Stored"));
        }

        DtcItemsControl.ItemsSource = null;
        DtcItemsControl.ItemsSource = dtcs;

        return dtcs;
    }

    private static DiagnosticTroubleCode CreateUnknownDtc(
        string code)
    {
        return new DiagnosticTroubleCode(
            code: code,
            description: "Unknown DTC",
            status: "Stored",
            possibleCauses: Array.Empty<DtcCause>(),
            diagnosticSteps: Array.Empty<DiagnosticStep>(),
            relatedLiveData: Array.Empty<RelatedLiveDataItem>(),
            possibleSolutions: Array.Empty<DtcSolution>());
    }
}