using System;
using Genie.Core;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace Genie.App.ViewModels;

/// <summary>
/// Backs the Experience dock panel. Content is pushed by the Experience
/// plugin via the host's <c>SetWindow("Experience", …)</c> — the App doesn't
/// parse exp data itself, it just renders whatever the plugin produces. This is
/// the named-window seam that keeps plugins free of any UI dependency.
///
/// <para>The panel's density slider (public #125) sets <c>experiencedensity</c>
/// through the normal <c>#config</c> command so the slider, the command line, and
/// settings.cfg all drive one value. <see cref="DensityValue"/> is the slider's
/// 0–4 position; <see cref="DensityLabel"/> names the current stop.</para>
/// </summary>
public class ExperienceViewModel : ReactiveObject
{
    private GenieCore? _core;
    private double _densityValue;
    private int _appliedLevel = -1;

    /// <summary>Stop names for the 0–4 density slider, indexed by level.</summary>
    private static readonly string[] LevelNames =
        { "Full", "No count", "Numbers only", "Short names", "Brief" };

    [Reactive] public string Content { get; private set; } =
        "(no experience data yet — train a skill, or type 'exp')";

    /// <summary>Slider position (0 Full … 4 Brief). Snapped to whole steps by the
    /// slider; setting a new level routes through <c>#config experiencedensity N</c>
    /// so it applies, persists, and re-renders exactly like typing the command.</summary>
    public double DensityValue
    {
        get => _densityValue;
        set
        {
            this.RaiseAndSetIfChanged(ref _densityValue, value);
            this.RaisePropertyChanged(nameof(DensityLabel));

            var level = Math.Clamp((int)Math.Round(value), 0, 4);
            if (_core is not null && level != _appliedLevel)
            {
                _appliedLevel = level;
                _core.Commands.ProcessInput($"#config experiencedensity {level}");
            }
        }
    }

    /// <summary>Human-readable name of the current density stop, shown beside the slider.</summary>
    public string DensityLabel => LevelNames[Math.Clamp((int)Math.Round(_densityValue), 0, 4)];

    public void Attach(GenieCore core)
    {
        _core = core;
        // Seed from config without firing the command (level == _appliedLevel).
        _appliedLevel = core.Config.ExperienceDensity;
        DensityValue  = core.Config.ExperienceDensity;

        core.SetPluginWindow += (window, content) =>
        {
            if (!string.Equals(window, "Experience", StringComparison.OrdinalIgnoreCase)) return;
            Avalonia.Threading.Dispatcher.UIThread.Post(() => Content = content);
        };
    }
}
