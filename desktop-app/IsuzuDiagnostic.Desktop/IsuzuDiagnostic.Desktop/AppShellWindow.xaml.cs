using System.Windows;
using System;
using System.Windows.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Threading.Tasks;

using IsuzuDiagnostic.Desktop.Views;
using IsuzuDiagnostic.Desktop.Models;
using IsuzuDiagnostic.Desktop.Communication.Serial;
using IsuzuDiagnostic.Desktop.Communication.Protocol;

namespace IsuzuDiagnostic.Desktop
{
    public partial class AppShellWindow : Window
    {
        private object? _contentBeforeDeveloperConsole;
        private string? _titleBeforeDeveloperConsole = "Vehicle Connection";

        private DiagnosticSession? _activeSession;
        
        private readonly SerialGatewayService _serialGatewayService = new SerialGatewayService();

        private readonly RequestIdGenerator _requestIdGenerator = new RequestIdGenerator();

        private readonly DispatcherTimer _connectionWatchdogTimer;

        private readonly DispatcherTimer _reconnectTimer;

        private DateTimeOffset _lastPongAt;

        private bool _connectionLossHandled;

        private bool _watchdogPingInProgress;

        private bool _reconnectAttemptInProgress;

        public AppShellWindow()
        {
            InitializeComponent();

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
            
            _serialGatewayService.LineReceived += SerialGatewayService_LineReceived;

            _serialGatewayService.CommunicationError += SerialGatewayService_CommunicationError;

            Closed += AppShellWindow_Closed;

            ShowVehicleConnectionView();
        }

        private void ShowVehicleConnectionView()
        {
            VehicleConnectionView view = new VehicleConnectionView(_serialGatewayService, _requestIdGenerator);

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

            PageTitleTextBlock.Text = "Diagnostic Dashboard";
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
            DtcListView view = new DtcListView();

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

            _serialGatewayService.LineReceived -= SerialGatewayService_LineReceived;

            _serialGatewayService.CommunicationError -= SerialGatewayService_CommunicationError;

            if (_serialGatewayService.IsConnected)
            {
                _serialGatewayService.Disconnect();
            }

            _serialGatewayService.Dispose();
        }

        private void SerialGatewayService_CommunicationError(string errorMessage)
        {
            Dispatcher.InvokeAsync(() =>
            {
                HandleConnectionLost(errorMessage);

                if (_activeSession is null)
                {
                    return;
                }

                if (_activeSession.State == DiagnosticSessionState.Faulted)
                {
                    return;
                }

                _activeSession.MarkFaulted();

                if (_serialGatewayService.IsConnected)
                {
                    _serialGatewayService.Disconnect();
                }

                MessageBox.Show("Communication with the ESP32 wa lost.\n\n" + errorMessage,
                                "Diagnostic Connection Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);

                _activeSession = null;

                ShowVehicleConnectionView();
            });
        }

        private void SerialGatewayService_LineReceived(string line)
        {
            if (_activeSession?.State != DiagnosticSessionState.Connected)
            {
                return;
            }

            bool parsed = GatewayResponseParser.TryParse(line, out GatewayResponse? response, out _);

            if (!parsed || response is null || !response.IsSuccess)
            {
                return;
            }

            if (!string.Equals(response.Payload, "PONG", StringComparison.Ordinal))
            {
                return;
            }

            Dispatcher.InvokeAsync(() =>
            {
                if (_activeSession?.State == DiagnosticSessionState.Connected)
                {
                    _lastPongAt = DateTimeOffset.Now;
                }
            });

        } 

        private async void ConnectionWatchdogTimer_Tick(object sender, EventArgs e)
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
                int requestId = _requestIdGenerator.GetNext();

                string request = GatewayProtocol.CreateRequest(requestId, GatewayCommand.Ping);

                await Task.Run(() =>
                {
                    _serialGatewayService.SendLine(request);
                });

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

        private async void ReconnectTimer_Tick(object sender, EventArgs e)
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

            bool portIsAvailable = SerialGatewayService.GetAvailablePortNames().Any(portName => string.Equals(portName, expectedPortName, StringComparison.OrdinalIgnoreCase));

            if (!portIsAvailable)
            {
                return;
            }

            _reconnectAttemptInProgress = true;

            try
            {
                bool reconnected = await TryReconnectToGatewayAsync(expectedPortName);

                if (!reconnected)
                {
                    return;
                }

                _reconnectTimer.Stop();

                _activeSession.MarkConnecting();

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

                _serialGatewayService.Connect(serialPortName);

                await Task.Delay(750);

                int requestId = _requestIdGenerator.GetNext();

                string expectedRespose = $"RES|{requestId}|OK|PONG";

                TaskCompletionSource<bool> pongReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                void HandleReceivedLine(string line)
                {
                    if (string.Equals(line.Trim(), expectedRespose, StringComparison.Ordinal))
                    {
                        pongReceived.TrySetResult(true);
                    }
                }

                _serialGatewayService.LineReceived += HandleReceivedLine;

                try
                {
                    string request = GatewayProtocol.CreateRequest(requestId, GatewayCommand.Ping);

                    await Task.Run(() =>
                    {
                        _serialGatewayService.SendLine(request);
                    });

                    Task timeoutTask = Task.Delay(3000);

                    Task completedTask = await Task.WhenAny(pongReceived.Task, timeoutTask);

                    if (completedTask == pongReceived.Task)
                    {
                        return true;
                    }
                }
                finally
                {
                    _serialGatewayService.LineReceived -= HandleReceivedLine;
                }
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
