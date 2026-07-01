using Avalonia.Media;
using Dock.Model.Mvvm.Controls;
using Genie.App.ViewModels;
using Genie.Core.Layout;

namespace Genie.App.Docking;

/// <summary>
/// Dock panel for the Spell Timer's "Active Spells" named-window output.
/// Monospaced so the tracker's column-aligned spell rows line up. A first-class
/// tool (like <see cref="ExperienceTool"/>), not a dynamic plugin window — that
/// is what gives it MDI decorations and stops it re-opening on every prompt
/// after the user closes it (public #112).
/// </summary>
public class ActiveSpellsTool : Tool, IWindowMenuHost
{
    public ActiveSpellsViewModel ViewModel { get; }

    /// <summary>Right-click window menu (Close), built by <see cref="GenieDockFactory"/>.</summary>
    public WindowMenuModel? WindowMenu { get; set; }

    public FontFamily ToolFontFamily { get; } =
        new("Cascadia Mono,Consolas,Courier New,monospace");
    public double ToolFontSize { get; } = 12;

    public ActiveSpellsTool(ActiveSpellsViewModel vm, WindowSettings? settings = null)
    {
        ViewModel = vm;
        Id        = "active-spells";
        Title     = "Active Spells";

        if (settings is not null)
        {
            ApplyTitle(settings);
            settings.Changed += () => ApplyTitle(settings);
        }
    }

    private void ApplyTitle(WindowSettings s) =>
        Title = string.IsNullOrEmpty(s.DisplayTitle) ? s.DefaultTitle : s.DisplayTitle;
}
