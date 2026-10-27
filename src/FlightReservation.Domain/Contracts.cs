namespace FlightReservation.Domain;

public sealed record CreateFlightRequest(
    string Number,
    string Origin,
    string Destination,
    DateTimeOffset DepartureAt,
    int Capacity);

public sealed record BookRequest(string PassengerName, int Seats);

/// <param name="SeatsAvailable">Seats that are still free on the flight.</param>
public sealed record ReservationView(
    Guid Id,
    string Reference,
    string FlightNumber,
    string PassengerName,
    int Seats,
    ReservationStatus Status,
    int SeatsAvailable,
    DateTimeOffset CreatedAt);

public sealed record FlightView(
    Guid Id,
    string Number,
    string Origin,
    string Destination,
    DateTimeOffset DepartureAt,
    int Capacity,
    int SeatsAvailable,
    IReadOnlyList<ReservationView> Reservations);
