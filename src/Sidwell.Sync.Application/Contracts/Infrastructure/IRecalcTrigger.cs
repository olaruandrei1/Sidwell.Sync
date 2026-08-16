namespace Sidwell.Sync.Application.Contracts.Infrastructure;

public interface IRecalcTrigger
{
    Task TriggerAsync(Guid tickerId, DateOnly asOf, CancellationToken ct = default);
}
