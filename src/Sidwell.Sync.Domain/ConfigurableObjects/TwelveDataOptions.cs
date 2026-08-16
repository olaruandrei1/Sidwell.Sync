namespace Sidwell.Sync.Domain.ConfigurableObjects;

public sealed class TwelveDataOptions
{
    public const string SectionName = "TwelveData";

    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.twelvedata.com/";
}
