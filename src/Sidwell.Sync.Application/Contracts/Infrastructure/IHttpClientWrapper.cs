namespace Sidwell.Sync.Application.Contracts.Infrastructure;

public interface IHttpClientWrapper
{
    Task<T?> GetAsync<T>(string url, CancellationToken ct = default);

    Task<T?> PostAsync<T>(string url, object? body = null, CancellationToken ct = default);

    Task PostAsync(string url, CancellationToken ct = default);
}
