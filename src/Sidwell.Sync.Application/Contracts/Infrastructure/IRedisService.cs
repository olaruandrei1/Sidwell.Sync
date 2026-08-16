namespace Sidwell.Sync.Application.Contracts.Infrastructure;

public interface IRedisService
{
    Task<long> IncrementAsync(string key, CancellationToken ct = default);

    Task ExpireAsync(string key, TimeSpan expiry, CancellationToken ct = default);

    Task<bool> SetIfNotExistsAsync(string key, string value, TimeSpan expiry, CancellationToken ct = default);

    Task<TimeSpan?> TimeToLiveAsync(string key, CancellationToken ct = default);

    Task SetAsync(string key, string value, CancellationToken ct = default);

    Task SetAsync(string key, string value, TimeSpan expiry, CancellationToken ct = default);

    Task<string?> GetAsync(string key, CancellationToken ct = default);

    Task DeleteAsync(string key, CancellationToken ct = default);
}
