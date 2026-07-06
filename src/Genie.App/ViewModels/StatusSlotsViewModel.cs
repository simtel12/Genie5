using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace Genie.App.ViewModels;

/// <summary>
/// Genie 4's ten <c>#statusbar</c> slots (1-10), rendered as a positional row
/// directly under the vitals Status Bar (#111 follow-up; the text originally
/// rode on the Script Bar). Positional means <c>#statusbar 5 {text}</c> stays
/// in cell 5 regardless of what the other slots hold — Genie 4 scripts that
/// use the slots as columns keep their columns. Text persists until
/// overwritten or cleared — a bare <c>#statusbar N</c> clears slot N,
/// <c>#statusbar clearall</c> empties all ten — matching Genie 4's StatusStrip
/// labels: deliberately NOT cleared when the last script finishes.
/// </summary>
public sealed class StatusSlotsViewModel : ReactiveObject
{
    /// <summary>The ten slot cells, index 0 = slot 1. Fixed size — the XAML
    /// UniformGrid lays them out as equal columns.</summary>
    public IReadOnlyList<StatusSlot> Slots { get; } =
        Enumerable.Range(1, 10).Select(n => new StatusSlot(n)).ToArray();

    /// <summary>True when any slot has text. Gates the row's visibility so it
    /// costs zero height until a script (or the user) writes a status.</summary>
    [Reactive] public bool HasAny { get; private set; }

    /// <summary>
    /// Apply a <c>#statusbar</c> write (Genie 4 <c>#statusbar [N] {text}</c>),
    /// routed from <see cref="Genie.Core.GenieCore.StatusBarRequested"/>.
    /// <paramref name="index"/> is the 1-10 slot; out-of-range indices clamp
    /// to 1. Empty text clears the slot. Must be called on the UI thread (the
    /// caller marshals) since it mutates reactive state.
    /// </summary>
    public void SetStatus(int index, string text)
    {
        var slot = index is >= 1 and <= 10 ? index - 1 : 0;
        Slots[slot].Text = text ?? "";
        HasAny = Slots.Any(s => s.Text.Length > 0);
    }
}

/// <summary>One positional <c>#statusbar</c> cell.</summary>
public sealed class StatusSlot : ReactiveObject
{
    public StatusSlot(int number) => Number = number;

    /// <summary>1-based slot number, surfaced in the cell's tooltip.</summary>
    public int Number { get; }

    [Reactive] public string Text { get; set; } = "";
}
