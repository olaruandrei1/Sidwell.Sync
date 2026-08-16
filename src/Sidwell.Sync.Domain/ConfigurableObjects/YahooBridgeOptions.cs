namespace Sidwell.Sync.Domain.ConfigurableObjects;

public sealed class YahooBridgeOptions
{
    public const string SectionName = "YahooBridge";

    public string BaseUrl { get; set; } = "http://localhost:8000/";
}
