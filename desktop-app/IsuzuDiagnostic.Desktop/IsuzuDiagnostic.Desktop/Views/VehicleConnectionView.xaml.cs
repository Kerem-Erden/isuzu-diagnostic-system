using System;
using System.Windows;
using System.Windows.Controls;
using IsuzuDiagnostic.Desktop.Catalogs;
using IsuzuDiagnostic.Desktop.Models;

namespace IsuzuDiagnostic.Desktop.Views
{
    public partial class VehicleConnectionView : UserControl
    {
        public event EventHandler? ContinueRequested;

        public DiagnosticSession? CreatedSession { get; private set; }

        public VehicleConnectionView()
        {
            InitializeComponent();
            LoadVehicleCatalog();
        }

        private void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            if (VehicleModelComboBox.SelectedItem is not string model)
            {
                ShowValidationMessage("Please select a vehicle model.");
                return;
            }

            if (ModelYearComboBox.SelectedItem is not int modelYear)
            {
                ShowValidationMessage("Please select a model year.");
                return;
            }
            
            if (EngineCodeComboBox.SelectedItem is not string engineCode)
            {
                ShowValidationMessage("Please select an engine code.");
                return;
            }

            if (EcuTypeComboBox.SelectedItem is not string ecuType)
            {
                ShowValidationMessage("Please select an ECU type.");
                return;
            }

            string? serialPortName = GetSelectedComboBoxText(SerialPortComboBox);

            if (string.IsNullOrWhiteSpace(serialPortName))
            {
                ShowValidationMessage("Please select a serial port.");
                return;
            }

            VehicleProfile vehicleProfile = new VehicleProfile(
                manufacturer: VehicleProfileCatolog.Manufacturer,
                model: model,
                modelYear: modelYear,
                engineCode: engineCode,
                ecuType: ecuType
            );

            CreatedSession = new DiagnosticSession(vehicle: vehicleProfile, serialPortName: serialPortName);

            ContinueRequested?.Invoke(this, EventArgs.Empty);
        }

        private void LoadVehicleCatalog()
        {
            VehicleModelComboBox.ItemsSource = VehicleProfileCatolog.Models;

            ModelYearComboBox.ItemsSource = VehicleProfileCatolog.ModelYears;

            EngineCodeComboBox.ItemsSource = VehicleProfileCatolog.EngineCodes;

            EcuTypeComboBox.ItemsSource = VehicleProfileCatolog.EcuTypes;

            VehicleModelComboBox.SelectedIndex = -1;
            ModelYearComboBox.SelectedIndex = -1;
            EngineCodeComboBox.SelectedIndex = -1;

            EcuTypeComboBox.SelectedIndex = 0;
        }

        private static string? GetSelectedComboBoxText(ComboBox comboBox)
        {
            return comboBox.SelectedItem switch
            {
                ComboBoxItem comboBoxItem => comboBoxItem.Content?.ToString()?.Trim(),

                null => null,

                object selectedItem => selectedItem.ToString()?.Trim()
            };
        }

        private static void ShowValidationMessage(string message)
        {
            MessageBox.Show(message, "Missing vehicle information", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
