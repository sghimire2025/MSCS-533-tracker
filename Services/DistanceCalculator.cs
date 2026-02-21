using Microsoft.Maui.Devices.Sensors;

namespace tracker.Services;

/// <summary>
/// Utility class for calculating distances between geographic coordinates
/// </summary>
public static class DistanceCalculator
{
    /// <summary>
    /// Earth's radius in meters
    /// </summary>
    private const double EarthRadiusMeters = 6371000.0;

    /// <summary>
    /// Calculates the distance between two geographic points using the Haversine formula
    /// </summary>
    /// <param name="point1">First location</param>
    /// <param name="point2">Second location</param>
    /// <returns>Distance in meters</returns>
    public static double CalculateDistance(Location point1, Location point2)
    {
        if (point1 == null || point2 == null)
            return 0.0;

        // Convert latitude and longitude from degrees to radians
        var lat1Rad = point1.Latitude * Math.PI / 180;
        var lat2Rad = point2.Latitude * Math.PI / 180;
        var deltaLat = (point2.Latitude - point1.Latitude) * Math.PI / 180;
        var deltaLon = (point2.Longitude - point1.Longitude) * Math.PI / 180;

        // Haversine formula
        var a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2) +
                Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return EarthRadiusMeters * c;
    }

    /// <summary>
    /// Interpolates a point between two locations at a specified distance from the start
    /// </summary>
    /// <param name="start">Starting location</param>
    /// <param name="end">Ending location</param>
    /// <param name="distanceFromStart">Distance from start point in meters</param>
    /// <returns>Interpolated location</returns>
    public static Location InterpolatePoint(Location start, Location end, double distanceFromStart)
    {
        if (start == null || end == null)
            return start ?? end ?? new Location(0, 0);

        var totalDistance = CalculateDistance(start, end);
        if (totalDistance == 0 || distanceFromStart <= 0)
            return start;
        if (distanceFromStart >= totalDistance)
            return end;

        var fraction = distanceFromStart / totalDistance;
        
        var lat = start.Latitude + (end.Latitude - start.Latitude) * fraction;
        var lon = start.Longitude + (end.Longitude - start.Longitude) * fraction;

        return new Location(lat, lon);
    }
}