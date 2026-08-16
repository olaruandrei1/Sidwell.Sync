using System.Data;
using Dapper;

namespace Sidwell.Sync.Persistence.Implementations;

// Npgsql returns `timestamp with time zone` as a DateTime (UTC) via the untyped reader Dapper uses,
// so records with DateTimeOffset members fail constructor matching. This handler bridges the read.
public sealed class DateTimeOffsetTypeHandler : SqlMapper.TypeHandler<DateTimeOffset>
{
    public override DateTimeOffset Parse(object value) => value switch
    {
        DateTimeOffset dto => dto,
        DateTime dt => new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc)),
        _ => DateTimeOffset.Parse(value.ToString()!),
    };

    public override void SetValue(IDbDataParameter parameter, DateTimeOffset value) => parameter.Value = value;
}
