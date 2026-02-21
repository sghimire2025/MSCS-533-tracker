using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Devices.Sensors;
using MapControl = Microsoft.Maui.Controls.Maps.Map;

namespace tracker.Services;

/// <summary>
/// Service for managing dotted polyline visualization on maps
/// </summary>
public class RouteVisualizationService
{
    private readonly List<Polyline> _dottedSegments = new();
    private readonly List<Location> _routePoints = new();
    private readonly DottedPolylineConfig _config;
    private const int MaxSegments = 1000; // Limit to prevent memory issues

    /// <summary>
    /// Initializes a new instance of RouteVisualizationService
    /// </summary>
    /// <param name="config">Configuration for dotted polylines</param>
    public RouteVisualizationService(DottedPolylineConfig? config = null)
    {
        _config = config ?? DottedPolylineConfig.Default;
    }

    /// <summary>
    /// Gets the current route points
    /// </summary>
    public IReadOnlyList<Location> RoutePoints => _routePoints.AsReadOnly();

    /// <summary>
    /// Gets the current dotted segments
    /// </summary>
    public IReadOnlyList<Polyline> DottedSegments => _dottedSegments.AsReadOnly();

    /// <summary>
    /// Adds a new route point and updates the dotted pattern on the map
    /// </summary>
    /// <param name="point">New route point to add</param>
    /// <param name="map">Map to update</param>
    public void AddRoutePoint(Location point, MapControl map)
    {
        if (point == null || map == null)
            return;

        // Skip invalid coordinates
        if (double.IsNaN(point.Latitude) || double.IsNaN(point.Longitude) ||
            Math.Abs(point.Latitude) > 90 || Math.Abs(point.Longitude) > 180)
            return;

        // Skip duplicate points (within 1 meter)
        if (_routePoints.Count > 0)
        {
            var lastPoint = _routePoints[_routePoints.Count - 1];
            var distance = DistanceCalculator.CalculateDistance(lastPoint, point);
            if (distance < 1.0) // Skip points closer than 1 meter
                return;
        }

        _routePoints.Add(point);

        // Limit route points to prevent memory issues
        if (_routePoints.Count > 10000)
        {
            _routePoints.RemoveAt(0);
        }

        UpdateDottedPattern(map);
    }

    /// <summary>
    /// Updates the entire dotted pattern on the map
    /// </summary>
    /// <param name="map">Map to update</param>
    public void UpdateDottedPattern(MapControl map)
    {
        if (map == null)
            return;

        try
        {
            // Clear existing segments from map
            ClearSegmentsFromMap(map);

            // Generate new dotted segments
            _dottedSegments.Clear();
            var newSegments = DottedPolylineRenderer.CreateDottedPolyline(_routePoints, _config);
            
            // Limit segments to prevent performance issues
            if (newSegments.Count > MaxSegments)
            {
                newSegments = newSegments.Take(MaxSegments).ToList();
            }
            
            _dottedSegments.AddRange(newSegments);

            // Add new segments to map
            foreach (var segment in _dottedSegments)
            {
                map.MapElements.Add(segment);
            }
        }
        catch (Exception ex)
        {
            // Log error and fallback to clearing the route
            System.Diagnostics.Debug.WriteLine($"Error updating dotted pattern: {ex.Message}");
            ClearSegmentsFromMap(map);
        }
    }

    /// <summary>
    /// Clears all route points and segments from the map
    /// </summary>
    /// <param name="map">Map to clear</param>
    public void ClearRoute(MapControl map)
    {
        if (map == null)
            return;

        ClearSegmentsFromMap(map);
        _dottedSegments.Clear();
        _routePoints.Clear();
    }

    /// <summary>
    /// Sets the entire route at once and updates the visualization
    /// </summary>
    /// <param name="points">Route points to set</param>
    /// <param name="map">Map to update</param>
    public void SetRoute(IEnumerable<Location> points, MapControl map)
    {
        if (map == null)
            return;

        ClearRoute(map);
        
        if (points != null)
        {
            _routePoints.AddRange(points.Where(p => p != null && 
                !double.IsNaN(p.Latitude) && !double.IsNaN(p.Longitude)));
            UpdateDottedPattern(map);
        }
    }

    /// <summary>
    /// Gets the total distance of the current route in meters
    /// </summary>
    /// <returns>Total route distance in meters</returns>
    public double GetTotalDistance()
    {
        if (_routePoints.Count < 2)
            return 0.0;

        double totalDistance = 0.0;
        for (int i = 0; i < _routePoints.Count - 1; i++)
        {
            totalDistance += DistanceCalculator.CalculateDistance(_routePoints[i], _routePoints[i + 1]);
        }

        return totalDistance;
    }

    /// <summary>
    /// Removes dotted segments from the map
    /// </summary>
    /// <param name="map">Map to clear segments from</param>
    private void ClearSegmentsFromMap(MapControl map)
    {
        try
        {
            foreach (var segment in _dottedSegments)
            {
                if (map.MapElements.Contains(segment))
                {
                    map.MapElements.Remove(segment);
                }
            }
        }
        catch (Exception ex)
        {
            // Log error but continue
            System.Diagnostics.Debug.WriteLine($"Error clearing segments from map: {ex.Message}");
        }
    }
}