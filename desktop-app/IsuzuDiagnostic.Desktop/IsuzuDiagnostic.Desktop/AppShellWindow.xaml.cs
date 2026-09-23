using System.Windows;
using System;
using System.Windows.Threading;
using System.Threading.Tasks;
using System.Linq;

using IsuzuDiagnostic.Desktop.Views;
using IsuzuDiagnostic.Desktop.Services;
using IsuzuDiagnostic.Desktop.Models;
using IsuzuDiagnostic.Desktop.Data;
using IsuzuDiagnostic.Desktop.Communication.Serial;
using IsuzuDiagnostic.Desktop.Communication.Protocol;

namespace IsuzuDiagnostic.Desktop
{
    public partial class AppShellWindow : Window
    {
        private DtcWorkflow? _dtcWorkflow;
        private GatewayRequestClient _diagnosticClient;
        private object? _contentBeforeDeveloperConsole;
        private string? _titleBeforeDeveloperConsole = "Vehicle Connection";

        private DiagnosticSession? _activeSession;
        
        private readonly SerialGatewayService _serialGatewayService = new SerialGatewayService();

        private readonly RequestIdGenerator _requestIdGenerator = new RequestIdGenerator();

        private readonly DispatcherTimer _connectionWatchdogTimer;

        private readonly DispatcherTimer _reconnectTimer;

        private readonly VehicleProfileRepository _vehicleProfileRepository = new VehicleProfileRepository();

        private DateTimeOffset _lastPongAt;

        private bool _connectionLossHandled;

        private bool _watchdogPingInProgress;

        private bool _reconnectAttemptInProgress;

        private string? _expectedGatewaySource;

        private string? _expectedGatewayEcu;

        public AppShellWindow()
        {
            InitializeComponent();
            _diagnosticClient = new GatewayRequestClient(_serialGatewayService, _requestIdGenerator);

            _connectionWatchdogTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };

            _connectionWatchdogTimer.Tick += ConnectionWatchdogTimer_Tick;

            _reconnectTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };

            _reconnectTimer.Tick += ReconnectTimer_Tick;
            
            _serialGatewayService.CommunicationError += SerialGatewayService_CommunicationError;

            Closed += AppShellWindow_Closed;

            ShowVehicleConnectionView();
        }

        private void ShowVehicleConnectionView()
        {
            VehicleConnectionView view = new VehicleConnectionView(_serialGatewayService, _requestIdGenerator, _vehicleProfileRepository);

            view.ContinueRequested += VehicleConnectionView_ContinueRequested;

            MainContent.Content = view;

            PageTitleTextBlock.Text = "Vehicle Connection";
        }

        private void VehicleConnectionView_ContinueRequested(object? sender, EventArgs e)
        {
            if (sender is not VehicleConnectionView connectionView)
            {
                MessageBox.Show("The vehicle connection screen could not be identified.", "Session Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (connectionView.CreatedSession is null)
            {
                MessageBox.Show("The diagnostic session could not be created.", "Session Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return; 
            }

            _activeSession = connectionView.CreatedSession;
            _expectedGatewaySource = _serialGatewayService.Source;
            _expectedGatewayEcu = _serialGatewayService.Ecu;
            var audit = new DiagnosticAuditLog(_activeSession.Id, _serialGatewayService.SourceDescription);
            _dtcWorkflow = new DtcWorkflow((command, token) => _diagnosticClient.RequestAsync(command == "SCAN_DTC" ? GatewayCommand.ScanDtc : GatewayCommand.ClearDtc, token), audit);
            try
            {
                audit.Write("SessionStarted", new { _activeSession.Vehicle, _activeSession.SerialPortName });
            }
            catch (Exception exception)
            {
                MessageBox.Show("The diagnostic audit log could not be written. The session was not started.\n\n" + exception.Message,
                    "Audit Log Error", MessageBoxButton.OK, MessageBoxImage.Error);
                _serialGatewayService.Disconnect();
                _activeSession = null;
                _dtcWorkflow = null;
                _expectedGatewaySource = null;
                _expectedGatewayEcu = null;
                ShowVehicleConnectionView();
                return;
            }

            StartConnectionWatchdog();

            ShowDiagnosticDashboardView();
        }

        private void StartConnectionWatchdog()
        {
            _connectionLossHandled = false;

            _lastPongAt = DateTimeOffset.Now;

            _connectionWatchdogTimer.Start();
        }

        private void ShowDiagnosticDashboardView()
        {
            if (_activeSession is null)
            {
                ShowVehicleConnectionView();
                return;
            }

            DiagnosticDashboardView view = new DiagnosticDashboardView();

            view.DataContext = _activeSession;

            view.EndSessionRequested += DiagnosticDashboardView_EndSessionRequested;

            view.LiveDataRequested += DiagnosticDashboardView_LiveDataRequested;

            view.DtcListRequested += DiagnosticDashboardView_DtcListRequested;

            view.VehicleInformationRequested += DiagnosticDashboardView_VehicleInformationRequested;

            MainContent.Content = view;

            PageTitleTextBlock.Text = "Diagnostic Dashboard — " + _serialGatewayService.SourceDescription;
        }

        private void DiagnosticDashboardView_EndSessionRequested(object? sender, EventArgs e)
        {
            _connectionWatchdogTimer.Stop();
            _reconnectTimer.Stop();

            _reconnectAttemptInProgress = false;
            _connectionLossHandled = true;

            if (_activeSession is not null)
            {
                _activeSession.End();
            }

            if (_serialGatewayService.IsConnected)
            {
                _serialGatewayService.Disconnect();
            }

            _activeSession = null;
            _dtcWorkflow = null;
            _expectedGatewaySource = null;
            _expectedGatewayEcu = null;
            _contentBeforeDeveloperConsole = null;

            ShowVehicleConnectionView();
        }

        private void DiagnosticDashboardView_LiveDataRequested(object? sender, EventArgs e)
        {
            ShowLiveDataView();
        }

        private void ShowLiveDataView()
        {
            if (_activeSession is null)
            {
                ShowVehicleConnectionView();
                return;
            }

            LiveDataView view = new LiveDataView(_activeSession, _serialGatewayService, _requestIdGenerator);

            view.BackRequested += LiveDataView_BackRequested;

            MainContent.Content = view;

            PageTitleTextBlock.Text = "Live Data";
        }

        private void LiveDataView_BackRequested(object? sender, EventArgs e)
        {
            ShowDiagnosticDashboardView();
        }

        private void DiagnosticDashboardView_DtcListRequested(object? sender, EventArgs e)
        {
            ShowDtcListView();
        }

        private void DiagnosticDashboardView_VehicleInformationRequested(object? sender, EventArgs e)
        {
            if (_activeSession is null)
            {
                ShowVehicleConnectionView();
                return;
            }

            VehicleInformationView view = new VehicleInformationView(_activeSession);

            view.BackRequested += VehicleInformationView_BackRequested;

            MainContent.Content = view;

            PageTitleTextBlock.Text = "Vehicle Information";
        }

        private void VehicleInformationView_BackRequested(Object? sender, EventArgs e)
        {
            ShowDiagnosticDashboardView();
        }

        private void ShowDtcListView()
        {
            if (_activeSession is null || _dtcWorkflow is null) return;
            DtcListView view = new DtcListView(_dtcWorkflow, _serialGatewayService);

            view.DtcDetailsRequested += DtcListView_DtcDetailsRequested;

            view.BackRequested += DtcListView_BackRequested;

            MainContent.Content = view;

            PageTitleTextBlock.Text = "DTC List";

        }

        private void DtcListView_DtcDetailsRequested(DiagnosticTroubleCode dtc)
        {
            ShowDtcDetailsView(dtc);
        }

        private void DtcListView_BackRequested(object? sender, EventArgs e)
        {
            ShowDiagnosticDashboardView();
        }

        private void ShowDtcDetailsView(DiagnosticTroubleCode dtc)
        {
            DtcDetailView view = new DtcDetailView(dtc);

            view.BackRequested += DtcDetailView_BackRequested;
            view.RelatedLiveDataRequested += related =>
            {
                if (_activeSession is null) return;
                var live = new LiveDataView(_activeSession, _serialGatewayService, _requestIdGenerator, related);
                live.BackRequested += (_, _) => ShowDtcDetailsView(related);
                MainContent.Content = live;
                PageTitleTextBlock.Text = "Related Live Data — " + related.Code;
            };

            MainContent.Content = view;

            PageTitleTextBlock.Text = $"DTC Details - {dtc.Code}";
        }

        private void DtcDetailView_BackRequested(object? sender, EventArgs e)
        {
            ShowDtcListView();
        }

        private void DeveloperConsoleButton_Click(object sender, RoutedEventArgs e)
        {
            if (MainContent.Content is DeveloperConsoleView)
            {
                return;
            }

            _contentBeforeDeveloperConsole = MainContent.Content;

            _titleBeforeDeveloperConsole = PageTitleTextBlock.Text;

            ShowDeveloperConsoleView();
        }

        private void ShowDeveloperConsoleView()
        {
            DeveloperConsoleView view = new DeveloperConsoleView( _serialGatewayService, _requestIdGenerator);

            view.BackRequested += DeveloperConsoleView_BackRequested;

            MainContent.Content = view;

            PageTitleTextBlock.Text = "Developer Console";
        }

        private void DeveloperConsoleView_BackRequested(object? sender, EventArgs e)
        {
            if (_contentBeforeDeveloperConsole != null)
            {
                MainContent.Content = _contentBeforeDeveloperConsole;
       
                PageTitleTextBlock.Text = _titleBeforeDeveloperConsole;

                _contentBeforeDeveloperConsole = null;

                return;
            }
            ShowVehicleConnectionView();
        }

        private void AppShellWindow_Closed(object? sender, EventArgs e)
        {
            _connectionWatchdogTimer.Stop();

            _reconnectTimer.Stop();

            _connectionWatchdogTimer.Tick -= ConnectionWatchdogTimer_Tick;

            _reconnectTimer.Tick -= ReconnectTimer_Tick;

            _serialGatewayService.CommunicationError -= SerialGatewayService_CommunicationError;

            if (_serialGatewayService.IsConnected)
            {
                _serialGatewayService.Disconnect();
            }

            _diagnosticClient.Dispose();
            _serialGatewayService.Dispose();
        }

        private void SerialGatewayService_CommunicationError(string errorMessage)
        {
            Dispatcher.InvokeAsync(() =>
            {
                HandleConnectionLost(errorMessage);
            });
        }

        private async void ConnectionWatchdogTimer_Tick(object? sender, EventArgs e)
        {
            if (_activeSession?.State != DiagnosticSessionState.Connected)
            {
                _connectionWatchdogTimer.Stop();
                return;
            }

            if (!_serialGatewayService.IsConnected)
            {
                HandleConnectionLost("The serial port is no longer open.");

                return;
            }
            
            TimeSpan timeSinceLastPong = DateTimeOffset.Now - _lastPongAt;

            if (timeSinceLastPong > TimeSpan.FromSeconds(5))
            {
                HandleConnectionLost($"No heartbeat response for {timeSinceLastPong.TotalSeconds:F1} seconds.");

                return;
            }

            if (_watchdogPingInProgress)
            {
                return;
            }

            _watchdogPingInProgress = true;

            try
            {
                string payload = await _diagnosticClient.RequestAsync(GatewayCommand.Ping);
                if (!string.Equals(payload, "PONG", StringComparison.Ordinal))
                    throw new InvalidOperationException("The heartbeat response was invalid.");
                _lastPongAt = DateTimeOffset.Now;
            }
            catch (Exception exception)
            {
                HandleConnectionLost(exception.Message);
            }
            finally
            {
                _watchdogPingInProgress = false;
            }
        }

        private void HandleConnectionLost(string errorMessage)
        {
            if (_activeSession is null)
            {
                return;
            }

            if (_connectionLossHandled)
            {
                return;
            }

            _connectionLossHandled = true;

            _connectionWatchdogTimer.Stop();

            if (_activeSession.State != DiagnosticSessionState.Faulted)
            {
                _activeSession.MarkFaulted();
            }

            // Warn the user before touching the failed serial port.
            MessageBox.Show(
                "Communication with the ESP32 was lost.\n\n" +
                errorMessage +
                "\n\nReconnect the diagnostic gateway to continue.",
                "Diagnostic Connection Lost",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);


            try
            {
                if (_serialGatewayService.IsConnected)
                {
                    _serialGatewayService.Disconnect();
                }
            }
            catch
            {
                // The physical serial device may already be gone.
            }

            _reconnectAttemptInProgress = false;

            _reconnectTimer.Start();
        }

        private async void ReconnectTimer_Tick(object? sender, EventArgs e)
        {
            if (_activeSession is null)
            {
                _reconnectTimer.Stop();

                return;
            }

            if (_activeSession.State != DiagnosticSessionState.Faulted)
            {
                _reconnectTimer.Stop();
                return;
            }

            if (_reconnectAttemptInProgress)
            {
                return;
            }

            string expectedPortName = _activeSession.SerialPortName;
            DiagnosticSession reconnectingSession = _activeSession;

            bool portIsAvailable = SerialGatewayService.GetAvailablePortNames().Any(portName => string.Equals(portName, expectedPortName, StringComparison.OrdinalIgnoreCase));

            if (!portIsAvailable)
            {
                return;
            }

            _reconnectAttemptInProgress = true;

            try
            {
                bool reconnected = await TryReconnectToGatewayAsync(expectedPortName);

                if (!reconnected || !ReferenceEquals(_activeSession, reconnectingSession))
                {
                    return;
                }

                _reconnectTimer.Stop();

                _activeSession.MarkConnected();

                _connectionLossHandled = false;

                _lastPongAt = DateTimeOffset.Now;

                _connectionWatchdogTimer.Start();

                MessageBox.Show("The connection to ESP32 diagnostic gateway was restored.",
                                "Diagnostic Connection Restored",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
            }
            finally
            {
                _reconnectAttemptInProgress = false;
            }
        }

        private async Task<bool> TryReconnectToGatewayAsync(string serialPortName)
        {
            try
            {
                if (_serialGatewayService.IsConnected)
                {
                    _serialGatewayService.Disconnect();
                }

                await Task.Run(() => _serialGatewayService.Connect(serialPortName));

                await Task.Delay(750);

                if (await _diagnosticClient.RequestAsync(GatewayCommand.Ping) != "PONG")
                    throw new InvalidOperationException("Invalid heartbeat response.");
                _serialGatewayService.ApplyInfo(await _diagnosticClient.RequestAsync(GatewayCommand.Info));
                if (_serialGatewayService.Source != _expectedGatewaySource || _serialGatewayService.Ecu != _expectedGatewayEcu)
                    throw new InvalidOperationException("Gateway source changed; start a new session.");
                return true;
            }
            catch
            {
                // Reconnection attempts are expected to fail
                // while the gateway is still unplugged.
            }

            try
            {
                if (_serialGatewayService.IsConnected)
                {
                    _serialGatewayService.Disconnect();
                }
            }
            catch
            {

            }

            return false;
        }
    }
}
