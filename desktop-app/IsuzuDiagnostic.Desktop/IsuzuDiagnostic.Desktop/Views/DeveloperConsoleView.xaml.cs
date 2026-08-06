using System;
using System.Windows;
using System.Windows.Controls;

using IsuzuDiagnostic.Desktop.Communication.Protocol;
using IsuzuDiagnostic.Desktop.Communication.Serial;

namespace IsuzuDiagnostic.Desktop.Views
{
    public partial class DeveloperConsoleView : UserControl
    {
        private readonly RequestIdGenerator _requestIdGenerator = new RequestIdGenerator();

        private readonly SerialGatewayService _serialGatewayService;
           
        public event EventHandler? BackRequested;

        public DeveloperConsoleView(SerialGatewayService serialGatewayService)
        {
            InitializeComponent();

            _serialGatewayService = serialGatewayService  ?? throw new ArgumentNullException(nameof(serialGatewayService));

            _serialGatewayService.LineReceived += SerialGatewayService_LineReceived;
               
            _serialGatewayService.CommunicationError += SerialGatewayService_CommunicationError;

            Unloaded += DeveloperConsoleView_Unloaded;

            LoadAvailablePorts();
            
            UpdateConnectionControls();

            AppendConsoleLine("[APP] Developer console initialized." );

            AppendConsoleLine( "[APP] Select the ESP32 serial port and connect." );
               
        }

        private void LoadAvailablePorts()
        {
            var portNames = SerialGatewayService.GetAvailablePortNames();

            SerialPortComboBox.ItemsSource = portNames;

            if (portNames.Count > 0)
            {
                SerialPortComboBox.SelectedIndex = 0;

                PortStatusTextBlock.Text = $"{portNames.Count} port(s) found";

                return;
            }

            SerialPortComboBox.SelectedIndex = -1;

            PortStatusTextBlock.Text = "No serial ports found";
               
        }

        private void RefreshPortsButton_Click( object sender, RoutedEventArgs e      
        )
        {
            if (_serialGatewayService.IsConnected)
            {
                return;
            }

            LoadAvailablePorts();

            AppendConsoleLine( "[APP] Serial port list refreshed." );
         
        }

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (SerialPortComboBox.SelectedItem is not string portName)  
            {
                MessageBox.Show( "Please select a serial port.", "Serial Connection", MessageBoxButton.OK, MessageBoxImage.Warning );
              
                return;
            }

            try
            {
                _serialGatewayService.Connect(portName, 115200 );
          
                AppendConsoleLine($"[APP] Connected to {portName} at 115200 baud." );
   
                UpdateConnectionControls();
            }
            catch (Exception exception)
            {
                AppendConsoleLine( $"[CONNECTION ERROR] {exception.Message}" );

                MessageBox.Show(
                    exception.Message,
                    "Serial Connection Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        private void DisconnectButton_Click(
            object sender,
            RoutedEventArgs e
        )
        {
            DisconnectSerialGateway();
        }

        private void DisconnectSerialGateway()
        {
            string? connectedPortName =
                _serialGatewayService.ConnectedPortName;

            _serialGatewayService.Disconnect();

            AppendConsoleLine(
                connectedPortName is null
                    ? "[APP] Serial gateway disconnected."
                    : $"[APP] Disconnected from {connectedPortName}."
            );

            UpdateConnectionControls();
        }

        private void UpdateConnectionControls()
        {
            bool isConnected =
                _serialGatewayService.IsConnected;

            SerialPortComboBox.IsEnabled =
                !isConnected;

            ConnectButton.IsEnabled =
                !isConnected;

            DisconnectButton.IsEnabled =
                isConnected;

            PortStatusTextBlock.Text =
                isConnected
                    ? $"Connected: {_serialGatewayService.ConnectedPortName}"
                    : "Disconnected";
        }

        private void PingButton_Click(
            object sender,
            RoutedEventArgs e
        )
        {
            CreateAndDisplayRequest(
                GatewayCommand.Ping
            );
        }

        private void StartButton_Click(
            object sender,
            RoutedEventArgs e
        )
        {
            CreateAndDisplayRequest(
                GatewayCommand.Start
            );
        }

        private void StopButton_Click(
            object sender,
            RoutedEventArgs e
        )
        {
            CreateAndDisplayRequest(
                GatewayCommand.Stop
            );
        }

        private void StatusButton_Click(
            object sender,
            RoutedEventArgs e
        )
        {
            CreateAndDisplayRequest(
                GatewayCommand.Status
            );
        }

        private void CreateAndDisplayRequest(
            GatewayCommand command
        )
        {
            if (!_serialGatewayService.IsConnected)
            {
                AppendConsoleLine(
                    "[SEND ERROR] Connect to the ESP32 first."
                );

                return;
            }

            int requestId =
                _requestIdGenerator.GetNext();

            string requestMessage =
                GatewayProtocol.CreateRequest(
                    requestId,
                    command
                );

            try
            {
                _serialGatewayService.SendLine(requestMessage);

                AppendConsoleLine( $"TX > {requestMessage}");
                
            }
            catch (Exception exception)
            {
                AppendConsoleLine( $"[SEND ERROR] {exception.Message}" );
     
            }
        }

        private void SerialGatewayService_LineReceived(
            string line
        )
        {
            Dispatcher.InvokeAsync(
                () => ProcessReceivedLine(line)
            );
        }

        private void SerialGatewayService_CommunicationError(string errorMessage)
        {
            Dispatcher.InvokeAsync(
                () =>
                {
                    AppendConsoleLine($"[SERIAL ERROR] {errorMessage}");
                }
            );
        }

        private void ProcessReceivedLine( string line)

        {
            AppendConsoleLine( $"RX < {line}" );

            bool parsed =
                GatewayResponseParser.TryParse(line, out GatewayResponse? response, out string errorMessage);

            if (!parsed || response is null)
            {
                AppendConsoleLine($"[PARSE ERROR] {errorMessage}" );

                return;
            }

            string statusText = response.IsSuccess ? "OK" : "ERR";

            AppendConsoleLine($"[PARSED] ID={response.RequestId}, " + $"STATUS={statusText}, " + $"PAYLOAD={response.Payload}" );
        }

        private void TestParserButton_Click(object sender, RoutedEventArgs e )    
       
        {
            AppendConsoleLine( "----- PARSER SELF-TEST -----" );

            (string Line, bool ExpectedValid)[] testCases =
            {
                ("RES|100|OK|PONG", true),
                ("RES|101|ERR|UNKNOWN_COMMAND", true),
                ("RES|102|OK|MODEL=NPR|ENGINE=4JZ1", true),
                ("RES|0|OK|PONG", false),
                ("RES|ABC|OK|PONG", false),
                ("REQ|103|PING", false),
                ("RES|104|WHAT|PONG", false),
                ("RES|105|OK", false),
                ("   ", false)
            };

            int passedTestCount = 0;

            foreach ((string line, bool expectedValid) in testCases)
                
                
            
            {
                bool parsed = GatewayResponseParser.TryParse(line, out GatewayResponse? response, out string errorMessage);
 
                bool testPassed =
                    parsed == expectedValid;

                if (testPassed)
                {
                    passedTestCount++;
                }

                string visibleLine = string.IsNullOrWhiteSpace(line) ? "[EMPTY OR WHITESPACE]" : line;

                AppendConsoleLine($"TEST > {visibleLine}"); 

                AppendConsoleLine(testPassed ? "[TEST PASS]" : "[TEST FAIL]" );

                if (parsed && response is not null)
                {
                    AppendConsoleLine($"[PARSED] ID={response.RequestId}, " + $"STATUS={response.Status}, " + $"PAYLOAD={response.Payload}" );

                }
                else
                {
                    AppendConsoleLine( $"[EXPECTED ERROR] {errorMessage}" );

                }

                AppendConsoleLine(string.Empty);

            }

            AppendConsoleLine($"[TEST SUMMARY] " + $"{passedTestCount}/{testCases.Length} passed.");

            AppendConsoleLine("----- TEST FINISHED -----");

        }

        private void AppendConsoleLine(
            string message
        )
        {
            ConsoleOutputTextBox.AppendText( message + Environment.NewLine);

            ConsoleOutputTextBox.ScrollToEnd();
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e
        )
        {
            ConsoleOutputTextBox.Clear();

            AppendConsoleLine("[APP] Console cleared." );
           
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            BackRequested?.Invoke(this, EventArgs.Empty);
            
        }

        private void DeveloperConsoleView_Unloaded(object sender, RoutedEventArgs e)  
        {
            _serialGatewayService.LineReceived -= SerialGatewayService_LineReceived;
               

            _serialGatewayService.CommunicationError -= SerialGatewayService_CommunicationError;
               

            _serialGatewayService.Dispose();

            Unloaded -= DeveloperConsoleView_Unloaded;
        }
    }
}