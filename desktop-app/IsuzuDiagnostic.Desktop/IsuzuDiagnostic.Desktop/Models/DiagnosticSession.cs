using System;

namespace IsuzuDiagnostic.Desktop.Models;

public sealed class DiagnosticSession
{
    public VehicleProfile Vehicle { get; }
    
    public string SerialPortName { get; }

    public DateTimeOffset StartedAt { get; }

    public DiagnosticSessionState State { get; private set; }

    public DiagnosticSession(VehicleProfile vehicle, string serialPortName)
    {
        Vehicle = vehicle ?? throw new ArgumentNullException(nameof(vehicle));

        if (string.IsNullOrWhiteSpace(serialPortName))
        {
            throw new ArgumentException("Serial port must be selected.", nameof(serialPortName));
        }

        SerialPortName = serialPortName.Trim();
        StartedAt = DateTimeOffset.Now;
        State = DiagnosticSessionState.Created;
    }

    public void MarkConnecting()
    {
        State = DiagnosticSessionState.Connecting;
    }

    public void MarkConnected()
    {
        State = DiagnosticSessionState.Connected;
    }

    public void MarkFaulted()
    {
        State = DiagnosticSessionState.Faulted;
    }

    public void End()
    {
        State = DiagnosticSessionState.Disconnected;
    }
}
