// -----------------------------------------------------------------------
// <copyright file="PgvectorFixture.cs" company="Sassy Solutions">
//     Copyright (c) 2026 Sassy Solutions. Licensed under the MIT License.
//     See LICENSE in the project root for license information.
// </copyright>
// -----------------------------------------------------------------------

using Testcontainers.PostgreSql;
using Xunit;

namespace Compendium.Adapters.Pgvector.IntegrationTests.Fixtures;

/// <summary>
/// Shared xUnit fixture that starts a PostgreSQL container with the pgvector extension
/// pre-installed (<c>pgvector/pgvector:pg17</c>) and exposes its connection string.
/// </summary>
public sealed class PgvectorFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;

    public string ConnectionString { get; private set; } = string.Empty;

    public bool IsAvailable { get; private set; }

    public async Task InitializeAsync()
    {
        if (!DockerDetection.IsDockerAvailable)
        {
            IsAvailable = false;
            return;
        }

        // The `pgvector/pgvector:pg17` image bundles the vector extension; nothing else needed.
        _container = new PostgreSqlBuilder("pgvector/pgvector:pg17")
            .WithDatabase("compendium_pgvec_test")
            .WithUsername("test_user")
            .WithPassword("test_password")
            .WithCleanUp(true)
            .Build();

        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();
        IsAvailable = true;
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}
