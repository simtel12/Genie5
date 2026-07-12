using System;
using System.IO;
using Genie.Core.Config;
using Genie.Core.Runtime;
using Xunit;

namespace Genie.Core.Tests;

/// <summary>
/// The per-character config-directory contract between Core and any host UI.
/// Core loads <c>*.cfg</c> rule files at connect from the directory
/// <see cref="GenieConfig.ApplyCharacterProfile"/> switches to
/// (<c>Profiles/{Char}-{Acct}/</c>); hosts persist per-character files
/// (Genie 4 imports, windows.json, *.json rule sets, Layouts) to
/// <see cref="GenieConfig.ProfileDirFor"/>. These tests pin the two to the
/// same path — the regression they guard is the Genie 4 Import dialog
/// writing "this character only" cfg files into a <c>Config/Profiles/{guid}/</c>
/// directory that the engine's load path never read, so a 1767-rule import
/// silently vanished on restart.
/// </summary>
public class ProfileDirTests : IDisposable
{
    private readonly string _root;
    private readonly GenieConfig _config;

    public ProfileDirTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "genie_profiledir_tests_" + Guid.NewGuid().ToString("N"));
        var lds = new LocalDirectoryService("GenieProfileDirTest", _root);
        lds.UseExplicitRoot(_root);
        _config = new GenieConfig(lds);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch { /* best-effort temp cleanup */ }
    }

    [Fact]
    public void Import_target_equals_the_dir_ApplyCharacterProfile_loads_from()
    {
        // ProfileDirFor is what the App resolves as the import/save target
        // (MainWindowViewModel.GetProfileConfigDir); ApplyCharacterProfile is
        // where the engine actually loads rules from at connect. They must be
        // the same directory or per-character persistence is written to a
        // place that is never read back.
        var loadDir   = _config.ApplyCharacterProfile("Renucci", "MONIL");
        var importDir = GenieConfig.ProfileDirFor(_root, "Renucci", "MONIL");

        Assert.Equal(Path.GetFullPath(loadDir), Path.GetFullPath(importDir), ignoreCase: true);
        Assert.Equal(Path.Combine(_root, "Profiles", "Renucci-MONIL"), Path.GetFullPath(importDir));
    }

    [Theory]
    [InlineData(null,      "MONIL")]
    [InlineData("Renucci", null)]
    [InlineData("",        "")]
    [InlineData("  ",      "MONIL")]
    public void Missing_identity_falls_back_to_the_shared_Config_dir_on_both_sides(
        string? character, string? account)
    {
        var loadDir   = _config.ApplyCharacterProfile(character, account);
        var importDir = GenieConfig.ProfileDirFor(_root, character, account);

        Assert.Equal(Path.GetFullPath(loadDir), Path.GetFullPath(importDir), ignoreCase: true);
        Assert.Equal(Path.Combine(_root, "Config"), Path.GetFullPath(importDir));
    }

    [Fact]
    public void Illegal_filename_characters_sanitize_identically()
    {
        // Same slug rule on both paths — rogue names must not fork the dirs.
        var loadDir   = _config.ApplyCharacterProfile("Se'Karan", "AC<>ME");
        var importDir = GenieConfig.ProfileDirFor(_root, "Se'Karan", "AC<>ME");

        Assert.Equal(Path.GetFullPath(loadDir), Path.GetFullPath(importDir), ignoreCase: true);
    }

    [Fact]
    public void ProfileDirFor_is_pure_and_creates_nothing()
    {
        var dir = GenieConfig.ProfileDirFor(_root, "Renucci", "MONIL");
        Assert.False(Directory.Exists(dir));
    }

    [Fact]
    public void Legacy_cfg_seed_survives_the_host_precreating_the_dir()
    {
        // The App resolves (and creates) Profiles/{Char}-{Acct}/ for
        // windows.json / Layouts, possibly BEFORE the character's first
        // connect. The one-time legacy Config/*.cfg seed must still fire —
        // its trigger is "no cfg files yet", not "dir doesn't exist".
        var legacyCfg = Path.Combine(_root, "Config");
        Directory.CreateDirectory(legacyCfg);
        File.WriteAllText(Path.Combine(legacyCfg, "aliases.cfg"), "#alias {hi} {wave}");

        var dir = GenieConfig.ProfileDirFor(_root, "Renucci", "MONIL");
        Directory.CreateDirectory(dir);   // host pre-creates, e.g. for windows.json

        var applied = _config.ApplyCharacterProfile("Renucci", "MONIL");

        Assert.Equal(Path.GetFullPath(dir), Path.GetFullPath(applied), ignoreCase: true);
        Assert.True(File.Exists(Path.Combine(applied, "aliases.cfg")),
            "legacy aliases.cfg was not seeded into the pre-created profile dir");
    }

    [Fact]
    public void Seed_never_overwrites_existing_per_character_cfg()
    {
        var legacyCfg = Path.Combine(_root, "Config");
        Directory.CreateDirectory(legacyCfg);
        File.WriteAllText(Path.Combine(legacyCfg, "aliases.cfg"), "GLOBAL");

        var dir = GenieConfig.ProfileDirFor(_root, "Renucci", "MONIL");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "aliases.cfg"), "PER-CHARACTER");

        var applied = _config.ApplyCharacterProfile("Renucci", "MONIL");

        Assert.Equal("PER-CHARACTER", File.ReadAllText(Path.Combine(applied, "aliases.cfg")));
    }
}
