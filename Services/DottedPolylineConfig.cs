using Microsoft.Maui.Graphics;

namespace tracker.Services;

/// <summary>
/// Configuration for dotted polyline appearance and behavior
/// </summary>
public class DottedPolylineConfig
{
    /// <summary>
    /// Length of each dash segment in meters
    /// </summary>
    public double DashLength { get; set; } = 20.0;

    /// <summary>
    /// Length of gaps between dash segments in meters
    /// </summary>
    public double GapLength { get; set; } = 10.0;

    /// <summary>
    /// Color of the polyline segments
    /// </summary>
    public Color StrokeColor { get; set; } = Colors.Blue;

    /// <summary>
    /// Width of the polyline stroke in pixels
    /// </summary>
    public float StrokeWidth { get; set; } = 8.0f;

    /// <summary>
    /// Default configuration instance
    /// </summary>
    public static DottedPolylineConfig Default => new();
}