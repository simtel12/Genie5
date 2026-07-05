using System;
using System.Reactive.Linq;
using Genie.Core;
using Genie.Core.Events;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace Genie.App.ViewModels;

/// <summary>
/// Backs the Icon Bar strip (Genie 4 ComponentIconBar parity, issue #26) —
/// character status chips fed by <see cref="IndicatorEvent"/>. One posture
/// chip resolved in Genie 4's priority order (dead &gt; standing &gt; kneeling
/// &gt; sitting &gt; prone) plus a chip per condition flag. Poisoned/Diseased
/// are a Genie 5 bonus — the parser has always carried them but Genie 4's bar
/// had no slot. Rendering is text chips (no image assets); the strip itself
/// lives in MainWindow.axaml below the vitals status bar, toggled via
/// Layout ▸ Icon Bar (<c>DisplaySettings.ShowIconBar</c>).
/// </summary>
public sealed class IconBarViewModel : ReactiveObject
{
    // Raw posture flags — DR sends each as its own indicator; the chip shows
    // the highest-priority active one (G4 UpdateStatusBox order).
    private bool _dead, _standing, _kneeling, _sitting, _prone;

    /// <summary>The posture chip's label, empty when no posture flag is set
    /// (pre-login / no indicator seen yet) — empty collapses the chip.</summary>
    [Reactive] public string PostureText { get; private set; } = "";

    [Reactive] public bool Stunned   { get; private set; }
    [Reactive] public bool Bleeding  { get; private set; }
    [Reactive] public bool Hidden    { get; private set; }
    [Reactive] public bool Invisible { get; private set; }
    [Reactive] public bool Webbed    { get; private set; }
    [Reactive] public bool Joined    { get; private set; }
    [Reactive] public bool Poisoned  { get; private set; }
    [Reactive] public bool Diseased  { get; private set; }

    /// <summary>Subscribe to the persistent core's indicator stream. Called once
    /// from WireCore — the relay observable survives reconnects, so no re-attach
    /// is needed per connect.</summary>
    public void Attach(GenieCore core)
    {
        core.GameEvents
            .OfType<IndicatorEvent>()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(e => Apply(e.IndicatorId, e.Visible));
    }

    private void Apply(string id, bool on)
    {
        switch (id)
        {
            case "IconDEAD":     _dead     = on; UpdatePosture(); break;
            case "IconSTANDING": _standing = on; UpdatePosture(); break;
            case "IconKNEELING": _kneeling = on; UpdatePosture(); break;
            case "IconSITTING":  _sitting  = on; UpdatePosture(); break;
            case "IconPRONE":    _prone    = on; UpdatePosture(); break;

            case "IconSTUNNED":   Stunned   = on; break;
            case "IconBLEEDING":  Bleeding  = on; break;
            case "IconHIDDEN":    Hidden    = on; break;
            case "IconINVISIBLE": Invisible = on; break;
            case "IconWEBBED":    Webbed    = on; break;
            case "IconJOINED":    Joined    = on; break;
            case "IconPOISONED":  Poisoned  = on; break;
            case "IconDISEASED":  Diseased  = on; break;
        }
    }

    private void UpdatePosture() => PostureText =
        _dead     ? "DEAD"     :
        _standing ? "STANDING" :
        _kneeling ? "KNEELING" :
        _sitting  ? "SITTING"  :
        _prone    ? "PRONE"    : "";
}
