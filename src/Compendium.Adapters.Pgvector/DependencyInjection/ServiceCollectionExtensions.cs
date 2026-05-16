// -----------------------------------------------------------------------
// <copyright file="ServiceCollectionExtensions.cs" company="Sassy Solutions">
//     Copyright (c) 2026 Sassy Solutions. Licensed under the MIT License.
//     See LICENSE in the project root for license information.
// </copyright>
// -----------------------------------------------------------------------

using Compendium.Abstractions.VectorStore;
using Compendium.Adapters.Pgvector.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Compendium.Adapters.Pgvector.DependencyInjection;

/// <summary>
/// DI registration helpers for the pgvector adapter.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="PgvectorVectorStore"/> as <see cref="IVectorStore"/> bound to <see cref="PgvectorOptions.SectionName"/>.
    /// </summary>
    /// <param name="services">DI container.</param>
    /// <param name="configuration">Source configuration; section <see cref="PgvectorOptions.SectionName"/> is bound.</param>
    /// <returns>The same <paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddCompendiumPgvector(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<PgvectorOptions>()
            .Bind(configuration.GetSection(PgvectorOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<PgvectorVectorStore>();
        services.AddSingleton<IVectorStore>(sp => sp.GetRequiredService<PgvectorVectorStore>());

        return services;
    }

    /// <summary>
    /// Registers <see cref="PgvectorVectorStore"/> as <see cref="IVectorStore"/> with an inline configuration callback.
    /// </summary>
    /// <param name="services">DI container.</param>
    /// <param name="configure">Callback to mutate <see cref="PgvectorOptions"/>.</param>
    /// <returns>The same <paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddCompendiumPgvector(
        this IServiceCollection services,
        Action<PgvectorOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddOptions<PgvectorOptions>()
            .Configure(configure)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<PgvectorVectorStore>();
        services.AddSingleton<IVectorStore>(sp => sp.GetRequiredService<PgvectorVectorStore>());

        return services;
    }
}
