using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sidwell.Sync.Application.Contracts.Application;
using Sidwell.Sync.Application.Contracts.Infrastructure;
using Sidwell.Sync.Application.Contracts.Persistence;
using Sidwell.Sync.Domain.Entities;
using Sidwell.Sync.Domain.Models;

namespace Sidwell.Sync.Application.Implementations;

public sealed class TickerProfileSyncService(
    IUnitOfWork uow,
    IEnumerable<ITickerProfileSource> profileSources,
    ILogger<TickerProfileSyncService> logger
) : ITickerProfileSyncService
{
    public async Task<bool> SyncProfileAsync(string symbol, CancellationToken ct = default)
    {
        Ticker ticker = await uow.Tickers.FirstOrDefaultAsync(t => t.Symbol == symbol, ct)
            ?? throw new InvalidOperationException($"Ticker '{symbol}' not found.");

        TickerProfile? profile = null;
        foreach (ITickerProfileSource source in profileSources)
        {
            TickerProfile? candidate = await source.GetProfileAsync(symbol, ct);
            if (candidate is null)
                continue;

            if (profile is null)
            {
                profile = candidate;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(profile.Name) && !string.IsNullOrWhiteSpace(candidate.Name))
                    profile = profile with { Name = candidate.Name };
                if (string.IsNullOrWhiteSpace(profile.Currency) && !string.IsNullOrWhiteSpace(candidate.Currency))
                    profile = profile with { Currency = candidate.Currency };
                if (string.IsNullOrWhiteSpace(profile.Exchange) && !string.IsNullOrWhiteSpace(candidate.Exchange))
                    profile = profile with { Exchange = candidate.Exchange };
            }

            if (!string.IsNullOrWhiteSpace(profile.Name) && !string.IsNullOrWhiteSpace(profile.Currency))
                break;
        }

        if (profile is null)
            return false;

        bool changed = false;

        if (!string.IsNullOrWhiteSpace(profile.Name) && (string.IsNullOrWhiteSpace(ticker.Name) || ticker.Name == ticker.Symbol))
        {
            ticker.Name = profile.Name;
            changed = true;
        }

        if (!string.IsNullOrWhiteSpace(profile.Exchange) &&
            (string.IsNullOrWhiteSpace(ticker.Exchange) || ticker.Exchange == "UNKNOWN"))
        {
            ticker.Exchange = profile.Exchange.Trim();
            changed = true;
        }

        if (!string.IsNullOrWhiteSpace(profile.Currency) && !string.Equals(ticker.Currency, profile.Currency.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            ticker.Currency = profile.Currency.Trim();
            changed = true;
        }

        if (changed)
            await uow.SaveChangesAsync(ct);

        logger.LogInformation("Profile {Symbol}: updated={Changed}", symbol, changed);
        
        return changed;
    }
}
