using System.Windows;
using System;
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

        public AppShellWindow()
        {
            InitializeComponent();

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

            ShowDiagnosticDashboardView();
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
    }
}
