using Avalonia.Media;
using Dock.Model.Mvvm.Controls;
using Genie.App.Controls;
using Genie.App.ViewModels;
using Genie.Core.Layout;

namespace Genie.App.Docking;

/// <summary>
/// Raw XML dock tool (issue #14). A read-only live view of the raw server XML
/// stream — capped rolling buffer, auto-scroll, default hidden. Re-opens via
/// Window → Raw XML. A dev/debug panel, grouped beside the other utility tabs
/// (Scripts / Scene) in the default layout.
/// </summary>
public class RawXmlTool : Tool, IWindowMenuHost
{
    public RawXmlViewModel ViewModel { get; }

    /// <summary>Right-click window menu (Clear / Close), built by
    /// <see cref="GenieDockFactory"/>.</summary>
    public WindowMenuModel? WindowMenu { get; set; }

    // Per-window font, resolved from this panel's WindowSettings the same way
    // StreamTool does, so the Layout-tab font change reaches the Raw XML dump
    // instead of being ignored (it used to be hardcoded in the template).
    // Foreground stays the panel's distinctive green — only the font is tunable.
    private FontFamily _toolFontFamily = new("Cascadia Mono,Consolas,Courier New,monospace");
    public  FontFamily ToolFontFamily { get => _toolFontFamily; private set => SetProperty(ref _toolFontFamily, value); }

    private double     _toolFontSize = 11;
    public  double     ToolFontSize { get => _toolFontSize; private set => SetProperty(ref _toolFontSize, value); }

    public RawXmlTool(RawXmlViewModel vm, WindowSettings? settings = null)
    {
        ViewModel = vm;
        Id        = "raw-xml";
        Title     = "Raw XML";

        if (settings is not null)
        {
            ApplySettings(settings);
            settings.Changed += () => ApplySettings(settings);
        }
    }

    private void ApplySettings(WindowSettings s)
    {
        Title          = string.IsNullOrEmpty(s.DisplayTitle) ? s.DefaultTitle : s.DisplayTitle;
        ToolFontFamily = WindowSettingsResolver.ResolveFontFamily(s.FontFamily);
        ToolFontSize   = WindowSettingsResolver.ResolveFontSize(s.FontSize);
    }
}
