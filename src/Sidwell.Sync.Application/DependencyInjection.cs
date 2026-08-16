using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sidwell.Sync.Application.Contracts.Application;
using Sidwell.Sync.Application.Implementations;

namespace Sidwell.Sync.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddSyncApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IPriceSyncService, PriceSyncService>();
        services.AddScoped<INewsSyncService, NewsSyncService>();
        services.AddScoped<IFxSyncService, FxSyncService>();
        services.AddScoped<ITickerProfileSyncService, TickerProfileSyncService>();
        services.AddScoped<ISecSyncService, SecSyncService>();
        services.AddScoped<ITickerAnalysisSyncService, TickerAnalysisSyncService>();
        services.AddScoped<IDividendTaxSyncService, DividendTaxSyncService>();

        return services;
    }
}
