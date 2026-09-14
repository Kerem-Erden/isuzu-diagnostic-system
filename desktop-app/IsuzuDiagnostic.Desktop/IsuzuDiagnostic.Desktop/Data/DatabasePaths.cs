using System;
using System.IO;

namespace IsuzuDiagnostic.Desktop.Data;

public static class DatabasePaths
{
    public static string DatabaseDirectory
    {
        get
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            return Path.Combine(localAppData, "IsuzuDiagnostic");
        }
    }

    public static string DatabaseFile => Path.Combine(DatabaseDirectory, "isuzu-diagnostic.db");

    public static string KnowledgeDatabaseFilePath =>
    Path.Combine(AppContext.BaseDirectory, "Data", "isuzu-knowledge.db");

}
