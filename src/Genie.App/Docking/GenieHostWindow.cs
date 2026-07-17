using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Platform;
using Avalonia.Reactive;
using Dock.Avalonia.Controls;

namespace Genie.App.Docking;

/// <summary>
/// Dock floating-panel window (used when a Tool/Document is floated out to its own
/// OS window) with <b>double-click-to-maximize</b> on its title bar — the gesture
/// Dock's stock <see cref="HostWindow"/> chrome doesn't wire. A floated panel (e.g.
/// the Mapper) now maximizes / restores on a title-bar double-click, matching normal
/// window behavior, in addition to the chrome's maximize button.
/// </summary>
public sealed class GenieHostWindow : HostWindow
{
    /// <summary>Dock's HostWindow chrome title-bar height is ~30px; allow slack for
    /// the window-level fallback band when the named part can't be bound.</summary>
    private const double TitleBarBandHeight = 34;

    private Control? _titleBar;

    public GenieHostWindow()
    {
        // #170: floated panels are secondary tool windows of the one Genie
        // instance — they must not each claim a taskbar button (a layout with
        // several floats filled the taskbar with identical Genie entries).
        // One instance = one button (the main window's).
        ShowInTaskbar = false;

        // The other half of the tool-window model: with no taskbar button of
        // their own (and no minimize button in Dock's chrome — the button that
        // USED to minimize a float was its taskbar entry), floats minimize and
        // restore WITH the main window, like IDE tool palettes. Minimize Genie
        // → the whole layout goes; restore Genie → floats come back in their
        // prior state (a maximized float restores maximized). Individual
        // dismissal stays what it was: Close, then reopen from the Window menu.
        DoubleTapped += OnWindowDoubleTapped;

        // Issue #3: a floated panel that was maximized then closed can reopen
        // off-screen (saved restore-bounds land beyond the visible desktop on a
        // multi-monitor setup — seen as a sliver at a monitor's far edge). On open,
        // pull it back fully onto a visible screen so it can never vanish.
        Opened += (_, _) => { EnsureOnVisibleScreen(); FollowMainWindowMinimize(); };
        Closed += (_, _) => { _mainStateSub?.Dispose(); _mainStateSub = null; };
    }

    private IDisposable? _mainStateSub;
    private WindowState  _stateBeforeMainMinimize = WindowState.Normal;
    private bool         _minimizedWithMain;

    /// <summary>Mirror the main window's minimize/restore onto this float
    /// (#170 follow-up). Floats have no taskbar button and Dock's chrome has no
    /// minimize button, so main-window minimize is the one gesture that hides
    /// them — and the one that brings them back.</summary>
    private void FollowMainWindowMinimize()
    {
        if (_mainStateSub is not null) return;
        if (Application.Current?.ApplicationLifetime is not
                IClassicDesktopStyleApplicationLifetime desktop
            || desktop.MainWindow is not { } main || ReferenceEquals(main, this))
            return;

        _mainStateSub = main.GetObservable(WindowStateProperty)
            .Subscribe(new AnonymousObserver<WindowState>(s =>
            {
                if (s == WindowState.Minimized)
                {
                    if (WindowState == WindowState.Minimized) return;
                    _stateBeforeMainMinimize = WindowState;
                    _minimizedWithMain       = true;
                    WindowState = WindowState.Minimized;
                }
                else if (_minimizedWithMain)
                {
                    _minimizedWithMain = false;
                    WindowState = _stateBeforeMainMinimize;
                }
            }));
    }

    /// <summary>Clamp a normal-state floating window's bounds onto the working area
    /// of whichever screen it most overlaps (else the primary), so a stale/off-screen
    /// restore position can't leave it invisible. Maximized windows already fill a
    /// screen, so they're left alone.</summary>
    private void EnsureOnVisibleScreen()
    {
        var screens = Screens;
        if (screens?.All is not { Count: > 0 } all) return;
        if (WindowState == WindowState.Maximized) return;

        var scale = RenderScaling <= 0 ? 1.0 : RenderScaling;
        var w = Math.Max(200, (int)(Bounds.Width  * scale));
        var h = Math.Max(120, (int)(Bounds.Height * scale));
        var rect = new PixelRect(Position.X, Position.Y, w, h);

        // Best-overlapping screen (handles the "sliver at the edge" case), else primary.
        Screen? best = null;
        long bestArea = -1;
        foreach (var s in all)
        {
            var i = s.WorkingArea.Intersect(rect);
            long a = (long)i.Width * i.Height;
            if (a > bestArea) { bestArea = a; best = s; }
        }
        var wa = (best ?? screens.Primary ?? all[0]).WorkingArea;

        var x = Math.Clamp(Position.X, wa.X, Math.Max(wa.X, wa.X + wa.Width  - w));
        var y = Math.Clamp(Position.Y, wa.Y, Math.Max(wa.Y, wa.Y + wa.Height - h));
        if (x != Position.X || y != Position.Y)
            Position = new PixelPoint(x, y);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_titleBar is not null)
            _titleBar.DoubleTapped -= OnTitleBarDoubleTapped;

        // Dock's HostWindow chrome names its title bar PART_TitleBar. Bind the
        // double-tap there precisely so a double-tap in the content area is ignored.
        _titleBar = e.NameScope.Find<Control>("PART_TitleBar");
        if (_titleBar is not null)
            _titleBar.DoubleTapped += OnTitleBarDoubleTapped;
    }

    private void OnTitleBarDoubleTapped(object? sender, TappedEventArgs e) => ToggleMaximize(e);

    private void OnWindowDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (e.Handled || _titleBar is not null) return;            // precise handler covers it
        if (e.GetPosition(this).Y <= TitleBarBandHeight)
            ToggleMaximize(e);
    }

    private void ToggleMaximize(TappedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
        e.Handled = true;
    }
}
