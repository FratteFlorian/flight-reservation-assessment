using System.Text.Json.Serialization;
using FlightReservation.Api;
using FlightReservation.Data;
using FlightReservation.Domain;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var databasePath = builder.Configuration["Database:Path"]
    ?? Path.Combine(AppContext.BaseDirectory, "flights.db");

builder.Services.AddDbContext<BookingDbContext>(options => options.UseSqlite($"Data Source={databasePath}"));
builder.Services.AddScoped<IBookingService, BookingService>();

// Statuses travel as text ("Held"), not as numbers.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();

var app = builder.Build();

// SQLite has no migration pipeline in this exercise: the schema comes from the EF model.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.UseExceptionHandler();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost(
    "/api/flights",
    async (CreateFlightRequest request, IBookingService service, CancellationToken cancellationToken) =>
    {
        var flight = await service.CreateFlightAsync(request, cancellationToken);

        return Results.Created($"/api/flights/{flight.Number}", flight);
    });

app.MapGet(
    "/api/flights/{number}",
    async (string number, IBookingService service, CancellationToken cancellationToken) =>
    {
        var flight = await service.FindFlightAsync(number, cancellationToken);

        return flight is null ? Results.NotFound() : Results.Ok(flight);
    });

app.MapPost(
    "/api/flights/{number}/reservations",
    async (string number, BookRequest request, IBookingService service, CancellationToken cancellationToken) =>
    {
        var reservation = await service.BookAsync(number, request, cancellationToken);

        return Results.Created($"/api/reservations/{reservation.Reference}", reservation);
    });

app.MapGet(
    "/api/reservations/{reference}",
    async (string reference, IBookingService service, CancellationToken cancellationToken) =>
    {
        var reservation = await service.FindReservationAsync(reference, cancellationToken);

        return reservation is null ? Results.NotFound() : Results.Ok(reservation);
    });

app.MapPost(
    "/api/reservations/{reference}/confirm",
    async (string reference, IBookingService service, CancellationToken cancellationToken) =>
        Results.Ok(await service.ConfirmAsync(reference, cancellationToken)));

app.MapPost(
    "/api/reservations/{reference}/cancel",
    async (string reference, IBookingService service, CancellationToken cancellationToken) =>
        Results.Ok(await service.CancelAsync(reference, cancellationToken)));

app.Run();

/// <summary>Exposed so an integration test can host the API in-process.</summary>
public partial class Program;
