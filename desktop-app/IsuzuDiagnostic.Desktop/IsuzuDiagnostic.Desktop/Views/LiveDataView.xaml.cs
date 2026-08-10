using System;
using System.Windows;
using System.Windows.Controls;
using IsuzuDiagnostic.Desktop.Communication.Protocol;
using IsuzuDiagnostic.Desktop.Communication.Serial;
using IsuzuDiagnostic.Desktop.Models;

namespace IsuzuDiagnostic.Desktop.Views
{
    public partial class LiveDataView : UserControl
    {
        public event EventHandler? BackRequested;

        private readonly DiagnosticSession _session;

        private readonly SerialGatewayService _serialGatewayService;

        private readonly RequestIdGenerator _requestIdGenerator;

        public LiveDataView(DiagnosticSession session, SerialGatewayService serialGatewayService, RequestIdGenerator requestIdGenerator)
        {
            InitializeComponent();

            _session = session?? throw new ArgumentNullException(nameof(session));

            _serialGatewayService = serialGatewayService?? throw new ArgumentNullException(nameof(serialGatewayService));

            _requestIdGenerator = requestIdGenerator ?? throw new ArgumentNullException(nameof(requestIdGenerator));

            DataContext = _session;

            Loaded += LiveDataView_Loaded;

            Unloaded += LiveDataView_Unloaded;


        }

        private void LiveDataView_Loaded(object sender, RoutedEventArgs e)
        {
            _serialGatewayService.LineReceived -= SerialGatewayService_LineReceived;

            _serialGatewayService.LineReceived += SerialGatewayService_LineReceived;

            if (!_serialGatewayService.IsConnected)
            {
                return;
            }

            int requestId = _requestIdGenerator.GetNext();

            string request = GatewayProtocol.CreateRequest(requestId, GatewayCommand.Start);

            _serialGatewayService.SendLine(request); 

        }


        private void SerialGatewayService_LineReceived(string line)
        {
            if (!LiveDataParser.TryParse(line, out LiveDataMessage? message))
            {
                return;
            }

            if (message is null)
            {
                return;
            }

            Dispatcher.InvokeAsync(() =>
            {
                LastReceivedTextBlock.Text = $"Last received: {DateTimeOffset.Now:HH:mm:ss}";

                switch (message.Parameter)
                {
                    case "RPM":
                        RpmValueTextBlock.Text = message.Value.ToString("0");
                        break;

                    case "COOLANT_TEMP":
                        CoolantValueTextBlock.Text = message.Value.ToString("0");
                        break;

                    case "BATTERY_VOLTAGE":
                        BatteryValueTextBlock.Text = message.Value.ToString("0.0");
                        break;
                }
            });
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            BackRequested?.Invoke(this, EventArgs.Empty);
        }

        private void LiveDataView_Unloaded(object sender, RoutedEventArgs e)
        {
            _serialGatewayService.LineReceived -= SerialGatewayService_LineReceived;

            if (!_serialGatewayService.IsConnected)
            {
                return;
            }

            int requestId = _requestIdGenerator.GetNext();

            string request = GatewayProtocol.CreateRequest(requestId, GatewayCommand.Stop);

            _serialGatewayService.SendLine(request);

        }


    }
}
