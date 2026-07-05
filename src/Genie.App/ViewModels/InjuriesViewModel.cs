using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using Avalonia.Media;
using Genie.Core;
using Genie.Core.Events;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace Genie.App.ViewModels;

/// <summary>
/// Backs the dockable Injuries panel (public issue #18) — a body-silhouette
/// grid fed by the server's injuries dialog
/// (<c>&lt;dialogData id="injuries"&gt;&lt;image id="rightLeg" name="Injury1"/&gt;…</c>).
///
/// Each of DR's 16 hit-test regions gets a cell whose colour encodes the last
/// reading (grey = healthy, yellow→orange→red = wound 1–3, steel blue = scar,
/// purple = nerve damage — nsys can't distinguish wound from scar);
/// the list below the grid spells the same readings out in words, so severity
/// never rides on colour alone. The dialog reflects the player's selected
/// display mode (E/I Wound/Scar/Both — the <c>_injury N</c> radios), and DR
/// re-pushes the full dialog at every login, so a stale panel self-corrects on
/// reconnect.
///
/// Hidden by default; re-open via Window → Injuries.
/// </summary>
public sealed class InjuriesViewModel : ReactiveObject
{
    /// <summary>One body-region cell in the silhouette grid.</summary>
    public sealed class InjuryCell : ReactiveObject
    {
        /// <summary>Short grid label ("R Arm").</summary>
        public string Label    { get; }
        /// <summary>Full region name for tooltips + the summary list ("Right Arm").</summary>
        public string FullName { get; }

        [Reactive] public IBrush Background { get; private set; } = HealthyBg;
        [Reactive] public IBrush Foreground { get; private set; } = HealthyFg;
        [Reactive] public string Tip        { get; private set; }

        public InjuryCell(string label, string fullName)
        {
            Label    = label;
            FullName = fullName;
            Tip      = $"{fullName} — healthy";
        }

        internal InjuryKind Kind     { get; private set; } = InjuryKind.None;
        internal int        Severity { get; private set; }

        internal void Set(InjuryKind kind, int severity)
        {
            Kind     = kind;
            Severity = severity;
            Background = kind switch
            {
                InjuryKind.Wound when severity <= 1 => Wound1Bg,
                InjuryKind.Wound when severity == 2 => Wound2Bg,
                InjuryKind.Wound                    => Wound3Bg,
                InjuryKind.Scar                     => ScarBg,
                InjuryKind.Damage                   => DamageBg,
                _                                   => HealthyBg,
            };
            Foreground = kind == InjuryKind.None ? HealthyFg : InjuredFg;
            Tip = kind == InjuryKind.None
                ? $"{FullName} — healthy"
                : $"{FullName} — {KindWord(kind)} ({severity})";
        }

        /// <summary>Display word per kind. Nerve damage is "damage" — the
        /// dialog can't say wound vs scar for nsys (see <see cref="InjuryKind.Damage"/>).</summary>
        internal static string KindWord(InjuryKind kind) => kind switch
        {
            InjuryKind.Wound  => "wound",
            InjuryKind.Scar   => "scar",
            InjuryKind.Damage => "damage",
            _                 => "healthy",
        };

        // Static brushes shared by all cells (dark-theme palette; wound
        // severity also lands in the text list, so colour is never the only
        // carrier). Healthy sits a step above the ~#1f1f1f panel background so
        // the silhouette figure stays visible when uninjured.
        private static readonly IBrush HealthyBg = new SolidColorBrush(Color.Parse("#343434"));
        private static readonly IBrush HealthyFg = new SolidColorBrush(Color.Parse("#808080"));
        private static readonly IBrush InjuredFg = new SolidColorBrush(Color.Parse("#f0f0f0"));
        private static readonly IBrush Wound1Bg  = new SolidColorBrush(Color.Parse("#8a7a20"));
        private static readonly IBrush Wound2Bg  = new SolidColorBrush(Color.Parse("#a86018"));
        private static readonly IBrush Wound3Bg  = new SolidColorBrush(Color.Parse("#a82828"));
        private static readonly IBrush ScarBg    = new SolidColorBrush(Color.Parse("#5a6f80"));
        private static readonly IBrush DamageBg  = new SolidColorBrush(Color.Parse("#7a5a9a"));
    }

    // The 16 regions the injuries dialog reports (ids verbatim from the XML).
    public InjuryCell Head      { get; } = new("Head",   "Head");
    public InjuryCell Neck      { get; } = new("Neck",   "Neck");
    public InjuryCell RightEye  { get; } = new("R Eye",  "Right Eye");
    public InjuryCell LeftEye   { get; } = new("L Eye",  "Left Eye");
    public InjuryCell RightArm  { get; } = new("R Arm",  "Right Arm");
    public InjuryCell LeftArm   { get; } = new("L Arm",  "Left Arm");
    public InjuryCell RightHand { get; } = new("R Hand", "Right Hand");
    public InjuryCell LeftHand  { get; } = new("L Hand", "Left Hand");
    public InjuryCell Chest     { get; } = new("Chest",  "Chest");
    public InjuryCell Abdomen   { get; } = new("Abdomen","Abdomen");
    public InjuryCell Back      { get; } = new("Back",   "Back");
    public InjuryCell RightLeg  { get; } = new("R Leg",  "Right Leg");
    public InjuryCell LeftLeg   { get; } = new("L Leg",  "Left Leg");
    public InjuryCell RightFoot { get; } = new("R Foot", "Right Foot");
    public InjuryCell LeftFoot  { get; } = new("L Foot", "Left Foot");
    public InjuryCell Nsys      { get; } = new("Nerves", "Nervous System");

    private readonly Dictionary<string, InjuryCell> _cells;

    /// <summary>Injured regions in words ("Right Leg — wound (1)"), silhouette
    /// order. Rebuilt on every reading.</summary>
    public ObservableCollection<string> Injured { get; } = new();

    /// <summary>Number of regions currently reading wound or scar — drives the
    /// panel header.</summary>
    [Reactive] public int  Count   { get; private set; }

    /// <summary>True when every region reads healthy — drives the empty-state
    /// placeholder.</summary>
    [Reactive] public bool IsEmpty { get; private set; } = true;

    // ── Auto-refresh picker ─────────────────────────────────────────────────
    // The nsys image can't say wound vs scar; only the `health` verb's text
    // can. This cadence drives Core's opt-in silent poll (#config
    // injuriespoll). Persisted quietly — straight to config, not through
    // #config — so changing it doesn't spam the Game window (same pattern as
    // the Experience density slider).

    private static readonly int[] RefreshSeconds = { 0, 30, 60, 120, 300 };

    /// <summary>Cadence stops shown by the picker, index-aligned with
    /// <see cref="RefreshSeconds"/>.</summary>
    public IReadOnlyList<string> RefreshOptions { get; } =
        new[] { "Off", "30 s", "1 min", "2 min", "5 min" };

    private GenieCore? _core;
    private int _refreshIndex;
    private int _appliedSeconds = -1;

    /// <summary>Selected picker index. Setting it applies + saves the config
    /// value; seeding from config in <see cref="Attach"/> is a no-op write.</summary>
    public int RefreshIndex
    {
        get => _refreshIndex;
        set
        {
            var index = Math.Clamp(value, 0, RefreshSeconds.Length - 1);
            this.RaiseAndSetIfChanged(ref _refreshIndex, index);
            var seconds = RefreshSeconds[index];
            if (_core is not null && seconds != _appliedSeconds)
            {
                _appliedSeconds = seconds;
                _core.Config.SetSetting("injuriespoll", seconds.ToString(), showException: false);
                _core.Config.Save();
            }
        }
    }

    public InjuriesViewModel()
    {
        _cells = new Dictionary<string, InjuryCell>(StringComparer.OrdinalIgnoreCase)
        {
            ["head"] = Head,           ["neck"] = Neck,
            ["rightEye"] = RightEye,   ["leftEye"] = LeftEye,
            ["rightArm"] = RightArm,   ["leftArm"] = LeftArm,
            ["rightHand"] = RightHand, ["leftHand"] = LeftHand,
            ["chest"] = Chest,         ["abdomen"] = Abdomen,
            ["back"] = Back,           ["rightLeg"] = RightLeg,
            ["leftLeg"] = LeftLeg,     ["rightFoot"] = RightFoot,
            ["leftFoot"] = LeftFoot,   ["nsys"] = Nsys,
        };
    }

    public void Attach(GenieCore core)
    {
        _core = core;

        // Seed the picker from config: snap a hand-edited value to the nearest
        // stop for display, but record the REAL value as applied so merely
        // attaching never rewrites the user's setting.
        _appliedSeconds = core.Config.InjuriesPollSeconds;
        var nearest = 0;
        for (var i = 0; i < RefreshSeconds.Length; i++)
            if (Math.Abs(RefreshSeconds[i] - _appliedSeconds) < Math.Abs(RefreshSeconds[nearest] - _appliedSeconds))
                nearest = i;
        _refreshIndex = nearest;
        this.RaisePropertyChanged(nameof(RefreshIndex));

        core.GameEvents.OfType<InjuryEvent>()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(e => Apply(e.Area, e.Kind, e.Severity));

        // Seed from the persistent snapshot — the panel may attach after the
        // login burst already delivered the dialog (GameStateEngine subscribes
        // in GenieCore's ctor, so State.Injuries is ahead of this handler).
        foreach (var kv in core.State.Injuries)
            Apply(kv.Key, kv.Value.Kind, kv.Value.Severity);
    }

    private void Apply(string area, InjuryKind kind, int severity)
    {
        if (!_cells.TryGetValue(area, out var cell)) return;   // unknown region
        cell.Set(kind, severity);
        RebuildSummary();
    }

    private void RebuildSummary()
    {
        Injured.Clear();
        foreach (var cell in _cells.Values)
        {
            if (cell.Kind == InjuryKind.None) continue;
            Injured.Add($"{cell.FullName} — {InjuryCell.KindWord(cell.Kind)} ({cell.Severity})");
        }
        Count   = Injured.Count;
        IsEmpty = Injured.Count == 0;
    }
}
