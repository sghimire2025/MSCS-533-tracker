using SQLite;
using tracker.Models;

namespace tracker.Services;

public class LocationDatabase
{
    private readonly SQLiteAsyncConnection _db;

    public LocationDatabase(string dbPath)
    {
        _db = new SQLiteAsyncConnection(dbPath);
        // Initialize table asynchronously - don't use .Wait() to avoid deadlocks
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await _db.CreateTableAsync<LocationPoint>();
    }

    public Task InsertAsync(LocationPoint point)
        => _db.InsertAsync(point);

    public Task<List<LocationPoint>> GetAllAsync()
        => _db.Table<LocationPoint>().ToListAsync();
}
