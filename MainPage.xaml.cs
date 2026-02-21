using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Maps;
using tracker.Services;
using tracker.Models;

namespace tracker;

public partial class MainPage : ContentPage
{
    private LocationDatabase? _database;
    private IDispatcherTimer? _timer;

    private RouteWalkingProvider.GoogleMapsResponse? _routeData;
    private List<Location> _routePoints = new();
    private int _routeIndex = 0;

    private Pin? _walkerPin;
    private RouteVisualizationService? _routeVisualizationService;

    private bool _isTracking = false;

    public MainPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        _database ??= Application.Current!
            .Handler!
            .MauiContext!
            .Services
            .GetRequiredService<LocationDatabase>();

        if (_timer == null)
        {
            _timer = Dispatcher.CreateTimer();
            _timer.Interval = TimeSpan.FromSeconds(RouteWalkingProvider.TickSeconds);
            _timer.Tick += OnTimerTick;
        }

        // Load route once
        if (_routeData == null)
        {
            _routeData = await RouteWalkingProvider.LoadRouteAsync();
            _routePoints = RouteWalkingProvider.ToLocations(_routeData);
        }

        // Prepare map visuals (only if route exists)
        if (_routePoints.Count > 0)
        {
            SetupMapForRoute();
        }
    }

    private void SetupMapForRoute()
    {
        // Clear existing
        map.Pins.Clear();
        map.MapElements.Clear();

        _routeIndex = 0;
        _isTracking = false;

        var start = _routePoints.First();
        var end = _routePoints.Last();

        // Start pin
        map.Pins.Add(new Pin
        {
            Label = "Start",
            Location = start,
            Type = PinType.Place
        });

        // End pin
        map.Pins.Add(new Pin
        {
            Label = "End",
            Location = end,
            Type = PinType.Place
        });

        // Initialize dotted polyline service
        _routeVisualizationService = new RouteVisualizationService();

        // Center map on start
        map.MoveToRegion(MapSpan.FromCenterAndRadius(start, Distance.FromMeters(500)));
    }

    // Hook this to your Start Tracking button click
    private async void StartTracking_Clicked(object sender, EventArgs e)
    {
        if (_routePoints.Count == 0)
            return;

        if (_isTracking)
            return;

        _isTracking = true;

        // ✅ FIX: advance once immediately so walking starts right away
        await AdvanceOneStepAsync();

        _timer?.Start();
    }

    private async void OnTimerTick(object? sender, EventArgs e)
    {
        await AdvanceOneStepAsync();
    }

    private async Task AdvanceOneStepAsync()
    {
        if (!_isTracking) return;
        if (_routePoints.Count == 0) return;

        // finished?
        if (_routeIndex >= _routePoints.Count)
        {
            StopTracking();
            return;
        }

        var loc = _routePoints[_routeIndex];
        _routeIndex++;

        // Save to DB (if you already do this, keep it here)
        if (_database != null)
        {
            await _database.InsertAsync(new LocationPoint
            {
                Latitude = loc.Latitude,
                Longitude = loc.Longitude,
                Timestamp = DateTime.UtcNow
            });
        }

        // Create or update walker pin
        if (_walkerPin == null)
        {
            _walkerPin = new Pin
            {
                Label = "🚶",
                Type = PinType.Generic,
                Location = loc
            };
            map.Pins.Add(_walkerPin);
        }
        else
        {
            _walkerPin.Location = loc;
        }

        // Add point to dotted polyline visualization
        _routeVisualizationService?.AddRoutePoint(loc, map);

        // Keep camera following
        map.MoveToRegion(MapSpan.FromCenterAndRadius(loc, Distance.FromMeters(220)));
    }

    private void StopTracking()
    {
        _isTracking = false;
        _timer?.Stop();
    }

    private void ClearRoute()
    {
        _routeVisualizationService?.ClearRoute(map);
        _routeIndex = 0;
    }
}