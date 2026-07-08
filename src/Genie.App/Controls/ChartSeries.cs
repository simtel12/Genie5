using System.Collections.Generic;

namespace Genie.App.Controls;

/// <summary>How a <see cref="ChartSeries"/> is drawn.</summary>
public enum ChartSeriesKind
{
    /// <summary>Connected line over numeric/time X values.</summary>
    Line,
    /// <summary>One bar per point; the point's Label is the category name.</summary>
    Bar,
}

/// <summary>One data point. For line series X is the horizontal value
/// (seconds, or unix time when the chart's XIsTime is set); for bar series X
/// is ignored and <see cref="Label"/> names the category.</summary>
public sealed record ChartPoint(double X, double Y, string? Label = null);

/// <summary>
/// Renderer-neutral series model produced by <c>AnalyticsViewModel</c> and
/// consumed by <see cref="ChartCanvas"/>. Deliberately knows nothing about
/// drawing — the chart layer stays swappable (custom canvas today, a charting
/// library later) without touching the view-model.
/// </summary>
public sealed class ChartSeries
{
    public string Name { get; init; } = "";
    public ChartSeriesKind Kind { get; init; } = ChartSeriesKind.Line;
    public List<ChartPoint> Points { get; init; } = new();
    /// <summary>Palette slot — series with the same index share a colour.</summary>
    public int ColorIndex { get; init; }
}
