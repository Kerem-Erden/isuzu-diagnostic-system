using System;

namespace IsuzuDiagnostic.Desktop.Communication.Protocol;

public sealed class GatewayResponse
{
    private const string SuccessStatus = "OK";
    private const string ErrorStatus = "ERR";

    public int RequestId { get; }
    public GatewayResponseStatus Status { get; }
    public string Payload { get; }
    public string RawMessage { get; }
    public bool IsSuccess => Status == GatewayResponseStatus.Ok;

    public GatewayResponse(int requestId, GatewayResponseStatus status, string payload, string rawMessage)
    {
        if (requestId < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(requestId), "The request identifier must be greater than zero");
        }

        RequestId = requestId;
        Status = status;
        Payload = payload ?? throw new ArgumentNullException(nameof(payload));
        RawMessage = rawMessage ?? throw new ArgumentNullException(nameof(rawMessage));
    }
}
