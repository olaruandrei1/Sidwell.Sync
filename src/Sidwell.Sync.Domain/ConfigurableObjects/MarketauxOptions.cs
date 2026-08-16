namespace Sidwell.Sync.Domain.ConfigurableObjects;

public sealed class MarketauxOptions
{
    public const string SectionName = "Marketaux";

    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.marketaux.com/v1/";
}
