using System;
using System.Windows;
using System.Windows.Controls;
using System.Threading.Tasks;

using IsuzuDiagnostic.Desktop.Catalogs;
using IsuzuDiagnostic.Desktop.Models;
using IsuzuDiagnostic.Desktop.Data;
using IsuzuDiagnostic.Desktop.Communication.Protocol;
using IsuzuDiagnostic.Desktop.Communication.Serial;

namespace IsuzuDiagnostic.Desktop.Views
{
    public partial class VehicleConnectionView : UserControl
    {
        public event EventHandler? ContinueRequested;

        public DiagnosticSession? CreatedSession { get; private set; }

        private readonly SerialGatewayService _serialGatewayService;

        private readonly RequestIdGenerator _requestIdGenarator;

        private readonly VehicleProfileRepository _vehicleProfileRepository;

        private string _lastConnectionError = "No gateway response was received.";

        public VehicleConnectionView(SerialGatewayService serialGatewayService, RequestIdGenerator requestIdGenarator, VehicleProfileRepository vehicleProfileRepository)
        {
            InitializeComponent();

            _serialGatewayService = serialGatewayService ?? throw new ArgumentNullException(nameof(serialGatewayService));

            _requestIdGenarator = requestIdGenarator ?? throw new ArgumentNullException(nameof(requestIdGenarator));

            _vehicleProfileRepository = vehicleProfileRepository ?? throw new ArgumentNullException(nameof(vehicleProfileRepository));

            LoadVehicleCatalog();

            LoadSerialPorts();
        }

        private async void ContinueButton_Click(object sender, RoutedEventArgs e)
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

            CreatedSession.MarkConnecting();

            IsEnabled = false;
            bool handshakeSucceeded;
            try { handshakeSucceeded = await TryConnectToGatewayAsync(serialPortName); }
            finally { IsEnabled = true; }

            if (!handshakeSucceeded)
            {
                CreatedSession.MarkFaulted();

                // Show the actual failed stage before releasing the serial
                // handle. Some Windows USB-serial drivers can block in Close.
                MessageBox.Show(
                    "Gateway handshake/source verification failed.\n\n" + _lastConnectionError,
                    "Gateway Connection Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                if (_serialGatewayService.IsConnected)
                {
                    _serialGatewayService.Disconnect();
                }

                return;
            }

            CreatedSession.MarkConnected();

            try { _vehicleProfileRepository.Save(vehicleProfile); }
            catch (Exception ex) { _serialGatewayService.Disconnect(); CreatedSession.MarkFaulted(); ShowValidationMessage("Cannot save session: " + ex.Message); return; }

            ContinueRequested?.Invoke(this, EventArgs.Empty);
        }

        private async Task<bool> TryConnectToGatewayAsync(string serialPortName)
        {
            _lastConnectionError = "No gateway response was received.";
            try
            {
                if (_serialGatewayService.IsConnected)
                {
                    _serialGatewayService.Disconnect();
                }

                // SerialPort.Open enters the Windows USB-serial driver and can
                // block during a VM hand-off or a failing CP210x reset. Keep it
                // off the WPF dispatcher so the window remains responsive.
                await Task.Run(() => _serialGatewayService.Connect(serialPortName));

                // Give the ESP32 serial gateway a short time to become ready
                // after opening the COM port.
                await Task.Delay(750);

                using var client = new GatewayRequestClient(_serialGatewayService, _requestIdGenarator);
                if (await client.RequestAsync(GatewayCommand.Ping) != "PONG")
                    throw new InvalidOperationException("PING returned an unexpected response.");
                _serialGatewayService.ApplyInfo(await client.RequestAsync(GatewayCommand.Info));
                return true;
            }
            catch (Exception exception)
            {
                _lastConnectionError = exception.Message;
                if (exception is TimeoutException)
                    _lastConnectionError += "\nLast serial line: " + _serialGatewayService.LastReceivedLine;
                return false;
            }
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

        private void LoadSerialPorts()
        {
            var portNames = SerialGatewayService.GetAvailablePortNames();

            SerialPortComboBox.ItemsSource = portNames;

            if (portNames.Count > 0 )
            {
                SerialPortComboBox.SelectedIndex = 0;
            }
            else
            {
                SerialPortComboBox.SelectedIndex = -1;
            }

        }
    }
}
