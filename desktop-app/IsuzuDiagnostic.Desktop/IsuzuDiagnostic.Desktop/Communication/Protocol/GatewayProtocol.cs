using System;
using System.Globalization;

namespace IsuzuDiagnostic.Desktop.Communication.Protocol;

public static class GatewayProtocol
{
    public const char FieldSeparator = '|';
    public const string RequestPrefix = "REQ";
    public const string ResponsePrefix = "RES";
    public const string EventPrefix = "EVT";

    public static string CreateRequest(int requestId, GatewayCommand command)
    {
        if (requestId <= 0)
        {
           throw new ArgumentOutOfRangeException(nameof(requestId), "Request ID must be greater than zero.");
        }

        string commandText = ConvertCommandToText(command);

        return string.Join(FieldSeparator, RequestPrefix, requestId.ToString(CultureInfo.InvariantCulture), commandText);
    }

    private static string ConvertCommandToText(GatewayCommand command)
    {
        return command switch
        {
            GatewayCommand.Ping => "PING",
            GatewayCommand.Start => "START",
            GatewayCommand.Stop => "STOP",
            GatewayCommand.Status => "STATUS",

            _ => throw new ArgumentOutOfRangeException(nameof(command), command, "The gateway command is not supported.")
        };
    }
}