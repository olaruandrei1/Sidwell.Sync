using Microsoft.EntityFrameworkCore;
using Sidwell.Sync.Application.Contracts.Persistence;
using Sidwell.Sync.Domain.Entities;
using Sidwell.Sync.Persistence.Configurations;

namespace Sidwell.Sync.Persistence;

public sealed class UnitOfWork(SyncDbContext context, IDapperExecutor dapper) : IUnitOfWork
{
    public DbSet<Ticker> Tickers => context.Tickers;
    public DbSet<PriceHistory> PriceHistory => context.PriceHistory;
    public DbSet<SyncJob> SyncJobs => context.SyncJobs;

    public IDapperExecutor Dapper => dapper;

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => context.SaveChangesAsync(ct);
}
