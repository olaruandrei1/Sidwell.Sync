using Prometheus;
using Quartz;
using Sidwell.Sync.Application;
using Sidwell.Sync.Application.Contracts.Application;
using Sidwell.Sync.Application.Contracts.Infrastructure;
using Sidwell.Sync.Infrastructure;
using Sidwell.Sync.Jobs;
using Sidwell.Sync.Persistence;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Sidwell")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:Sidwell in configuration.");

builder.Services
    .AddSyncApplication()
    .AddSyncPersistence(connectionString)
    .AddSyncInfrastructure(builder.Configuration);

builder.Services.AddQuartz(q =>
{
    AddInterval<PriceSyncJob>(q, "price-sync", startDelayMinutes: 1, intervalHours: 24);
    AddInterval<NewsSyncJob>(q, "news-sync", startDelayMinutes: 2, intervalHours: 24);
    AddCron<FxSyncJob>(q, "fx-sync", "0 0 9 * * ?");
    AddInterval<ProfileSyncJob>(q, "profile-sync", startDelayMinutes: 5, intervalHours: 168);
    AddInterval<TickerAnalysisSyncJob>(q, "analysis-sync", startDelayMinutes: 10, intervalHours: 24);
    AddCron<DividendTaxSyncJob>(q, "dividend-tax-sync", "0 0 3 1 1 ?");
});

builder.Services.AddQuartzHostedService(o => o.WaitForJobsToComplete = true);

static void AddInterval<TJob>(IServiceCollectionQuartzConfigurator q, string name, int startDelayMinutes, int intervalHours)
    where TJob : IJob
{
    var jobKey = new JobKey(name);
    q.AddJob<TJob>(jobKey);
    q.AddTrigger(t => t
        .ForJob(jobKey)
        .WithIdentity($"{name}-trigger")
        .StartAt(DateBuilder.FutureDate(startDelayMinutes, IntervalUnit.Minute))
        .WithSimpleSchedule(s => s.WithIntervalInHours(intervalHours).RepeatForever()));
}

static void AddCron<TJob>(IServiceCollectionQuartzConfigurator q, string name, string cron)
    where TJob : IJob
{
    var jobKey = new JobKey(name);
    q.AddJob<TJob>(jobKey);
    q.AddTrigger(t => t
        .ForJob(jobKey)
        .WithIdentity($"{name}-trigger")
        .WithCronSchedule(cron));
}

var app = builder.Build();

app.UseHttpMetrics();
app.Use(async (ctx, next) =>
{
    if (!ctx.Request.Path.StartsWithSegments("/health") && !ctx.Request.Path.StartsWithSegments("/metrics"))
    {
        var start = DateTimeOffset.UtcNow;
        await next();
        app.Logger.LogInformation("{Method} {Path} {Status} {Ms}ms",
            ctx.Request.Method, ctx.Request.Path, ctx.Response.StatusCode,
            (long)(DateTimeOffset.UtcNow - start).TotalMilliseconds);
        return;
    }
    await next();
});

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/sync/full/{symbol}", async (string symbol, ITickerProfileSyncService profileSync, IPriceSyncService priceSync, INewsSyncService newsSync, ISecSyncService secSync, ITickerAnalysisSyncService analysisSync, IBroadcastPublisher pub, CancellationToken ct) =>
{
    await pub.PublishAsync("SYNC_STARTED", null, new { symbol, step = "full" }, ct);

    bool failed = false;
    string? error = null;

    async Task TryStep(Func<Task> action)
    {
        try { await action(); }
        catch (Exception ex)
        {
            failed = true;
            error ??= ex.Message;
        }
    }

    await TryStep(async () => await profileSync.SyncProfileAsync(symbol, ct));
    await TryStep(async () => await priceSync.SyncTickerAsync(symbol, forceRefresh: true, ct: ct));
    await TryStep(async () => await newsSync.SyncTickerNewsAsync(symbol, ct));
    await TryStep(async () => await secSync.SyncAsync(symbol, ct));
    await TryStep(async () => await analysisSync.SyncTickerAnalysisAsync(symbol, ct));

    if (failed)
    {
        await pub.PublishAsync("SYNC_COMPLETE", null, new { symbol, step = "full", status = "FAILED", error }, ct);
        throw new Exception($"Sync failed: {error}");
    }

    await pub.PublishAsync("SYNC_COMPLETE", null, new { symbol, step = "full", status = "SUCCEEDED" }, ct);
    return Results.Ok(new { symbol, status = "SUCCEEDED" });
});

app.MapPost("/sync/prices/{symbol}", (string symbol, IPriceSyncService sync, IBroadcastPublisher pub, CancellationToken ct) =>
    SyncStep.RunAsync(pub, symbol, "prices", async () => await sync.SyncTickerAsync(symbol, forceRefresh: true, ct: ct), ct));

app.MapPost("/sync/news/{symbol}", (string symbol, INewsSyncService sync, IBroadcastPublisher pub, CancellationToken ct) =>
    SyncStep.RunAsync(pub, symbol, "news", async () => new { symbol, inserted = await sync.SyncTickerNewsAsync(symbol, ct) }, ct));

app.MapPost("/sync/profile/{symbol}", (string symbol, ITickerProfileSyncService sync, IBroadcastPublisher pub, CancellationToken ct) =>
    SyncStep.RunAsync(pub, symbol, "profile", async () => new { symbol, updated = await sync.SyncProfileAsync(symbol, ct) }, ct));

app.MapPost("/sync/fx", async (IFxSyncService sync, CancellationToken ct) =>
    Results.Ok(new { upserted = await sync.SyncRatesAsync(ct) }));

app.MapPost("/sync/fx/currencies", async (FxCurrenciesRequest request, IFxSyncService sync, CancellationToken ct) =>
    Results.Ok(new { upserted = await sync.SyncRatesAsync(request.Currencies, ct) }));

app.MapPost("/sync/sec/{symbol}", (string symbol, ISecSyncService sync, IBroadcastPublisher pub, CancellationToken ct) =>
    SyncStep.RunAsync(pub, symbol, "sec", async () => await sync.SyncAsync(symbol, ct), ct));

app.MapPost("/sync/analysis/{symbol}", (string symbol, ITickerAnalysisSyncService sync, IBroadcastPublisher pub, CancellationToken ct) =>
    SyncStep.RunAsync(pub, symbol, "analysis", async () => new { symbol, stored = await sync.SyncTickerAnalysisAsync(symbol, ct) }, ct));

app.MapPost("/internal/sync/dividend-tax", async (IDividendTaxSyncService sync, CancellationToken ct) =>
    Results.Ok(new { upserted = await sync.SyncDividendTaxRatesAsync(ct) }));

app.MapPost("/sync/discover/us", async (ITickerDiscoveryService svc, CancellationToken ct) =>
    Results.Ok(new { upserted = await svc.DiscoverUsAsync(ct) }));

app.MapPost("/sync/discover/eu", async (DiscoverEuRequest req, ITickerDiscoveryService svc, CancellationToken ct) =>
    Results.Ok(new { upserted = await svc.DiscoverEuAsync(req.Exchanges, ct) }));

app.MapPost("/sync/discover/bvb", async (ITickerDiscoveryService svc, CancellationToken ct) =>
    Results.Ok(new { upserted = await svc.DiscoverBvbAsync(ct) }));

app.MapMetrics("/metrics");

app.Run();

public sealed record FxCurrenciesRequest(List<string> Currencies);
public sealed record DiscoverEuRequest(IReadOnlyList<string> Exchanges);

// Wraps a per-symbol sync step with global SYNC_STARTED / SYNC_COMPLETE broadcast events (fire-and-forget).
// SYNC_PROGRESS is emitted separately from inside the running services via ISyncNotifier.
static class SyncStep
{
    public static async Task<IResult> RunAsync(
        IBroadcastPublisher publisher, string symbol, string step, Func<Task<object>> work, CancellationToken ct)
    {
        await publisher.PublishAsync("SYNC_STARTED", null, new { symbol, step }, ct);

        try
        {
            object result = await work();

            await publisher.PublishAsync("SYNC_COMPLETE", null, new { symbol, step, status = "SUCCEEDED" }, ct);

            return Results.Ok(result);
        }
        catch (Exception ex)
        {
            await publisher.PublishAsync("SYNC_COMPLETE", null, new { symbol, step, status = "FAILED", error = ex.Message }, ct);

            throw;
        }
    }
}
