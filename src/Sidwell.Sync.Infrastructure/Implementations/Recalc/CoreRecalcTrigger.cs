using Microsoft.Extensions.Logging;
using Sidwell.Sync.Application.Contracts.Infrastructure;

namespace Sidwell.Sync.Infrastructure.Implementations.Recalc;

public sealed class CoreRecalcTrigger(IHttpClientWrapper http, ILogger<CoreRecalcTrigger> logger) : IRecalcTrigger
{
    public async Task TriggerAsync(Guid tickerId, DateOnly asOf, CancellationToken ct = default)
    {
        try
        {
            await http.PostAsync($"recalc/{tickerId}?asOf={asOf:yyyy-MM-dd}", ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Core recalc trigger for {Ticker} failed (non-fatal).", tickerId);
        }
    }
}
