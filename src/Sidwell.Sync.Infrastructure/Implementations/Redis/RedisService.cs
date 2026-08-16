using Sidwell.Sync.Application.Contracts.Infrastructure;
using StackExchange.Redis;

namespace Sidwell.Sync.Infrastructure.Implementations.Redis;

public sealed class RedisService(IConnectionMultiplexer redis) : IRedisService
{
    private IDatabase Db => redis.GetDatabase();

    public async Task<long> IncrementAsync(string key, CancellationToken ct = default) =>
        await Db.StringIncrementAsync(key);

    public async Task ExpireAsync(string key, TimeSpan expiry, CancellationToken ct = default) =>
        await Db.KeyExpireAsync(key, expiry);

    public async Task<bool> SetIfNotExistsAsync(string key, string value, TimeSpan expiry, CancellationToken ct = default) =>
        await Db.StringSetAsync(key, value, expiry, When.NotExists);

    public async Task<TimeSpan?> TimeToLiveAsync(string key, CancellationToken ct = default) =>
        await Db.KeyTimeToLiveAsync(key);

    public async Task SetAsync(string key, string value, CancellationToken ct = default) =>
        await Db.StringSetAsync(key, value);

    public async Task SetAsync(string key, string value, TimeSpan expiry, CancellationToken ct = default) =>
        await Db.StringSetAsync(key, value, expiry);

    public async Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        var value = await Db.StringGetAsync(key);
        return value.HasValue ? value.ToString() : null;
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default) =>
        await Db.KeyDeleteAsync(key);
}
