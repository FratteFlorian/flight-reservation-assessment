using FlightReservation.Domain;
using Microsoft.EntityFrameworkCore;

namespace FlightReservation.Data;

public sealed class BookingService(BookingDbContext db) : IBookingService
{
    private const string ReferenceAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int ReferenceLength = 6;

    public async Task<FlightView> CreateFlightAsync(
        CreateFlightRequest request,
        CancellationToken cancellationToken = default)
    {
        var number = Required(request.Number, "flight number", 16);
        var origin = Required(request.Origin, "origin", 8);
        var destination = Required(request.Destination, "destination", 8);

        if (request.Capacity < 1)
        {
            throw new ArgumentException("A flight needs at least one seat.", nameof(request));
        }

        if (await db.Flights.AnyAsync(x => x.Number == number, cancellationToken))
        {
            throw new DuplicateFlightNumberException(number);
        }

        var flight = new Flight
        {
            Number = number,
            Origin = origin,
            Destination = destination,
            DepartureAt = request.DepartureAt,
            Capacity = request.Capacity
        };

        db.Flights.Add(flight);
        await db.SaveChangesAsync(cancellationToken);

        return new FlightView(
            flight.Id,
            flight.Number,
            flight.Origin,
            flight.Destination,
            flight.DepartureAt,
            flight.Capacity,
            flight.Capacity,
            []);
    }

    public async Task<FlightView?> FindFlightAsync(string number, CancellationToken cancellationToken = default)
    {
        var flight = await db.Flights
            .AsNoTracking()
            .Include(x => x.Reservations)
            .FirstOrDefaultAsync(x => x.Number == number, cancellationToken);

        if (flight is null)
        {
            return null;
        }

        var booked = await CountBookedSeatsAsync(flight.Id, cancellationToken);
        var seatsAvailable = flight.Capacity - booked;

        var reservations = flight.Reservations
            .OrderBy(x => x.CreatedAt)
            .Select(x => ToView(x, flight.Number, seatsAvailable))
            .ToList();

        return new FlightView(
            flight.Id,
            flight.Number,
            flight.Origin,
            flight.Destination,
            flight.DepartureAt,
            flight.Capacity,
            seatsAvailable,
            reservations);
    }

    public async Task<ReservationView> BookAsync(
        string flightNumber,
        BookRequest request,
        CancellationToken cancellationToken = default)
    {
        var number = Required(flightNumber, "flight number", 16);
        var passengerName = Required(request.PassengerName, "passenger name", 80);

        if (request.Seats < 1)
        {
            throw new ArgumentException("A booking needs at least one seat.", nameof(request));
        }

        var flight = await db.Flights.FirstOrDefaultAsync(x => x.Number == number, cancellationToken)
            ?? throw new FlightNotFoundException(number);

        var reservation = new Reservation
        {
            Reference = NewReference(),
            FlightId = flight.Id,
            PassengerName = passengerName,
            Seats = request.Seats,
            Status = ReservationStatus.Held,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.Reservations.Add(reservation);
        await db.SaveChangesAsync(cancellationToken);

        var booked = await CountBookedSeatsAsync(flight.Id, cancellationToken);

        return ToView(reservation, flight.Number, flight.Capacity - booked);
    }

    public async Task<ReservationView?> FindReservationAsync(
        string reference,
        CancellationToken cancellationToken = default)
    {
        var reservation = await db.Reservations
            .AsNoTracking()
            .Include(x => x.Flight)
            .FirstOrDefaultAsync(x => x.Reference == reference.Trim(), cancellationToken);

        if (reservation is null)
        {
            return null;
        }

        var booked = await CountBookedSeatsAsync(reservation.FlightId, cancellationToken);

        return ToView(reservation, reservation.Flight!.Number, reservation.Flight.Capacity - booked);
    }

    public async Task<ReservationView> ConfirmAsync(string reference, CancellationToken cancellationToken = default)
    {
        var reservation = await LoadReservationAsync(reference, cancellationToken);

        if (reservation.Status == ReservationStatus.Cancelled)
        {
            throw new InvalidReservationStateException(reference, "it is cancelled.");
        }

        reservation.Status = ReservationStatus.Confirmed;
        await db.SaveChangesAsync(cancellationToken);

        return await ViewAsync(reservation, cancellationToken);
    }

    public async Task<ReservationView> CancelAsync(string reference, CancellationToken cancellationToken = default)
    {
        var reservation = await LoadReservationAsync(reference, cancellationToken);

        if (reservation.Status == ReservationStatus.Cancelled)
        {
            // Cancelling twice must be harmless: nothing changes, and the seats stay free.
            return await ViewAsync(reservation, cancellationToken);
        }

        reservation.Status = ReservationStatus.Cancelled;
        await db.SaveChangesAsync(cancellationToken);

        return await ViewAsync(reservation, cancellationToken);
    }

    private async Task<Reservation> LoadReservationAsync(string reference, CancellationToken cancellationToken)
    {
        var normalized = Required(reference, "reference", 8);

        return await db.Reservations
            .Include(x => x.Flight)
            .FirstOrDefaultAsync(x => x.Reference == normalized, cancellationToken)
            ?? throw new ReservationNotFoundException(normalized);
    }

    private async Task<ReservationView> ViewAsync(Reservation reservation, CancellationToken cancellationToken)
    {
        var booked = await CountBookedSeatsAsync(reservation.FlightId, cancellationToken);

        return ToView(reservation, reservation.Flight!.Number, reservation.Flight.Capacity - booked);
    }

    /// <summary>
    /// Seats on this flight that are taken right now. A reservation occupies seats while it is
    /// <see cref="ReservationStatus.Held"/> or <see cref="ReservationStatus.Confirmed"/>;
    /// a cancelled reservation gives them back.
    /// </summary>
    private Task<int> CountBookedSeatsAsync(Guid flightId, CancellationToken cancellationToken)
        => db.Reservations
            .Where(x => x.FlightId == flightId && x.Status != ReservationStatus.Cancelled)
            .SumAsync(x => x.Seats, cancellationToken);

    private static ReservationView ToView(Reservation reservation, string flightNumber, int seatsAvailable)
        => new(
            reservation.Id,
            reservation.Reference,
            flightNumber,
            reservation.PassengerName,
            reservation.Seats,
            reservation.Status,
            seatsAvailable,
            reservation.CreatedAt);

    private static string NewReference()
    {
        Span<char> characters = stackalloc char[ReferenceLength];

        for (var i = 0; i < characters.Length; i++)
        {
            characters[i] = ReferenceAlphabet[Random.Shared.Next(ReferenceAlphabet.Length)];
        }

        return new string(characters);
    }

    private static string Required(string? value, string what, int maxLength)
    {
        var trimmed = value?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
        {
            throw new ArgumentException($"A {what} must not be empty.", what);
        }

        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"A {what} must not be longer than {maxLength} characters.", what);
        }

        return trimmed;
    }
}
