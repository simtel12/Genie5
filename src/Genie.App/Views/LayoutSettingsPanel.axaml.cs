using Avalonia.Controls;
using Genie.App.Settings;

namespace Genie.App.Views;

/// <summary>
/// App-wide window-behaviour settings (the "Settings" sub-tab of the Layout tab
/// in the Configuration dialog). Currently exposes the Always-on-Top toggle;
/// a natural home for future whole-window display settings.
///
/// <para>Unlike the rule-engine tabs these settings live on the app-wide
/// <see cref="DisplaySettings"/> (<c>display.json</c>), not per-profile, so the
/// panel is profile-independent. It binds the live <see cref="DisplaySettings"/>
/// instance the main window uses — flipping a toggle updates the window and the
/// Layout-menu checkmark at once — and invokes the persistence callback (which
/// writes <c>display.json</c>) on every change. Follows ScriptsPanel's
/// named-control + persistence-callback idiom, but applies live (single
/// toggles, matching the menu) instead of batching behind an Apply button.</para>
/// </summary>
public partial class LayoutSettingsPanel : UserControl
{
    private DisplaySettings? _display;
    private Action?          _onChanged;

    /// <summary>Guards the initial <see cref="LoadForm"/> so setting the
    /// checkbox state doesn't fire the change handler and re-persist on open.</summary>
    private bool _loading;

    public LayoutSettingsPanel()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Hand the panel the live <see cref="DisplaySettings"/> plus a callback that
    /// persists it. A null display means the app didn't supply one — the form
    /// disables itself, mirroring how the other panels stay empty when their
    /// backing store isn't available.
    /// </summary>
    public void Initialize(DisplaySettings? display, Action? onChanged = null)
    {
        _display   = display;
        _onChanged = onChanged;

        IsEnabled = display is not null;
        if (display is null)
        {
            StatusText.Text = "Display settings are unavailable.";
            return;
        }

        LoadForm(display);
        StatusText.Text = string.Empty;
    }

    private void LoadForm(DisplaySettings d)
    {
        _loading = true;
        AlwaysOnTopCheck.IsChecked = d.AlwaysOnTop;
        _loading = false;
    }

    private void OnAlwaysOnTopChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_loading || _display is null) return;
        _display.AlwaysOnTop = AlwaysOnTopCheck.IsChecked == true;
        _onChanged?.Invoke();
        StatusText.Text = $"Always on top {(_display.AlwaysOnTop ? "on" : "off")}.";
    }
}
