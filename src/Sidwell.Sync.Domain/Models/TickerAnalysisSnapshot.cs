using Sidwell.Sync.Domain.External;

namespace Sidwell.Sync.Domain.Models;

public sealed record TickerAnalysisSnapshot(
    string Symbol,
    string? NewsSummary,
    SentimentResult? Sentiment,
    Synthesis? Synthesis,
    DateTimeOffset GeneratedAt);
