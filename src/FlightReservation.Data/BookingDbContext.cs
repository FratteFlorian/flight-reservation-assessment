using FlightReservation.Domain;
using Microsoft.EntityFrameworkCore;

namespace FlightReservation.Data;

public sealed class BookingDbContext(DbContextOptions<BookingDbContext> options) : DbContext(options)
{
    public DbSet<Flight> Flights => Set<Flight>();

    public DbSet<Reservation> Reservations => Set<Reservation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var flight = modelBuilder.Entity<Flight>();
        flight.HasKey(x => x.Id);
        flight.Property(x => x.Number).IsRequired().HasMaxLength(16);
        flight.HasIndex(x => x.Number).IsUnique();
        flight.Property(x => x.Origin).IsRequired().HasMaxLength(8);
        flight.Property(x => x.Destination).IsRequired().HasMaxLength(8);
        flight.HasMany(x => x.Reservations)
            .WithOne(x => x.Flight!)
            .HasForeignKey(x => x.FlightId)
            .OnDelete(DeleteBehavior.Cascade);

        var reservation = modelBuilder.Entity<Reservation>();
        reservation.HasKey(x => x.Id);
        reservation.Property(x => x.Reference).IsRequired().HasMaxLength(8);
        reservation.HasIndex(x => x.Reference).IsUnique();
        reservation.Property(x => x.PassengerName).IsRequired().HasMaxLength(80);
        reservation.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
    }
}
