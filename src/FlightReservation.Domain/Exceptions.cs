namespace FlightReservation.Domain;

/// <summary>Thrown when a flight number is not known.</summary>
public sealed class FlightNotFoundException(string number)
    : Exception($"No flight with number '{number}'.")
{
    public string Number { get; } = number;
}

/// <summary>Thrown when a booking reference is not known.</summary>
public sealed class ReservationNotFoundException(string reference)
    : Exception($"No reservation with reference '{reference}'.")
{
    public string Reference { get; } = reference;
}

/// <summary>Thrown when a flight number is already used.</summary>
public sealed class DuplicateFlightNumberException(string number)
    : Exception($"A flight with number '{number}' already exists.")
{
    public string Number { get; } = number;
}

/// <summary>Thrown when a booking would put more seats on a flight than it has.</summary>
public sealed class FlightFullException(string number, int capacity, int booked, int requested)
    : Exception($"Flight '{number}' has {capacity} seats, {booked} are taken and {requested} were requested.")
{
    public string Number { get; } = number;

    public int Capacity { get; } = capacity;

    public int Booked { get; } = booked;

    public int Requested { get; } = requested;
}

/// <summary>Thrown when a reservation cannot move to the requested state.</summary>
public sealed class InvalidReservationStateException(string reference, string reason)
    : Exception($"Reservation '{reference}' cannot be changed: {reason}")
{
    public string Reference { get; } = reference;
}
