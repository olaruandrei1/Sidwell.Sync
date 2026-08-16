namespace Sidwell.Sync.Domain.ConfigurableObjects;

public sealed class SecOptions
{
    public const string SectionName = "Sec";

    public string UserAgent { get; set; } = "Sidwell research admin@sidwell.local";
    public string DataBaseUrl { get; set; } = "https://data.sec.gov/";
    public string CompanyTickersUrl { get; set; } = "https://www.sec.gov/files/company_tickers.json";
}
