using Genie.Core.Mapper;
using Xunit;

namespace Genie.Core.Tests;

/// <summary>
/// Public #123 — Genie 4 map arcs carry non-DR pacing prefixes ("rt north",
/// "slow south") that DR rejects with "Please rephrase that command." when sent
/// verbatim. <see cref="MoveVerb.Normalize"/> strips the known prefix and sends
/// the bare movement; real DR verbs are left untouched.
/// </summary>
public class MoveVerbTests
{
    [Theory]
    // Pacing prefixes stripped → bare direction (the #123 fix + the pre-existing rt case).
    [InlineData("slow south", "south")]
    [InlineData("slow northwest", "northwest")]
    [InlineData("rt north", "north")]
    [InlineData("RT DOWN", "DOWN")]              // case-insensitive prefix match
    [InlineData("slow   out", "out")]            // collapses padding after the prefix
    // Real DR verbs left untouched.
    [InlineData("go small alleyway", "go small alleyway")]
    [InlineData("climb wall", "climb wall")]
    [InlineData("swim west", "swim west")]
    [InlineData("dive pool", "dive pool")]
    [InlineData("search bushes", "search bushes")]
    [InlineData("north", "north")]
    // Not a prefix match: no trailing space / no remainder.
    [InlineData("slower", "slower")]             // "slow" not followed by a space
    [InlineData("rt", "rt")]                     // bare token, nothing to strip
    [InlineData("slow", "slow")]
    public void Normalize_StripsPacingPrefixesOnly(string input, string expected)
    {
        Assert.Equal(expected, MoveVerb.Normalize(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_EmptyOrBlank_ReturnsInput(string input)
    {
        Assert.Equal(input, MoveVerb.Normalize(input));
    }
}
