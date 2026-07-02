using Genie.Core.Parsing;
using Xunit;

namespace Genie.Core.Tests;

/// <summary>
/// Genie 4 parity for command-line separator splitting (#132). ArgumentParser
/// .SafeSplit honours a `\;` escape and doesn't split on a separator inside
/// "quotes" or {braces}, preserving the escaping backslash — matching Genie 4's
/// Utility.SafeSplit. A plain string.Split used to truncate `#var t a\;b` at the
/// semicolon and leak the tail to the game.
/// </summary>
public class SafeSplitTests
{
    [Fact]
    public void Escaped_separator_does_not_split_and_keeps_backslash()
    {
        var parts = SafeSplit(@"a\;b");
        Assert.Single(parts);
        Assert.Equal(@"a\;b", parts[0]);
    }

    [Fact]
    public void Issue132_repro_stays_one_command()
    {
        const string line = @"#var test This is a test\;of the escape character.";
        var parts = SafeSplit(line);
        Assert.Single(parts);
        Assert.Equal(line, parts[0]);   // backslash + semicolon preserved intact
    }

    [Fact]
    public void Unescaped_separator_splits()
    {
        Assert.Equal(new[] { "a", "b", "c" }, SafeSplit("a;b;c"));
    }

    [Fact]
    public void Double_backslash_resets_escape_so_next_separator_splits()
    {
        // `\\` is a literal backslash and cancels the escape, so the ; that
        // follows is a real separator.
        Assert.Equal(new[] { @"a\\", "b" }, SafeSplit(@"a\\;b"));
    }

    [Fact]
    public void Separator_inside_quotes_is_literal()
    {
        var parts = SafeSplit("say \"hi;there\"");
        Assert.Single(parts);
        Assert.Equal("say \"hi;there\"", parts[0]);
    }

    [Fact]
    public void Separator_inside_braces_is_literal()
    {
        var parts = SafeSplit("#action {foo;bar}");
        Assert.Single(parts);
        Assert.Equal("#action {foo;bar}", parts[0]);
    }

    [Fact]
    public void Custom_separator_char_is_honoured()
    {
        Assert.Equal(new[] { "a", "b" }, SafeSplit("a|b", '|'));
    }

    private static string[] SafeSplit(string input, char sep = ';')
    {
        var list = ArgumentParser.SafeSplit(input, sep);
        return System.Linq.Enumerable.ToArray(list);
    }
}
