using IsuzuDiagnostic.Desktop.Data;
using IsuzuDiagnostic.Desktop.Models;
using System.Windows;

namespace IsuzuDiagnostic.Desktop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DatabaseInitializer.Initialize();

        base.OnStartup(e);
    }
}

