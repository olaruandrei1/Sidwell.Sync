using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Sidwell.Sync.Domain.Entities;
using Sidwell.Sync.Domain.Enums;

namespace Sidwell.Sync.Persistence.Configurations;

public sealed class SyncDbContext(DbContextOptions<SyncDbContext> options) : DbContext(options)
{
    public DbSet<Ticker> Tickers => Set<Ticker>();
    public DbSet<PriceHistory> PriceHistory => Set<PriceHistory>();
    public DbSet<SyncJob> SyncJobs => Set<SyncJob>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        ValueConverter<SyncJobStatus, string> statusConverter = new ValueConverter<SyncJobStatus, string>(
            v => v.ToString().ToUpperInvariant(),
            v => Enum.Parse<SyncJobStatus>(v, ignoreCase: true));

        b.Entity<Ticker>(e =>
        {
            e.ToTable("tickers");
            e.HasKey(x => x.Id);
            e.Property(x => x.Currency).HasColumnType("char(3)");
        });

        b.Entity<PriceHistory>(e =>
        {
            e.ToTable("price_history");
            e.HasKey(x => new { x.TickerId, x.Date });
        });

        b.Entity<SyncJob>(e =>
        {
            e.ToTable("sync_jobs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasConversion(statusConverter).HasMaxLength(20);
        });
    }
}
