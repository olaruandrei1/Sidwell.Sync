using System.Data;
using System.Data.Common;

namespace Sidwell.Sync.Application.Contracts.Persistence;

public interface IDatabaseConnectionFactory
{
    DbConnection Create();

    Task<IDbConnection> CreateOpenAsync(CancellationToken ct = default);
}
