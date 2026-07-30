using System;
using System.IO.Ports;
using System.Windows;

namespace IsuzuDiagnostic.Desktop;

public partial class MainWindow : Window
{
    private const int SerialBaudRate = 115200;

    private SerialPort? _serialPort;

    public MainWindow()
    {
        InitializeComponent();

        LoadAvailablePorts();
    }

    private void LoadAvailablePorts()
    {
        string? previouslySelectedPort =
            PortComboBox.SelectedItem as string;

        string[] availablePorts = SerialPort.GetPortNames();

        Array.Sort(
            availablePorts,
            StringComparer.OrdinalIgnoreCase
        );

        PortComboBox.Items.Clear();

        foreach (string portName in availablePorts)
        {
            PortComboBox.Items.Add(portName);
        }

        if (previouslySelectedPort is not null &&
            PortComboBox.Items.Contains(previouslySelectedPort))
        {
            PortComboBox.SelectedItem = previouslySelectedPort;
        }
        else if (PortComboBox.Items.Count > 0)
        {
            PortComboBox.SelectedIndex = 0;
        }

        bool isConnected = _serialPort?.IsOpen == true;
        bool hasAvailablePort = PortComboBox.Items.Count > 0;

        ConnectButton.IsEnabled =
            !isConnected && hasAvailablePort;

        if (!isConnected && !hasAvailablePort)
        {
            ConnectionStatusTextBlock.Text =
                "No serial ports found";
        }
        else if (!isConnected)
        {
            ConnectionStatusTextBlock.Text =
                "Disconnected";
        }
    }

    private void RefreshPortsButton_Click(
        object sender,
        RoutedEventArgs e
    )
    {
        LoadAvailablePorts();
    }

    private void ConnectButton_Click(
        object sender,
        RoutedEventArgs e
    )
    {
        if (PortComboBox.SelectedItem is not string selectedPortName)
        {
            MessageBox.Show(
                "Please select a serial port.",
                "Serial Port",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );

            return;
        }

        try
        {
            _serialPort = new SerialPort(
                selectedPortName,
                SerialBaudRate,
                Parity.None,
                8,
                StopBits.One
            )
            {
                Handshake = Handshake.None,
                ReadTimeout = 500,
                WriteTimeout = 500,
                DtrEnable = false,
                RtsEnable = false
            };

            _serialPort.DataReceived += SerialPort_DataReceived;

            _serialPort.Open();

            ConnectionStatusTextBlock.Text =
                $"Connected: {selectedPortName}";

            PortComboBox.IsEnabled = false;
            RefreshPortsButton.IsEnabled = false;
            ConnectButton.IsEnabled = false;
            DisconnectButton.IsEnabled = true;

            AppendApplicationMessage(
                $"Opened {selectedPortName} at {SerialBaudRate} baud."
            );
        }
        catch (Exception exception)
        {
            DisconnectSerialPort();

            MessageBox.Show(
                $"Could not open the serial port.\n\n{exception.Message}",
                "Connection Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
    }

    private void DisconnectButton_Click(
        object sender,
        RoutedEventArgs e
    )
    {
        string? disconnectedPortName = _serialPort?.PortName;

        DisconnectSerialPort();

        if (disconnectedPortName is not null)
        {
            AppendApplicationMessage(
                $"Disconnected from {disconnectedPortName}."
            );
        }
    }

    private void SerialPort_DataReceived(
        object sender,
        SerialDataReceivedEventArgs e
    )
    {
        if (sender is not SerialPort serialPort)
        {
            return;
        }

        try
        {
            string receivedText = serialPort.ReadExisting();

            if (string.IsNullOrEmpty(receivedText))
            {
                return;
            }

            Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    SerialOutputTextBox.AppendText(receivedText);
                    SerialOutputTextBox.ScrollToEnd();
                })
            );
        }
        catch (InvalidOperationException)
        {
            /*
             * The port may have been closed while a receive event
             * was still waiting to run. During disconnect this is
             * an expected race condition, so no user error is shown.
             */
        }
    }

    private void DisconnectSerialPort()
    {
        SerialPort? serialPortToClose = _serialPort;

        _serialPort = null;

        if (serialPortToClose is not null)
        {
            serialPortToClose.DataReceived -=
                SerialPort_DataReceived;

            try
            {
                if (serialPortToClose.IsOpen)
                {
                    serialPortToClose.Close();
                }
            }
            finally
            {
                serialPortToClose.Dispose();
            }
        }

        ConnectionStatusTextBlock.Text = "Disconnected";

        PortComboBox.IsEnabled = true;
        RefreshPortsButton.IsEnabled = true;
        DisconnectButton.IsEnabled = false;
        ConnectButton.IsEnabled =
            PortComboBox.Items.Count > 0;
    }

    private void AppendApplicationMessage(string message)
    {
        SerialOutputTextBox.AppendText(
            $"[APP] {message}{Environment.NewLine}"
        );

        SerialOutputTextBox.ScrollToEnd();
    }

    protected override void OnClosed(EventArgs e)
    {
        DisconnectSerialPort();

        base.OnClosed(e);
    }
}