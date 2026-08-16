namespace Sidwell.Sync.Domain.ConfigurableObjects;

public sealed class AlphaVantageOptions
{
    public const string SectionName = "AlphaVantage";

    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://www.alphavantage.co/";
}
