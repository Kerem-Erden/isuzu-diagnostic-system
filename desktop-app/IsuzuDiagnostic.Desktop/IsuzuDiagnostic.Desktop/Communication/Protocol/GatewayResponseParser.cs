using System;
using System.Globalization;

namespace IsuzuDiagnostic.Desktop.Communication.Protocol;

public static class GatewayResponseParser
{
    private const string SuccessStatus = "OK";
    private const string ErrorStatus = "ERR";

    public static bool TryParse(
        string? line,
        out GatewayResponse? response,
        out string errorMessage
    )
    {
        response = null;
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(line))
        {
            errorMessage = "The gateway response is empty.";
            return false;
        }

        string normalizedLine = line.Trim();

        string[] fields = normalizedLine.Split(
            GatewayProtocol.FieldSeparator,
            4,
            StringSplitOptions.None
        );

        if (fields.Length != 4)
        {
            errorMessage =
                "The gateway response must contain exactly four fields.";

            return false;
        }

        if (!string.Equals(
                fields[0],
                GatewayProtocol.ResponsePrefix,
                StringComparison.Ordinal
            ))
        {
            errorMessage =
                "The message is not a gateway response.";

            return false;
        }

        if (!int.TryParse(
                fields[1],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int requestId
            ) ||
            requestId <= 0)
        {
            errorMessage =
                "The response contains an invalid request identifier.";

            return false;
        }

        if (!TryParseStatus(
                fields[2],
                out GatewayResponseStatus status
            ))
        {
            errorMessage =
                $"The response contains an unsupported status value: '{fields[2]}'.";

            return false;
        }

        response = new GatewayResponse(
            requestId,
            status,
            fields[3],
            normalizedLine
        );

        return true;
    }

    private static bool TryParseStatus(
        string statusText,
        out GatewayResponseStatus status
    )
    {
        string normalizedStatus =
            statusText.Trim().ToUpperInvariant();

        switch (normalizedStatus)
        {
            case SuccessStatus:
                status = GatewayResponseStatus.Ok;
                return true;

            case ErrorStatus:
                status = GatewayResponseStatus.Error;
                return true;

            default:
                status = default;
                return false;
        }
    }
}