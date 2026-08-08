
namespace IsuzuDiagnostic.Desktop.Communication.Protocol
{
    public sealed class LiveDataMessage
    {
        public string Parameter { get; }

        public double Value { get; }

        public LiveDataMessage(string parameter, double value)
        {
            Parameter = parameter;
            Value = value;
        }
    }
}
