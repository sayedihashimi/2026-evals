using Microsoft.EntityFrameworkCore;

namespace FunctionApp1.HealthChecks;

public sealed class HealthCheckDbContext(DbContextOptions<HealthCheckDbContext> options) : DbContext(options)
{
    public DbSet<HealthCheckResult> Results => Set<HealthCheckResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<HealthCheckResult>();
        entity.HasKey(e => e.Id);

        entity.Property(e => e.CheckedUrl).IsRequired();
        entity.Property(e => e.TimestampUtc).IsRequired();
        entity.Property(e => e.IsSuccess).IsRequired();

        entity.Property(e => e.StatusCode);
        entity.Property(e => e.ErrorMessage);

        entity.HasIndex(e => e.TimestampUtc);
    }
}
