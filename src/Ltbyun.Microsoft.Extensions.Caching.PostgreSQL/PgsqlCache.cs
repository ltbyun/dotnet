using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Ltbyun.Microsoft.Extensions.Caching.PostgreSQL;

/// <summary>
/// Distributed cache implementation using pgsql database.
/// </summary>
public class PgsqlCache : IDistributedCache, IDisposable
{
    private static readonly TimeSpan MinimumExpiredItemsDeletionInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan DefaultExpiredItemsDeletionInterval = TimeSpan.FromMinutes(30);

    private readonly NpgsqlDataSource _dataSource;
    private readonly IDatabaseOperations _dbOperations;
    private readonly ISystemClock _systemClock;
    private readonly TimeSpan _expiredItemsDeletionInterval;
    private DateTimeOffset _lastExpirationScan;
    private readonly Action _deleteExpiredCachedItemsDelegate;
    private readonly TimeSpan _defaultSlidingExpiration;
    private readonly object _mutex = new();

    /// <summary>
    /// Initializes a new instance of <see cref="PgsqlCache"/>.
    /// </summary>
    /// <param name="options">The configuration options.</param>
    /// <param name="dataSource">The pgsql datasource, if provided, the ConnectionString in options is ignored</param>
    public PgsqlCache(IOptions<PgsqlCacheOptions> options, NpgsqlDataSource? dataSource = null)
    {
        var cacheOptions = options.Value;

        if (dataSource == null && string.IsNullOrEmpty(cacheOptions.ConnectionString))
        {
            throw new ArgumentException(
                $"{nameof(PgsqlCacheOptions.ConnectionString)} cannot be empty or null.");
        }

        if (string.IsNullOrEmpty(cacheOptions.SchemaName))
        {
            throw new ArgumentException(
                $"{nameof(PgsqlCacheOptions.SchemaName)} cannot be empty or null.");
        }

        if (string.IsNullOrEmpty(cacheOptions.TableName))
        {
            throw new ArgumentException(
                $"{nameof(PgsqlCacheOptions.TableName)} cannot be empty or null.");
        }

        if (cacheOptions.ExpiredItemsDeletionInterval.HasValue &&
            cacheOptions.ExpiredItemsDeletionInterval.Value < MinimumExpiredItemsDeletionInterval)
        {
            throw new ArgumentException(
                $"{nameof(PgsqlCacheOptions.ExpiredItemsDeletionInterval)} cannot be less than the minimum " +
                $"value of {MinimumExpiredItemsDeletionInterval.TotalMinutes} minutes.");
        }

        if (cacheOptions.DefaultSlidingExpiration <= TimeSpan.Zero)
        {
#pragma warning disable CA2208 // Instantiate argument exceptions correctly
            throw new ArgumentOutOfRangeException(
                nameof(cacheOptions.DefaultSlidingExpiration),
                cacheOptions.DefaultSlidingExpiration,
                "The sliding expiration value must be positive.");
#pragma warning restore CA2208 // Instantiate argument exceptions correctly
        }

        _systemClock = cacheOptions.SystemClock ?? new SystemClock();
        _expiredItemsDeletionInterval =
            cacheOptions.ExpiredItemsDeletionInterval ?? DefaultExpiredItemsDeletionInterval;
        _deleteExpiredCachedItemsDelegate = DeleteExpiredCacheItems;
        _defaultSlidingExpiration = cacheOptions.DefaultSlidingExpiration;

        _dataSource = dataSource ?? new NpgsqlDataSourceBuilder(cacheOptions.ConnectionString).Build();
        _dbOperations = new DatabaseOperations(
            _dataSource,
            cacheOptions.SchemaName,
            cacheOptions.TableName,
            _systemClock);
        if (cacheOptions.CreateTableIfNotExists)
        {
            _dbOperations.CreateTableIfNotExists();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _dataSource.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public byte[]? Get(string key)
    {
        ArgumentNullException.ThrowIfNull(key, nameof(key));

        var value = _dbOperations.GetCacheItem(key);

        ScanForExpiredItemsIfRequired();

        return value;
    }

    /// <inheritdoc />
    public async Task<byte[]?> GetAsync(string key, CancellationToken token = default(CancellationToken))
    {
        ArgumentNullException.ThrowIfNull(key, nameof(key));

        token.ThrowIfCancellationRequested();

        var value = await _dbOperations.GetCacheItemAsync(key, token).ConfigureAwait(false);

        ScanForExpiredItemsIfRequired();

        return value;
    }

    /// <inheritdoc />
    public void Refresh(string key)
    {
        ArgumentNullException.ThrowIfNull(key, nameof(key));

        _dbOperations.RefreshCacheItem(key);

        ScanForExpiredItemsIfRequired();
    }

    /// <inheritdoc />
    public async Task RefreshAsync(string key, CancellationToken token = default(CancellationToken))
    {
        ArgumentNullException.ThrowIfNull(key, nameof(key));

        token.ThrowIfCancellationRequested();

        await _dbOperations.RefreshCacheItemAsync(key, token).ConfigureAwait(false);

        ScanForExpiredItemsIfRequired();
    }

    /// <inheritdoc />
    public void Remove(string key)
    {
        ArgumentNullException.ThrowIfNull(key, nameof(key));

        _dbOperations.DeleteCacheItem(key);

        ScanForExpiredItemsIfRequired();
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string key, CancellationToken token = default(CancellationToken))
    {
        ArgumentNullException.ThrowIfNull(key, nameof(key));

        token.ThrowIfCancellationRequested();

        await _dbOperations.DeleteCacheItemAsync(key, token).ConfigureAwait(false);

        ScanForExpiredItemsIfRequired();
    }

    /// <inheritdoc />
    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        ArgumentNullException.ThrowIfNull(key, nameof(key));
        ArgumentNullException.ThrowIfNull(value, nameof(value));
        ArgumentNullException.ThrowIfNull(options, nameof(options));

        GetOptions(ref options);

        _dbOperations.SetCacheItem(key, value, options);

        ScanForExpiredItemsIfRequired();
    }

    /// <inheritdoc />
    public async Task SetAsync(
        string key,
        byte[] value,
        DistributedCacheEntryOptions options,
        CancellationToken token = default(CancellationToken))
    {
        ArgumentNullException.ThrowIfNull(key, nameof(key));
        ArgumentNullException.ThrowIfNull(value, nameof(value));
        ArgumentNullException.ThrowIfNull(options, nameof(options));

        token.ThrowIfCancellationRequested();

        GetOptions(ref options);

        await _dbOperations.SetCacheItemAsync(key, value, options, token).ConfigureAwait(false);

        ScanForExpiredItemsIfRequired();
    }

    // Called by multiple actions to see how long it's been since we last checked for expired items.
    // If sufficient time has elapsed then a scan is initiated on a background task.
    private void ScanForExpiredItemsIfRequired()
    {
        lock (_mutex)
        {
            var utcNow = _systemClock.UtcNow;
            if ((utcNow - _lastExpirationScan) > _expiredItemsDeletionInterval)
            {
                _lastExpirationScan = utcNow;
                Task.Run(_deleteExpiredCachedItemsDelegate);
            }
        }
    }

    private void DeleteExpiredCacheItems()
    {
        _dbOperations.DeleteExpiredCacheItems();
    }

    private void GetOptions(ref DistributedCacheEntryOptions options)
    {
        if (!options.AbsoluteExpiration.HasValue
            && !options.AbsoluteExpirationRelativeToNow.HasValue
            && !options.SlidingExpiration.HasValue)
        {
            options = new DistributedCacheEntryOptions() { SlidingExpiration = _defaultSlidingExpiration };
        }
    }
}
