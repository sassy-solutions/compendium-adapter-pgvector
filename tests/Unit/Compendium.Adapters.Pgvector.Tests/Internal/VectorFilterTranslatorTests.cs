// -----------------------------------------------------------------------
// <copyright file="VectorFilterTranslatorTests.cs" company="Sassy Solutions">
//     Copyright (c) 2026 Sassy Solutions. Licensed under the MIT License.
//     See LICENSE in the project root for license information.
// </copyright>
// -----------------------------------------------------------------------

using Compendium.Abstractions.VectorStore.Models;
using Compendium.Adapters.Pgvector.Internal;

namespace Compendium.Adapters.Pgvector.Tests.Internal;

public class VectorFilterTranslatorTests
{
    [Fact]
    public void Build_NoFilterNoTenant_RestrictsToNullTenant()
    {
        // Arrange / Act
        var result = VectorFilterTranslator.Build(null, tenantOverride: null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Sql.Should().Be("tenant_id IS NULL");
        result.Value.Parameters.Should().BeEmpty();
    }

    [Fact]
    public void Build_TenantOverrideOnly_EmitsEqualityWithParameter()
    {
        // Arrange / Act
        var result = VectorFilterTranslator.Build(filter: null, tenantOverride: "tenant-1");

        // Assert
        result.IsSuccess.Should().BeTrue();
        var translator = result.Value!;
        translator.Sql.Should().Be("tenant_id = @p0");
        translator.Parameters.Should().HaveCount(1);
        translator.Parameters[0].Name.Should().Be("@p0");
        translator.Parameters[0].Value.Should().Be("tenant-1");
    }

    [Fact]
    public void Build_InvalidTenant_ReturnsFailure()
    {
        // Arrange / Act
        var result = VectorFilterTranslator.Build(filter: null, tenantOverride: "'; DROP TABLE--");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Pgvector.InvalidTenantId");
    }

    [Fact]
    public void Build_EqFilter_EmitsJsonbCastEquality()
    {
        // Arrange
        var filter = VectorFilter.Eq("category", "books").ForTenant("tenant-1");

        // Act
        var result = VectorFilterTranslator.Build(filter, tenantOverride: null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Sql.Should().Contain("tenant_id = @p0");
        result.Value.Sql.Should().Contain("(metadata ->> 'category') = @p1::text");
    }

    [Fact]
    public void Build_NeFilter_EmitsNotEquals()
    {
        // Arrange
        var filter = VectorFilter.Ne("status", "archived");

        // Act
        var result = VectorFilterTranslator.Build(filter, tenantOverride: null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Sql.Should().Contain("(metadata ->> 'status') <> @p0::text");
    }

    [Fact]
    public void Build_InFilter_EmitsInClause()
    {
        // Arrange
        var filter = VectorFilter.In("tag", new object[] { "a", "b", "c" });

        // Act
        var result = VectorFilterTranslator.Build(filter, tenantOverride: null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Sql.Should().Contain("IN (@p0::text, @p1::text, @p2::text)");
        result.Value.Parameters.Select(p => p.Value).Should().Equal("a", "b", "c");
    }

    [Fact]
    public void Build_InFilterEmptyValues_VectorFilterFactoryThrows()
    {
        // The abstraction's VectorFilter.In factory itself rejects empty value sets, so the
        // translator never sees one. We assert the abstraction's invariant here to prevent
        // regressions if it ever loosens.

        // Arrange / Act
        Action act = () => VectorFilter.In("tag", Array.Empty<object>());

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Build_RangeFilter_EmitsBothBoundsByDefault()
    {
        // Arrange
        var filter = VectorFilter.Range("score", min: 0.0, max: 1.0, minInclusive: true, maxInclusive: false);

        // Act
        var result = VectorFilterTranslator.Build(filter, tenantOverride: null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Sql.Should().Contain(">= @p0");
        result.Value.Sql.Should().Contain("< @p1");
    }

    [Fact]
    public void Build_RangeFilterOnlyMin_EmitsOneBound()
    {
        // Arrange
        var filter = VectorFilter.Range("score", min: 0.5, max: null, minInclusive: false, maxInclusive: false);

        // Act
        var result = VectorFilterTranslator.Build(filter, tenantOverride: null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Sql.Should().Contain("> @p0");
        result.Value.Sql.Should().NotContain("<");
    }

    [Fact]
    public void Build_AndFilter_JoinsWithAnd()
    {
        // Arrange
        var filter = VectorFilter.And(
            VectorFilter.Eq("a", "1"),
            VectorFilter.Eq("b", "2"));

        // Act
        var result = VectorFilterTranslator.Build(filter, tenantOverride: null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Sql.Should().Contain(" AND ");
    }

    [Fact]
    public void Build_OrFilter_JoinsWithOr()
    {
        // Arrange
        var filter = VectorFilter.Or(
            VectorFilter.Eq("a", "1"),
            VectorFilter.Eq("b", "2"));

        // Act
        var result = VectorFilterTranslator.Build(filter, tenantOverride: null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Sql.Should().Contain(" OR ");
    }

    [Theory]
    [InlineData("with'quote")]
    [InlineData("with\"quote")]
    [InlineData("with\\backslash")]
    [InlineData("with\nnewline")]
    [InlineData("with\0null")]
    public void Build_InvalidFieldName_ReturnsFailure(string field)
    {
        // Arrange
        var filter = VectorFilter.Eq(field, "v");

        // Act
        var result = VectorFilterTranslator.Build(filter, tenantOverride: null);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Pgvector.InvalidFilterField");
    }

    [Fact]
    public void Build_FieldNameTooLong_ReturnsFailure()
    {
        // Arrange
        var filter = VectorFilter.Eq(new string('a', 129), "v");

        // Act
        var result = VectorFilterTranslator.Build(filter, tenantOverride: null);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Pgvector.InvalidFilterField");
    }
}
