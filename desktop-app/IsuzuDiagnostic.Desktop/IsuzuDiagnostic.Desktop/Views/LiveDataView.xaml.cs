using System;
using System.Windows;
using System.Windows.Controls;
using IsuzuDiagnostic.Desktop.Communication.Protocol;
using IsuzuDiagnostic.Desktop.Communication.Serial;
using IsuzuDiagnostic.Desktop.Models;
using IsuzuDiagnostic.Desktop.Data;
using IsuzuDiagnostic.Desktop.Diagnostics;
using System.Diagnostics;

namespace IsuzuDiagnostic.Desktop.Views
{
    public partial class LiveDataView : UserControl
    {
        public event EventHandler? BackRequested;

        private readonly DiagnosticSession _session;

        private readonly SerialGatewayService _serialGatewayService;

        private readonly RequestIdGenerator _requestIdGenerator;

        private readonly LiveReferenceRepository _liveReferenceRepository = new();

        private readonly LiveDataRuleEvaluator _liveDataRuleEvaluator = new();

        private readonly Dictionary<string, LiveReferenceRule> _diagnosticRules = new(StringComparer.OrdinalIgnoreCase);

        private readonly LiveDiagnosticContext _liveDiagnosticContext = new();

        public LiveDataView(DiagnosticSession session, SerialGatewayService serialGatewayService, RequestIdGenerator requestIdGenerator)
        {
            InitializeComponent();

            _session = session?? throw new ArgumentNullException(nameof(session));

            _serialGatewayService = serialGatewayService?? throw new ArgumentNullException(nameof(serialGatewayService));

            _requestIdGenerator = requestIdGenerator ?? throw new ArgumentNullException(nameof(requestIdGenerator));

            DataContext = _session;

            LoadDiagnosticRules();

            Loaded += LiveDataView_Loaded;

            Unloaded += LiveDataView_Unloaded;


        }

        private void LiveDataView_Loaded(object sender, RoutedEventArgs e)
        {
            _serialGatewayService.LineReceived -= SerialGatewayService_LineReceived;

            _serialGatewayService.LineReceived += SerialGatewayService_LineReceived;

            if (!_serialGatewayService.IsConnected)
            {
                return;
            }

            int requestId = _requestIdGenerator.GetNext();

            string request = GatewayProtocol.CreateRequest(requestId, GatewayCommand.Start);

            _serialGatewayService.SendLine(request); 

        }


        private void SerialGatewayService_LineReceived(string line)
        {
            if (!LiveDataParser.TryParse(line, out LiveDataMessage? message))
            {
                return;
            }

            if (message is null)
            {
                return;
            }

            LiveDataEvaluation? evaluation = null;

            if (_diagnosticRules.TryGetValue(message.Parameter, out LiveReferenceRule? rule))
            {
                evaluation = _liveDataRuleEvaluator.Evaluate(rule, message.Value, DateTimeOffset.UtcNow);
            }

            if (evaluation is { StateChanged: true})
            {
                Debug.WriteLine(
                    $"LIVE_DIAG:{evaluation.ParameterKey}:" +
                    $"{evaluation.PreviousSeverity}->{evaluation.Severity}" +
                    $":VALUE={evaluation.Value}");
            }

            Dispatcher.InvokeAsync(() =>
            {
                LastReceivedTextBlock.Text = $"Last received: {DateTimeOffset.Now:HH:mm:ss}";

                switch (message.Parameter)
                {
                    case "RPM":
                        RpmValueTextBlock.Text = message.Value.ToString("0");
                        break;

                    case "COOLANT_TEMP":
                        CoolantValueTextBlock.Text = message.Value.ToString("0");
                        break;

                    case "BATTERY_VOLTAGE":
                        BatteryValueTextBlock.Text = message.Value.ToString("0.0");
                        break;
                }
            });
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            BackRequested?.Invoke(this, EventArgs.Empty);
        }

        private void LiveDataView_Unloaded(object sender, RoutedEventArgs e)
        {
            _serialGatewayService.LineReceived -= SerialGatewayService_LineReceived;

            _liveDataRuleEvaluator.Reset();

            if (!_serialGatewayService.IsConnected)
            {
                return;
            }

            int requestId = _requestIdGenerator.GetNext();

            string request = GatewayProtocol.CreateRequest(requestId, GatewayCommand.Stop);

            _serialGatewayService.SendLine(request);

        }

        private void SetOperatingCondition(string? operatingConditionKey)
        {
            _liveDiagnosticContext.SetOperatingCondition(operatingConditionKey);

            _diagnosticRules.Clear();
            _liveDataRuleEvaluator.Reset();

            LoadDiagnosticRules();
        }

        private void LoadDiagnosticRules()
        {
            TryLoadRule("RPM");
            TryLoadRule("COOLANT_TEMP");
            TryLoadRule("BATTERY_VOLTAGE");
        }

        private void TryLoadRule(string parameterKey)
        {
            string? applicationKey = LiveDiagnosticApplicationResolver.Resolve(_session.Vehicle);

            if (applicationKey is null)
            {
                return;
            }

            IReadOnlyList<LiveReferenceValue> references = _liveReferenceRepository.FindByParameter(parameterKey, applicationKey);

            LiveReferenceValue? reference = LiveReferenceSelector.Select(references, _liveDiagnosticContext);

            if (reference is null)
            {
                return;
            }

            LiveDiagnosticPolicy? policy = GetPolicy(parameterKey);

            if (policy is null)
            {
                return;
            }

            LiveReferenceRule rule = LiveReferenceRuleFactory.CreateFromRange(
                reference,
                policy.WarningMargin,
                policy.CriticalMargin,
                policy.Hysteresis,
                policy.ConfirmationDuration);

            _diagnosticRules[parameterKey] = rule;
        }

        private static LiveDiagnosticPolicy? GetPolicy(string parameterKey)
        {
            return parameterKey switch
            {
                "BATTERY_VOLTAGE" => new LiveDiagnosticPolicy("BATTERY_VOLTAGE", warningMargin: 0.5, criticalMargin: 1.0, hysteresis: 0.1, confirmationDuration: TimeSpan.FromSeconds(2)),

                _ => null
            };
        }
    }
}
