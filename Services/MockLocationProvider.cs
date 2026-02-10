using System.Text.Json;
using Microsoft.Maui.Devices.Sensors;

namespace tracker.Services;

public class MockLocationProvider
{
    private readonly List<Location> _mockPath;
    private int _index = 0;

    public MockLocationProvider(string resourceFile = "mock_path_live.json")
    {
        _mockPath = LoadFromResource(resourceFile);

        if (_mockPath.Count == 0)
            throw new InvalidOperationException("Mock path resource is empty.");
    }

    private static List<Location> LoadFromResource(string fileName)
    {
        using var stream = FileSystem
            .OpenAppPackageFileAsync(fileName)
            .GetAwaiter()
            .GetResult();

        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();

        var points = JsonSerializer.Deserialize<List<MockLocationDto>>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new();

        return points
            .Select(p => new Location(p.Lat, p.Lng))
            .ToList();
    }

    public Location GetNext()
    {
        var loc = _mockPath[_index];
        _index = (_index + 1) % _mockPath.Count;

        return new Location(loc.Latitude, loc.Longitude)
        {
            Timestamp = DateTimeOffset.UtcNow,
            Accuracy = 5
        };
    }

    // 🔑 Matches your JSON exactly
    private class MockLocationDto
    {
        public double Lat { get; set; }
        public double Lng { get; set; }
    }
}
