namespace Sidwell.Sync.Domain.Entities;

public sealed class Ticker
{
    public Guid Id { get; set; }
    public string Symbol { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Exchange { get; set; } = null!;
    public string Currency { get; set; } = null!;
    public string? SecCik { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
