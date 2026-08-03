using System;
using System.Windows;
using System.Windows.Controls;


namespace IsuzuDiagnostic.Desktop.Views
{
    public partial class DeveloperConsoleView : UserControl
    {
        public event EventHandler? BackRequested;

        public DeveloperConsoleView()
        {
            InitializeComponent();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            BackRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
