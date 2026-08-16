namespace Sidwell.Sync.Application.Contracts.Infrastructure;

public interface ISecCikResolver
{
    Task<string?> ResolveAsync(string symbol, CancellationToken ct = default);
}
