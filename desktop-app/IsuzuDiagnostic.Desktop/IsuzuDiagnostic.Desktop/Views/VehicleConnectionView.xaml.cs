using System;
using System.Windows;
using System.Windows.Controls;
using System.Threading.Tasks;

using IsuzuDiagnostic.Desktop.Catalogs;
using IsuzuDiagnostic.Desktop.Models;
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

        public VehicleConnectionView(SerialGatewayService serialGatewayService, RequestIdGenerator requestIdGenarator)
        {
            InitializeComponent();

            _serialGatewayService = serialGatewayService ?? throw new ArgumentNullException(nameof(serialGatewayService));

            _requestIdGenarator = requestIdGenarator ?? throw new ArgumentNullException(nameof(requestIdGenarator));

            LoadVehicleCatalog();
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

            bool handshakeSucceeded = await TryConnectToGatewayAsync(serialPortName);

            if (!handshakeSucceeded)
            {
                CreatedSession.MarkFaulted();

                if (_serialGatewayService.IsConnected)
                {
                    _serialGatewayService.Disconnect();
                }

                MessageBox.Show("The ESP32 diagnostic gateway did not respond to PING.", "Gateway Connection Failed", MessageBoxButton.OK, MessageBoxImage.Error);

                return;
            }

            CreatedSession.MarkConnecting();

            ContinueRequested?.Invoke(this, EventArgs.Empty);
        }

        private async Task<bool> TryConnectToGatewayAsync(string serialPortName)
        {
            try
            {
                if (_serialGatewayService.IsConnected)
                {
                    _serialGatewayService.Disconnect();
                }

                _serialGatewayService.Connect(serialPortName);

                int requestId = _requestIdGenarator.GetNext();

                string expectedResponse = $"RES|{requestId}|OK|PONG";

                TaskCompletionSource<bool> pongReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                void HandleReceivedLine(string line)
                {
                    if (string.Equals(line.Trim(), expectedResponse, StringComparison.Ordinal))
                    {
                        pongReceived.TrySetResult(true);
                    }
                }

                _serialGatewayService.LineReceived += HandleReceivedLine;

                try
                {
                    string request = GatewayProtocol.CreateRequest(requestId, GatewayCommand.Ping);

                    _serialGatewayService.SendLine(request);

                    Task timeoutTask = Task.Delay(2000);

                    Task completeTask = await Task.WhenAny(pongReceived.Task, timeoutTask);

                    return completeTask == pongReceived.Task;
                }
                finally
                {
                    _serialGatewayService.LineReceived -= HandleReceivedLine;
                }
            }
            catch
            {
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
    }
}
