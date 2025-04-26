using System.Globalization;

namespace Ltbyun.Microsoft.Extensions.Caching.PostgreSQL;

internal sealed class SqlQueries
{
    private const string UpdateCacheItemFormat =
        "UPDATE {0} " +
        "SET expires_at_time = " +
        "(CASE " +
        "WHEN EXTRACT(EPOCH FROM absolute_expiration - @UtcNow) <= sliding_expiration_in_seconds " +
        "THEN absolute_expiration " +
        "ELSE " +
        "@UtcNow + make_interval(secs => sliding_expiration_in_seconds) " +
        "END) " +
        "WHERE id = @Id " +
        "AND @UtcNow <= expires_at_time " +
        "AND sliding_expiration_in_seconds IS NOT NULL " +
        "AND (absolute_expiration IS NULL OR absolute_expiration <> expires_at_time) ;";

    private const string GetCacheItemFormat =
        "SELECT value " +
        "FROM {0} WHERE id = @Id AND @UtcNow <= expires_at_time;";

    private const string SetCacheItemFormat =
        "INSERT INTO {0} " +
        "(id, value, expires_at_time, sliding_expiration_in_seconds, absolute_expiration) " +
        "VALUES (@Id, @Value, @ExpiresAtTime, @SlidingExpirationInSeconds, @AbsoluteExpiration) " +
        "ON CONFLICT (id) DO UPDATE SET value = @Value, expires_at_time = @ExpiresAtTime," +
        "sliding_expiration_in_seconds = @SlidingExpirationInSeconds, absolute_expiration = @AbsoluteExpiration;";

    private const string DeleteCacheItemFormat = "DELETE FROM {0} WHERE id = @Id";

    public const string DeleteExpiredCacheItemsFormat = "DELETE FROM {0} WHERE @UtcNow > expires_at_time";

    public SqlQueries(string schemaName, string tableName)
    {
        var tableNameWithSchema = string.Format(CultureInfo.InvariantCulture, "{0}.{1}", schemaName, tableName);

        // when retrieving an item, we do an UPDATE first and then a SELECT
        GetCacheItem = string.Format(CultureInfo.InvariantCulture, UpdateCacheItemFormat + GetCacheItemFormat,
            tableNameWithSchema);
        GetCacheItemWithoutValue =
            string.Format(CultureInfo.InvariantCulture, UpdateCacheItemFormat, tableNameWithSchema);
        DeleteCacheItem = string.Format(CultureInfo.InvariantCulture, DeleteCacheItemFormat, tableNameWithSchema);
        DeleteExpiredCacheItems =
            string.Format(CultureInfo.InvariantCulture, DeleteExpiredCacheItemsFormat, tableNameWithSchema);
        SetCacheItem = string.Format(CultureInfo.InvariantCulture, SetCacheItemFormat, tableNameWithSchema);
    }

    public string GetCacheItem { get; }

    public string GetCacheItemWithoutValue { get; }

    public string SetCacheItem { get; }

    public string DeleteCacheItem { get; }

    public string DeleteExpiredCacheItems { get; }
}
