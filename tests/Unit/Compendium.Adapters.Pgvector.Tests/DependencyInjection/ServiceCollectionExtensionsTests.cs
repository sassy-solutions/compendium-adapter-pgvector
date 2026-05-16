// -----------------------------------------------------------------------
// <copyright file="ServiceCollectionExtensionsTests.cs" company="Sassy Solutions">
//     Copyright (c) 2026 Sassy Solutions. Licensed under the MIT License.
//     See LICENSE in the project root for license information.
// </copyright>
// -----------------------------------------------------------------------

using Compendium.Abstractions.VectorStore;
using Compendium.Adapters.Pgvector.DependencyInjection;
using Compendium.Adapters.Pgvector.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Compendium.Adapters.Pgvector.Tests.DependencyInjection;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddCompendiumPgvector_WithConfiguration_RegistersVectorStore()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Compendium:Adapters:Pgvector:ConnectionString"] = "Host=localhost;Database=pgvec",
                ["Compendium:Adapters:Pgvector:Schema"] = "public",
            })
            .Build();

        // Act
        var actual = services.AddCompendiumPgvector(configuration);
        var sp = actual.BuildServiceProvider();

        // Assert
        actual.Should().BeSameAs(services);
        sp.GetRequiredService<PgvectorVectorStore>().Should().NotBeNull();
        sp.GetRequiredService<IVectorStore>().Should().BeOfType<PgvectorVectorStore>();
    }

    [Fact]
    public void AddCompendiumPgvector_WithCallback_RegistersVectorStore()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddCompendiumPgvector(o =>
        {
            o.ConnectionString = "Host=localhost;Database=pgvec";
            o.Schema = "public";
        });
        var sp = services.BuildServiceProvider();

        // Assert
        sp.GetRequiredService<PgvectorVectorStore>().Should().NotBeNull();
        sp.GetRequiredService<IVectorStore>().Should().BeOfType<PgvectorVectorStore>();
    }

    [Fact]
    public void AddCompendiumPgvector_RegistersIVectorStoreAndConcreteAsSameSingleton()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCompendiumPgvector(o =>
        {
            o.ConnectionString = "Host=localhost;Database=pgvec";
        });
        var sp = services.BuildServiceProvider();

        // Act
        var concrete = sp.GetRequiredService<PgvectorVectorStore>();
        var port = sp.GetRequiredService<IVectorStore>();

        // Assert
        port.Should().BeSameAs(concrete);
    }

    [Fact]
    public void AddCompendiumPgvector_NullServicesWithConfiguration_Throws()
    {
        // Arrange
        IServiceCollection? services = null;
        var configuration = new ConfigurationBuilder().Build();

        // Act
        var act = () => services!.AddCompendiumPgvector(configuration);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddCompendiumPgvector_NullConfiguration_Throws()
    {
        // Arrange
        var services = new ServiceCollection();
        IConfiguration? configuration = null;

        // Act
        var act = () => services.AddCompendiumPgvector(configuration!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddCompendiumPgvector_NullServicesWithCallback_Throws()
    {
        // Arrange
        IServiceCollection? services = null;

        // Act
        var act = () => services!.AddCompendiumPgvector(_ => { });

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddCompendiumPgvector_NullCallback_Throws()
    {
        // Arrange
        var services = new ServiceCollection();
        Action<PgvectorOptions>? configure = null;

        // Act
        var act = () => services.AddCompendiumPgvector(configure!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }
}
