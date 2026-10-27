using FlightReservation.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace FlightReservation.Api;

public sealed class DomainExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            FlightNotFoundException => (StatusCodes.Status404NotFound, "Unknown flight"),
            ReservationNotFoundException => (StatusCodes.Status404NotFound, "Unknown reservation"),
            DuplicateFlightNumberException => (StatusCodes.Status409Conflict, "Flight number already used"),
            FlightFullException => (StatusCodes.Status409Conflict, "Not enough seats left"),
            InvalidReservationStateException => (StatusCodes.Status409Conflict, "Reservation cannot be changed"),
            ArgumentException => (StatusCodes.Status400BadRequest, "Invalid request"),
            _ => (0, string.Empty)
        };

        if (status == 0)
        {
            return false;
        }

        httpContext.Response.StatusCode = status;

        await httpContext.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = exception.Message,
                Instance = httpContext.Request.Path
            },
            cancellationToken);

        return true;
    }
}
