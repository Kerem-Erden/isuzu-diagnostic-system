using System.Text.RegularExpressions;
namespace IsuzuDiagnostic.Desktop.Services;

public interface IDiagnosticAudit
{
    void Write(string kind, object payload);
}
public sealed record ClearResult(bool Confirmed, bool RescanSucceeded, IReadOnlyList<string> Codes, string Message);

// One instance per session: serializes scan/clear and never retries a write.
public sealed class DtcWorkflow
{
    private readonly Func<string, CancellationToken, Task<string>> _request;
    private readonly IDiagnosticAudit _audit;
    private readonly SemaphoreSlim _gate = new(1, 1);
    public DtcWorkflow(Func<string, CancellationToken, Task<string>> request, IDiagnosticAudit audit)
    { _request = request; _audit = audit; }
    public static IReadOnlyList<string> ParseCodes(string payload)
    {
        if (!payload.StartsWith("DTCS=", StringComparison.Ordinal)) throw new FormatException("Incomplete DTC response.");
        string text = payload[5..];
        if (text.Length == 0) return [];
        string[] codes = text.Split(',');
        if (codes.Length > 32 || codes.Any(c => !Regex.IsMatch(c, @"\A[PCBU][0-3][0-9A-F]{3}\z"))) throw new FormatException("Invalid DTC response.");
        return codes.Distinct(StringComparer.Ordinal).ToArray();
    }
    private async Task<IReadOnlyList<string>> ScanCore(CancellationToken token)
    {
        var codes = ParseCodes(await _request("SCAN_DTC", token));
        _audit.Write("DtcScan", new { codes });
        return codes;
    }
    public async Task<IReadOnlyList<string>> ScanAsync(CancellationToken token)
    {
        await _gate.WaitAsync(token);
        try { return await ScanCore(token); }
        finally { _gate.Release(); }
    }
    public async Task<ClearResult> ClearAsync(Func<IReadOnlyList<string>, Task<bool>> confirm, CancellationToken token)
    {
        await _gate.WaitAsync(token);
        try
        {
            var before = await ScanCore(token);
            // Durable evidence must exist before confirmation and before ECU write.
            _audit.Write("DtcSnapshotBeforeClear", new { codes = before });
            if (!await confirm(before)) return new(false, false, before, "Clear cancelled; no clear command sent.");
            token.ThrowIfCancellationRequested();
            _audit.Write("DtcClearRequested", new { codes = before });
            try
            {
                string response = await _request("CLEAR_DTC", token);
                if (response != "CLEARED") throw new FormatException("Clear acknowledgement missing.");
                _audit.Write("DtcClearAcknowledged", new { });
                await Task.Delay(500, token);
                var after = await ScanCore(token);
                _audit.Write("DtcClearRescan", new { before, after });
                return new(true, true, after, $"Clear acknowledged. {after.Count} stored code(s) returned on rescan. This does not establish active-fault status.");
            }
            catch (Exception ex)
            {
                // Do not turn timeout, disconnect or failed rescan into an empty list.
                try { _audit.Write("DtcClearUnverified", new { error = ex.Message, before }); } catch { /* the durable pre-clear snapshot remains */ }
                return new(true, false, before, "Clear/rescan not verified. Showing the PRE-CLEAR snapshot. Do not assume codes were erased. " + ex.Message);
            }
        }
        finally { _gate.Release(); }
    }
}
