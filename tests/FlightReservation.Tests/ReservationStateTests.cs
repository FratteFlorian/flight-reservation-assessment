using FlightReservation.Data;
using FlightReservation.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FlightReservation.Tests;

public sealed class ReservationStateTests
{
    [Fact]
    public async Task Confirm_moves_a_held_reservation_to_confirmed()
    {
        using var database = new TestDatabase();
        await database.CreateFlightAsync("OS201", capacity: 4);

        await using var db = database.NewContext();
        var service = new BookingService(db);
        var held = await service.BookAsync("OS201", new BookRequest("Anna Weber", 2));

        var confirmed = await service.ConfirmAsync(held.Reference);

        Assert.Equal(held.Reference, confirmed.Reference);
        Assert.Equal(ReservationStatus.Confirmed, confirmed.Status);
        Assert.Equal(2, confirmed.SeatsAvailable);
    }

    [Fact]
    public async Task Confirm_keeps_an_already_confirmed_reservation_unchanged()
    {
        using var database = new TestDatabase();
        await database.CreateFlightAsync("OS201", capacity: 4);

        await using var db = database.NewContext();
        var service = new BookingService(db);
        var held = await service.BookAsync("OS201", new BookRequest("Anna Weber", 2));
        await service.ConfirmAsync(held.Reference);

        var again = await service.ConfirmAsync(held.Reference);

        Assert.Equal(ReservationStatus.Confirmed, again.Status);
        Assert.Equal(2, again.SeatsAvailable);
    }

    [Fact]
    public async Task Cancel_releases_the_seats()
    {
        using var database = new TestDatabase();
        await database.CreateFlightAsync("OS201", capacity: 4);

        await using var db = database.NewContext();
        var service = new BookingService(db);
        var held = await service.BookAsync("OS201", new BookRequest("Anna Weber", 2));

        var cancelled = await service.CancelAsync(held.Reference);

        Assert.Equal(ReservationStatus.Cancelled, cancelled.Status);
        Assert.Equal(4, cancelled.SeatsAvailable);
    }

    [Fact]
    public async Task Confirm_rejects_an_unknown_reference()
    {
        using var database = new TestDatabase();

        await using var db = database.NewContext();
        var exception = await Assert.ThrowsAsync<ReservationNotFoundException>(() =>
            new BookingService(db).ConfirmAsync("ZZZZZZ"));

        Assert.Equal("ZZZZZZ", exception.Reference);
    }

    [Fact]
    public async Task Cancel_rejects_an_unknown_reference()
    {
        using var database = new TestDatabase();

        await using var db = database.NewContext();
        await Assert.ThrowsAsync<ReservationNotFoundException>(() =>
            new BookingService(db).CancelAsync("ZZZZZZ"));
    }

    [Fact]
    public async Task FindReservation_returns_null_for_an_unknown_reference()
    {
        using var database = new TestDatabase();

        await using var db = database.NewContext();
        Assert.Null(await new BookingService(db).FindReservationAsync("ZZZZZZ"));
    }

    [Fact]
    public async Task Confirm_rejects_a_cancelled_reservation()
    {
        using var database = new TestDatabase();
        await database.CreateFlightAsync("OS201", capacity: 2);

        string reference;

        await using (var db = database.NewContext())
        {
            var service = new BookingService(db);
            reference = (await service.BookAsync("OS201", new BookRequest("Anna Weber", 2))).Reference;
            await service.CancelAsync(reference);
        }

        await using (var db = database.NewContext())
        {
            var exception = await Assert.ThrowsAsync<InvalidReservationStateException>(() =>
                new BookingService(db).ConfirmAsync(reference));

            Assert.Equal(reference, exception.Reference);
        }

        // The seats stay free: a cancelled booking cannot be brought back to life by confirming it.
        await using (var check = database.NewContext())
        {
            var stored = await check.Reservations.SingleAsync();
            Assert.Equal(ReservationStatus.Cancelled, stored.Status);
        }

        await using (var db = database.NewContext())
        {
            var flight = await new BookingService(db).FindFlightAsync("OS201");

            Assert.NotNull(flight);
            Assert.Equal(2, flight.SeatsAvailable);
        }
    }

    [Fact]
    public async Task Cancel_is_harmless_when_repeated()
    {
        using var database = new TestDatabase();
        await database.CreateFlightAsync("OS201", capacity: 2);

        string reference;

        await using (var db = database.NewContext())
        {
            var service = new BookingService(db);
            reference = (await service.BookAsync("OS201", new BookRequest("Anna Weber", 2))).Reference;
            await service.CancelAsync(reference);
        }

        await using (var db = database.NewContext())
        {
            var again = await new BookingService(db).CancelAsync(reference);

            Assert.Equal(ReservationStatus.Cancelled, again.Status);
            Assert.Equal(2, again.SeatsAvailable);
        }

        // Cancelling twice must not hand the seats out twice.
        await using (var check = database.NewContext())
        {
            Assert.Equal(1, await check.Reservations.CountAsync());

            var flight = await new BookingService(check).FindFlightAsync("OS201");

            Assert.NotNull(flight);
            Assert.Equal(2, flight.SeatsAvailable);
        }
    }
}
