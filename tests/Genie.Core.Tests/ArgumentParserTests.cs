using Genie.Core.Parsing;
using Xunit;

namespace Genie.Core.Tests;

/// <summary>
/// Tokenizer contract for <see cref="ArgumentParser.ParseArgs"/> — bare /
/// "quoted" / {braced} token shapes, and Genie 4's rule that an explicitly
/// grouped EMPTY token ("" or {}) is preserved as an empty arg instead of
/// being dropped (dropping it shifted mm_train's gosub args and made its menu
/// redraw clear the main Game window).
/// </summary>
public class ArgumentParserTests
{
    [Fact]
    public void Bare_quoted_and_braced_tokens_split_as_expected()
    {
        var parts = ArgumentParser.ParseArgs("one \"two words\" {three {nested} words} four");

        Assert.Equal(new[] { "one", "two words", "three {nested} words", "four" }, parts);
    }

    [Fact]
    public void Empty_quoted_token_is_preserved_in_position()
    {
        // mm_train: gosub Menu.Build "%array" "var" "trigger" "" "%MENU_WINDOW"
        var parts = ArgumentParser.ParseArgs("\"a|b\" \"var\" \"trigger\" \"\" \"Moonmage Training Menu\"");

        Assert.Equal(5, parts.Count);
        Assert.Equal("", parts[3]);
        Assert.Equal("Moonmage Training Menu", parts[4]);
    }

    [Fact]
    public void Empty_braced_token_is_preserved_in_position()
    {
        var parts = ArgumentParser.ParseArgs("first {} last");

        Assert.Equal(new[] { "first", "", "last" }, parts);
    }

    [Fact]
    public void Trailing_empty_quoted_token_is_preserved()
    {
        var parts = ArgumentParser.ParseArgs("first \"\"");

        Assert.Equal(new[] { "first", "" }, parts);
    }

    [Fact]
    public void Plain_whitespace_still_produces_no_empty_tokens()
    {
        var parts = ArgumentParser.ParseArgs("  one   two  ");

        Assert.Equal(new[] { "one", "two" }, parts);
    }
}
