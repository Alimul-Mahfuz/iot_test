using Microsoft.EntityFrameworkCore;

namespace SensorApi;

public sealed class SensorDbContext(DbContextOptions<SensorDbContext> options) : DbContext(options)
{
    public DbSet<SensorReading> Readings => Set<SensorReading>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SensorReading>()
            .HasKey(reading => reading.Id);

        modelBuilder.Entity<SensorReading>()
            .Property(reading => reading.ReceivedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
    }
}
