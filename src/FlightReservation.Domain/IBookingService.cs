namespace FlightReservation.Domain;

public interface IBookingService
{
    /// <summary>
    /// Creates a flight. Throws <see cref="ArgumentException"/> for blank values or a capacity
    /// below one, and <see cref="DuplicateFlightNumberException"/> when the number is taken.
    /// </summary>
    Task<FlightView> CreateFlightAsync(CreateFlightRequest request, CancellationToken cancellationToken = default);

    /// <summary>Returns the flight with its reservations, or <c>null</c> when the number is unknown.</summary>
    Task<FlightView?> FindFlightAsync(string number, CancellationToken cancellationToken = default);

    /// <summary>
    /// Books seats on a flight as <see cref="ReservationStatus.Held"/>.
    /// <para>
    /// Throws <see cref="FlightNotFoundException"/> for an unknown flight,
    /// <see cref="ArgumentException"/> for a seat count below one and
    /// <see cref="FlightFullException"/> when the booking does not fit on the flight.
    /// </para>
    /// </summary>
    Task<ReservationView> BookAsync(string flightNumber, BookRequest request, CancellationToken cancellationToken = default);

    /// <summary>Returns the reservation, or <c>null</c> when the reference is unknown.</summary>
    Task<ReservationView?> FindReservationAsync(string reference, CancellationToken cancellationToken = default);

    /// <summary>
    /// Turns a held reservation into a confirmed one.
    /// Throws <see cref="ReservationNotFoundException"/> and
    /// <see cref="InvalidReservationStateException"/>.
    /// </summary>
    Task<ReservationView> ConfirmAsync(string reference, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gives up a reservation and frees its seats.
    /// Throws <see cref="ReservationNotFoundException"/>.
    /// </summary>
    Task<ReservationView> CancelAsync(string reference, CancellationToken cancellationToken = default);
}
