using System.Text;
namespace IsuzuDiagnostic.Desktop.Communication.Protocol;

// Discard an oversized line THROUGH its newline; never execute its suffix.
public sealed class LineFramer
{
    private readonly StringBuilder _buffer = new();
    private bool _discard;
    public IReadOnlyList<string> Feed(string text)
    {
        List<string> lines = [];
        foreach (char c in text)
        {
            if (c == '\n')
            {
                if (!_discard && _buffer.Length > 0) lines.Add(_buffer.ToString().TrimEnd('\r'));
                Reset();
            }
            else if (!_discard)
            {
                if (_buffer.Length >= 1024) { _discard = true; _buffer.Clear(); }
                else _buffer.Append(c);
            }
        }
        return lines;
    }
    public void Reset() { _buffer.Clear(); _discard = false; }
}
