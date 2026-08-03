using System;
using System.Windows;
using System.Windows.Controls;

namespace IsuzuDiagnostic.Desktop.Views
{
    public partial class DtcListView : UserControl
    {
        public event EventHandler? BackRequested;

        public event Action<string>? DtcDetailsRequested;

        public DtcListView()
        {
            InitializeComponent();
        }

        private void DetailsButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string dtcCode)
            {
                DtcDetailsRequested?.Invoke(dtcCode);
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            BackRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
