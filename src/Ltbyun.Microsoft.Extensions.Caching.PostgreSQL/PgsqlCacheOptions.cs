﻿using System;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Ltbyun.Microsoft.Extensions.Caching.PostgreSQL;

/// <summary>
/// Configuration options for <see cref="PgsqlCache"/>.
/// </summary>
public class PgsqlCacheOptions : IOptions<PgsqlCacheOptions>
{
    /// <summary>
    /// An abstraction to represent the clock of a machine in order to enable unit testing.
    /// </summary>
    public ISystemClock? SystemClock { get; set; } = new SystemClock();

    /// <summary>
    /// The periodic interval to scan and delete expired items in the cache. Default is 30 minutes.
    /// </summary>
    public TimeSpan? ExpiredItemsDeletionInterval { get; set; }

    /// <summary>
    /// The connection string to the database.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// The data source used to cache
    /// </summary>
    public NpgsqlDataSource? DataSource { get; set; }

    /// <summary>
    /// The schema name of the table.
    /// </summary>
    public string? SchemaName { get; set; }

    /// <summary>
    /// Name of the table where the cache items are stored.
    /// </summary>
    public string? TableName { get; set; }

    /// <summary>
    /// Whether to create table if not exists. pgsql version less than 9.1 is not supported.
    /// </summary>
    public bool CreateTableIfNotExists { get; set; } = true;

    /// <summary>
    /// The default sliding expiration set for a cache entry if neither Absolute or SlidingExpiration has been set explicitly.
    /// By default, its 20 minutes.
    /// </summary>
    public TimeSpan DefaultSlidingExpiration { get; set; } = TimeSpan.FromMinutes(20);

    PgsqlCacheOptions IOptions<PgsqlCacheOptions>.Value
    {
        get { return this; }
    }
}
