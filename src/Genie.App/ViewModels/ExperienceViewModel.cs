using System;
using System.Collections.ObjectModel;
using Genie.Core;
using ReactiveUI;

namespace Genie.App.ViewModels;

/// <summary>
/// Backs the Experience dock panel. Content is pushed by the Experience
/// extension via the host's <c>SetWindow("Experience", …)</c> — the App doesn't
/// parse exp data itself, it just renders whatever the tracker produces. This is
/// the named-window seam that keeps trackers free of any UI dependency.
///
/// <para>Rendered as a collection of <see cref="TextLine"/> (not a single string)
/// so each row runs through the same tokenizer the game/stream windows use —
/// that's what lets user highlights fire on the Experience window (#144).</para>
///
/// <para>The panel's density slider (public #125) and the Track-gain checkbox
/// (#144) each drive their <c>#config</c> value directly, so the control, the
/// command line, and settings.cfg all stay in sync.</para>
/// </summary>
public class ExperienceViewModel : ReactiveObject
{
    private GenieCore? _core;
    private double _densityValue;
    private int _appliedLevel = -1;
    private bool _trackGain;

    /// <summary>Stop names for the 0–4 density slider, indexed by level.</summary>
    private static readonly string[] LevelNames =
        { "Full", "No count", "Numbers only", "Short names", "Brief" };

    private const string Placeholder = "(no experience data yet — train a skill, or type 'exp')";

    /// <summary>The panel's lines. Rendered via <see cref="TextLine.Inlines"/>, so
    /// user highlight rules colour the Experience window exactly as they do the
    /// stream panels (#144).</summary>
    public ObservableCollection<TextLine> Lines { get; } = new() { new TextLine(Placeholder, StreamColor.Main) };

    /// <summary>Slider position (0 Full … 4 Brief). Snapped to whole steps by the
    /// slider; a new level is applied + persisted <b>quietly</b> — straight to config,
    /// not through <c>#config</c> — so dragging doesn't spam the Game window with
    /// "[config] … (saved)" lines. The config change still fires the tracker notify,
    /// which re-renders the panel live.</summary>
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
                _core.Config.SetSetting("experiencedensity", level.ToString(), showException: false);
                _core.Config.Save();
            }
        }
    }

    /// <summary>Human-readable name of the current density stop, shown beside the slider.</summary>
    public string DensityLabel => LevelNames[Math.Clamp((int)Math.Round(_densityValue), 0, 4)];

    /// <summary>Track-gain toggle (#144). Writes <c>experiencetrackgain</c> quietly (like
    /// the density slider); the config change fires the tracker notify, which re-renders
    /// the panel with the gain column + session total.</summary>
    public bool TrackGain
    {
        get => _trackGain;
        set
        {
            this.RaiseAndSetIfChanged(ref _trackGain, value);
            if (_core is not null && _core.Config.ExperienceTrackGain != value)
            {
                _core.Config.SetSetting("experiencetrackgain", value.ToString(), showException: false);
                _core.Config.Save();
            }
        }
    }

    public void Attach(GenieCore core)
    {
        _core = core;
        // Seed from config without firing the command (level == _appliedLevel).
        _appliedLevel = core.Config.ExperienceDensity;
        DensityValue  = core.Config.ExperienceDensity;
        _trackGain    = core.Config.ExperienceTrackGain;
        this.RaisePropertyChanged(nameof(TrackGain));

        core.SetPluginWindow += (window, content) =>
        {
            if (!string.Equals(window, "Experience", StringComparison.OrdinalIgnoreCase)) return;
            Avalonia.Threading.Dispatcher.UIThread.Post(() => SetContent(content));
        };
    }

    /// <summary>Replace the panel with the tracker's latest render, one
    /// <see cref="TextLine"/> per row so highlights tokenize per line.</summary>
    private void SetContent(string content)
    {
        Lines.Clear();
        if (string.IsNullOrEmpty(content))
        {
            Lines.Add(new TextLine(Placeholder, StreamColor.Main));
            return;
        }
        foreach (var line in content.Replace("\r\n", "\n").Split('\n'))
            Lines.Add(new TextLine(line, StreamColor.Main));
    }
}
