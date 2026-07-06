using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Genie.Core.Layout;
using Genie.Core.Persistence;
using Xunit;

namespace Genie.Core.Tests;

/// <summary>
/// Per-window Word Wrap (#120): defaults on, survives the windows.json
/// round-trip, and pre-#120 files (no WordWrap field) keep the shipped
/// always-wrap behaviour.
/// </summary>
public class WindowSettingsWordWrapTests
{
    [Fact]
    public void WordWrap_defaults_on()
    {
        Assert.True(new WindowSettings().WordWrap);
    }

    [Fact]
    public void WordWrap_round_trips_through_windows_json()
    {
        var store = new WindowSettingsStore();
        store.Register("talk", "Talk");
        store.Get("talk").WordWrap = false;

        var path = Path.Combine(Path.GetTempPath(), $"g5-wraptest-{Guid.NewGuid():N}.json");
        try
        {
            new PersistenceService().SaveWindowSettings(path, store);

            var fresh = new WindowSettingsStore();
            fresh.Register("talk", "Talk");
            foreach (var m in new PersistenceService().LoadWindowSettings(path))
                fresh.Apply(m);

            Assert.False(fresh.Get("talk").WordWrap);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Legacy_windows_json_without_field_stays_wrapped()
    {
        // A pre-#120 entry — no WordWrap property at all.
        var legacy = """[{"Id":"talk","DisplayTitle":"","FontFamily":"","FontSize":0,"Foreground":"Default","Background":"","Timestamp":false,"NameListOnly":false,"EchoToMain":false,"IfClosed":null,"HasIfClosed":true}]""";
        var models = JsonSerializer.Deserialize<List<WindowSettingsPersistenceModel>>(legacy)!;

        var store = new WindowSettingsStore();
        store.Register("talk", "Talk");
        foreach (var m in models) store.Apply(m);

        Assert.True(store.Get("talk").WordWrap);
    }
}
