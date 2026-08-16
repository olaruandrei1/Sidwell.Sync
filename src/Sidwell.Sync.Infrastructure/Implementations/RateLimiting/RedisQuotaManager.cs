using Microsoft.Extensions.Logging;
using Sidwell.Sync.Application.Contracts.Infrastructure;
using Sidwell.Sync.Domain.Enums;

namespace Sidwell.Sync.Infrastructure.Implementations.RateLimiting;

public sealed class RedisQuotaManager(IRedisService redis, ILogger<RedisQuotaManager> logger) : IQuotaManager
{
    public async Task WaitForSlotAsync(DataSource source, CancellationToken ct = default)
    {
        if (!QuotaRules.ByApi.TryGetValue(source, out var rule))
            return;

        string api = source.ToString().ToLowerInvariant();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        if (rule.MaxPerDay > 0)
        {
            string dayKey = $"ratelimit:{api}:day:{now:yyyyMMdd}";

            long count = await redis.IncrementAsync(dayKey, ct);

            if (count == 1)
                await redis.ExpireAsync(dayKey, TimeSpan.FromHours(25), ct);

            if (count > rule.MaxPerDay)
                throw new InvalidOperationException($"Daily quota exceeded for {api} (limit {rule.MaxPerDay}).");
        }

        if (rule.MaxPerMinute > 0)
        {
            while (true)
            {
                ct.ThrowIfCancellationRequested();

                string minuteKey = $"ratelimit:{api}:min:{now.ToUnixTimeSeconds() / 60}";
                long count = await redis.IncrementAsync(minuteKey, ct);
                
                if (count == 1)
                    await redis.ExpireAsync(minuteKey, TimeSpan.FromMinutes(2), ct);
                
                if (count <= rule.MaxPerMinute)
                    break;

                await Task.Delay(TimeSpan.FromSeconds(60 - (now.ToUnixTimeSeconds() % 60)), ct);

                now = DateTimeOffset.UtcNow;
            }
        }

        if (rule.MinInterval > TimeSpan.Zero)
        {
            string lockKey = $"ratelimit:{api}:interval_lock";

            while (true)
            {
                ct.ThrowIfCancellationRequested();

                if (await redis.SetIfNotExistsAsync(lockKey, "1", rule.MinInterval, ct))
                    break;

                TimeSpan? ttl = await redis.TimeToLiveAsync(lockKey, ct);

                await Task.Delay(ttl is { } remaining && remaining > TimeSpan.Zero ? remaining : TimeSpan.FromMilliseconds(50), ct);
            }
        }
    }

    public async Task RecordResultAsync(DataSource source, int statusCode)
    {
        string api = source.ToString().ToLowerInvariant();
        string errorsKey = $"{api}:breaker:consecutive_errors";
        string stopKey = $"{api}:breaker:stop";

        if (statusCode is 403 or 429)
        {
            long errors = await redis.IncrementAsync(errorsKey);
            
            if (errors >= 3)
            {
                await redis.SetAsync(stopKey, "true");
            
                logger.LogError("Circuit breaker for {Api}: 3 consecutive 403/429 errors, stopped (manual reset required).", api);
            }
        }
        else if (statusCode is >= 200 and < 300)
        {
            await redis.DeleteAsync(errorsKey);
        }
    }

    public async Task<bool> IsCircuitOpenAsync(DataSource source)
    {
        string? value = await redis.GetAsync($"{source.ToString().ToLowerInvariant()}:breaker:stop");

        return value == "true";
    }
}
