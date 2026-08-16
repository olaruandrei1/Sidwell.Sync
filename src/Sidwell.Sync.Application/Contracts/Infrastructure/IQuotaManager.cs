using Sidwell.Sync.Domain.Enums;

namespace Sidwell.Sync.Application.Contracts.Infrastructure;

public interface IQuotaManager
{
    Task WaitForSlotAsync(DataSource source, CancellationToken ct = default);

    Task RecordResultAsync(DataSource source, int statusCode);

    Task<bool> IsCircuitOpenAsync(DataSource source);
}
