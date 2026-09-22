using IsuzuDiagnostic.Desktop.Data;
using IsuzuDiagnostic.Desktop.Models;
using System.Windows;

namespace IsuzuDiagnostic.Desktop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            DatabaseInitializer.Initialize();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Application data could not be opened.\n\n" + ex.Message, "Startup error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        base.OnStartup(e);
    }
}
