// End-to-end regression test mirroring mm_train's Menu.Build call chain:
// %-var set, gosub with an empty "" arg + quoted %MENU_WINDOW, and the
// `!($5 = "")` fallback that decides whether the menu targets the Game
// window. Guards BOTH fixes at once: ParseArgs keeping the empty ""
// placeholder in position, and the expression evaluator comparing a bare
// multi-word operand ("Moonmage Training Menu") without throwing — either
// failure sends the menu to the main Game window and wipes it on redraw.
using System;
using System.Collections.Generic;
using System.IO;
using Genie.Core.Scripting;
using Xunit;

namespace Genie.Core.Tests;

public class MmTrainMenuRepro
{
    [Fact]
    public void MenuBuild_window_arg_survives_the_empty_placeholder()
    {
        var echoed = new List<string>();
        var sent   = new List<string>();
        var dir = Path.Combine(Path.GetTempPath(), "gc_mmrepro_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "repro.cmd"),
                "var MENU_WINDOW Moonmage Training Menu\n" +
                "var MAIN Magic|Astrology|Extra|Done\n" +
                "gosub Menu.Build \"%MAIN\" \"selection\" \"continue.script\" \"\" \"%MENU_WINDOW\"\n" +
                "exit\n" +
                "Menu.Build:\n" +
                "if !($5 = \"\") then\n" +
                "{\n" +
                "var this.window $5\n" +
                "}\n" +
                "else var this.window Game\n" +
                "echo W=[%this.window]\n" +
                "echo A4=[$4]\n" +
                "return\n");
            var engine = new ScriptEngine(dir, new TypeAheadSession(),
                                          sendCommand: c => sent.Add(c), echo: l => echoed.Add(l));
            engine.TryStart("repro", new List<string>());
            for (int i = 0; i < 300; i++) engine.Tick();

            Assert.Contains("W=[Moonmage Training Menu]", echoed);
            Assert.Contains("A4=[]", echoed);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }
}
