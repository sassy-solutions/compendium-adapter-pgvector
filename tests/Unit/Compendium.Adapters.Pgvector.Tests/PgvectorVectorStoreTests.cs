// -----------------------------------------------------------------------
// <copyright file="PgvectorVectorStoreTests.cs" company="Sassy Solutions">
//     Copyright (c) 2026 Sassy Solutions. Licensed under the MIT License.
//     See LICENSE in the project root for license information.
// </copyright>
// -----------------------------------------------------------------------

using Compendium.Abstractions.VectorStore.Models;
using Compendium.Adapters.Pgvector.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Compendium.Adapters.Pgvector.Tests;

/// <summary>
/// Unit-testable surface of <see cref="PgvectorVectorStore"/> — argument validation,
/// short-circuit empty-input behaviour, and tenant-id rejection.
/// Behaviour that requires a live PostgreSQL + pgvector backend (table creation,
/// upsert, search ordering) is covered by the integration suite.
/// </summary>
public class PgvectorVectorStoreTests
{
    private readonly ILogger<PgvectorVectorStore> _logger = Substitute.For<ILogger<PgvectorVectorStore>>();

    private static IOptions<PgvectorOptions> Options(PgvectorOptions? options = null) =>
        Microsoft.Extensions.Options.Options.Create(options ?? new PgvectorOptions
        {
            ConnectionString = "Host=localhost;Database=pgvec;Username=u;Password=p",
        });

    private PgvectorVectorStore CreateStore(PgvectorOptions? options = null)
    {
        // Use the data-source-owning constructor; never opens a connection until a method is called.
        return new PgvectorVectorStore(Options(options), _logger);
    }

    [Fact]
    public void Constructor_NullOptions_Throws()
    {
        // Arrange / Act
        var act = () => new PgvectorVectorStore(null!, _logger);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        // Arrange / Act
        var act = () => new PgvectorVectorStore(Options(), null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_EmptyConnectionString_Throws()
    {
        // Arrange
        var opts = Options(new PgvectorOptions { ConnectionString = "" });

        // Act
        var act = () => new PgvectorVectorStore(opts, _logger);

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("*ConnectionString*");
    }

    [Fact]
    public void Constructor_NullOptionsValue_Throws()
    {
        // Arrange — IOptions with null .Value
        var options = Substitute.For<IOptions<PgvectorOptions>>();
        options.Value.Returns((PgvectorOptions)null!);

        // Act
        var act = () => new PgvectorVectorStore(options, _logger);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithDataSource_NullDataSource_Throws()
    {
        // Arrange / Act
        var act = () => new PgvectorVectorStore(null!, Options(), _logger);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task DisposeAsync_OwnsDataSource_DisposesIt()
    {
        // Arrange
        var store = CreateStore();

        // Act
        await store.DisposeAsync();

        // Assert — second call should not throw; idempotent.
        await store.Invoking(s => s.DisposeAsync().AsTask()).Should().NotThrowAsync();
    }

    [Fact]
    public async Task DisposeAsync_ExternalDataSource_DoesNotDispose()
    {
        // Arrange
        var builder = new NpgsqlDataSourceBuilder("Host=localhost;Database=pgvec;Username=u;Password=p");
        builder.UseVector();
        await using var external = builder.Build();
        var store = new PgvectorVectorStore(external, Options(), _logger);

        // Act
        await store.DisposeAsync();

        // Assert — external data source still usable (no ObjectDisposedException).
        external.ConnectionString.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EnsureCollectionAsync_BadCollection_ReturnsValidation(string? collection)
    {
        // Arrange
        await using var store = CreateStore();

        // Act
        var result = await store.EnsureCollectionAsync(collection!, 3, DistanceMetric.Cosine, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Pgvector.InvalidCollection");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task EnsureCollectionAsync_NonPositiveDimension_ReturnsValidation(int dim)
    {
        // Arrange
        await using var store = CreateStore();

        // Act
        var result = await store.EnsureCollectionAsync("documents", dim, DistanceMetric.Cosine, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Pgvector.InvalidDimension");
    }

    [Fact]
    public async Task EnsureCollectionAsync_InvalidSchema_ReturnsValidation()
    {
        // Arrange
        await using var store = CreateStore(new PgvectorOptions
        {
            ConnectionString = "Host=localhost;Database=pgvec;Username=u;Password=p",
            Schema = "bad schema",
        });

        // Act
        var result = await store.EnsureCollectionAsync("documents", 3, DistanceMetric.Cosine, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Pgvector.InvalidSchema");
    }

    [Fact]
    public async Task EnsureCollectionAsync_InvalidCollectionIdentifier_ReturnsValidation()
    {
        // Arrange
        await using var store = CreateStore();

        // Act — collection name that produces a non-identifier table
        var result = await store.EnsureCollectionAsync("with-dash", 3, DistanceMetric.Cosine, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Pgvector.InvalidCollection");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpsertAsync_BadCollection_ReturnsValidation(string? collection)
    {
        // Arrange
        await using var store = CreateStore();
        var records = new List<VectorRecord>
        {
            new("id1", new float[] { 1, 2, 3 }, new Dictionary<string, object>()),
        };

        // Act
        var result = await store.UpsertAsync(collection!, records, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Pgvector.InvalidCollection");
    }

    [Fact]
    public async Task UpsertAsync_NullRecords_Throws()
    {
        // Arrange
        await using var store = CreateStore();

        // Act
        Func<Task> act = () => store.UpsertAsync("documents", null!, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task UpsertAsync_EmptyList_ReturnsSuccessWithoutHittingDb()
    {
        // Arrange
        await using var store = CreateStore();

        // Act
        var result = await store.UpsertAsync("documents", new List<VectorRecord>(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task UpsertAsync_RecordWithBlankId_ReturnsValidation()
    {
        // Arrange
        await using var store = CreateStore();
        var records = new List<VectorRecord>
        {
            new("  ", new float[] { 1, 2, 3 }, new Dictionary<string, object>()),
        };

        // Act
        var result = await store.UpsertAsync("documents", records, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Pgvector.InvalidRecordId");
    }

    [Fact]
    public async Task UpsertAsync_RecordWithInvalidTenant_ReturnsValidation()
    {
        // Arrange
        await using var store = CreateStore();
        var records = new List<VectorRecord>
        {
            new("id1", new float[] { 1, 2, 3 }, new Dictionary<string, object>(), "bad tenant"),
        };

        // Act
        var result = await store.UpsertAsync("documents", records, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Pgvector.InvalidTenantId");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DeleteAsync_BadCollection_ReturnsValidation(string? collection)
    {
        // Arrange
        await using var store = CreateStore();

        // Act
        var result = await store.DeleteAsync(collection!, new List<string> { "id1" }, null, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Pgvector.InvalidCollection");
    }

    [Fact]
    public async Task DeleteAsync_NullIds_Throws()
    {
        // Arrange
        await using var store = CreateStore();

        // Act
        Func<Task> act = () => store.DeleteAsync("documents", null!, null, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task DeleteAsync_EmptyIds_ReturnsSuccessWithoutHittingDb()
    {
        // Arrange
        await using var store = CreateStore();

        // Act
        var result = await store.DeleteAsync("documents", new List<string>(), null, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_InvalidTenant_ReturnsValidation()
    {
        // Arrange
        await using var store = CreateStore();

        // Act
        var result = await store.DeleteAsync(
            "documents",
            new List<string> { "id1" },
            tenantId: "bad tenant",
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Pgvector.InvalidTenantId");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SearchAsync_BadCollection_ReturnsValidation(string? collection)
    {
        // Arrange
        await using var store = CreateStore();

        // Act
        var result = await store.SearchAsync(collection!, new float[] { 1, 2, 3 }, 5, null, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Pgvector.InvalidCollection");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task SearchAsync_NonPositiveTopK_ReturnsValidation(int topK)
    {
        // Arrange
        await using var store = CreateStore();

        // Act
        var result = await store.SearchAsync("documents", new float[] { 1, 2, 3 }, topK, null, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Pgvector.InvalidTopK");
    }

    [Fact]
    public async Task SearchAsync_EmptyQueryVector_ReturnsValidation()
    {
        // Arrange
        await using var store = CreateStore();

        // Act
        var result = await store.SearchAsync("documents", ReadOnlyMemory<float>.Empty, 5, null, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Pgvector.EmptyQueryVector");
    }
}
