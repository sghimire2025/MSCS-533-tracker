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
            _timer.Interval = TimeSpan.FromSeconds(5); // ⏱ X seconds
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

        // 3️⃣ Move map (optional but helps visualization)
        map.MoveToRegion(
            MapSpan.FromCenterAndRadius(
                location,
                Distance.FromMeters(300)));

        // 4️⃣ Draw ONE small blue circle (incremental simulation)
        var circle = new Circle
        {
            Center = location,
            Radius = Distance.FromMeters(25),     // 🔵 small circle
            StrokeWidth = 0,
            FillColor = Colors.Blue.WithAlpha(0.35f)
        };

        map.MapElements.Add(circle);
    }
}
