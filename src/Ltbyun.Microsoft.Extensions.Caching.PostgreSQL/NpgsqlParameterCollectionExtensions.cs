using System;
using Npgsql;
using NpgsqlTypes;

namespace Ltbyun.Microsoft.Extensions.Caching.PostgreSQL;

internal static class NpgsqlParameterCollectionExtensions
{
    // Maximum size of a primary key column is 900 bytes (898 bytes from the key + 2 additional bytes required by
    // the Sql Server).
    public const int CacheItemIdColumnWidth = 200;

    public static NpgsqlParameterCollection AddCacheItemId(this NpgsqlParameterCollection parameters, string value)
    {
        parameters.AddWithValue(Columns.Names.CacheItemId, NpgsqlDbType.Varchar, CacheItemIdColumnWidth, value);
        return parameters;
    }

    public static NpgsqlParameterCollection AddCacheItemValue(this NpgsqlParameterCollection parameters, byte[]? value)
    {
        parameters.AddWithValue(
            Columns.Names.CacheItemValue, NpgsqlDbType.Bytea, value == null ? DBNull.Value : value);
        return parameters;
    }

    public static NpgsqlParameterCollection AddExpiresAtTime(this NpgsqlParameterCollection parameters,
        DateTimeOffset? utcTime)
    {
        parameters.AddWithValue(
            Columns.Names.ExpiresAtTime, NpgsqlDbType.TimestampTz, utcTime.HasValue ? utcTime.Value : DBNull.Value);
        return parameters;
    }

    public static NpgsqlParameterCollection AddSlidingExpirationInSeconds(this NpgsqlParameterCollection parameters,
        TimeSpan? value)
    {
        parameters.AddWithValue(Columns.Names.SlidingExpirationInSeconds, NpgsqlDbType.Bigint,
            value.HasValue ? value.Value.TotalSeconds : DBNull.Value);
        return parameters;
    }

    public static NpgsqlParameterCollection AddAbsoluteExpiration(this NpgsqlParameterCollection parameters,
        DateTimeOffset? utcTime)
    {
        parameters.AddWithValue(
            Columns.Names.AbsoluteExpiration, NpgsqlDbType.TimestampTz,
            utcTime.HasValue ? utcTime.Value : DBNull.Value);
        return parameters;
    }
}
