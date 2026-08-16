using Sidwell.Sync.Application.Contracts.Infrastructure;
using Sidwell.Sync.Domain.Enums;

namespace Sidwell.Sync.Infrastructure.Implementations.Http;

public sealed class QuotaDelegatingHandler(IQuotaManager quota, DataSource source) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (await quota.IsCircuitOpenAsync(source))
            throw new HttpRequestException($"Circuit breaker open for {source}, request blocked.");

        await quota.WaitForSlotAsync(source, ct);

        HttpResponseMessage response = await base.SendAsync(request, ct);

        await quota.RecordResultAsync(source, (int)response.StatusCode);
        
        return response;
    }
}
