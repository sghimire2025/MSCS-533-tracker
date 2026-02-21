using System.Text.Json;
using Microsoft.Maui.Storage;
using Microsoft.Maui.Devices.Sensors;

namespace tracker.Services
{
    // This class is used to supply a "walking route" from a list of coordinates
    // and provide one point per tick (simulation).
    public class RouteWalkingProvider
    {
        public const double TickSeconds = 0.80;//Faster simulation - 10 updates per second
        public const double WalkSpeedMps = 5.0; // keep your existing speed

        // Google Maps API response structure
        public class GoogleMapsResponse
        {
            public List<Route> routes { get; set; } = new();
            public string status { get; set; } = "";
        }

        public class Route
        {
            public List<Leg> legs { get; set; } = new();
            public OverviewPolyline overview_polyline { get; set; } = new();
        }

        public class Leg
        {
            public List<Step> steps { get; set; } = new();
        }

        public class Step
        {
            public LocationPoint start_location { get; set; } = new();
            public LocationPoint end_location { get; set; } = new();
            public Polyline polyline { get; set; } = new();
        }

        public class LocationPoint
        {
            public double lat { get; set; }
            public double lng { get; set; }
        }

        public class Polyline
        {
            public string points { get; set; } = "";
        }

        public class OverviewPolyline
        {
            public string points { get; set; } = "";
        }

        public static async Task<GoogleMapsResponse> LoadRouteAsync()
        {
            // ✅ FIX: In .NET MAUI, Resources/Raw files are referenced by filename only.
            // Do NOT use "Raw/ApiGeneratedPath.json".
            using var stream = await FileSystem.OpenAppPackageFileAsync("ApiGeneratedPath.json");
            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync();

            var response = JsonSerializer.Deserialize<GoogleMapsResponse>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );

            return response ?? new GoogleMapsResponse();
        }

        // Converts Google Maps response into MAUI Location list
        public static List<Location> ToLocations(GoogleMapsResponse response)
        {
            var locations = new List<Location>();

            if (response.routes.Count == 0)
                return locations;

            var route = response.routes[0];
            
            foreach (var leg in route.legs)
            {
                foreach (var step in leg.steps)
                {
                    // Add start location
                    locations.Add(new Location(step.start_location.lat, step.start_location.lng));
                }
                
                // Add the final end location of the last step
                if (leg.steps.Count > 0)
                {
                    var lastStep = leg.steps.Last();
                    locations.Add(new Location(lastStep.end_location.lat, lastStep.end_location.lng));
                }
            }

            return locations;
        }
    }
}