using System.Net.Http.Json;
using Sidwell.Sync.Application.Contracts.Infrastructure;

namespace Sidwell.Sync.Infrastructure.Implementations.Http;

public sealed class HttpClientWrapper(HttpClient http) : IHttpClientWrapper
{
    public Task<T?> GetAsync<T>(string url, CancellationToken ct = default) => http.GetFromJsonAsync<T>(url, ct);

    public async Task<T?> PostAsync<T>(string url, object? body = null, CancellationToken ct = default)
    {
        using HttpResponseMessage response = await http.PostAsJsonAsync(url, body, ct);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<T>(ct);
    }

    public async Task PostAsync(string url, CancellationToken ct = default)
    {
        using HttpResponseMessage response = await http.PostAsync(url, content: null, ct);

        response.EnsureSuccessStatusCode();
    }
}
