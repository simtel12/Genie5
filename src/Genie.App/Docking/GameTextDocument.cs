using Avalonia;
using Avalonia.Media;
using Dock.Model.Mvvm.Controls;
using Genie.App.Controls;
using Genie.App.ViewModels;
using Genie.Core.Layout;

namespace Genie.App.Docking;

public class GameTextDocument : Document, IWindowMenuHost, IFindHost
{
    public GameTextViewModel ViewModel { get; }

    /// <summary>In-window Find bar state (#120). The overlay in the game-text
    /// template binds to this; Ctrl+F / the window menu opens it.</summary>
    public FindInWindowModel Find { get; }

    /// <summary>Right-click window menu (Clear / Time Stamp / Name List Only),
    /// built by <see cref="GenieDockFactory"/>. The main game window has no
    /// "Close Window" item — it is the primary document.</summary>
    public WindowMenuModel? WindowMenu { get; set; }

    // Per-window appearance overrides. SetProperty-backed (Dock.Model.Mvvm base
    // is a CommunityToolkit ObservableObject) instead of Fody [Reactive].

    /// <summary>Per-window foreground brush. Null falls through to the global GameBrush.</summary>
    private IBrush?    _toolForeground;
    public  IBrush?    ToolForeground { get => _toolForeground; private set => SetProperty(ref _toolForeground, value); }

    /// <summary>Per-window background brush. Null = transparent (default).</summary>
    private IBrush?    _toolBackground;
    public  IBrush?    ToolBackground { get => _toolBackground; private set => SetProperty(ref _toolBackground, value); }

    /// <summary>Per-window font family override.</summary>
    private FontFamily _toolFontFamily = new("Cascadia Mono,Consolas,Courier New,monospace");
    public  FontFamily ToolFontFamily { get => _toolFontFamily; private set => SetProperty(ref _toolFontFamily, value); }

    /// <summary>Per-window font size override.</summary>
    private double     _toolFontSize = 13;
    public  double     ToolFontSize { get => _toolFontSize; private set => SetProperty(ref _toolFontSize, value); }

    // Word Wrap (#120): resolved from WindowSettings.WordWrap. Wrap on (the
    // default) keeps the shipped look; wrap off pairs NoWrap with an Auto
    // horizontal scrollbar so long lines scroll instead of clipping.
    private TextWrapping _toolTextWrapping = TextWrapping.Wrap;
    public  TextWrapping ToolTextWrapping { get => _toolTextWrapping; private set => SetProperty(ref _toolTextWrapping, value); }

    private Avalonia.Controls.Primitives.ScrollBarVisibility _toolHScroll
        = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled;
    public  Avalonia.Controls.Primitives.ScrollBarVisibility ToolHScroll
    { get => _toolHScroll; private set => SetProperty(ref _toolHScroll, value); }

    /// <summary>"Pause Scrolling" window-menu state. Bound to the game-text
    /// ScrollViewer's <c>AutoScrollBehavior.Paused</c>; the window menu toggles
    /// it. Transient (not persisted).</summary>
    private bool       _isScrollPaused;
    public  bool       IsScrollPaused { get => _isScrollPaused; set => SetProperty(ref _isScrollPaused, value); }

    public GameTextDocument(GameTextViewModel vm, WindowSettings? settings = null)
    {
        ViewModel = vm;
        Id        = "game-text";
        Title     = "Game";
        Find      = new FindInWindowModel(() => vm.Lines.Select(l => l.Text).ToArray());

        if (settings is not null)
        {
            // #90: hand the VM its live settings so AddLine/AddEcho can prepend
            // a timestamp when the Layout-tab toggle is on.
            vm.Settings = settings;
            ApplySettings(settings);
            settings.Changed += () => ApplySettings(settings);
        }
    }

    /// <summary>
    /// Copy values from <see cref="WindowSettings"/> into the reactive UI
    /// properties. Called once at construction and re-called on every
    /// <c>Changed</c> notification so Layout-tab edits repaint live.
    /// </summary>
    private void ApplySettings(WindowSettings s)
    {
        Title          = string.IsNullOrEmpty(s.DisplayTitle) ? s.DefaultTitle : s.DisplayTitle;
        // Resolve per-window sentinels (empty FontFamily / non-positive
        // FontSize / "Default" Foreground) against the global DisplaySettings
        // values pushed to Application.Resources. Option A: per-window
        // overrides global only when explicitly set. See
        // WindowSettingsResolver for the full sentinel table.
        ToolFontFamily = WindowSettingsResolver.ResolveFontFamily(s.FontFamily);
        ToolFontSize   = WindowSettingsResolver.ResolveFontSize(s.FontSize);
        ToolForeground = WindowSettingsResolver.ResolveForeground(s.Foreground);
        ToolBackground = WindowSettingsResolver.ResolveBackground(s.Background); // null = transparent
        ToolTextWrapping = s.WordWrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
        ToolHScroll      = s.WordWrap
            ? Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled
            : Avalonia.Controls.Primitives.ScrollBarVisibility.Auto;
    }
}
