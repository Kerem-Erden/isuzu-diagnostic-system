using System;
using System.Windows;
using System.Windows.Controls;

namespace IsuzuDiagnostic.Desktop.Views
{
    public partial class DiagnosticDashboardView : UserControl
    {
        public event EventHandler? EndSessionRequested;
        public event EventHandler? LiveDataRequested;
        public event EventHandler? DtcListRequested;

        public DiagnosticDashboardView()
        {
            InitializeComponent();
        }

        private void EndSessionButton_Click(object sender, RoutedEventArgs e)
        {
            EndSessionRequested?.Invoke(this, EventArgs.Empty);
        }

        private void LiveDataButton_Click(object sender, RoutedEventArgs e)
        {
            LiveDataRequested?.Invoke(this, EventArgs.Empty);
        }

        private void ReadDtcsButton_Click(object sender, RoutedEventArgs e)
        {
            DtcListRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
