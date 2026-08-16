using Sidwell.Sync.Domain.Enums;

namespace Sidwell.Sync.Domain.Entities;

public sealed class SyncJob
{
    public Guid Id { get; set; }
    public string Source { get; set; } = null!;
    public SyncJobStatus Status { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public string? Error { get; set; }
}
