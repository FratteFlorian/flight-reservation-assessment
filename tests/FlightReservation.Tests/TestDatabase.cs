using FlightReservation.Data;
using FlightReservation.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FlightReservation.Tests;

/// <summary>
/// A private SQLite database file for one test. Every context the test opens talks to the same
/// file, so a second context can prove what actually reached the database.
/// </summary>
internal sealed class TestDatabase : IDisposable
{
    public static readonly DateTimeOffset Departure = new(2026, 11, 3, 9, 30, 0, TimeSpan.Zero);

    private readonly string _path;
    private readonly DbContextOptions<BookingDbContext> _options;

    public TestDatabase()
    {
        _path = Path.Combine(Path.GetTempPath(), $"flight-reservation-tests-{Guid.NewGuid():N}.db");

        _options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseSqlite($"Data Source={_path}")
            .Options;

        using var db = NewContext();
        db.Database.EnsureCreated();
    }

    public BookingDbContext NewContext() => new(_options);

    /// <summary>Creates a flight in a fresh connection and returns its number.</summary>
    public async Task<string> CreateFlightAsync(string number = "OS201", int capacity = 4)
    {
        await using var db = NewContext();

        await new BookingService(db).CreateFlightAsync(
            new CreateFlightRequest(number, "VIE", "FRA", Departure, capacity));

        return number;
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        try
        {
            File.Delete(_path);
        }
        catch (IOException)
        {
            // A leftover temp file is not worth failing a test for.
        }
    }
}
