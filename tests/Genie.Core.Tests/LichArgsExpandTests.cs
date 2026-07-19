using Genie.Core.Connection;
using Xunit;

namespace Genie.Core.Tests;

/// <summary>
/// <see cref="LichLauncher.TryExpandArguments"/> — dynamic <c>lichargs</c>
/// placeholders filled from the Lich-proxy profile at auto-launch.
/// </summary>
public class LichArgsExpandTests
{
    [Fact]
    public void Static_args_pass_through_unchanged()
    {
        var ok = LichLauncher.TryExpandArguments(
            "--login FixedChar --dragonrealms --genie --headless 8000",
            characterName: "Ignored",
            port: 9999,
            out var expanded,
            out var error);

        Assert.True(ok);
        Assert.Equal("--login FixedChar --dragonrealms --genie --headless 8000", expanded);
        Assert.Equal(string.Empty, error);
    }

    [Fact]
    public void Character_and_port_placeholders_expand()
    {
        var ok = LichLauncher.TryExpandArguments(
            "--login {character} --dragonrealms --genie --headless {port}",
            characterName: "MyChar",
            port: 8000,
            out var expanded,
            out var error);

        Assert.True(ok);
        Assert.Equal("--login MyChar --dragonrealms --genie --headless 8000", expanded);
        Assert.Equal(string.Empty, error);
    }

    [Theory]
    [InlineData("{character}", "{port}")]
    [InlineData("{CHARACTER}", "{PORT}")]
    [InlineData("{Character}", "{Port}")]
    public void Placeholders_are_case_insensitive(string charToken, string portToken)
    {
        var ok = LichLauncher.TryExpandArguments(
            $"--login {charToken} --headless {portToken}",
            characterName: "Ada",
            port: 9001,
            out var expanded,
            out _);

        Assert.True(ok);
        Assert.Equal("--login Ada --headless 9001", expanded);
    }

    [Fact]
    public void Character_placeholder_trims_the_name()
    {
        var ok = LichLauncher.TryExpandArguments(
            "--login {character}",
            characterName: "  Spaced  ",
            port: 8000,
            out var expanded,
            out _);

        Assert.True(ok);
        Assert.Equal("--login Spaced", expanded);
    }

    [Fact]
    public void Missing_character_fails_when_placeholder_present()
    {
        var ok = LichLauncher.TryExpandArguments(
            "--login {character} --headless {port}",
            characterName: "  ",
            port: 8000,
            out var expanded,
            out var error);

        Assert.False(ok);
        Assert.Equal(string.Empty, expanded);
        Assert.Contains("{character}", error);
        Assert.StartsWith("[lich]", error);
    }

    [Fact]
    public void Port_only_template_does_not_require_character()
    {
        var ok = LichLauncher.TryExpandArguments(
            "--login Fixed --headless {port}",
            characterName: null,
            port: 8123,
            out var expanded,
            out var error);

        Assert.True(ok);
        Assert.Equal("--login Fixed --headless 8123", expanded);
        Assert.Equal(string.Empty, error);
    }

    [Fact]
    public void Null_template_expands_to_empty()
    {
        var ok = LichLauncher.TryExpandArguments(
            null,
            characterName: "Anyone",
            port: 8000,
            out var expanded,
            out var error);

        Assert.True(ok);
        Assert.Equal(string.Empty, expanded);
        Assert.Equal(string.Empty, error);
    }
}
