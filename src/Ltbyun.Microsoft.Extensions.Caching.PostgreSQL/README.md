Distributed cache implementation of Microsoft.Extensions.Caching.Distributed.IDistributedCache using PostgreSQL.

# how to use it?

1. create table manual if pgsql version < 9.1
    ```postgresql
    CREATE TABLE {0} (
      "id" varchar(200) NOT NULL,
      "value" bytea,
      "expires_at_time" timestamptz(6) NOT NULL,
      "sliding_expiration_in_seconds" int8,
      "absolute_expiration" timestamptz(6),
      PRIMARY KEY ("id")
    )
    ```

2. add pgsql cache to IServiceCollection
    - use individual NpgsqlDataSource
      ```C#
      builder.Services.AddDistributedPgsqlCache(options =>
      {
          options.SchemaName = "public";
          options.TableName = "asp_net_core_cache";
          // CreateTableIfNotExists only support pgsql version >=9.1
          options.CreateTableIfNotExists = true;
          options.ConnectionString = builder.Configuration.GetConnectionString("Default");
      });
      ```
    - share the same NpgsqlDataSource
      ```C#
      builder.Services.AddSingleton<NpgsqlDataSource>(_ =>
          new NpgsqlDataSourceBuilder(builder.Configuration.GetConnectionString("Default")).Build());
      builder.Services.AddDistributedPgsqlCache(options =>
      {
          options.SchemaName = "public";
          options.TableName = "asp_net_core_cache";
          // CreateTableIfNotExists only support pgsql version >=9.1
          options.CreateTableIfNotExists = true;
      });
      ```
      or
      ```C#
      var dataSource = new NpgsqlDataSourceBuilder(builder.Configuration.GetConnectionString("Default")).Build();
      builder.Services.AddDistributedPgsqlCache(options =>
      {
          options.SchemaName = "public";
          options.TableName = "asp_net_core_cache";
          // CreateTableIfNotExists only support pgsql version >=9.1
          options.CreateTableIfNotExists = true;
          options.DataSource = dataSource;
      });
      ```

3. inject IDistributedCache to use

   refers to https://learn.microsoft.com/en-us/aspnet/core/performance/caching/distributed
