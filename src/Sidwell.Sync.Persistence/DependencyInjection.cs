using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sidwell.Sync.Application.Contracts.Persistence;
using Sidwell.Sync.Persistence.Configurations;
using Sidwell.Sync.Persistence.Implementations;

namespace Sidwell.Sync.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddSyncPersistence(this IServiceCollection services, string connectionString)
    {
        Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
        Dapper.SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());
        Dapper.SqlMapper.AddTypeHandler(new DateTimeOffsetTypeHandler());

        services.AddDbContext<SyncDbContext>(options => options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());

        services.AddSingleton<IDatabaseConnectionFactory>(new DatabaseConnectionFactory(connectionString));

        services.AddScoped<IDapperExecutor, DapperExecutor>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}
