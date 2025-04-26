using System;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace Ltbyun.Microsoft.Extensions.Caching.PostgreSQL;

/// <summary>
/// Extension methods for setting up Pgsql distributed cache services in an <see cref="IServiceCollection" />.
/// </summary>
public static class PgsqlCachingServicesExtensions
{
    /// <summary>
    /// Adds Pgsql distributed caching services to the specified <see cref="IServiceCollection" />.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
    /// <param name="setupAction">An <see cref="Action"/> to configure the provided <see cref="PgsqlCacheOptions"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddDistributedPgsqlCache(this IServiceCollection services, Action<PgsqlCacheOptions> setupAction)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));
        ArgumentNullException.ThrowIfNull(setupAction, nameof(setupAction));

        services.Add(ServiceDescriptor.Singleton<IDistributedCache, PgsqlCache>());
        services.Configure(setupAction);

        return services;
    }
}
