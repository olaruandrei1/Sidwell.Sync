using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sidwell.Sync.Application.Contracts.Application;
using Sidwell.Sync.Application.Contracts.Infrastructure;
using Sidwell.Sync.Domain.ConfigurableObjects;
using Sidwell.Sync.Domain.Enums;
using Sidwell.Sync.Infrastructure.ThirdParties.AlphaVantage;
using Sidwell.Sync.Infrastructure.ThirdParties.Finnhub;
using Sidwell.Sync.Infrastructure.ThirdParties.Frankfurter;
using Sidwell.Sync.Infrastructure.Implementations.Broadcast;
using Sidwell.Sync.Infrastructure.Implementations.Discovery;
using Sidwell.Sync.Infrastructure.Implementations.Gemini;
using Sidwell.Sync.Infrastructure.Implementations.Http;
using Sidwell.Sync.Infrastructure.ThirdParties.Marketaux;
using Sidwell.Sync.Infrastructure.Implementations.Notifications;
using Sidwell.Sync.Infrastructure.Implementations.RateLimiting;
using Sidwell.Sync.Infrastructure.Implementations.Recalc;
using Sidwell.Sync.Infrastructure.Implementations.Redis;
using Sidwell.Sync.Infrastructure.ThirdParties.Sec;
using Sidwell.Sync.Infrastructure.Implementations.Sources;
using Sidwell.Sync.Infrastructure.ThirdParties.TwelveData;
using Sidwell.Sync.Infrastructure.ThirdParties.Yahoo;
using StackExchange.Redis;

namespace Sidwell.Sync.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddSyncInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<FinnhubOptions>(config.GetSection(FinnhubOptions.SectionName));
        services.Configure<AlphaVantageOptions>(config.GetSection(AlphaVantageOptions.SectionName));
        services.Configure<TwelveDataOptions>(config.GetSection(TwelveDataOptions.SectionName));
        services.Configure<MarketauxOptions>(config.GetSection(MarketauxOptions.SectionName));
        services.Configure<FrankfurterOptions>(config.GetSection(FrankfurterOptions.SectionName));
        services.Configure<YahooBridgeOptions>(config.GetSection(YahooBridgeOptions.SectionName));
        services.Configure<SecOptions>(config.GetSection(SecOptions.SectionName));
        services.Configure<CoreOptions>(config.GetSection(CoreOptions.SectionName));
        services.Configure<GeminiOptions>(config.GetSection(GeminiOptions.SectionName));
        services.Configure<BroadcastOptions>(config.GetSection(BroadcastOptions.SectionName));

        string redisConnection = config.GetConnectionString("Redis") ?? "localhost:6379";

        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var options = ConfigurationOptions.Parse(redisConnection);
            options.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(options);
        });

        services.AddSingleton<IRedisService, RedisService>();
        services.AddSingleton<IQuotaManager, RedisQuotaManager>();

        AddQuotaClient<FinnhubOptions>(services, "finnhub", DataSource.Finnhub, o => o.BaseUrl);
        AddQuotaClient<AlphaVantageOptions>(services, "alphavantage", DataSource.AlphaVantage, o => o.BaseUrl);
        AddQuotaClient<TwelveDataOptions>(services, "twelvedata", DataSource.TwelveData, o => o.BaseUrl);
        AddQuotaClient<MarketauxOptions>(services, "marketaux", DataSource.Marketaux, o => o.BaseUrl);
        AddPlainClient<FrankfurterOptions>(services, "frankfurter", o => o.BaseUrl);
        AddPlainClient<YahooBridgeOptions>(services, "yahoo", o => o.BaseUrl);

        AddSecClient(services);

        services.AddHttpClient("core", (sp, c) =>
        {
            CoreOptions core = sp.GetRequiredService<IOptions<CoreOptions>>().Value;
            c.BaseAddress = new Uri(core.BaseUrl);
            if (!string.IsNullOrWhiteSpace(core.Secret))
                c.DefaultRequestHeaders.TryAddWithoutValidation("X-Internal-Secret", core.Secret);
        });

        services.AddScoped<IPriceSource>(sp => new AlphaVantagePriceSource(
            Wrap(sp, "alphavantage"), sp.GetRequiredService<IOptions<AlphaVantageOptions>>())
        );

        services.AddScoped<IPriceSource>(sp => new TwelveDataPriceSource(
            Wrap(sp, "twelvedata"), sp.GetRequiredService<IOptions<TwelveDataOptions>>())
        );

        services.AddScoped<IPriceSource>(sp => new YahooBridgePriceSource(Wrap(sp, "yahoo")));

        services.AddScoped<ISourceRouter, SourceRouter>();

        services.AddScoped<INewsSource>(sp => new FinnhubNewsSource(
            Wrap(sp, "finnhub"), sp.GetRequiredService<IOptions<FinnhubOptions>>())
        );

        services.AddScoped<INewsSource>(sp => new AlphaVantageNewsSource(
            Wrap(sp, "alphavantage"), sp.GetRequiredService<IOptions<AlphaVantageOptions>>())
        );
        
        services.AddScoped<INewsSource>(sp => new MarketauxNewsSource(
            Wrap(sp, "marketaux"), sp.GetRequiredService<IOptions<MarketauxOptions>>())
        );

        services.AddScoped<ITickerProfileSource>(sp => new FinnhubProfileSource(
            Wrap(sp, "finnhub"), sp.GetRequiredService<IOptions<FinnhubOptions>>())
        );

        services.AddScoped<ITickerProfileSource>(sp => new YahooBridgeProfileSource(Wrap(sp, "yahoo")));

        services.AddScoped<IFxRateSource>(sp => new FrankfurterFxSource(
            Wrap(sp, "frankfurter"), sp.GetRequiredService<TimeProvider>())
        );

        services.AddSingleton<ISecCikResolver, SecCikResolver>();

        services.AddScoped<IFundamentalsSource>(sp => new SecFundamentalsSource(
            Wrap(sp, "sec"), sp.GetRequiredService<ILogger<SecFundamentalsSource>>())
        );
        services.AddScoped<IFilingsSource>(sp => new SecFilingsSource(Wrap(sp, "sec")));

        services.AddScoped<SecTickerListSource>(sp => new SecTickerListSource(
            sp.GetRequiredService<IHttpClientFactory>(),
            sp.GetRequiredService<IOptions<SecOptions>>(),
            sp.GetRequiredService<ILogger<SecTickerListSource>>()));

        services.AddScoped<TwelveDataTickerListSource>(sp => new TwelveDataTickerListSource(
            sp.GetRequiredService<IHttpClientFactory>(),
            sp.GetRequiredService<ILogger<TwelveDataTickerListSource>>()));

        services.AddScoped<FinnhubTickerListSource>(sp => new FinnhubTickerListSource(
            sp.GetRequiredService<IHttpClientFactory>(),
            sp.GetRequiredService<IOptions<FinnhubOptions>>(),
            sp.GetRequiredService<ILogger<FinnhubTickerListSource>>()));

        services.AddScoped<ITickerDiscoveryService, TickerDiscoveryService>();

        services.AddScoped<IRecalcTrigger>(sp => new CoreRecalcTrigger(
            Wrap(sp, "core"), sp.GetRequiredService<ILogger<CoreRecalcTrigger>>())
        );

        services.AddHttpClient(BroadcastPublisher.HttpClientName, (sp, c) =>
        {
            BroadcastOptions broadcast = sp.GetRequiredService<IOptions<BroadcastOptions>>().Value;
            if (!string.IsNullOrWhiteSpace(broadcast.BaseUrl))
                c.BaseAddress = new Uri(broadcast.BaseUrl);
        });
        services.AddScoped<IBroadcastPublisher, BroadcastPublisher>();

        services.AddScoped<ISyncNotifier, BroadcastSyncNotifier>();

        services.AddHttpClient(GeminiClient.HttpClientName, (sp, c) =>
        {
            GeminiOptions gemini = sp.GetRequiredService<IOptions<GeminiOptions>>().Value;

            c.BaseAddress = new Uri(gemini.BaseUrl);
            c.Timeout = TimeSpan.FromSeconds(gemini.TimeoutSeconds);
            if (!string.IsNullOrWhiteSpace(gemini.ApiKey))
                c.DefaultRequestHeaders.TryAddWithoutValidation("x-goog-api-key", gemini.ApiKey);
        });

        services.AddScoped<IGeminiClient>(sp => new GeminiClient(
            sp.GetRequiredService<IHttpClientFactory>(),
            sp.GetRequiredService<IOptions<GeminiOptions>>(),
            sp.GetRequiredService<ILogger<GeminiClient>>())
        );

        return services;
    }

    private static HttpClientWrapper Wrap(IServiceProvider sp, string clientName) => new(sp.GetRequiredService<IHttpClientFactory>().CreateClient(clientName));

    private static void AddQuotaClient<TOptions>(IServiceCollection services, string name, DataSource source, Func<TOptions, string> baseUrl) where TOptions : class
    {
        AddResilientClient(services, name, sp => baseUrl(sp.GetRequiredService<IOptions<TOptions>>().Value))
            .AddHttpMessageHandler(sp => new QuotaDelegatingHandler(sp.GetRequiredService<IQuotaManager>(), source));
    }

    private static void AddPlainClient<TOptions>(IServiceCollection services, string name, Func<TOptions, string> baseUrl) where TOptions : class =>
        AddResilientClient(services, name, sp => baseUrl(sp.GetRequiredService<IOptions<TOptions>>().Value));

    private static void AddSecClient(IServiceCollection services)
    {
        IHttpClientBuilder client = services.AddHttpClient("sec", (sp, c) =>
        {
            SecOptions options = sp.GetRequiredService<IOptions<SecOptions>>().Value;
            
            c.BaseAddress = new Uri(options.DataBaseUrl);
            c.Timeout = Timeout.InfiniteTimeSpan;
            c.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", options.UserAgent);
            c.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");
            c.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Encoding", "gzip, deflate, br");
        });

        client.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All });

        client.AddStandardResilienceHandler(o =>
        {
            o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(90);
            o.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(3);
            o.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(3);
        });

        client.AddHttpMessageHandler(sp => new QuotaDelegatingHandler(sp.GetRequiredService<IQuotaManager>(), DataSource.SecEdgar));
    }

    private static IHttpClientBuilder AddResilientClient(IServiceCollection services, string name, Func<IServiceProvider, string> baseUrl)
    {
        IHttpClientBuilder client = services.AddHttpClient(name, (sp, c) =>
        {
            c.BaseAddress = new Uri(baseUrl(sp));
            c.Timeout = Timeout.InfiniteTimeSpan;
        });

        client.AddStandardResilienceHandler(o =>
        {
            o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(90);
            o.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(3);
            o.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(3);
        });

        return client;
    }
}
