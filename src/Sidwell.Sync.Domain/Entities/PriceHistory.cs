namespace Sidwell.Sync.Domain.Entities;

public sealed class PriceHistory
{
    public Guid TickerId { get; set; }
    public DateOnly Date { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public long Volume { get; set; }
    public string Source { get; set; } = null!;
}
