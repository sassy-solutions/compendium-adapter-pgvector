// -----------------------------------------------------------------------
// <copyright file="PgvectorAdapterTests.cs" company="Sassy Solutions">
//     Copyright (c) 2026 Sassy Solutions. Licensed under the MIT License.
//     See LICENSE in the project root for license information.
// </copyright>
// -----------------------------------------------------------------------

using Compendium.Adapters.Pgvector.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Compendium.Adapters.Pgvector.Tests;

/// <summary>
/// Unit tests for <see cref="PgvectorAdapter"/>.
/// Demonstrates the canonical xUnit + FluentAssertions + NSubstitute pattern.
/// </summary>
public class PgvectorAdapterTests
{
    private readonly PgvectorOptions _options = new() { BaseUrl = "https://api.example.com", ApiKey = "k1" };
    private readonly ILogger<PgvectorAdapter> _logger = Substitute.For<ILogger<PgvectorAdapter>>();

    [Fact]
    public void Constructor_NullOptions_Throws()
    {
        // Arrange
        IOptions<PgvectorOptions>? options = null;

        // Act
        var act = () => new PgvectorAdapter(options!, _logger);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        // Arrange
        ILogger<PgvectorAdapter>? logger = null;

        // Act
        var act = () => new PgvectorAdapter(Microsoft.Extensions.Options.Options.Create(_options), logger!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task EchoAsync_HappyPath_ReturnsPrefixedPayload()
    {
        // Arrange
        var adapter = new PgvectorAdapter(Microsoft.Extensions.Options.Options.Create(_options), _logger);

        // Act
        var actual = await adapter.EchoAsync("hello");

        // Assert
        actual.Should().Be("https://api.example.com::hello");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EchoAsync_NullOrWhitespacePayload_Throws(string payload)
    {
        // Arrange
        var adapter = new PgvectorAdapter(Microsoft.Extensions.Options.Options.Create(_options), _logger);

        // Act
        var act = () => adapter.EchoAsync(payload);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task EchoAsync_NullPayload_Throws()
    {
        // Arrange
        var adapter = new PgvectorAdapter(Microsoft.Extensions.Options.Options.Create(_options), _logger);

        // Act
        var act = () => adapter.EchoAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task EchoAsync_AlreadyCancelledToken_Throws()
    {
        // Arrange
        var adapter = new PgvectorAdapter(Microsoft.Extensions.Options.Options.Create(_options), _logger);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        var act = () => adapter.EchoAsync("anything", cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
