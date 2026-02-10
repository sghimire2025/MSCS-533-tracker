using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Maps;
using tracker.Models;
using tracker.Services;

namespace tracker;

public partial class MainPage : ContentPage
{
    private LocationDatabase? _database;
    private MockLocationProvider? _mockProvider;
    private IDispatcherTimer? _timer;

    // 🔥 Heatmap state
    private readonly List<Location> _heatPoints = new();
    private const int MaxHeatPoints = 150;
    private bool _mapCentered = false;

    public MainPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        _database ??= Application.Current!
            .Handler!
            .MauiContext!
            .Services
            .GetRequiredService<LocationDatabase>();

        _mockProvider ??= new MockLocationProvider();

        if (_timer == null)
        {
            _timer = Dispatcher.CreateTimer();
            _timer.Interval = TimeSpan.FromSeconds(1); // 🔁 update interval
            _timer.Tick += OnTimerTick;
            _timer.Start();
        }
    }

    protected override void OnDisappearing()
    {
        _timer?.Stop();
        base.OnDisappearing();
    }

    private async void OnTimerTick(object? sender, EventArgs e)
    {
        try
        {
            if (_database == null || _mockProvider == null)
                return;

            // 1️⃣ Get next mock location
            Location location = _mockProvider.GetNext();

            // 2️⃣ Save to SQLite
            var point = new LocationPoint
            {
                Latitude = location.Latitude,
                Longitude = location.Longitude,
                Timestamp = DateTime.UtcNow
            };

            await _database.InsertAsync(point);

            // 3️⃣ Center map ONCE (important for heatmap UX)
            if (!_mapCentered)
            {
                map.MoveToRegion(
                    MapSpan.FromCenterAndRadius(
                        location,
                        Distance.FromMeters(400)));

                _mapCentered = true;
            }

            // 4️⃣ Track heat points
            _heatPoints.Add(location);
            if (_heatPoints.Count > MaxHeatPoints)
                _heatPoints.RemoveAt(0);

            // 5️⃣ Heat intensity coloring
            Color heatColor =
                _heatPoints.Count switch
                {
                    < 20 => Colors.Blue.WithAlpha(0.65f),
                    < 60 => Colors.Yellow.WithAlpha(0.65f),
                    _ => Colors.Red.WithAlpha(0.55f)
                };

            // 6️⃣ Draw heatmap circle
            var heatCircle = new Circle
            {
                Center = location,
                Radius = Distance.FromMeters(20),
                StrokeWidth = 0,
                FillColor = heatColor
            };

            map.MapElements.Add(heatCircle);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Heatmap Error] {ex}");
        }
    }
}
