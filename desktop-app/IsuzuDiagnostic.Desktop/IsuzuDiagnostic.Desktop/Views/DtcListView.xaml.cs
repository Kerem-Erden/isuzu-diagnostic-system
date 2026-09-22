using System.Windows;
using System.Windows.Controls;
using IsuzuDiagnostic.Desktop.Communication.Serial;
using IsuzuDiagnostic.Desktop.Data;
using IsuzuDiagnostic.Desktop.Models;
using IsuzuDiagnostic.Desktop.Services;
namespace IsuzuDiagnostic.Desktop.Views;

public partial class DtcListView : UserControl
{
    private readonly DtcWorkflow _workflow;
    private readonly SerialGatewayService _gateway;
    private readonly DtcKnowledgeRepository _knowledge = new();
    private CancellationTokenSource? _lifetime;
    private bool _busy;
    public event EventHandler? BackRequested;
    public event Action<DiagnosticTroubleCode>? DtcDetailsRequested;
    public DtcListView(DtcWorkflow workflow, SerialGatewayService gateway)
    {
        InitializeComponent(); _workflow = workflow; _gateway = gateway;
        SourceText.Text = gateway.SourceDescription;
        Loaded += async (_, _) => { _lifetime = new(); await Scan(); };
        Unloaded += (_, _) => { _lifetime?.Cancel(); _lifetime?.Dispose(); _lifetime = null; };
    }
    private void Busy(bool value)
    {
        _busy = value; RescanButton.IsEnabled = BackButton.IsEnabled = DtcItemsControl.IsEnabled = !value;
        ClearButton.IsEnabled = !value && _gateway.CanClear && _gateway.IsConnected;
    }
    private async Task Scan()
    {
        if (_busy || _lifetime == null) return;
        var token = _lifetime.Token;
        Busy(true); StatusText.Text = "Reading stored codes…";
        try { var codes = await _workflow.ScanAsync(token); if (!token.IsCancellationRequested) { ShowCodes(codes); StatusText.Text = $"{codes.Count} stored code(s)."; } }
        catch (Exception ex) { StatusText.Text = "Scan failed; previous results are not a current scan. " + ex.Message; }
        finally { Busy(false); }
    }
    private void ShowCodes(IReadOnlyList<string> codes)
    {
        var entries = new List<DiagnosticTroubleCode>();
        foreach (string code in codes)
        {
            var details = _knowledge.FindByCode(code);
            entries.Add(details == null
                ? new DiagnosticTroubleCode(code, "Unknown DTC", "Stored", Array.Empty<DtcCause>(), Array.Empty<DiagnosticStep>(), Array.Empty<RelatedLiveDataItem>(), Array.Empty<DtcSolution>())
                : DtcKnowledgeMapper.ToDiagnosticTroubleCode(details, "Stored"));
        }
        DtcItemsControl.ItemsSource = entries;
    }
    private async void RescanButton_Click(object sender, RoutedEventArgs e) => await Scan();
    private async void ClearDtcMemoryButton_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _lifetime == null || !_gateway.CanClear || !_gateway.IsConnected) return;
        var token = _lifetime.Token;
        Busy(true); StatusText.Text = "Saving a fresh pre-clear scan…";
        try
        {
            var result = await _workflow.ClearAsync(codes =>
            {
                if (token.IsCancellationRequested) return Task.FromResult(false);
                string effect = _gateway.IsSimulation ? "SIMULATION: no vehicle will be changed." : "VEHICLE: Mode 04 will erase emission-related diagnostic information, including freeze-frame/readiness data. Engine must be stopped and vehicle stationary.";
                return Task.FromResult(MessageBox.Show($"{effect}\n\n{codes.Count} stored code(s) have been saved locally.\nSend clear once, then rescan?", "Confirm DTC clear", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes);
            }, token);
            if (!token.IsCancellationRequested) { ShowCodes(result.Codes); StatusText.Text = result.Message; }
        }
        catch (Exception ex) { StatusText.Text = "Clear not started: " + ex.Message; }
        finally { Busy(false); }
    }
    private void DetailsButton_Click(object sender, RoutedEventArgs e)
    { if (!_busy && sender is Button { Tag: DiagnosticTroubleCode dtc }) DtcDetailsRequested?.Invoke(dtc); }
    private void BackButton_Click(object sender, RoutedEventArgs e) { if (!_busy) BackRequested?.Invoke(this, EventArgs.Empty); }
    private void HistoryButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var window = new Window { Title = "Diagnostic history — last 100 events", Width = 900, Height = 600, Owner = Window.GetWindow(this), Content = new TextBox { Text = DiagnosticAuditLog.ReadRecent(), IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } };
            window.ShowDialog();
        }
        catch (Exception ex) { MessageBox.Show("History unavailable: " + ex.Message); }
    }
}
