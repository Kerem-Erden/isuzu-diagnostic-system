using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;

namespace IsuzuDiagnostic.Desktop.Communication.Serial;

public sealed class SerialGatewayService : IDisposable
{
    private readonly object _syncRoot = new object();

    private readonly StringBuilder _receiveBuffer =
        new StringBuilder();

    private SerialPort? _serialPort;

    public event Action<string>? LineReceived;

    public event Action<string>? CommunicationError;

    public bool IsConnected =>
        _serialPort?.IsOpen == true;

    public string? ConnectedPortName =>
        IsConnected
            ? _serialPort?.PortName
            : null;

    public static IReadOnlyList<string> GetAvailablePortNames()
    {
        return SerialPort
            .GetPortNames()
            .OrderBy(
                portName => portName,
                StringComparer.OrdinalIgnoreCase
            )
            .ToArray();
    }

    public void Connect(
        string portName,
        int baudRate = 115200
    )
    {
        if (string.IsNullOrWhiteSpace(portName))
        {
            throw new ArgumentException(
                "A serial port name is required.",
                nameof(portName)
            );
        }

        if (baudRate <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(baudRate),
                "The baud rate must be greater than zero."
            );
        }

        lock (_syncRoot)
        {
            if (IsConnected)
            {
                throw new InvalidOperationException(
                    "The serial gateway is already connected."
                );
            }

            _receiveBuffer.Clear();

            SerialPort serialPort =
                new SerialPort(
                    portName.Trim(),
                    baudRate,
                    Parity.None,
                    8,
                    StopBits.One
                )
                {
                    NewLine = "\n",
                    Encoding = Encoding.ASCII,
                    ReadTimeout = 500,
                    WriteTimeout = 500,
                    DtrEnable = false,
                    RtsEnable = false
                };

            serialPort.DataReceived +=
                SerialPort_DataReceived;

            serialPort.ErrorReceived +=
                SerialPort_ErrorReceived;

            try
            {
                serialPort.Open();
                _serialPort = serialPort;
            }
            catch
            {
                serialPort.DataReceived -=
                    SerialPort_DataReceived;

                serialPort.ErrorReceived -=
                    SerialPort_ErrorReceived;

                serialPort.Dispose();

                throw;
            }
        }
    }

    public void SendLine(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException(
                "The serial message cannot be empty.",
                nameof(message)
            );
        }

        lock (_syncRoot)
        {
            if (_serialPort?.IsOpen != true)
            {
                throw new InvalidOperationException(
                    "The serial gateway is not connected."
                );
            }

            _serialPort.WriteLine(
                message.TrimEnd('\r', '\n')
            );
        }
    }

    public void Disconnect()
    {
        lock (_syncRoot)
        {
            if (_serialPort is null)
            {
                return;
            }

            SerialPort serialPort =
                _serialPort;

            _serialPort = null;

            serialPort.DataReceived -=
                SerialPort_DataReceived;

            serialPort.ErrorReceived -=
                SerialPort_ErrorReceived;

            try
            {
                if (serialPort.IsOpen)
                {
                    serialPort.Close();
                }
            }
            finally
            {
                serialPort.Dispose();
                _receiveBuffer.Clear();
            }
        }
    }

    private void SerialPort_DataReceived(
        object sender,
        SerialDataReceivedEventArgs e
    )
    {
        try
        {
            if (sender is not SerialPort serialPort)
            {
                return;
            }

            string receivedText =
                serialPort.ReadExisting();

            if (receivedText.Length == 0)
            {
                return;
            }

            ProcessReceivedText(receivedText);
        }
        catch (Exception exception)
        {
            CommunicationError?.Invoke(
                exception.Message
            );
        }
    }

    private void ProcessReceivedText(
        string receivedText
    )
    {
        List<string> completedLines =
            new List<string>();

        lock (_syncRoot)
        {
            _receiveBuffer.Append(receivedText);

            while (true)
            {
                string bufferedText =
                    _receiveBuffer.ToString();

                int lineEndingIndex =
                    bufferedText.IndexOf('\n');

                if (lineEndingIndex < 0)
                {
                    break;
                }

                string line =
                    bufferedText
                        .Substring(
                            0,
                            lineEndingIndex
                        )
                        .TrimEnd('\r');

                _receiveBuffer.Remove(
                    0,
                    lineEndingIndex + 1
                );

                if (!string.IsNullOrWhiteSpace(line))
                {
                    completedLines.Add(line);
                }
            }
        }

        foreach (string line in completedLines)
        {
            LineReceived?.Invoke(line);
        }
    }

    private void SerialPort_ErrorReceived(
        object sender,
        SerialErrorReceivedEventArgs e
    )
    {
        CommunicationError?.Invoke(
            $"Serial port error: {e.EventType}"
        );
    }

    public void Dispose()
    {
        Disconnect();
    }
}