using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Devices.Sensors;
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

        // ✅ Resolve services ONLY after page appears
        _database ??= Application.Current!
            .Handler!
            .MauiContext!
            .Services
            .GetRequiredService<LocationDatabase>();

        _mockProvider ??= new MockLocationProvider();

        if (_timer == null)
        {
            _timer = Dispatcher.CreateTimer();
            _timer.Interval = TimeSpan.FromSeconds(5);
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

        Location location = _mockProvider.GetNext();

        var point = new LocationPoint
        {
            Latitude = location.Latitude,
            Longitude = location.Longitude,
            Timestamp = DateTime.UtcNow
        };

        await _database.InsertAsync(point);

        map.MoveToRegion(
            MapSpan.FromCenterAndRadius(
                location,
                Distance.FromMeters(500)));

        map.Pins.Add(new Pin
        {
            Location = location,
            Label = point.Timestamp.ToShortTimeString(),
            Type = PinType.Place
        });
    }
}
