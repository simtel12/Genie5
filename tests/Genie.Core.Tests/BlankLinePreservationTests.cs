using System;
using System.Collections.Generic;
using System.Linq;
using Genie.Core.Events;
using Genie.Core.Parser;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace Genie.Core.Tests;

public class BlankLinePreservationTests
{
    private readonly ITestOutputHelper _out;
    public BlankLinePreservationTests(ITestOutputHelper o) => _out = o;

    private List<GameEvent> Feed(params string[] chunks)
    {
        var parser = new DrXmlParser(NullLogger<DrXmlParser>.Instance);
        var events = new List<GameEvent>();
        using var _ = parser.GameEvents.Subscribe(new C(events));
        foreach (var c in chunks) parser.Feed(c);
        return events;
    }
    private sealed class C : IObserver<GameEvent>
    { private readonly List<GameEvent> s; public C(List<GameEvent> x)=>s=x;
      public void OnNext(GameEvent e)=>s.Add(e); public void OnError(Exception e){} public void OnCompleted(){} }

    [Fact]
    public void Real_blank_preserved_tag_adjacent_newlines_not()
    {
        var ev = Feed(
            "line1\n\nline2\n" +                                   // real blank line between
            "<component id='room objs'>a rat</component>\n" +       // component then newline
            "line3\n" +
            "<prompt time='1'>&gt;</prompt>\n" +                    // prompt then newline
            "line4\n");

        var texts = ev.OfType<TextEvent>().Select(t => t.Text).ToList();
        foreach (var t in texts) _out.WriteLine($"[{t}]");

        // The real blank between line1/line2 is preserved as one empty line;
        // the component- and prompt-adjacent newlines emit NO blank.
        Assert.Equal(new[] { "line1", "", "line2", "line3", "line4" }, texts);
    }

    [Fact]
    public void Info_style_double_blanks_each_preserved()
    {
        // The INFO shape from #176: two blank-line separations.
        var ev = Feed("Redeemer.\n\nYour birthday is more than 1 month away.\n\nStrength : 12\n");
        var texts = ev.OfType<TextEvent>().Select(t => t.Text).ToList();
        Assert.Equal(new[]
        {
            "Redeemer.", "", "Your birthday is more than 1 month away.", "", "Strength : 12",
        }, texts);
    }

    [Fact]
    public void No_leading_blank_at_start_of_output()
    {
        // A stream that opens with a blank (before any real text) emits nothing.
        var ev = Feed("\nfirst real line\n");
        var texts = ev.OfType<TextEvent>().Select(t => t.Text).ToList();
        Assert.Equal(new[] { "first real line" }, texts);
    }
}
