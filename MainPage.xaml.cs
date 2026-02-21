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
    // ── services ───────────────────────────────────────────────────────────
    private LocationDatabase? _database;
    private RouteWalkingProvider? _walker;
    private IDispatcherTimer? _timer;

    // ── map elements ───────────────────────────────────────────────────────
    private Polyline? _plannedPolyline;   // blue  – full planned route
    private Polyline? _walkedPolyline;    // green – path walked so far
    private Pin? _walkerPin;         // moving marker

    // ── heatmap ────────────────────────────────────────────────────────────
    // Tracks how many dots have been dropped in each ~10m grid cell.
    // A cell only turns yellow/red when GENUINELY crowded (many passes
    // through the same spot), not just from walking a long time.
    private readonly Dictionary<(int, int), int> _cellVisits = new();

    // Drop a visible dot every N steps so the trail isn't a solid blob
    private const int DotEveryNSteps = 20;   // one dot every ~9 m
    private int _stepsSinceLastDot = 0;

    // ── tracking state ─────────────────────────────────────────────────────
    private bool _isTracking = false;
    private string _lastInstruction = "";

    // Timer: 1 s per tick, 0.8 m/s = realistic leisurely walk
    private static readonly TimeSpan TickInterval =
        TimeSpan.FromSeconds(RouteWalkingProvider.TickSeconds);

    // ══════════════════════════════════════════════════════════════════════
    // Lifecycle
    // ══════════════════════════════════════════════════════════════════════

    public MainPage() => InitializeComponent();

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        _database ??= Application.Current!
            .Handler!.MauiContext!.Services
            .GetRequiredService<LocationDatabase>();

        if (_walker == null)
        {
            _walker = new RouteWalkingProvider();
            await _walker.InitAsync();

            counterLabel.Text = $"🚶 0 / {_walker.TotalPoints}";
            arrivalAddress.Text = _walker.EndAddress;
            UpdateEta(_walker.TotalPoints);
        }

        if (_timer == null)
        {
            _timer = Dispatcher.CreateTimer();
            _timer.Interval = TickInterval;
            _timer.Tick += OnTimerTick;
        }

        DrawPlannedRoute();
    }

    protected override void OnDisappearing()
    {
        _timer?.Stop();
        base.OnDisappearing();
    }

    // ══════════════════════════════════════════════════════════════════════
    // Route drawing
    // ══════════════════════════════════════════════════════════════════════

    private void DrawPlannedRoute()
    {
        if (_plannedPolyline != null)
            map.MapElements.Remove(_plannedPolyline);

        // Decode overview polyline for the planned route preview
        var pts = RouteWalkingProvider.DecodePolyline(
            "ahbmF`tqcOh@rAYNJP^r@l@tAfBtDx@`Bj@l@t@z@zBhENf@F^@j@Cb@I^Wj@kAvAq@v@[Xu@d@_@N}Al@u@d@g@n@]`@}@hAURw@`AMBWVFRLLLHn@fAz@tAt@|@p@bAtC`Ep@dAY^eB`Ce@l@w@aASWXm@DFJJ");

        _plannedPolyline = new Polyline
        {
            StrokeColor = Color.FromArgb("#3498DB"),
            StrokeWidth = 5
        };
        foreach (var (lat, lng) in pts)
            _plannedPolyline.Geopath.Add(new Location(lat, lng));
        map.MapElements.Add(_plannedPolyline);

        // Start & End pins
        map.Pins.Clear();
        map.Pins.Add(new Pin
        {
            Label = _walker?.StartAddress ?? "Start",
            Location = new Location(pts.First().Lat, pts.First().Lng),
            Type = PinType.Place
        });
        map.Pins.Add(new Pin
        {
            Label = _walker?.EndAddress ?? "End",
            Location = new Location(pts.Last().Lat, pts.Last().Lng),
            Type = PinType.Place
        });

        // Fit map to show the whole route
        map.MoveToRegion(MapSpan.FromCenterAndRadius(
            new Location(39.0097, -84.6431),
            Distance.FromMeters(950)));
    }

    // ══════════════════════════════════════════════════════════════════════
    // Button
    // ══════════════════════════════════════════════════════════════════════

    private void OnTrackingButtonClicked(object? sender, EventArgs e)
    {
        if (_isTracking) StopTracking();
        else StartTracking();
    }

    private void StartTracking()
    {
        if (_walker == null) return;

        _isTracking = true;
        _lastInstruction = "";
        _stepsSinceLastDot = 0;
        _walker.Reset();
        _cellVisits.Clear();

        // Remove old heatmap circles
        foreach (var c in map.MapElements.OfType<Circle>().ToList())
            map.MapElements.Remove(c);

        // Fresh walked polyline
        if (_walkedPolyline != null)
            map.MapElements.Remove(_walkedPolyline);
        _walkedPolyline = new Polyline
        {
            StrokeColor = Color.FromArgb("#27AE60"),
            StrokeWidth = 4
        };
        map.MapElements.Add(_walkedPolyline);

        // Remove stale walker pin
        if (_walkerPin != null) { map.Pins.Remove(_walkerPin); _walkerPin = null; }

        arrivalBanner.IsVisible = false;
        instructionCard.IsVisible = true;

        _timer?.Start();

        trackingButton.Text = "⏹  Stop";
        trackingButton.BackgroundColor = Color.FromArgb("#E74C3C");
        statusDot.Fill = new SolidColorBrush(Color.FromArgb("#2ECC71"));
    }

    private void StopTracking()
    {
        _isTracking = false;
        _timer?.Stop();

        trackingButton.Text = "🚶  Start Walking";
        trackingButton.BackgroundColor = Color.FromArgb("#2ECC71");
        statusDot.Fill = new SolidColorBrush(Colors.Gray);
        instructionCard.IsVisible = false;
    }

    // ══════════════════════════════════════════════════════════════════════
    // Timer tick — one walking step
    // ══════════════════════════════════════════════════════════════════════

    private async void OnTimerTick(object? sender, EventArgs e)
    {
        try
        {
            if (_database == null || _walker == null) return;

            // ── ALWAYS get next point first ───────────────────────────────
            if (_walker.IsFinished)
            {
                StopTracking();
                arrivalBanner.IsVisible = true;
                instructionCard.IsVisible = false;
                counterLabel.Text = $"🚶 {_walker.TotalPoints} / {_walker.TotalPoints}";
                etaLabel.Text = "⏱ 00:00";
                return;
            }

            Location loc = _walker.GetNext();
            int idx = _walker.CurrentIndex;

            // ── persist ───────────────────────────────────────────────────
            await _database.InsertAsync(new LocationPoint
            {
                Latitude = loc.Latitude,
                Longitude = loc.Longitude,
                Timestamp = DateTime.UtcNow
            });

            // ── walker pin ────────────────────────────────────────────────
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

            // ── camera follows ────────────────────────────────────────────
            map.MoveToRegion(MapSpan.FromCenterAndRadius(
                loc, Distance.FromMeters(220)));

            // ── walked polyline ───────────────────────────────────────────
            _walkedPolyline?.Geopath.Add(loc);

            // ── turn instruction ──────────────────────────────────────────
            var instr = _walker.CurrentInstruction;
            if (instr != null && instr.PlainText != _lastInstruction)
            {
                _lastInstruction = instr.PlainText;
                instructionLabel.Text = instr.PlainText;
                maneuverIcon.Text = ManeuverIcon(instr.Maneuver);
            }

            // ── counters & ETA ────────────────────────────────────────────
            counterLabel.Text = $"🚶 {idx} / {_walker.TotalPoints}";
            UpdateEta(_walker.TotalPoints - idx);

            // ── trail dot ─────────────────────────────────────────────────
            _stepsSinceLastDot++;
            if (_stepsSinceLastDot >= DotEveryNSteps)
            {
                _stepsSinceLastDot = 0;
                map.MapElements.Add(new Circle
                {
                    Center = loc,
                    Radius = Distance.FromMeters(12),
                    StrokeColor = Color.FromArgb("#1A6FB5"),
                    StrokeWidth = 2,
                    FillColor = Color.FromArgb("#2E86DE")
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WalkingTracker] {ex}");
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    // Helpers
    // ══════════════════════════════════════════════════════════════════════

    private void UpdateEta(int stepsRemaining)
    {
        var ts = TimeSpan.FromSeconds(stepsRemaining * RouteWalkingProvider.TickSeconds);
        etaLabel.Text = $"⏱ {ts:mm\\:ss}";
    }

    /// Converts a lat/lng to a discrete grid cell of <gridMeters> size.
    private static (int, int) LatLngToCell(double lat, double lng, double gridMeters)
    {
        // 1 degree lat ≈ 111,320 m; 1 degree lng ≈ 111,320 * cos(lat) m
        double latStep = gridMeters / 111_320.0;
        double lngStep = gridMeters / (111_320.0 * Math.Cos(lat * Math.PI / 180));
        return ((int)Math.Floor(lat / latStep), (int)Math.Floor(lng / lngStep));
    }

    private static string ManeuverIcon(string? maneuver) => maneuver switch
    {
        "turn-right" => "➡",
        "turn-left" => "⬅",
        "turn-sharp-right" => "↪",
        "turn-sharp-left" => "↩",
        "turn-slight-right" => "↗",
        "turn-slight-left" => "↖",
        "uturn-right" => "🔄",
        "uturn-left" => "🔄",
        "roundabout-right" => "🔃",
        "roundabout-left" => "🔃",
        "straight" => "⬆",
        "ramp-right" => "↗",
        "ramp-left" => "↖",
        "merge" => "⬆",
        "fork-right" => "↗",
        "fork-left" => "↖",
        _ => "⬆"
    };
}
