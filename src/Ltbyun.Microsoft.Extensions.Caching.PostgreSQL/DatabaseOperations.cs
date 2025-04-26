using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Internal;
using Npgsql;
using NpgsqlTypes;

namespace Ltbyun.Microsoft.Extensions.Caching.PostgreSQL;

internal sealed class DatabaseOperations : IDatabaseOperations
{
    private readonly NpgsqlDataSource _dataSource;

    public DatabaseOperations(NpgsqlDataSource dataSource, string schemaName, string tableName,
        ISystemClock systemClock)
    {
        _dataSource = dataSource;
        SchemaName = schemaName;
        TableName = tableName;
        SystemClock = systemClock;
        SqlQueries = new SqlQueries(schemaName, tableName);
    }

    internal SqlQueries SqlQueries { get; }

    internal string SchemaName { get; }

    internal string TableName { get; }

    private ISystemClock SystemClock { get; }

    public void CreateTableIfNotExists()
    {
        using var command = _dataSource.CreateCommand(SqlQueries.CreateTable);
        command.ExecuteNonQuery();
    }

    public void DeleteCacheItem(string key)
    {
        using var command = _dataSource.CreateCommand(SqlQueries.DeleteCacheItem);
        command.Parameters.AddCacheItemId(key);

        command.ExecuteNonQuery();
    }

    public async Task DeleteCacheItemAsync(string key, CancellationToken token = default(CancellationToken))
    {
        token.ThrowIfCancellationRequested();

        await using var command = _dataSource.CreateCommand(SqlQueries.DeleteCacheItem);
        command.Parameters.AddCacheItemId(key);

        await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
    }

    public byte[]? GetCacheItem(string key)
    {
        return GetCacheItem(key, includeValue: true);
    }

    public async Task<byte[]?> GetCacheItemAsync(string key, CancellationToken token = default(CancellationToken))
    {
        token.ThrowIfCancellationRequested();

        return await GetCacheItemAsync(key, includeValue: true, token: token).ConfigureAwait(false);
    }

    public void RefreshCacheItem(string key)
    {
        GetCacheItem(key, includeValue: false);
    }

    public async Task RefreshCacheItemAsync(string key, CancellationToken token = default(CancellationToken))
    {
        token.ThrowIfCancellationRequested();

        await GetCacheItemAsync(key, includeValue: false, token: token).ConfigureAwait(false);
    }

    public void DeleteExpiredCacheItems()
    {
        var utcNow = SystemClock.UtcNow;

        using var command = _dataSource.CreateCommand(SqlQueries.DeleteExpiredCacheItems);
        command.Parameters.AddWithValue("UtcNow", NpgsqlDbType.TimestampTz, utcNow);
        command.ExecuteNonQuery();
    }

    public void SetCacheItem(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        var utcNow = SystemClock.UtcNow;

        var absoluteExpiration = GetAbsoluteExpiration(utcNow, options);
        var expiresAtTime = GetExpiresAtTime(utcNow, absoluteExpiration, options);

        using var upsertCommand = _dataSource.CreateCommand(SqlQueries.SetCacheItem);

        upsertCommand.Parameters
            .AddCacheItemId(key)
            .AddCacheItemValue(value)
            .AddSlidingExpirationInSeconds(options.SlidingExpiration)
            .AddAbsoluteExpiration(absoluteExpiration)
            .AddExpiresAtTime(expiresAtTime)
            .AddWithValue("UtcNow", NpgsqlDbType.TimestampTz, utcNow);

        try
        {
            upsertCommand.ExecuteNonQuery();
        }
        catch (PostgresException ex)
        {
            if (IsDuplicateKeyException(ex))
            {
                // There is a possibility that multiple requests can try to add the same item to the cache, in
                // which case we receive a 'duplicate key' exception on the primary key column.
            }
            else
            {
                throw;
            }
        }
    }

    private static DateTimeOffset GetExpiresAtTime(DateTimeOffset utcNow, DateTimeOffset? absoluteExpiration,
        DistributedCacheEntryOptions options)
    {
        if (options.SlidingExpiration.HasValue)
        {
            return utcNow.Add(options.SlidingExpiration.Value);
        }

        if (absoluteExpiration.HasValue)
        {
            return absoluteExpiration.Value;
        }

        throw new InvalidOperationException("Either absolute or sliding expiration needs to be provided.");
    }

    public async Task SetCacheItemAsync(string key, byte[] value, DistributedCacheEntryOptions options,
        CancellationToken token = default(CancellationToken))
    {
        token.ThrowIfCancellationRequested();

        var utcNow = SystemClock.UtcNow;

        var absoluteExpiration = GetAbsoluteExpiration(utcNow, options);
        var expiresAtTime = GetExpiresAtTime(utcNow, absoluteExpiration, options);

        await using var upsertCommand = _dataSource.CreateCommand(SqlQueries.SetCacheItem);
        upsertCommand.Parameters
            .AddCacheItemId(key)
            .AddCacheItemValue(value)
            .AddSlidingExpirationInSeconds(options.SlidingExpiration)
            .AddAbsoluteExpiration(absoluteExpiration)
            .AddExpiresAtTime(expiresAtTime)
            .AddWithValue("UtcNow", NpgsqlDbType.TimestampTz, utcNow);

        try
        {
            await upsertCommand.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }
        catch (PostgresException ex)
        {
            if (IsDuplicateKeyException(ex))
            {
                // There is a possibility that multiple requests can try to add the same item to the cache, in
                // which case we receive a 'duplicate key' exception on the primary key column.
            }
            else
            {
                throw;
            }
        }
    }

    private byte[]? GetCacheItem(string key, bool includeValue)
    {
        var utcNow = SystemClock.UtcNow;

        var query = includeValue ? SqlQueries.GetCacheItem : SqlQueries.GetCacheItemWithoutValue;

        byte[]? value = null;
        using var command = _dataSource.CreateCommand(query);
        command.Parameters
            .AddCacheItemId(key)
            .AddWithValue("UtcNow", NpgsqlDbType.TimestampTz, utcNow);

        using var reader = command.ExecuteReader(
            CommandBehavior.SequentialAccess | CommandBehavior.SingleRow | CommandBehavior.SingleResult);
        if (reader.Read())
        {
            if (includeValue)
            {
                value = reader.GetFieldValue<byte[]>(0);
            }
        }
        else
        {
            return null;
        }

        return value;
    }

    private async Task<byte[]?> GetCacheItemAsync(string key, bool includeValue,
        CancellationToken token = default(CancellationToken))
    {
        token.ThrowIfCancellationRequested();

        var utcNow = SystemClock.UtcNow;

        string query;
        if (includeValue)
        {
            query = SqlQueries.GetCacheItem;
        }
        else
        {
            query = SqlQueries.GetCacheItemWithoutValue;
        }

        byte[]? value = null;
        await using var command = _dataSource.CreateCommand(query);
        command.Parameters
            .AddCacheItemId(key)
            .AddWithValue("UtcNow", NpgsqlDbType.TimestampTz, utcNow);

        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SequentialAccess | CommandBehavior.SingleRow | CommandBehavior.SingleResult,
            token).ConfigureAwait(false);
        if (await reader.ReadAsync(token).ConfigureAwait(false))
        {
            if (includeValue)
            {
                value = await reader.GetFieldValueAsync<byte[]>(0, token)
                    .ConfigureAwait(false);
            }
        }
        else
        {
            return null;
        }

        return value;
    }

    private static bool IsDuplicateKeyException(PostgresException ex)
    {
        return ex.SqlState == "23505";
    }

    private static DateTimeOffset? GetAbsoluteExpiration(DateTimeOffset utcNow, DistributedCacheEntryOptions options)
    {
        // calculate absolute expiration
        DateTimeOffset? absoluteExpiration = null;
        if (options.AbsoluteExpirationRelativeToNow.HasValue)
        {
            absoluteExpiration = utcNow.Add(options.AbsoluteExpirationRelativeToNow.Value);
        }
        else if (options.AbsoluteExpiration.HasValue)
        {
            if (options.AbsoluteExpiration.Value <= utcNow)
            {
                throw new InvalidOperationException("The absolute expiration value must be in the future.");
            }

            absoluteExpiration = options.AbsoluteExpiration.Value;
        }

        return absoluteExpiration;
    }
}
