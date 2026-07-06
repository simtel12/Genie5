using Avalonia.Media;
using Dock.Model.Mvvm.Controls;
using Genie.App.ViewModels;
using Genie.Core.Layout;

namespace Genie.App.Docking;

/// <summary>
/// Dock panel for the built-in Time Tracker's "Time Tracker" named-window
/// output. Monospaced so the tracker's column-aligned moon rows line up. A
/// first-class tool (like <see cref="ActiveSpellsTool"/>), not a dynamic
/// plugin window — the tracker is builtin now, so its panel belongs in the
/// top-level Window menu, keeps MDI decorations, and never re-opens itself
/// on a heartbeat repaint after the user closes it.
/// </summary>
public class TimeTrackerTool : Tool, IWindowMenuHost
{
    public TimeTrackerViewModel ViewModel { get; }

    /// <summary>Right-click window menu (Close), built by <see cref="GenieDockFactory"/>.</summary>
    public WindowMenuModel? WindowMenu { get; set; }

    public FontFamily ToolFontFamily { get; } =
        new("Cascadia Mono,Consolas,Courier New,monospace");
    public double ToolFontSize { get; } = 12;

    public TimeTrackerTool(TimeTrackerViewModel vm, WindowSettings? settings = null)
    {
        ViewModel = vm;
        Id        = "time-tracker";
        Title     = "Time Tracker";

        if (settings is not null)
        {
            ApplyTitle(settings);
            settings.Changed += () => ApplyTitle(settings);
        }
    }

    private void ApplyTitle(WindowSettings s) =>
        Title = string.IsNullOrEmpty(s.DisplayTitle) ? s.DefaultTitle : s.DisplayTitle;
}
