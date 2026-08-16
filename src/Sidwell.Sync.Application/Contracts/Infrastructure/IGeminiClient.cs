using Sidwell.Sync.Domain.External;
using Sidwell.Sync.Domain.Models;

namespace Sidwell.Sync.Application.Contracts.Infrastructure;

public interface IGeminiClient
{
    Task<string?> SummarizeNewsAsync(IReadOnlyList<string> newsTitles, CancellationToken ct = default);

    Task<SentimentResult?> AnalyzeSentimentAsync(string symbol, IReadOnlyList<string> newsTitles, CancellationToken ct = default);

    Task<Synthesis?> SynthesizeTickerAsync(string symbol, IReadOnlyList<PriceBar> ohlcv, string? newsSummary, CancellationToken ct = default);

    Task<IReadOnlyList<GeminiDividendTaxRate>?> FetchDividendTaxRatesAsync(IReadOnlyList<string> countryCodes, CancellationToken ct = default);
}
