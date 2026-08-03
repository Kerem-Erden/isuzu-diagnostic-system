using System;
using System.Windows;
using System.Windows.Controls;

namespace IsuzuDiagnostic.Desktop.Views
{
    public partial class LiveDataView : UserControl
    {
        public event EventHandler? BackRequested;

        public LiveDataView()
        {
            InitializeComponent();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            BackRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
