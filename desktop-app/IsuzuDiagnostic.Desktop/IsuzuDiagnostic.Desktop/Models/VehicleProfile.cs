using System;
using System.Runtime.InteropServices;
using System.Windows.Navigation;

namespace IsuzuDiagnostic.Desktop.Models;

public sealed class VehicleProfile
{
    public string Manufacturer { get; }
    
    public string Model { get; }

    public int ModelYear { get; }

    public string EngineCode { get; }

    public  string EcuType { get; }

    public VehicleProfile (string manufacturer, string model, int modelYear, string engineCode, string ecuType)
    {
        Manufacturer = RequireValue(manufacturer, nameof(manufacturer));
        Model = RequireValue(model, nameof(model));
        
        if (modelYear < 1900 || modelYear > DateTime.Now.Year + 1)
        {
            throw new ArgumentOutOfRangeException(nameof(modelYear), "Model year must be between 1900 and next year.");
        }

        ModelYear = modelYear;

        EngineCode = RequireValue(engineCode, nameof(engineCode));
        EcuType = RequireValue(ecuType, nameof(ecuType));
    }

    public string DisplayName => $"{Manufacturer} {Model} - {EngineCode} - {EcuType}";

    private static string RequireValue(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
           throw new ArgumentException("A required vehicle-profile value is missing.", parameterName); 
        }
        
        return value.Trim();
    }

    


}
