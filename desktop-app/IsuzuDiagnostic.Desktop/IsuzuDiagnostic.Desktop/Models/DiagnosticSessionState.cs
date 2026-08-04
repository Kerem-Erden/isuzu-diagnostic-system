using System;

namespace IsuzuDiagnostic.Desktop.Models;

public enum DiagnosticSessionState
{
    Created,
    Connecting,
    Connected,
    Disconnected,
    Faulted
}

