using Sidwell.Sync.Domain.Models;

namespace Sidwell.Sync.Application.Contracts.Application;

public interface ISecSyncService
{
    Task<SecSyncResult> SyncAsync(string symbol, CancellationToken ct = default);
}
