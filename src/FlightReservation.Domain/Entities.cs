namespace FlightReservation.Domain;

/// <summary>Life cycle of a reservation.</summary>
public enum ReservationStatus
{
    /// <summary>Seats are blocked, the booking is not paid yet.</summary>
    Held = 0,

    /// <summary>Seats are blocked and the booking is final.</summary>
    Confirmed = 1,

    /// <summary>Booking was given up; the seats are free again.</summary>
    Cancelled = 2
}

public sealed class Flight
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Flight number, unique, for example <c>OS201</c>.</summary>
    public string Number { get; set; } = string.Empty;

    /// <summary>Three letter airport code the flight starts at.</summary>
    public string Origin { get; set; } = string.Empty;

    /// <summary>Three letter airport code the flight ends at.</summary>
    public string Destination { get; set; } = string.Empty;

    public DateTimeOffset DepartureAt { get; set; }

    /// <summary>Number of seats that may ever be sold on this flight.</summary>
    public int Capacity { get; set; }

    public List<Reservation> Reservations { get; set; } = [];
}

public sealed class Reservation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Booking code handed to the passenger, unique, for example <c>K7PM2Q</c>.</summary>
    public string Reference { get; set; } = string.Empty;

    public Guid FlightId { get; set; }

    public Flight? Flight { get; set; }

    public string PassengerName { get; set; } = string.Empty;

    /// <summary>How many seats this reservation takes.</summary>
    public int Seats { get; set; }

    public ReservationStatus Status { get; set; } = ReservationStatus.Held;

    public DateTimeOffset CreatedAt { get; set; }
}
