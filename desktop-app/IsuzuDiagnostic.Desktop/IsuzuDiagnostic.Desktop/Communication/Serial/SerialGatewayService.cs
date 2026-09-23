using System.IO;
using System.IO.Ports;
using System.Text;
using IsuzuDiagnostic.Desktop.Communication.Protocol;
using IsuzuDiagnostic.Desktop.Communication.Simulation;
namespace IsuzuDiagnostic.Desktop.Communication.Serial;

public sealed class SerialGatewayService : IDisposable
{
    public const string DemoPort = "DEMO (no vehicle)";
    private readonly object _syncRoot = new();
    private readonly LineFramer _framer = new();
    private SerialPort? _serialPort;
    private DemoGateway? _demo;
    private System.Threading.Timer? _demoTimer;
    private Task _portCloseTask = Task.CompletedTask;
    public string Source { get; private set; } = "UNKNOWN";
    public string Ecu { get; private set; } = "UNKNOWN";
    public bool CanClear { get; private set; }
    public bool IsSimulation => Source == "SIMULATION";
    public string SourceDescription => IsSimulation ? "SIMULATION — no vehicle data" : $"{Source} — ECU {Ecu}";
    public event Action<string>? LineReceived;
    public event Action<string>? CommunicationError;
    public event Action? Disconnected;
    public bool IsConnected { get { lock (_syncRoot) return _demo != null || _serialPort?.IsOpen == true; } }
    public string? ConnectedPortName { get { lock (_syncRoot) return _demo != null ? DemoPort : _serialPort?.PortName; } }
    public static IReadOnlyList<string> GetAvailablePortNames() => SerialPort.GetPortNames().OrderBy(x => x, StringComparer.OrdinalIgnoreCase).Append(DemoPort).ToArray();
    public void ApplyInfo(string info)
    {
        string[] fields = info.Split(';');
        if (fields.Length != 3 || (fields[0] != "SOURCE=SIMULATION" && fields[0] != "SOURCE=VEHICLE") || !fields[1].StartsWith("ECU=", StringComparison.Ordinal) || (fields[2] != "CLEAR=0" && fields[2] != "CLEAR=1"))
            throw new FormatException("Gateway source/capabilities could not be verified. Update firmware.");
        Source = fields[0][7..]; Ecu = fields[1][4..]; CanClear = fields[2] == "CLEAR=1";
    }
    public void Connect(string portName, int baudRate = 115200)
    {
        if (string.IsNullOrWhiteSpace(portName) || baudRate <= 0) throw new ArgumentException("Port and baud rate are required.");
        lock (_syncRoot)
        {
            if (IsConnected) throw new InvalidOperationException("Already connected.");
            if (!_portCloseTask.IsCompleted)
                throw new IOException("The previous serial connection is still closing. Wait a moment and try again.");
            _framer.Reset(); Source = "UNKNOWN"; Ecu = "UNKNOWN"; CanClear = false;
            if (portName == DemoPort)
            {
                var demo = new DemoGateway(); _demo = demo;
                _demoTimer = new System.Threading.Timer(_ =>
                {
                    IReadOnlyList<string> lines;
                    lock (_syncRoot) { if (_demo != demo) return; lines = demo.Tick(); }
                    foreach (string line in lines) { lock (_syncRoot) { if (_demo != demo) return; } LineReceived?.Invoke(line); }
                }, null, 1000, 1000);
                return;
            }
            var port = new SerialPort(portName.Trim(), baudRate, Parity.None, 8, StopBits.One)
            { NewLine = "\n", Encoding = Encoding.ASCII, ReadTimeout = 500, WriteTimeout = 500, DtrEnable = false, RtsEnable = false };
            port.DataReceived += Receive;
            port.ErrorReceived += Error;
            try { port.Open(); _serialPort = port; }
            catch { port.DataReceived -= Receive; port.ErrorReceived -= Error; port.Dispose(); throw; }
        }
    }
    public void SendLine(string message)
    {
        if (string.IsNullOrWhiteSpace(message) || message.Contains('\n') || message.Contains('\r')) throw new ArgumentException("One nonempty protocol line required.");
        string? demoResponse = null;
        lock (_syncRoot)
        {
            if (_demo != null) demoResponse = _demo.Handle(message);
            else
            {
                if (_serialPort?.IsOpen != true) throw new InvalidOperationException("Gateway disconnected.");
                _serialPort.WriteLine(message);
            }
        }
        if (demoResponse != null) LineReceived?.Invoke(demoResponse);
    }
    private void Receive(object sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            IReadOnlyList<string> lines;
            lock (_syncRoot)
            {
                if (sender is not SerialPort port || !ReferenceEquals(port, _serialPort)) return;
                lines = _framer.Feed(port.ReadExisting());
            }
            foreach (string line in lines) LineReceived?.Invoke(line);
        }
        catch (Exception ex) { lock (_syncRoot) { if (!ReferenceEquals(sender, _serialPort)) return; } CommunicationError?.Invoke(ex.Message); }
    }
    private void Error(object sender, SerialErrorReceivedEventArgs e)
    {
        lock (_syncRoot) { if (!ReferenceEquals(sender, _serialPort)) return; }
        CommunicationError?.Invoke($"Serial error: {e.EventType}");
    }
    public void Disconnect()
    {
        SerialPort? port;
        System.Threading.Timer? timer;
        bool hadConnection;
        TaskCompletionSource<bool>? closeCompletion = null;
        lock (_syncRoot)
        {
            port = _serialPort;
            timer = _demoTimer;
            hadConnection = port != null || timer != null || _demo != null;
            _serialPort = null;
            _demoTimer = null;
            _demo = null;
            _framer.Reset(); CanClear = false;
            if (port != null) { port.DataReceived -= Receive; port.ErrorReceived -= Error; }
            if (port != null)
            {
                closeCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
                _portCloseTask = closeCompletion.Task;
            }
        }
        if (hadConnection) Disconnected?.Invoke();
        timer?.Dispose();
        // SerialPort.Close can block indefinitely in a USB driver after an
        // unplug/VM hand-off. Never execute it on the WPF UI thread.
        if (port != null)
        {
            _ = Task.Run(() =>
            {
                try { port.Close(); }
                catch { }
                finally
                {
                    try { port.Dispose(); }
                    catch { }
                    closeCompletion!.TrySetResult(true);
                }
            });
        }
    }
    public void Dispose() => Disconnect();
}
