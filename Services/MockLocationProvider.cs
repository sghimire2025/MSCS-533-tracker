using Microsoft.Maui.Devices.Sensors;

namespace tracker.Services;

public class MockLocationProvider
{
    private readonly List<Location> _mockPath;
    private int _index = 0;

    public MockLocationProvider()
    {
        _mockPath = new List<Location>
        {
            new(37.7749, -122.4194), // SF
            new(37.7755, -122.4188),
            new(37.7760, -122.4180),
            new(37.7766, -122.4172),
            new(37.7772, -122.4165),
            new(37.7778, -122.4158)
        };
    }

    public Location GetNext()
    {
        var loc = _mockPath[_index];
        _index = (_index + 1) % _mockPath.Count;
        return loc;
    }
}
