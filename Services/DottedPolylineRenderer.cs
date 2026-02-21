using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Devices.Sensors;

namespace tracker.Services;

/// <summary>
/// Utility class for creating dotted polylines from route points
/// </summary>
public static class DottedPolylineRenderer
{
    /// <summary>
    /// Creates a collection of polyline segments that form a dotted pattern
    /// </summary>
    /// <param name="points">Route points to convert</param>
    /// <param name="config">Configuration for the dotted pattern</param>
    /// <returns>Collection of polyline segments</returns>
    public static List<Polyline> CreateDottedPolyline(
        IEnumerable<Location> points, 
        DottedPolylineConfig? config = null)
    {
        config ??= DottedPolylineConfig.Default;
        var segments = new List<Polyline>();
        var pointsList = points?.ToList();

        if (pointsList == null || pointsList.Count < 2)
            return segments;

        try
        {
            // Process each pair of consecutive points
            for (int i = 0; i < pointsList.Count - 1; i++)
            {
                var start = pointsList[i];
                var end = pointsList[i + 1];

                // Skip invalid points
                if (start == null || end == null || 
                    double.IsNaN(start.Latitude) || double.IsNaN(start.Longitude) ||
                    double.IsNaN(end.Latitude) || double.IsNaN(end.Longitude) ||
                    Math.Abs(start.Latitude) > 90 || Math.Abs(start.Longitude) > 180 ||
                    Math.Abs(end.Latitude) > 90 || Math.Abs(end.Longitude) > 180)
                    continue;

                var segmentPolylines = CreateSegmentsBetweenPoints(start, end, config);
                segments.AddRange(segmentPolylines);
            }
        }
        catch (Exception ex)
        {
            // Log error and return what we have so far
            System.Diagnostics.Debug.WriteLine($"Error creating dotted polyline: {ex.Message}");
        }

        return segments;
    }

    /// <summary>
    /// Creates dotted segments between two points
    /// </summary>
    /// <param name="start">Starting point</param>
    /// <param name="end">Ending point</param>
    /// <param name="config">Dotted pattern configuration</param>
    /// <returns>Collection of polyline segments</returns>
    private static List<Polyline> CreateSegmentsBetweenPoints(
        Location start, 
        Location end, 
        DottedPolylineConfig config)
    {
        var segments = new List<Polyline>();
        var totalDistance = DistanceCalculator.CalculateDistance(start, end);

        // If distance is too short, create a single segment
        if (totalDistance < config.DashLength)
        {
            var polyline = CreatePolylineSegment(start, end, config);
            segments.Add(polyline);
            return segments;
        }

        var currentDistance = 0.0;
        var patternLength = config.DashLength + config.GapLength;

        while (currentDistance < totalDistance)
        {
            // Calculate dash start and end positions
            var dashStart = currentDistance;
            var dashEnd = Math.Min(currentDistance + config.DashLength, totalDistance);

            // Create dash segment if it has meaningful length
            if (dashEnd > dashStart)
            {
                var segmentStart = DistanceCalculator.InterpolatePoint(start, end, dashStart);
                var segmentEnd = DistanceCalculator.InterpolatePoint(start, end, dashEnd);
                
                var polyline = CreatePolylineSegment(segmentStart, segmentEnd, config);
                segments.Add(polyline);
            }

            // Move to next pattern position (skip the gap)
            currentDistance += patternLength;
        }

        return segments;
    }

    /// <summary>
    /// Creates a single polyline segment with specified configuration
    /// </summary>
    /// <param name="start">Start location</param>
    /// <param name="end">End location</param>
    /// <param name="config">Configuration</param>
    /// <returns>Configured polyline segment</returns>
    private static Polyline CreatePolylineSegment(
        Location start, 
        Location end, 
        DottedPolylineConfig config)
    {
        var polyline = new Polyline
        {
            StrokeColor = config.StrokeColor,
            StrokeWidth = config.StrokeWidth
        };

        polyline.Geopath.Add(start);
        polyline.Geopath.Add(end);

        return polyline;
    }
}