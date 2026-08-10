using System;
using System.Windows;
using System.Windows.Controls;

using IsuzuDiagnostic.Desktop.Models;

namespace IsuzuDiagnostic.Desktop.Views
{
    public partial class VehicleInformationView : UserControl
    {
        public event EventHandler? BackRequested;

        private readonly DiagnosticSession _session;

        public VehicleInformationView(DiagnosticSession session)
        {
            InitializeComponent();

            _session = session ?? throw new ArgumentNullException(nameof(session));

            DataContext = _session;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            BackRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
