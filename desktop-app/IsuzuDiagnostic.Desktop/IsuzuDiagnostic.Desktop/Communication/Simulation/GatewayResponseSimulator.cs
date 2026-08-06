using System;
using System.Globalization;

using IsuzuDiagnostic.Desktop.Communication.Protocol;

namespace IsuzuDiagnostic.Desktop.Communication.Simulation;

public sealed class GatewayResponseSimulator
{
    private bool _isStreaming;

    public string CreateResponse(int requestId, GatewayCommand command)
    {
        if (requestId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(requestId), "The request identifier must be greater than zero");
        }

        string payload = command switch
        {
            GatewayCommand.Ping => "PONG",

            GatewayCommand.Start => StartStreaming(),

            GatewayCommand.Stop => StopStreaming(),

            GatewayCommand.Status => CreateStatusPayload(),

            _ => throw new ArgumentOutOfRangeException(nameof(command), command, "The gateway command is not supported")

        };

        return string.Join(GatewayProtocol.FieldSeparator, GatewayProtocol.ResponsePrefix, requestId.ToString(CultureInfo.InvariantCulture), "OK", payload);
    }

    private string StartStreaming()
    {
        _isStreaming = true;

        return "STREAMING";
    }

    private string StopStreaming()
    {
        _isStreaming = false;

        return "STOPPED";
    }

    private string CreateStatusPayload()
    {
        return _isStreaming ? "STATE=STREAMING" : "STATE=IDLE";
    }
}