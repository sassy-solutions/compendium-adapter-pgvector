// -----------------------------------------------------------------------
// <copyright file="ServiceCollectionExtensionsTests.cs" company="Sassy Solutions">
//     Copyright (c) 2026 Sassy Solutions. Licensed under the MIT License.
//     See LICENSE in the project root for license information.
// </copyright>
// -----------------------------------------------------------------------

using Compendium.Adapters.Pgvector.DependencyInjection;
using Compendium.Adapters.Pgvector.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Compendium.Adapters.Pgvector.Tests.DependencyInjection;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddCompendiumPgvectorAdapter_WithConfiguration_RegistersAdapterAndOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Compendium:Adapters:Pgvector:BaseUrl"] = "https://api.example.com",
                ["Compendium:Adapters:Pgvector:ApiKey"] = "k1",
            })
            .Build();

        // Act
        var actual = services.AddCompendiumPgvectorAdapter(configuration);
        var sp = actual.BuildServiceProvider();

        // Assert
        actual.Should().BeSameAs(services);
        sp.GetRequiredService<PgvectorAdapter>().Should().NotBeNull();
    }

    [Fact]
    public void AddCompendiumPgvectorAdapter_WithCallback_RegistersAdapterAndOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddCompendiumPgvectorAdapter(o =>
        {
            o.BaseUrl = "https://api.example.com";
            o.ApiKey = "k1";
        });
        var sp = services.BuildServiceProvider();

        // Assert
        sp.GetRequiredService<PgvectorAdapter>().Should().NotBeNull();
    }

    [Fact]
    public void AddCompendiumPgvectorAdapter_NullServices_Throws()
    {
        // Arrange
        IServiceCollection? services = null;

        // Act
        var act = () => services!.AddCompendiumPgvectorAdapter(_ => { });

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }
}
