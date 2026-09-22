using IsuzuDiagnostic.Desktop.Communication.Protocol;
using IsuzuDiagnostic.Desktop.Communication.Simulation;
using IsuzuDiagnostic.Desktop.Diagnostics;
using IsuzuDiagnostic.Desktop.Services;

static class Check
{
    public static void True(bool value, string name)
    {
        if (!value) throw new Exception("FAILED: " + name);
        Console.WriteLine("PASS: " + name);
    }
    public static async Task Throws<T>(Func<Task> action, string name) where T : Exception
    {
        try { await action(); }
        catch (T) { Console.WriteLine("PASS: " + name); return; }
        throw new Exception("FAILED: " + name);
    }
}

sealed class Audit : IDiagnosticAudit
{
    public List<string> Events { get; } = [];
    public string? FailOn { get; init; }
    public void Write(string kind, object payload)
    {
        if (kind == FailOn) throw new IOException("audit unavailable");
        Events.Add(kind);
    }
}

static class Program
{
    public static async Task Main()
    {
        ParserAndFramer();
        WarningStateMachine();
        await DtcWorkflowChecks();
        await DemoEndToEnd();
        Console.WriteLine("ALL DESKTOP CORE TESTS PASSED");
    }

    private static void ParserAndFramer()
    {
        Check.True(GatewayResponseParser.TryParse("RES|7|OK|PONG", out var r, out _) && r?.RequestId == 7, "correlated response parses");
        Check.True(!GatewayResponseParser.TryParse("RES|7|OK|A|B", out _, out _), "extra response field rejected");
        Check.True(!LiveDataParser.TryParse("LIVE:RPM:NaN", out _), "non-finite live value rejected");
        Check.True(LiveDataParser.TryParse("LIVE:RPM:1800", out var live) && live?.Value == 1800, "finite live value parses");
        var framer = new LineFramer();
        Check.True(framer.Feed("RES|1|OK|").Count == 0 && framer.Feed("PONG\r\n").Single() == "RES|1|OK|PONG", "split serial line reassembled");
        Check.True(framer.Feed(new string('X', 1025) + "REQ|9|CLEAR_DTC\n").Count == 0, "oversized line suffix discarded");
        Check.True(framer.Feed("SAFE\n").Single() == "SAFE", "framer recovers after oversized line");
    }

    private static void WarningStateMachine()
    {
        var evaluator = new LiveDataRuleEvaluator();
        var rule = new LiveReferenceRule("TEMP", 70, 85, 60, 100, 2, TimeSpan.FromSeconds(2));
        var t = DateTimeOffset.UtcNow;
        Check.True(evaluator.Evaluate(rule, 90, t).Severity == DiagnosticSeverity.Normal, "single spike not promoted");
        Check.True(evaluator.Evaluate(rule, 90, t.AddSeconds(1)).Severity == DiagnosticSeverity.Normal, "warning waits for confirmation");
        Check.True(evaluator.Evaluate(rule, 90, t.AddSeconds(2)).Severity == DiagnosticSeverity.Warning, "sustained warning promoted");
        Check.True(evaluator.Evaluate(rule, 84, t.AddSeconds(3)).Severity == DiagnosticSeverity.Warning, "hysteresis prevents chatter");
        Check.True(evaluator.Evaluate(rule, 82, t.AddSeconds(4)).Severity == DiagnosticSeverity.Warning, "recovery waits for confirmation");
        Check.True(evaluator.Evaluate(rule, 82, t.AddSeconds(7)).Severity == DiagnosticSeverity.Warning, "sample gap resets confirmation clock");
        Check.True(evaluator.Evaluate(rule, 82, t.AddSeconds(8)).Severity == DiagnosticSeverity.Warning, "fresh recovery accumulating");
        Check.True(evaluator.Evaluate(rule, 82, t.AddSeconds(9)).Severity == DiagnosticSeverity.Normal, "fresh recovery confirmed");
    }

    private static async Task DtcWorkflowChecks()
    {
        var audit = new Audit();
        var commands = new List<string>();
        var replies = new Queue<string>(["DTCS=P1093,P0087,P3FFF", "CLEARED", "DTCS=P0087"]);
        var flow = new DtcWorkflow((command, _) => { commands.Add(command); return Task.FromResult(replies.Dequeue()); }, audit);
        ClearResult result = await flow.ClearAsync(_ => Task.FromResult(true), CancellationToken.None);
        Check.True(result.RescanSucceeded && result.Codes.SequenceEqual(["P0087"]), "clear followed by verified rescan");
        Check.True(commands.SequenceEqual(["SCAN_DTC", "CLEAR_DTC", "SCAN_DTC"]), "safe clear command order");
        Check.True(audit.Events.IndexOf("DtcSnapshotBeforeClear") < audit.Events.IndexOf("DtcClearRequested"), "snapshot persisted before write");

        commands.Clear();
        var cancelled = new DtcWorkflow((command, _) => { commands.Add(command); return Task.FromResult("DTCS=P1093"); }, new Audit());
        ClearResult no = await cancelled.ClearAsync(_ => Task.FromResult(false), CancellationToken.None);
        Check.True(!no.Confirmed && commands.SequenceEqual(["SCAN_DTC"]), "user cancellation sends no clear");

        int call = 0;
        var uncertain = new DtcWorkflow((command, _) =>
        {
            call++;
            if (call == 1) return Task.FromResult("DTCS=P1093");
            if (call == 2) return Task.FromResult("CLEARED");
            throw new IOException("link lost");
        }, new Audit());
        ClearResult unknown = await uncertain.ClearAsync(_ => Task.FromResult(true), CancellationToken.None);
        Check.True(!unknown.RescanSucceeded && unknown.Codes.SequenceEqual(["P1093"]), "failed rescan preserves pre-clear snapshot");

        var auditFailure = new DtcWorkflow((_, _) => Task.FromResult("DTCS=P1093"), new Audit { FailOn = "DtcSnapshotBeforeClear" });
        await Check.Throws<IOException>(() => auditFailure.ClearAsync(_ => Task.FromResult(true), CancellationToken.None), "audit failure blocks clear");
        await Check.Throws<FormatException>(() => Task.FromResult(DtcWorkflow.ParseCodes("DTCS=P1093,BAD")), "malformed DTC list rejected");
        Check.True(DtcWorkflow.ParseCodes("DTCS=").Count == 0, "explicit empty DTC list accepted");
    }

    private static async Task DemoEndToEnd()
    {
        var demo = new DemoGateway();
        int id = 0;
        var audit = new Audit();
        var flow = new DtcWorkflow((command, _) =>
        {
            string responseLine = demo.Handle($"REQ|{++id}|{command}");
            if (!GatewayResponseParser.TryParse(responseLine, out var response, out _) || response is null || !response.IsSuccess)
                throw new IOException(responseLine);
            return Task.FromResult(response.Payload);
        }, audit);
        var before = await flow.ScanAsync(CancellationToken.None);
        Check.True(before.SequenceEqual(["P1093", "P0087", "P3FFF"]), "demo exposes known and unknown DTCs");
        var result = await flow.ClearAsync(_ => Task.FromResult(true), CancellationToken.None);
        Check.True(result.Codes.SequenceEqual(["P0087"]), "demo preserves persistent DTC after clear");
        Check.True(demo.Handle("REQ|99|START") == "RES|99|OK|STREAMING" && demo.Tick().Count == 5, "demo live stream produces five parameters");
    }
}
