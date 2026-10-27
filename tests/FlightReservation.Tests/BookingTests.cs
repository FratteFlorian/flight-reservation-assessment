using FlightReservation.Data;
using FlightReservation.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FlightReservation.Tests;

public sealed class BookingTests
{
    [Fact]
    public async Task Book_stores_a_held_reservation()
    {
        using var database = new TestDatabase();
        await database.CreateFlightAsync("OS201", capacity: 4);

        await using var db = database.NewContext();
        var reservation = await new BookingService(db).BookAsync("OS201", new BookRequest("Anna Weber", 2));

        Assert.Equal("Anna Weber", reservation.PassengerName);
        Assert.Equal("OS201", reservation.FlightNumber);
        Assert.Equal(2, reservation.Seats);
        Assert.Equal(ReservationStatus.Held, reservation.Status);
        Assert.Equal(2, reservation.SeatsAvailable);
        Assert.Equal(6, reservation.Reference.Length);
    }

    [Fact]
    public async Task Book_rejects_an_unknown_flight()
    {
        using var database = new TestDatabase();

        await using var db = database.NewContext();
        var exception = await Assert.ThrowsAsync<FlightNotFoundException>(() =>
            new BookingService(db).BookAsync("XX999", new BookRequest("Anna Weber", 1)));

        Assert.Equal("XX999", exception.Number);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public async Task Book_rejects_a_seat_count_below_one(int seats)
    {
        using var database = new TestDatabase();
        await database.CreateFlightAsync("OS201", capacity: 4);

        await using var db = database.NewContext();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            new BookingService(db).BookAsync("OS201", new BookRequest("Anna Weber", seats)));
    }

    [Fact]
    public async Task Book_rejects_a_missing_passenger_name()
    {
        using var database = new TestDatabase();
        await database.CreateFlightAsync("OS201", capacity: 4);

        await using var db = database.NewContext();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            new BookingService(db).BookAsync("OS201", new BookRequest("   ", 1)));
    }

    [Fact]
    public async Task Book_accepts_a_booking_that_exactly_fills_the_flight()
    {
        using var database = new TestDatabase();
        await database.CreateFlightAsync("OS201", capacity: 2);

        await using var db = database.NewContext();
        var reservation = await new BookingService(db).BookAsync("OS201", new BookRequest("Anna Weber", 2));

        Assert.Equal(0, reservation.SeatsAvailable);
    }

    // ---------------------------------------------------------------- not implemented yet

    [Fact]
    public async Task Book_rejects_a_booking_that_does_not_fit()
    {
        using var database = new TestDatabase();
        await database.CreateFlightAsync("OS201", capacity: 2);

        await using (var db = database.NewContext())
        {
            await new BookingService(db).BookAsync("OS201", new BookRequest("Anna Weber", 1));
        }

        await using (var db = database.NewContext())
        {
            var exception = await Assert.ThrowsAsync<FlightFullException>(() =>
                new BookingService(db).BookAsync("OS201", new BookRequest("Boris Kern", 2)));

            Assert.Equal("OS201", exception.Number);
            Assert.Equal(2, exception.Capacity);
            Assert.Equal(1, exception.Booked);
            Assert.Equal(2, exception.Requested);
        }

        // Checked through a new connection: the refused booking must not have been stored.
        await using (var check = database.NewContext())
        {
            var stored = await check.Reservations.SingleAsync();
            Assert.Equal("Anna Weber", stored.PassengerName);
        }
    }

    [Fact]
    public async Task Book_counts_only_active_reservations_against_the_capacity()
    {
        using var database = new TestDatabase();
        await database.CreateFlightAsync("OS201", capacity: 2);

        string cancelled;

        await using (var db = database.NewContext())
        {
            var service = new BookingService(db);
            cancelled = (await service.BookAsync("OS201", new BookRequest("Anna Weber", 2))).Reference;
            await service.CancelAsync(cancelled);
        }

        // The flight is full, but the cancelled booking gave its two seats back.
        await using (var db = database.NewContext())
        {
            var service = new BookingService(db);
            var replacement = await service.BookAsync("OS201", new BookRequest("Boris Kern", 2));

            Assert.Equal(0, replacement.SeatsAvailable);

            // And now it really is full.
            var exception = await Assert.ThrowsAsync<FlightFullException>(() =>
                service.BookAsync("OS201", new BookRequest("Clara Muth", 1)));

            Assert.Equal(2, exception.Booked);
        }
    }

    [Fact]
    public async Task Cancel_frees_the_seats_for_another_booking()
    {
        using var database = new TestDatabase();
        await database.CreateFlightAsync("OS201", capacity: 1);

        await using (var db = database.NewContext())
        {
            var service = new BookingService(db);
            var first = await service.BookAsync("OS201", new BookRequest("Anna Weber", 1));
            await service.CancelAsync(first.Reference);
        }

        await using (var db = database.NewContext())
        {
            var service = new BookingService(db);
            var second = await service.BookAsync("OS201", new BookRequest("Boris Kern", 1));

            Assert.Equal(0, second.SeatsAvailable);

            var flight = await service.FindFlightAsync("OS201");

            Assert.NotNull(flight);
            Assert.Equal(2, flight.Reservations.Count);
            Assert.Contains(flight.Reservations, x => x.Status == ReservationStatus.Cancelled);
            Assert.Contains(flight.Reservations, x => x.Status == ReservationStatus.Held);
        }
    }
}
