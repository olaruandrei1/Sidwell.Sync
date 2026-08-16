using Sidwell.Sync.Domain.Enums;

namespace Sidwell.Sync.Domain.Models;

public sealed record PriceSyncResult(
    string Symbol,
    Guid TickerId,
    DataSource Source,
    int BarsWritten,
    bool Skipped
);
