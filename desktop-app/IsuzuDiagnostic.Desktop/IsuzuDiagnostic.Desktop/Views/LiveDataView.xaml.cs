using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using IsuzuDiagnostic.Desktop.Communication.Protocol;
using IsuzuDiagnostic.Desktop.Communication.Serial;
using IsuzuDiagnostic.Desktop.Models;
using IsuzuDiagnostic.Desktop.Data;
using IsuzuDiagnostic.Desktop.Diagnostics;
namespace IsuzuDiagnostic.Desktop.Views;

public partial class LiveDataView : UserControl
{
    public sealed class Row
    {
        public string Key { get; init; } = "";
        public string Name { get; init; } = "";
        public string Unit { get; init; } = "";
        public string Value { get; set; } = "--";
        public string State { get; set; } = "No data";
        public string Received { get; set; } = "--";
        public DateTimeOffset LastAt { get; set; }
    }
    public event EventHandler? BackRequested;
    private readonly DiagnosticSession _session;
    private readonly SerialGatewayService _gateway;
    private readonly RequestIdGenerator _ids;
    private readonly DiagnosticAuditLog _audit;
    private readonly LiveDataRuleEvaluator _evaluator = new();
    private readonly Dictionary<string, LiveReferenceRule> _rules = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Row> _rows = [];
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private bool _ready, _starting, _restart;
    private CancellationTokenSource? _lifetime;
    private GatewayRequestClient? _client;
    private static string Normalize(string key) => key switch { "RPM" => "ENGINE_RPM", "COOLANT_TEMP" => "ENGINE_COOLANT_TEMPERATURE", "SPEED" => "VEHICLE_SPEED", _ => key };
    public LiveDataView(DiagnosticSession session, SerialGatewayService gateway, RequestIdGenerator ids, DiagnosticTroubleCode? related = null)
    {
        InitializeComponent(); _session = session; _gateway = gateway; _ids = ids;
        _audit = new(session.Id, gateway.SourceDescription);
        SourceText.Text = gateway.SourceDescription;
        _rows.AddRange(new[] {
            new Row { Key="ENGINE_RPM", Name="Engine RPM", Unit="rpm" },
            new Row { Key="ENGINE_COOLANT_TEMPERATURE", Name="Coolant temperature", Unit="°C" },
            new Row { Key="BATTERY_VOLTAGE", Name="Control module voltage", Unit="V" },
            new Row { Key="VEHICLE_SPEED", Name="Vehicle speed", Unit="km/h" },
            new Row { Key="ENGINE_LOAD", Name="Calculated engine load", Unit="%" }
        });
        if (related != null)
        {
            RelatedText.Text = $"Related to {related.Code}: unsupported parameters remain 'No data'.";
            var keys = related.RelatedLiveData.Select(x => Normalize(x.Parameter)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            _rows.RemoveAll(x => !keys.Contains(x.Key));
            foreach (var item in related.RelatedLiveData)
                if (!_rows.Any(x => x.Key == Normalize(item.Parameter))) _rows.Add(new Row {Key=Normalize(item.Parameter),Name=item.DisplayName,Unit=item.Unit});
        }
        ValuesGrid.ItemsSource = _rows;
        ConditionCombo.ItemsSource = new[] { "Not selected", "NON_OPERATIONAL_P_ROTATION", "TWO_PUMP_RELIEF" };
        ConditionCombo.SelectedIndex = 0;
        _ready = true; LoadRules();
        Loaded += async (_, _) =>
        {
            _lifetime = new(); _client = new(_gateway, _ids);
            _gateway.LineReceived += Receive;
            _gateway.Disconnected += Disconnected;
            _timer.Start();
            await StartStream();
        };
        Unloaded += (_, _) =>
        {
            _timer.Stop(); _gateway.LineReceived -= Receive; _gateway.Disconnected -= Disconnected;
            _lifetime?.Cancel(); _lifetime?.Dispose(); _lifetime = null;
            _client?.Dispose(); _client = null;
            _evaluator.Reset();
            try { if (_gateway.IsConnected) _gateway.SendLine(GatewayProtocol.CreateRequest(_ids.GetNext(), GatewayCommand.Stop)); } catch { }
        };
        _timer.Tick += async (_, _) =>
        {
            foreach (var row in _rows) if (row.LastAt != default && DateTimeOffset.UtcNow - row.LastAt > TimeSpan.FromSeconds(3)) { row.State="STALE"; row.Value="--"; }
            ValuesGrid.Items.Refresh();
            if (_restart && _session.State == DiagnosticSessionState.Connected) await StartStream();
        };
    }
    private async Task StartStream()
    {
        if (_starting || _client == null || _lifetime == null || !_gateway.IsConnected) return;
        _starting = true;
        try { string result = await _client.RequestAsync(GatewayCommand.Start, _lifetime.Token); if (result != "STREAMING") throw new FormatException(result); _restart = false; }
        catch (Exception ex) { LastReceivedTextBlock.Text = "Stream unavailable: " + ex.Message; }
        finally { _starting = false; }
    }
    private void Disconnected() => Dispatcher.InvokeAsync(() =>
    {
        _restart = true; _evaluator.Reset();
        foreach (var row in _rows) { row.State="DISCONNECTED"; row.Value="--"; }
        ValuesGrid.Items.Refresh(); LastReceivedTextBlock.Text="Disconnected — displayed values invalidated";
    });
    private void Receive(string line)
    {
        if (line.StartsWith("EVT|OBD_ERROR|", StringComparison.Ordinal)) { Dispatcher.InvokeAsync(() => LastReceivedTextBlock.Text = line); return; }
        if (!LiveDataParser.TryParse(line, out var message) || message == null) return;
        Dispatcher.InvokeAsync(() =>
        {
            if (!IsLoaded || _session.State != DiagnosticSessionState.Connected) return;
            var now = DateTimeOffset.UtcNow;
            string key = Normalize(message.Parameter);
            Row? row = _rows.FirstOrDefault(r => r.Key == key);
            if (row == null) return;
            row.Value = message.Value.ToString("0.##"); row.LastAt=now; row.Received=now.ToLocalTime().ToString("HH:mm:ss");
            row.State="No applicable rule";
            if (_rules.TryGetValue(key, out var rule))
            {
                var evaluation = _evaluator.Evaluate(rule, message.Value, now);
                row.State = evaluation.Severity.ToString().ToUpperInvariant();
                if (evaluation.StateChanged)
                    try { _audit.Write("WarningTransition", new { key, evaluation.Value, from = evaluation.PreviousSeverity.ToString(), to = evaluation.Severity.ToString(), condition = ConditionCombo.SelectedItem?.ToString() }); }
                    catch (Exception ex) { PolicyText.Text="Warning history could not be saved: " + ex.Message; }
            }
            LastReceivedTextBlock.Text = "Last received: " + row.Received;
            ValuesGrid.Items.Refresh();
        });
    }
    private void ConditionChanged(object sender, SelectionChangedEventArgs e) { if (_ready) LoadRules(); }
    private void LoadRules()
    {
        _rules.Clear(); _evaluator.Reset();
        if (_gateway.IsSimulation)
        {
            _rules["ENGINE_RPM"] = new("ENGINE_RPM", 1700, 1850, 1600, 2000, 10, TimeSpan.FromSeconds(2));
            _rules["ENGINE_COOLANT_TEMPERATURE"] = new("ENGINE_COOLANT_TEMPERATURE", 70, 85, 60, 100, 2, TimeSpan.FromSeconds(2));
            _rules["BATTERY_VOLTAGE"] = new("BATTERY_VOLTAGE", 27.8, 29, 27, 29.5, .1, TimeSpan.FromSeconds(2));
            PolicyText.Text="DEMO thresholds: illustrative only, not Isuzu service limits. Values cycle through normal/warning/critical/recovery.";
            ConditionCombo.IsEnabled = false;
            return;
        }
        PolicyText.Text="Select the actual operating condition. Reference coverage is limited to the mapped 4HK1 TM application; verify applicability. No matching reference means no assessment.";
        if (ConditionCombo.SelectedIndex <= 0) return;
        string? application = LiveDiagnosticApplicationResolver.Resolve(_session.Vehicle);
        if (application == null) return;
        try
        {
            var repository = new LiveReferenceRepository();
            foreach (var (key, warning, critical, hysteresis) in new[] { ("ENGINE_RPM",50d,150d,10d),("ENGINE_COOLANT_TEMPERATURE",3d,10d,1d),("BATTERY_VOLTAGE",.5,1d,.1) })
            {
                var reference = LiveReferenceSelector.Select(repository.FindByParameter(key, application), ConditionCombo.SelectedItem?.ToString() ?? "");
                if (reference?.MinimumValue == null || reference.MaximumValue == null) continue;
                _rules[key] = LiveReferenceRuleFactory.CreateFromRange(reference, warning, critical, hysteresis, TimeSpan.FromSeconds(2));
            }
            PolicyText.Text += " Warning/critical margins are application policies, not manufacturer alarm thresholds.";
        }
        catch (Exception ex) { PolicyText.Text="Reference database unavailable: " + ex.Message; }
    }
    private void BackButton_Click(object sender, RoutedEventArgs e) => BackRequested?.Invoke(this, EventArgs.Empty);
}
