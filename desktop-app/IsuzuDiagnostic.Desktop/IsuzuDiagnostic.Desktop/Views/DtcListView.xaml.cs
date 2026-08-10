using System;
using System.Windows;
using System.Windows.Controls;

using IsuzuDiagnostic.Desktop.Catalogs;
using IsuzuDiagnostic.Desktop.Models;

namespace IsuzuDiagnostic.Desktop.Views
{
    public partial class DtcListView : UserControl
    {
        public event EventHandler? BackRequested;

        public event Action<DiagnosticTroubleCode>? DtcDetailsRequested;

        public DtcListView()
        {
            InitializeComponent();

            LoadMockDtcs();
        }

        private void DetailsButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is DiagnosticTroubleCode dtc)
            {
                DtcDetailsRequested?.Invoke(dtc);
            }
        }

        private void RescanButton_Click(object sender, RoutedEventArgs e)
        {
            LoadMockDtcs();

            MessageBox.Show($"{MockDtcCatalog.Items.Count} mock DTC(s) detected.");
        }

        private void ClearDtcMemoryButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show("Clear diagnostic trouble code memory?\n\n" +
                                                      "This is currently a simulated operation." +
                                                      "No command will be sent to the vehicle ECU.",
                                                      "Clear DTC memory.",
                                                      MessageBoxButton.YesNo,
                                                      MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            MessageBox.Show("DTC clearing is not implemented yet.\n\n" +
                            "No command was sent to the vehicle ECU.",
                            "Clear DTC memory.",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);

            // Simulate the automatic rescan that will occur
            // after a real DTC clear operation in the future.
            LoadMockDtcs();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            BackRequested?.Invoke(this, EventArgs.Empty);
        }

        private void LoadMockDtcs()
        {
            DtcItemsControl.ItemsSource = null;

            DtcItemsControl.ItemsSource = MockDtcCatalog.Items;
        }
    }
}
