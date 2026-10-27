using FlightReservation.Data;
using FlightReservation.Domain;
using Xunit;

namespace FlightReservation.Tests;

public sealed class FlightTests
{
    [Fact]
    public async Task CreateFlight_stores_the_flight()
    {
        using var database = new TestDatabase();

        await using var db = database.NewContext();
        var flight = await new BookingService(db).CreateFlightAsync(
            new CreateFlightRequest("os201", "vie", "fra", TestDatabase.Departure, 120));

        Assert.Equal("os201", flight.Number);
        Assert.Equal("vie", flight.Origin);
        Assert.Equal("fra", flight.Destination);
        Assert.Equal(TestDatabase.Departure, flight.DepartureAt);
        Assert.Equal(120, flight.Capacity);
        Assert.Equal(120, flight.SeatsAvailable);
        Assert.Empty(flight.Reservations);
    }

    [Fact]
    public async Task CreateFlight_rejects_a_duplicate_number()
    {
        using var database = new TestDatabase();
        await database.CreateFlightAsync("OS201");

        await using var db = database.NewContext();
        var exception = await Assert.ThrowsAsync<DuplicateFlightNumberException>(() =>
            new BookingService(db).CreateFlightAsync(
                new CreateFlightRequest("OS201", "VIE", "FRA", TestDatabase.Departure, 120)));

        Assert.Equal("OS201", exception.Number);
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("")]
    public async Task CreateFlight_rejects_a_missing_number(string number)
    {
        using var database = new TestDatabase();

        await using var db = database.NewContext();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            new BookingService(db).CreateFlightAsync(
                new CreateFlightRequest(number, "VIE", "FRA", TestDatabase.Departure, 120)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task CreateFlight_rejects_a_capacity_below_one(int capacity)
    {
        using var database = new TestDatabase();

        await using var db = database.NewContext();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            new BookingService(db).CreateFlightAsync(
                new CreateFlightRequest("OS201", "VIE", "FRA", TestDatabase.Departure, capacity)));
    }

    [Fact]
    public async Task FindFlight_returns_null_for_an_unknown_number()
    {
        using var database = new TestDatabase();

        await using var db = database.NewContext();
        Assert.Null(await new BookingService(db).FindFlightAsync("XX999"));
    }

    [Fact]
    public async Task FindFlight_reports_all_seats_as_free_on_a_new_flight()
    {
        using var database = new TestDatabase();
        await database.CreateFlightAsync("OS201", capacity: 4);

        await using var db = database.NewContext();
        var flight = await new BookingService(db).FindFlightAsync("OS201");

        Assert.NotNull(flight);
        Assert.Equal(4, flight.SeatsAvailable);
        Assert.Empty(flight.Reservations);
    }

    [Fact]
    public async Task FindFlight_reports_the_seats_that_are_still_free()
    {
        using var database = new TestDatabase();
        await database.CreateFlightAsync("OS201", capacity: 4);

        await using (var db = database.NewContext())
        {
            await new BookingService(db).BookAsync("OS201", new BookRequest("Anna Weber", 3));
        }

        await using (var db = database.NewContext())
        {
            var flight = await new BookingService(db).FindFlightAsync("OS201");

            Assert.NotNull(flight);
            Assert.Equal(1, flight.SeatsAvailable);
            Assert.Equal(3, Assert.Single(flight.Reservations).Seats);
        }
    }
}
