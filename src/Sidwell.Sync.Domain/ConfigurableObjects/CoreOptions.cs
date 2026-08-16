namespace Sidwell.Sync.Domain.ConfigurableObjects;

public sealed class CoreOptions
{
    public const string SectionName = "Core";

    public string BaseUrl { get; set; } = "http://localhost:5000/";
    public string Secret { get; set; } = string.Empty;
}
