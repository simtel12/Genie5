using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Genie.App.Controls;

/// <summary>
/// PageUp / PageDown keyboard scrolling for the game-text windows (#136,
/// Genie 3/4 parity). In Genie 4 the keys page the <i>active</i> output window
/// while keyboard focus never leaves the command box, and Ctrl+PageUp /
/// Ctrl+PageDown jump to the top / bottom of that window's scrollback.
///
/// <para><b>Selection model:</b> mark a window's ScrollViewer with
/// <c>controls:PageScroll.IsTarget="True"</c> — clicking anywhere inside it
/// makes it the window the keys scroll (Genie 4's "active form"). The main
/// game window additionally sets <c>IsDefaultTarget="True"</c> so the keys
/// work before the user has ever clicked a window. Tracking lives on the
/// ScrollViewer itself (not the main window) so clicks inside floated dock
/// windows still update the target.</para>
///
/// <para>The key handling itself is wired in MainWindow, where the command
/// bar's keystrokes actually arrive.</para>
/// </summary>
public static class PageScroll
{
    /// <summary>Clicking inside this ScrollViewer makes it the window that
    /// PageUp/PageDown scroll.</summary>
    public static readonly AttachedProperty<bool> IsTargetProperty =
        AvaloniaProperty.RegisterAttached<ScrollViewer, bool>(
            "IsTarget", typeof(PageScroll));

    /// <summary>Fallback target used until the user clicks a window (and when
    /// the clicked window has since been closed). Set on the main game window.</summary>
    public static readonly AttachedProperty<bool> IsDefaultTargetProperty =
        AvaloniaProperty.RegisterAttached<ScrollViewer, bool>(
            "IsDefaultTarget", typeof(PageScroll));

    public static bool GetIsTarget(AvaloniaObject o)         => o.GetValue(IsTargetProperty);
    public static void SetIsTarget(AvaloniaObject o, bool v) => o.SetValue(IsTargetProperty, v);

    public static bool GetIsDefaultTarget(AvaloniaObject o)         => o.GetValue(IsDefaultTargetProperty);
    public static void SetIsDefaultTarget(AvaloniaObject o, bool v) => o.SetValue(IsDefaultTargetProperty, v);

    // Weak so a closed window (dockable removed, floated window closed) can be
    // collected; Resolve() also checks the control is still in a visual tree.
    private static WeakReference<ScrollViewer>? _current;
    private static WeakReference<ScrollViewer>? _default;

    static PageScroll()
    {
        IsTargetProperty.Changed.AddClassHandler<ScrollViewer>((sv, e) =>
        {
            // Set once from a DataTemplate, never toggled off — no unhook path.
            // Tunnel + handledEventsToo because LineSelection owns (and marks
            // handled) pointer presses over the game text.
            if (e.NewValue is true)
                sv.AddHandler(InputElement.PointerPressedEvent, OnPressed,
                              RoutingStrategies.Tunnel, handledEventsToo: true);
        });

        IsDefaultTargetProperty.Changed.AddClassHandler<ScrollViewer>((sv, e) =>
        {
            if (e.NewValue is true)
                _default = new WeakReference<ScrollViewer>(sv);
        });
    }

    private static void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is ScrollViewer sv)
            _current = new WeakReference<ScrollViewer>(sv);
    }

    /// <summary>
    /// Applies a PageUp/PageDown keystroke to the selected window.
    /// Plain key = one page; Ctrl = jump to top / bottom (Genie 4 parity).
    /// Returns false when no target window exists (nothing to scroll).
    /// </summary>
    public static bool HandleKey(Key key, KeyModifiers mods)
    {
        var sv = Resolve();
        if (sv is null) return false;

        var toEdge = mods.HasFlag(KeyModifiers.Control);
        if (key == Key.PageUp)
        {
            if (toEdge) sv.ScrollToHome(); else sv.PageUp();
        }
        else
        {
            // Paging back to the bottom lands inside AutoScrollBehavior's
            // at-bottom band, so auto-follow of new lines resumes by itself.
            if (toEdge) sv.ScrollToEnd(); else sv.PageDown();
        }
        return true;
    }

    /// <summary>The currently-selected target window's ScrollViewer (or the
    /// default game window). Exposed for features that share the "last-clicked
    /// window" targeting — e.g. Ctrl+F opens that window's Find bar (#120).</summary>
    public static ScrollViewer? CurrentTarget => Resolve();

    private static ScrollViewer? Resolve()
    {
        if (_current?.TryGetTarget(out var sv) == true && sv.GetVisualRoot() is not null)
            return sv;
        if (_default?.TryGetTarget(out var def) == true && def.GetVisualRoot() is not null)
            return def;
        return null;
    }
}
