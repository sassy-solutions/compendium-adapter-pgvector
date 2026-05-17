// -----------------------------------------------------------------------
// <copyright file="TenantIdentifierTests.cs" company="Sassy Solutions">
//     Copyright (c) 2026 Sassy Solutions. Licensed under the MIT License.
//     See LICENSE in the project root for license information.
// </copyright>
// -----------------------------------------------------------------------

// The injection corpus below mirrors the one used by
// compendium-adapter-postgresql / RowLevelSecurityExtensionsTests to ensure we
// reject the same set of attacks. Keep the two lists in sync.

using Compendium.Adapters.Pgvector.Security;

namespace Compendium.Adapters.Pgvector.Tests.Security;

public class TenantIdentifierTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValid_NullOrWhitespace_ReturnsFalse(string? tenantId)
    {
        // Arrange / Act
        var actual = TenantIdentifier.IsValid(tenantId);

        // Assert
        actual.Should().BeFalse();
    }

    [Theory]
    [InlineData("tenant-1")]
    [InlineData("tenant_42")]
    [InlineData("Tenant-ABC-123")]
    [InlineData("a")]
    [InlineData("0123456789")]
    [InlineData("acme_corp_2026")]
    public void IsValid_WellFormed_ReturnsTrue(string tenantId)
    {
        // Arrange / Act
        var actual = TenantIdentifier.IsValid(tenantId);

        // Assert
        actual.Should().BeTrue();
    }

    [Theory]
    // SQL-injection payload corpus — mirrors compendium-adapter-postgresql.
    [InlineData("tenant 1")] // space
    [InlineData("tenant;DROP TABLE")]
    [InlineData("tenant'--")]
    [InlineData("tenant/with/slash")]
    [InlineData("tenant.dot")]
    [InlineData("tenant@domain")]
    [InlineData("'; --")]
    [InlineData("' OR '1'='1")]
    [InlineData("tenant\"quote")]
    [InlineData("tenant\\backslash")]
    [InlineData("tenant\nnewline")]
    [InlineData("tenant\rcarriage")]
    [InlineData("tenant\ttab")]
    [InlineData("tenant\0null")]
    [InlineData("tenant;DELETE FROM events;")]
    public void IsValid_InjectionAttempts_AllRejected(string tenantId)
    {
        // Arrange / Act
        var actual = TenantIdentifier.IsValid(tenantId);

        // Assert
        actual.Should().BeFalse();
    }

    [Fact]
    public void IsValid_LengthExceedsLimit_ReturnsFalse()
    {
        // Arrange
        var tenantId = new string('a', TenantIdentifier.MaxLength + 1);

        // Act
        var actual = TenantIdentifier.IsValid(tenantId);

        // Assert
        actual.Should().BeFalse();
    }

    [Fact]
    public void IsValid_LengthExactlyAtLimit_ReturnsTrue()
    {
        // Arrange
        var tenantId = new string('a', TenantIdentifier.MaxLength);

        // Act
        var actual = TenantIdentifier.IsValid(tenantId);

        // Assert
        actual.Should().BeTrue();
    }
}
