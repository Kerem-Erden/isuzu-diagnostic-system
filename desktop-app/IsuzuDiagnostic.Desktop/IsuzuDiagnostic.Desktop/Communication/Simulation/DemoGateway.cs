using System.Globalization;
namespace IsuzuDiagnostic.Desktop.Communication.Simulation;

public sealed class DemoGateway
{
    private bool _streaming;
    private bool _cleared;
    private int _sample;
    public string Handle(string request)
    {
        string[] p = request.Split('|');
        if (p.Length != 3 || p[0] != "REQ" || !int.TryParse(p[1], NumberStyles.None, CultureInfo.InvariantCulture, out int id) || id <= 0)
            return "SYS:INVALID_REQUEST";
        string status = "OK";
        string payload;
        switch (p[2])
        {
            case "PING": payload = "PONG"; break;
            case "INFO": payload = "SOURCE=SIMULATION;ECU=DEMO;CLEAR=1"; break;
            case "START": _streaming = true; payload = "STREAMING"; break;
            case "STOP": _streaming = false; payload = "STOPPED"; break;
            case "STATUS": payload = _streaming ? "STATE=STREAMING" : "STATE=IDLE"; break;
            case "SCAN_DTC": payload = _cleared ? "DTCS=P0087" : "DTCS=P1093,P0087,P3FFF"; break;
            case "CLEAR_DTC": _cleared = true; payload = "CLEARED"; break;
            default: status = "ERR"; payload = "UNKNOWN_COMMAND"; break;
        }
        return $"RES|{id}|{status}|{payload}";
    }
    public IReadOnlyList<string> Tick()
    {
        if (!_streaming) return [];
        int phase = (_sample++ / 6) % 4;
        double rpm = new[] {1800d, 1900d, 2050d, 1800d}[phase];
        double temp = new[] {82d, 90d, 105d, 82d}[phase];
        double volts = new[] {28.4, 29.2, 30.1, 28.4}[phase];
        return new[] { $"LIVE:RPM:{rpm.ToString(CultureInfo.InvariantCulture)}", $"LIVE:COOLANT_TEMP:{temp.ToString(CultureInfo.InvariantCulture)}", $"LIVE:BATTERY_VOLTAGE:{volts.ToString(CultureInfo.InvariantCulture)}", "LIVE:SPEED:0", "LIVE:ENGINE_LOAD:20" };
    }
}
