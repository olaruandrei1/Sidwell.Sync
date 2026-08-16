using Sidwell.Sync.Application.Contracts.Infrastructure;
using Sidwell.Sync.Domain.Helpers;
using Sidwell.Sync.Domain.Models;

namespace Sidwell.Sync.Infrastructure.ThirdParties.Yahoo;

public sealed class YahooBridgeProfileSource(IHttpClientWrapper http) : ITickerProfileSource
{
    public async Task<TickerProfile?> GetProfileAsync(string symbol, CancellationToken ct = default)
    {
        var url = $"api/v1/profile?symbol={Uri.EscapeDataString(SymbolNormalizer.ForExternalApi(symbol))}";
        var response = await http.GetAsync<YahooProfileResponse>(url, ct);

        if (response is null || string.IsNullOrWhiteSpace(response.Name))
            return null;

        return new TickerProfile(response.Name, response.Currency, response.Exchange);
    }

    private sealed record YahooProfileResponse(string? Name, string? Currency, string? Exchange);
}
