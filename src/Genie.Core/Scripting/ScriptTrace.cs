namespace Genie.Core.Scripting;

/// <summary>
/// Rolling buffer of the last 20 control-flow events (goto/gosub/return,
/// label passes, match hits, matchwait timeouts, exit) for a running
/// <c>.cmd</c> script — the backing store for <c>#script trace</c>. Port of
/// Genie 4's Script/Trace.cs ring buffer, including its entry format
/// (<c>goto HUNT hunt(214)</c>) and oldest→newest dump order.
/// </summary>
public sealed class ScriptTrace
{
    private const int Size = 20;
    private readonly string?[] _entries = new string?[Size];
    private int _next;

    public void Add(string value, string origin = "", int lineNumber = 0)
    {
        if (origin.Length > 0 && lineNumber > 0) value += $" {origin}({lineNumber})";
        else if (origin.Length > 0)              value += $" {origin}";
        else if (lineNumber > 0)                 value += $" ({lineNumber})";
        _entries[_next] = value;
        _next = (_next + 1) % Size;
    }

    /// <summary>Recorded entries, oldest first.</summary>
    public IReadOnlyList<string> Lines()
    {
        var result = new List<string>(Size);
        for (int i = 0; i < Size; i++)
        {
            var e = _entries[(_next + i) % Size];
            if (!string.IsNullOrEmpty(e)) result.Add(e);
        }
        return result;
    }
}
