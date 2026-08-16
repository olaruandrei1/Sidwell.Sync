using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sidwell.Sync.Application.Contracts.Infrastructure;
using Sidwell.Sync.Domain.ConfigurableObjects;
using Sidwell.Sync.Domain.External;
using Sidwell.Sync.Domain.Models;

namespace Sidwell.Sync.Infrastructure.Implementations.Gemini;

public sealed class GeminiClient(
    IHttpClientFactory httpClientFactory,
    IOptions<GeminiOptions> options,
    ILogger<GeminiClient> logger) : IGeminiClient
{
    public const string HttpClientName = "gemini";

    private static readonly SemaphoreSlim Gate = new(3, 3);

    private static readonly JsonSerializerOptions ParseOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly GeminiOptions _options = options.Value;

    public async Task<string?> SummarizeNewsAsync(IReadOnlyList<string> newsTitles, CancellationToken ct = default)
    {
        if (newsTitles.Count == 0)
            return null;

        var prompt = new StringBuilder();
        prompt.AppendLine("Summarize the following recent news articles for a stock ticker into 3-4 bullet points. Focus on key financial impacts, earnings results, product launches, or leadership/management updates. Avoid introductory or concluding text.");
        prompt.AppendLine();
        prompt.AppendLine("Articles:");

        for (int i = 0; i < newsTitles.Count; i++)
            prompt.AppendLine($"{i + 1}. {newsTitles[i]}");

        string? content = await PostGenerateAsync(prompt.ToString(), jsonMode: false, useSearch: false, ct);

        return string.IsNullOrWhiteSpace(content) ? null : content.Trim();
    }

    public async Task<SentimentResult?> AnalyzeSentimentAsync(string symbol, IReadOnlyList<string> newsTitles, CancellationToken ct = default)
    {
        if (newsTitles.Count == 0)
            return null;

        var prompt = new StringBuilder();
        prompt.AppendLine($"Analyze these news items about {symbol}. Return ONLY raw JSON with this exact shape:");
        prompt.AppendLine("{\"sentiment_score\": <number -100 to 100>, \"confidence\": <number 0-100>, \"key_themes\": [<string>...], \"risk_factors\": [<string>...], \"catalysts\": [<string>...]}");
        prompt.AppendLine();
        prompt.AppendLine("News:");

        int limit = Math.Min(newsTitles.Count, 20);

        for (int i = 0; i < limit; i++)
            prompt.AppendLine($"{i + 1}. {newsTitles[i]}");

        return await PostAndParseAsync<SentimentResult>(prompt.ToString(), symbol, ct);
    }

    public async Task<Synthesis?> SynthesizeTickerAsync(string symbol, IReadOnlyList<PriceBar> ohlcv, string? newsSummary, CancellationToken ct = default)
    {
        var prompt = new StringBuilder();
        prompt.AppendLine($"You are a financial analyst expert. Synthesize the provided market data and news summary for ticker symbol {symbol}.");
        prompt.AppendLine("Return a JSON object with the following fields:");
        prompt.AppendLine("\"summary\": a brief summary (1-2 sentences) of the overall analysis.");
        prompt.AppendLine("\"worth_score\": a number from 0 to 100 representing investment worthiness.");
        prompt.AppendLine("\"estimates\": a JSON object with float keys \"low\" and \"high\" (forward price boundaries).");
        prompt.AppendLine("\"composite_score\": a number from -100 to 100 indicating market/technical outlook.");
        prompt.AppendLine("\"direction\": exactly one of \"BULL\", \"BEAR\", or \"NEUTRAL\".");
        prompt.AppendLine("\"critical_alert\": an object with keys \"is_critical\" (bool), \"severity\" (\"CRITICAL\", \"HARD\", or \"\"), \"title\" (string), \"message\" (string).");
        prompt.AppendLine();

        if (ohlcv.Count > 0)
        {
            prompt.AppendLine("Recent historical prices (most recent first):");

            int limit = Math.Min(ohlcv.Count, 15);

            for (int i = 0; i < limit; i++)
            {
                PriceBar bar = ohlcv[i];
                prompt.AppendLine($"- Date: {bar.Date:yyyy-MM-dd}, Open: {bar.Open:F2}, High: {bar.High:F2}, Low: {bar.Low:F2}, Close: {bar.Close:F2}, Volume: {bar.Volume}");
            }

            prompt.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(newsSummary))
        {
            prompt.AppendLine("News summary:");
            prompt.AppendLine(newsSummary);
        }

        return await PostAndParseAsync<Synthesis>(prompt.ToString(), symbol, ct);
    }

    public async Task<IReadOnlyList<GeminiDividendTaxRate>?> FetchDividendTaxRatesAsync(IReadOnlyList<string> countryCodes, CancellationToken ct = default)
    {
        if (countryCodes.Count == 0)
            return null;

        string codes = string.Join(", ", countryCodes);

        var prompt = new StringBuilder();
        prompt.AppendLine("Provide the current statutory withholding/income tax rate applied to cash dividends received by a resident individual investor, for each of the following countries.");
        prompt.AppendLine($"Countries (codes): {codes}.");
        prompt.AppendLine("Return a JSON object with this exact shape:");
        prompt.AppendLine("{\"rates\": [{\"country_code\": \"<code>\", \"rate_percent\": <number>, \"notes\": \"<short note>\", \"source_url\": \"<url>\"}, ...]}");
        prompt.AppendLine("rate_percent must be the percentage as a number (e.g. 16 for 16%), not a fraction. Include every requested country exactly once, using the same codes provided.");

        GeminiDividendTaxResponse? parsed = await PostAndParseAsync<GeminiDividendTaxResponse>(prompt.ToString(), "dividend-tax", ct, useSearch: true);

        return parsed?.Rates;
    }

    private async Task<T?> PostAndParseAsync<T>(string prompt, string context, CancellationToken ct, bool useSearch = false) where T : class
    {
        string? content = await PostGenerateAsync(prompt, jsonMode: true, useSearch, ct);

        if (string.IsNullOrWhiteSpace(content))
            return null;

        string clean = StripJsonFences(content);

        try
        {
            return JsonSerializer.Deserialize<T>(clean, ParseOptions);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Gemini: failed to parse {Type} response for {Context}", typeof(T).Name, context);

            return null;
        }
    }

    private async Task<string?> PostGenerateAsync(string prompt, bool jsonMode, bool useSearch, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            logger.LogWarning("Gemini: API key is not configured, skipping call");

            return null;
        }

        var payload = new Dictionary<string, object?>
        {
            ["contents"] = new[] { new { parts = new[] { new { text = prompt } } } },
        };

        // google_search grounding cannot be combined with a forced JSON response mime type,
        // so grounded calls ask for JSON in the prompt and rely on defensive fence-stripping.
        if (useSearch)
            payload["tools"] = new[] { new { google_search = new { } } };
        else if (jsonMode)
            payload["generationConfig"] = new { responseMimeType = "application/json" };

        await Gate.WaitAsync(ct);

        try
        {
            HttpClient client = httpClientFactory.CreateClient(HttpClientName);

            using HttpResponseMessage response =
                await client.PostAsJsonAsync($"models/{_options.Model}:generateContent", payload, ct);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Gemini: call returned status {Status}", (int)response.StatusCode);

                return null;
            }

            GeminiGenerateResponse? body = await response.Content.ReadFromJsonAsync<GeminiGenerateResponse>(ct);

            string? content = body?.Candidates?.FirstOrDefault()?.Content?.Parts is { } parts
                ? string.Concat(parts.Select(p => p.Text))
                : null;

            if (string.IsNullOrWhiteSpace(content))
            {
                logger.LogWarning("Gemini: response contained no content");

                return null;
            }

            return content;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Gemini: call failed");

            return null;
        }
        finally
        {
            Gate.Release();
        }
    }

    private static string StripJsonFences(string raw)
    {
        string clean = raw.Trim();

        if (clean.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            clean = clean[7..];
        else if (clean.StartsWith("```", StringComparison.Ordinal))
            clean = clean[3..];

        if (clean.EndsWith("```", StringComparison.Ordinal))
            clean = clean[..^3];

        return clean.Trim();
    }
}
