using Genie.Core;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace Genie.App.ViewModels;

/// <summary>
/// Backs the Time Tracker dock panel. Content is pushed by the built-in
/// tracker (<c>TimeTrackerExtension</c>) via the host's
/// <c>SetWindow("Time Tracker", …)</c> — the App just renders whatever the
/// tracker produces. Same named-window seam the Experience / Active Spells
/// panels use; being a first-class reserved window (not a dynamic plugin
/// window) puts it in the top-level Window menu and stops it re-opening
/// itself after the user closes it.
/// </summary>
public class TimeTrackerViewModel : ReactiveObject
{
    [Reactive] public string Content { get; private set; } =
        "(waiting for game data)";

    public void Attach(GenieCore core)
    {
        core.SetPluginWindow += (window, content) =>
        {
            if (!string.Equals(window, "Time Tracker", StringComparison.OrdinalIgnoreCase)) return;
            Avalonia.Threading.Dispatcher.UIThread.Post(() => Content = content);
        };
    }
}
