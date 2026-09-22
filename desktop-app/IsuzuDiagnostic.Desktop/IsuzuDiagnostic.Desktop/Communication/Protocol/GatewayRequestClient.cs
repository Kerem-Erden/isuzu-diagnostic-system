using System.Collections.Concurrent;
using System.IO;
using IsuzuDiagnostic.Desktop.Communication.Serial;
namespace IsuzuDiagnostic.Desktop.Communication.Protocol;

public sealed class GatewayRequestClient : IDisposable
{
    private readonly SerialGatewayService _transport;
    private readonly RequestIdGenerator _ids;
    private readonly ConcurrentDictionary<int, TaskCompletionSource<string>> _pending = new();
    public GatewayRequestClient(SerialGatewayService transport, RequestIdGenerator ids)
    {
        _transport = transport; _ids = ids;
        transport.LineReceived += Receive;
        transport.Disconnected += Disconnect;
        transport.CommunicationError += Error;
    }
    public async Task<string> RequestAsync(GatewayCommand command, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        int id = _ids.GetNext();
        var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pending.TryAdd(id, completion)) throw new InvalidOperationException("Duplicate request ID.");
        try
        {
            await Task.Run(() => { token.ThrowIfCancellationRequested(); _transport.SendLine(GatewayProtocol.CreateRequest(id, command)); }, token);
            return await completion.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
        }
        finally { _pending.TryRemove(id, out _); }
    }
    private void Receive(string line)
    {
        if (!GatewayResponseParser.TryParse(line, out var response, out _) || response is null || !_pending.TryGetValue(response.RequestId, out var completion)) return;
        if (response.IsSuccess) completion.TrySetResult(response.Payload);
        else completion.TrySetException(new IOException("Gateway: " + response.Payload));
    }
    private void Disconnect() => Error("Gateway disconnected. Any in-flight clear has an unknown outcome; rescan before retrying.");
    private void Error(string error)
    {
        foreach (var completion in _pending.Values) completion.TrySetException(new IOException(error));
    }
    public void Dispose()
    {
        _transport.LineReceived -= Receive;
        _transport.Disconnected -= Disconnect;
        _transport.CommunicationError -= Error;
        Disconnect();
    }
}
