using Microsoft.Maui.Devices.Sensors;
using System.Text.Json.Nodes;

namespace tracker.Services;

/// <summary>
/// Parses Raw/ApiGeneratedPath.json (Google Directions API response),
/// decodes every step polyline, then interpolates points so the walker
/// advances at 0.8 m/s — a realistic, leisurely walking pace.
///
/// Setup:
///   1. Drop ApiGeneratedPath.json into your project under Raw/
///   2. Set its Build Action to MauiAsset
///   3. Call await walker.InitAsync() before starting the timer
/// </summary>
public class RouteWalkingProvider
{
    // ── walking constants ──────────────────────────────────────────────────
    public const double TickSeconds = 0.2;   // was 1.0
    public const double WalkSpeedMps = 5.0;   // was 0.8  
    public const double MetersPerTick = WalkSpeedMps * TickSeconds; // 0.5 m per tick



    // ── route metadata ─────────────────────────────────────────────────────
    public string StartAddress { get; private set; } = string.Empty;
    public string EndAddress { get; private set; } = string.Empty;
    public string DistanceText { get; private set; } = string.Empty;
    public string DurationText { get; private set; } = string.Empty;

    // ── step instructions ──────────────────────────────────────────────────
    public record StepInstruction(int PointIndex, string RawHtml, string? Maneuver)
    {
        public string PlainText => System.Text.RegularExpressions.Regex
            .Replace(RawHtml, "<.*?>", " ").Trim();
    }
    public List<StepInstruction> StepInstructions { get; } = new();

    // ── state ──────────────────────────────────────────────────────────────
    private List<Location> _walkPoints = new();
    private int _index = 0;

    public int TotalPoints => _walkPoints.Count;
    public int CurrentIndex => _index;
    public bool IsFinished => _index >= _walkPoints.Count;  // back to Count, not Count-1
    public void Reset() => _index = 0;

    public StepInstruction? CurrentInstruction
    {
        get
        {
            StepInstruction? cur = null;
            foreach (var s in StepInstructions)
            {
                if (s.PointIndex <= _index) cur = s;
                else break;
            }
            return cur;
        }
    }

    // ── init ───────────────────────────────────────────────────────────────

    public async Task InitAsync()
    {
        try
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync(
                "Raw/ApiGeneratedPath.json");
            using var reader = new StreamReader(stream);
            ParseDirectionsJson(await reader.ReadToEndAsync());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[RouteWalkingProvider] Asset load failed ({ex.Message}) — using embedded JSON.");
            ParseDirectionsJson(EmbeddedJson);
        }
    }

    // ── public API ─────────────────────────────────────────────────────────



    // To this:
    public Location GetNext()
    {
        if (_index >= _walkPoints.Count)
            return _walkPoints[^1];
        return _walkPoints[_index++];  // advance THEN return
    }

    // ── parsing ────────────────────────────────────────────────────────────

    private void ParseDirectionsJson(string json)
    {
        var root = JsonNode.Parse(json)!;
        var route = root["routes"]![0]!;
        var leg = route["legs"]![0]!;

        StartAddress = leg["start_address"]?.GetValue<string>() ?? "";
        EndAddress = leg["end_address"]?.GetValue<string>() ?? "";
        DistanceText = leg["distance"]?["text"]?.GetValue<string>() ?? "";
        DurationText = leg["duration"]?["text"]?.GetValue<string>() ?? "";

        var steps = leg["steps"]!.AsArray();
        var rawPts = new List<(double Lat, double Lng)>();
        StepInstructions.Clear();

        foreach (var step in steps)
        {
            var encoded = step!["polyline"]!["points"]!.GetValue<string>();
            var decoded = DecodePolyline(encoded);

            int instrIdx = rawPts.Count;
            if (rawPts.Count > 0 && decoded.Count > 0)
                decoded.RemoveAt(0);    // drop duplicate start point

            rawPts.AddRange(decoded);

            StepInstructions.Add(new StepInstruction(
                instrIdx,
                step["html_instructions"]?.GetValue<string>() ?? "",
                step["maneuver"]?.GetValue<string>()));
        }

        _walkPoints = Interpolate(rawPts, MetersPerTick);

        System.Diagnostics.Debug.WriteLine(
            $"[RouteWalkingProvider] {rawPts.Count} raw pts → " +
            $"{_walkPoints.Count} walk pts " +
            $"≈ {_walkPoints.Count * TickSeconds / 60:F1} min walk");
    }

    // ── polyline decoder ───────────────────────────────────────────────────

    public static List<(double Lat, double Lng)> DecodePolyline(string encoded)
    {
        var list = new List<(double, double)>();
        int i = 0, lat = 0, lng = 0;

        while (i < encoded.Length)
        {
            int b, shift = 0, acc = 0;
            do { b = encoded[i++] - 63; acc |= (b & 0x1f) << shift; shift += 5; }
            while (b >= 0x20);
            lat += (acc & 1) != 0 ? ~(acc >> 1) : acc >> 1;

            shift = 0; acc = 0;
            do { b = encoded[i++] - 63; acc |= (b & 0x1f) << shift; shift += 5; }
            while (b >= 0x20);
            lng += (acc & 1) != 0 ? ~(acc >> 1) : acc >> 1;

            list.Add((lat / 1e5, lng / 1e5));
        }
        return list;
    }

    // ── interpolation ──────────────────────────────────────────────────────

    private static List<Location> Interpolate(
        List<(double Lat, double Lng)> raw, double maxMeters)
    {
        var out_ = new List<Location>();
        for (int i = 0; i < raw.Count - 1; i++)
        {
            var (lat1, lng1) = raw[i];
            var (lat2, lng2) = raw[i + 1];
            double dist = Haversine(lat1, lng1, lat2, lng2);
            int steps = Math.Max(1, (int)Math.Ceiling(dist / maxMeters));
            for (int s = 0; s < steps; s++)
            {
                double t = (double)s / steps;
                out_.Add(new Location(
                    lat1 + t * (lat2 - lat1),
                    lng1 + t * (lng2 - lng1)));
            }
        }
        if (raw.Count > 0)
            out_.Add(new Location(raw[^1].Lat, raw[^1].Lng));
        return out_;
    }

    private static double Haversine(double la1, double lo1, double la2, double lo2)
    {
        const double R = 6_371_000;
        double φ1 = la1 * Math.PI / 180, φ2 = la2 * Math.PI / 180;
        double Δφ = (la2 - la1) * Math.PI / 180;
        double Δλ = (lo2 - lo1) * Math.PI / 180;
        double a = Math.Sin(Δφ / 2) * Math.Sin(Δφ / 2)
                  + Math.Cos(φ1) * Math.Cos(φ2) * Math.Sin(Δλ / 2) * Math.Sin(Δλ / 2);
        return R * 2 * Math.Asin(Math.Sqrt(a));
    }

    // ── embedded fallback ──────────────────────────────────────────────────

    private const string EmbeddedJson = """
        {
          "routes": [{
            "legs": [{
              "distance": { "text": "1.0 mi", "value": 1607 },
              "duration": { "text": "4 mins", "value": 227 },
              "start_address": "430 Meijer Dr, Florence, KY 41042, USA",
              "end_address":   "6835 Houston Rd, Florence, KY 41042, USA",
              "steps": [
                { "html_instructions": "Head <b>southwest</b> toward <b>Meijer Dr</b>",
                  "polyline": { "points": "ahbmF`tqcOHPHPHTJX" } },
                { "html_instructions": "Turn <b>right</b> toward <b>Meijer Dr</b>",
                  "maneuver": "turn-right",
                  "polyline": { "points": "wfbmFtvqcOYN" } },
                { "html_instructions": "Turn <b>left</b> onto <b>Meijer Dr</b>",
                  "maneuver": "turn-left",
                  "polyline": { "points": "qgbmFdwqcOJPR^JRBHP\\Rd@BFNZP^Rb@p@tAHR`@v@JN@Dd@d@DFTR^f@R`@l@hAj@hALRHTDP@DDX@T" } },
                { "html_instructions": "Continue onto <b>Ted Bushelman Blvd</b>",
                  "polyline": { "points": "}wamFjqrcO?T?LCTABGZIVMRA@IJ_AhAGFY\\IHEFCBQNEDUN_@TC?[NC@a@Na@NUJUJ_@X?@g@l@C@GJONABIHMPA@c@j@OLEDw@`A" } },
                { "html_instructions": "Turn <b>left</b> onto <b>KY-842 W</b>",
                  "maneuver": "turn-left",
                  "polyline": { "points": "ykbmFvfscOMBGHOLFRLLLHn@fAp@lAHFNTd@f@\\f@RZnAdBPV`@j@PVX`@Vb@" } },
                { "html_instructions": "Turn <b>right</b> onto <b>Kiley Pl</b>",
                  "maneuver": "turn-right",
                  "polyline": { "points": "__bmFfzscOOTIHOTIHOT{@jAe@l@" } },
                { "html_instructions": "Turn <b>right</b> onto <b>Doering Dr</b>",
                  "maneuver": "turn-right",
                  "polyline": { "points": "edbmFv`tcOw@aAQWA?" } },
                { "html_instructions": "Turn <b>right</b>",
                  "maneuver": "turn-right",
                  "polyline": { "points": "qfbmF|}scOXm@" } },
                { "html_instructions": "Turn <b>right</b>",
                  "maneuver": "turn-right",
                  "polyline": { "points": "webmFn|scODFJJ" } }
              ]
            }]
          }],
          "status": "OK"
        }
        """;
}
