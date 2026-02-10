using SQLite;
using tracker.Models;

namespace tracker.Services;

public class LocationDatabase
{
    private readonly SQLiteAsyncConnection _db;

    public LocationDatabase(string dbPath)
    {
        _db = new SQLiteAsyncConnection(dbPath);
        _db.CreateTableAsync<LocationPoint>().Wait();
    }

    public Task InsertAsync(LocationPoint point)
        => _db.InsertAsync(point);

    public Task<List<LocationPoint>> GetAllAsync()
        => _db.Table<LocationPoint>().ToListAsync();
}
