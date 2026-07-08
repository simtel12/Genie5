using Dock.Model.Mvvm.Controls;
using Genie.App.ViewModels;
using Genie.Core.Layout;

namespace Genie.App.Docking;

/// <summary>
/// Dock panel for the Analytics dashboard — skill-history charts (XP/hour,
/// gain curves, session comparison) over the local recorder's data. Same
/// shape as <see cref="ExperienceTool"/>; hidden by default, re-opens beside
/// the Backpack via Window → Analytics.
/// </summary>
public class AnalyticsTool : Tool, IWindowMenuHost
{
    public AnalyticsViewModel ViewModel { get; }

    /// <summary>Right-click window menu (Close), built by <see cref="GenieDockFactory"/>.</summary>
    public WindowMenuModel? WindowMenu { get; set; }

    public AnalyticsTool(AnalyticsViewModel vm, WindowSettings? settings = null)
    {
        ViewModel = vm;
        Id        = "analytics";
        Title     = "Analytics";

        if (settings is not null)
        {
            ApplyTitle(settings);
            settings.Changed += () => ApplyTitle(settings);
        }
    }

    private void ApplyTitle(WindowSettings s) =>
        Title = string.IsNullOrEmpty(s.DisplayTitle) ? s.DefaultTitle : s.DisplayTitle;
}
