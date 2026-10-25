namespace IsuzuDiagnostic.Desktop.Communication.Protocol;

public sealed class GatewayDiagnosticTrace
{
    private readonly Queue<string> _lines = new();
    private readonly object _sync = new();

    public void Record(string direction, string line)
    {
        // Continuous live samples must not evict the scan evidence.
        if (line.StartsWith("LIVE:", StringComparison.Ordinal)) return;
        string entry = $"{DateTimeOffset.UtcNow:O} {direction} {line[..Math.Min(line.Length, 1024)].Replace('\r', ' ').Replace('\n', ' ')}";
        lock (_sync)
        {
            if (_lines.Count == 256) _lines.Dequeue();
            _lines.Enqueue(entry);
        }
    }

    public string Snapshot()
    {
        lock (_sync) return string.Join(Environment.NewLine, _lines);
    }
}
