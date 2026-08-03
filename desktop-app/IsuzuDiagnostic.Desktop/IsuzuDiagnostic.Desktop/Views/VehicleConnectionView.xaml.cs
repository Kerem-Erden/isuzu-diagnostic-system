using System;
using System.Windows;
using System.Windows.Controls;

namespace IsuzuDiagnostic.Desktop.Views
{
    public partial class VehicleConnectionView : UserControl
    {
        public event EventHandler? ContinueRequested;
        public VehicleConnectionView()
        {
            InitializeComponent();
        }

        private void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            ContinueRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
