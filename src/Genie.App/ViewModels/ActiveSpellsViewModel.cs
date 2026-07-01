using Genie.Core;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace Genie.App.ViewModels;

/// <summary>
/// Backs the Active Spells dock panel. Content is pushed by the built-in Spell
/// Timer (<c>SpellTimerExtension</c>) via the host's
/// <c>SetWindow("Active Spells", …)</c> — the App just renders whatever the
/// tracker produces. Same named-window seam the Experience panel uses; being a
/// first-class reserved window (not a dynamic plugin window) is what stops it
/// re-opening itself after the user closes it (public #112).
/// </summary>
public class ActiveSpellsViewModel : ReactiveObject
{
    [Reactive] public string Content { get; private set; } =
        "(no active spells)";

    public void Attach(GenieCore core)
    {
        core.SetPluginWindow += (window, content) =>
        {
            if (!string.Equals(window, "Active Spells", StringComparison.OrdinalIgnoreCase)) return;
            Avalonia.Threading.Dispatcher.UIThread.Post(() => Content = content);
        };
    }
}
