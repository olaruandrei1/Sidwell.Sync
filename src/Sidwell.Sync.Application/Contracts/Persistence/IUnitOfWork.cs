using Microsoft.EntityFrameworkCore;
using Sidwell.Sync.Domain.Entities;

namespace Sidwell.Sync.Application.Contracts.Persistence;

public interface IUnitOfWork
{
    DbSet<Ticker> Tickers { get; }
    DbSet<PriceHistory> PriceHistory { get; }
    DbSet<SyncJob> SyncJobs { get; }

    IDapperExecutor Dapper { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
