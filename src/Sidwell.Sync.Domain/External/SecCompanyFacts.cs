using System.Text.Json.Serialization;

namespace Sidwell.Sync.Domain.External;

public sealed record SecCompanyFacts(
    [property: JsonPropertyName("facts")] SecFacts? Facts);

public sealed record SecFacts(
    [property: JsonPropertyName("us-gaap")] Dictionary<string, SecFact>? UsGaap);

public sealed record SecFact(
    [property: JsonPropertyName("units")] Dictionary<string, List<SecFactValue>>? Units);

public sealed record SecFactValue(
    [property: JsonPropertyName("start")] string? Start,
    [property: JsonPropertyName("end")] string? End,
    [property: JsonPropertyName("val")] decimal Val,
    [property: JsonPropertyName("form")] string? Form,
    [property: JsonPropertyName("fy")] int? Fy,
    [property: JsonPropertyName("fp")] string? Fp,
    [property: JsonPropertyName("filed")] string? Filed,
    [property: JsonPropertyName("frame")] string? Frame);
