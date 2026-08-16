namespace Sidwell.Sync.Domain.Models;

public sealed record DiscoveredTicker(
    string Symbol,
    string Name,
    string Exchange,
    string Currency,
    string? Country,
    string? AssetType,
    string? SecCik
);
