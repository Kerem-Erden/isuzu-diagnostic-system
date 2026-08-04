using System.Windows;
using System;
using IsuzuDiagnostic.Desktop.Views;
using IsuzuDiagnostic.Desktop.Models;

namespace IsuzuDiagnostic.Desktop
{
    public partial class AppShellWindow : Window
    {
        private object? _contentBeforeDeveloperConsole;
        private string? _titleBeforeDeveloperConsole = "Vehicle Connection";

        private DiagnosticSession? _activeSession;

        public AppShellWindow()
        {
            InitializeComponent();

            ShowVehicleConnectionView();
        }

        private void ShowVehicleConnectionView()
        {
            VehicleConnectionView view = new VehicleConnectionView();

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

            MainContent.Content = view;

            PageTitleTextBlock.Text = "Diagnostic Dashboard";
        }

        private void DiagnosticDashboardView_EndSessionRequested(object? sender, EventArgs e)
        {
            if (_activeSession is not null)
            {
                _activeSession.End();
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
            LiveDataView view = new LiveDataView();

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

        private void ShowDtcListView()
        {
            DtcListView view = new DtcListView();

            view.DtcDetailsRequested += DtcListView_DtcDetailsRequested;

            view.BackRequested += DtcListView_BackRequested;

            MainContent.Content = view;

            PageTitleTextBlock.Text = "DTC List";

        }

        private void DtcListView_DtcDetailsRequested(string dtcCode)
        {
            ShowDtcDetailsView(dtcCode);
        }

        private void DtcListView_BackRequested(object? sender, EventArgs e)
        {
            ShowDiagnosticDashboardView();
        }

        private void ShowDtcDetailsView(string dtcCode)
        {
            DtcDetailView view = new DtcDetailView(dtcCode);

            view.BackRequested += DtcDetailView_BackRequested;

            MainContent.Content = view;

            PageTitleTextBlock.Text = $"DTC Details - {dtcCode}";
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
            DeveloperConsoleView view = new DeveloperConsoleView();

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
    }
}
