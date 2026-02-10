using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.Maps;
using tracker.Services;

namespace tracker;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseMauiMaps()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // ✅ SQLite database registration (SAFE)
        string dbPath = Path.Combine(
            FileSystem.AppDataDirectory,
            "locations.db");

        builder.Services.AddSingleton<LocationDatabase>(
            _ => new LocationDatabase(dbPath));

        return builder.Build();
    }
}
