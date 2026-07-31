using System;
using System.IO.Ports;
using System.Windows;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Diagnostics;

namespace IsuzuDiagnostic.Desktop;

public partial class MainWindow : Window
{
    private const int SerialBaudRate = 115200;

    private SerialPort? _serialPort;

    private readonly StringBuilder _receiveBuffer = new();
    private readonly object _receiveBufferLock = new();

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

    private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
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

            List<string> completeLines = ExtractCompleteLines(receivedText);

            Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    /*
                    * Eski veya kapatılmış bir porttan gecikmeli event
                    * geldiyse arayüzü güncelleme.
                    */
                    if (!ReferenceEquals(_serialPort, serialPort) || (!serialPort.IsOpen))
                    {
                        return;
                    }

                    SerialOutputTextBox.AppendText(receivedText);

                    foreach (string line in completeLines)
                    {
                        ProcessProtocolLine(line);
                    }

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

    private List<string> ExtractCompleteLines(string receivedText)
    {
        List<string> completeLines = new();

        lock (_receiveBufferLock)
        {
            _receiveBuffer.Append(receivedText);

            while (true)
            {
                int newLineIndex = FindNewLineIndex(_receiveBuffer);

                if (newLineIndex < 0)
                {
                    break;
                }

                string completeLine = _receiveBuffer.ToString(0, newLineIndex).TrimEnd('\r');
                /*
                * İşlenen satırı ve onun \n karakterini
                * tamponun başından kaldır.
                */

                _receiveBuffer.Remove(0, newLineIndex + 1);

                if (completeLine.Length > 0)
                {
                    completeLines.Add(completeLine);
                }
            }
        }
        return completeLines;
    }

    private static int FindNewLineIndex(StringBuilder buffer)
    {
        for (int index = 0; index < buffer.Length; index++)
        {
            if (buffer[index] == '\n')
            {
                return index;
            }
        }
        return -1;
    }

    private void ProcessProtocolLine(String line)
    {
        string[] parts = line.Split(':', 3, StringSplitOptions.None);

        /*
        * Beklenen yapı:
        *
        * LIVE:RPM:750
        *   0    1   2
        */

        if (parts.Length != 3)
        {
            return;
        }

        if (!parts[0].Equals("LIVE", StringComparison.Ordinal))
        {
            return;
        }

        string dataName = parts[1];
        string valueText = parts[2];

        switch (dataName)
        {
            case "RPM":
                if (int.TryParse(
                        valueText,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int rpm))
                {
                    RpmValueTextBlock.Text =
                        rpm.ToString(
                            CultureInfo.InvariantCulture
                        );
                }

                break;

            case "COOLANT_TEMP":
                if (int.TryParse(
                        valueText,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int coolantTemperature))
                {
                    CoolantTemperatureValueTextBlock.Text =
                        coolantTemperature.ToString(
                            CultureInfo.InvariantCulture
                        );
                }

                break;

            case "BATTERY_VOLTAGE":
                if (double.TryParse(
                        valueText,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out double batteryVoltage))
                {
                    BatteryVoltageValueTextBlock.Text =
                        batteryVoltage.ToString(
                            "F1",
                            CultureInfo.InvariantCulture
                        );
                }

                break;
        }
    }

    private void DisconnectSerialPort()
    {
        SerialPort? serialPortToClose = _serialPort;

        _serialPort = null;

        lock (_receiveBufferLock)
        {
            _receiveBuffer.Clear();
        }

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

        /*
        * Clear values that are no longer being received
        * from the diagnostic gateway.
        */
        RpmValueTextBlock.Text = "--";
        CoolantTemperatureValueTextBlock.Text = "--";
        BatteryVoltageValueTextBlock.Text = "--";

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