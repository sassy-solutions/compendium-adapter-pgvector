// -----------------------------------------------------------------------
// <copyright file="CollectionNamingTests.cs" company="Sassy Solutions">
//     Copyright (c) 2026 Sassy Solutions. Licensed under the MIT License.
//     See LICENSE in the project root for license information.
// </copyright>
// -----------------------------------------------------------------------

using Compendium.Adapters.Pgvector.Internal;
using Compendium.Adapters.Pgvector.Options;

namespace Compendium.Adapters.Pgvector.Tests.Internal;

public class CollectionNamingTests
{
    [Fact]
    public void GetTableName_AppliesPrefix()
    {
        // Arrange
        var options = new PgvectorOptions { TablePrefix = "vec_" };

        // Act
        var actual = CollectionNaming.GetTableName(options, "documents");

        // Assert
        actual.Should().Be("vec_documents");
    }

    [Fact]
    public void GetTableName_EmptyPrefix_ReturnsCollection()
    {
        // Arrange
        var options = new PgvectorOptions { TablePrefix = string.Empty };

        // Act
        var actual = CollectionNaming.GetTableName(options, "documents");

        // Assert
        actual.Should().Be("documents");
    }

    [Fact]
    public void GetQualifiedQuotedTable_WellFormed_ReturnsQuoted()
    {
        // Arrange
        var options = new PgvectorOptions { Schema = "public", TablePrefix = "vec_" };

        // Act
        var actual = CollectionNaming.GetQualifiedQuotedTable(options, "documents");

        // Assert
        actual.Should().Be("\"public\".\"vec_documents\"");
    }

    [Fact]
    public void GetQualifiedQuotedTable_InvalidSchema_Throws()
    {
        // Arrange
        var options = new PgvectorOptions { Schema = "bad schema", TablePrefix = "vec_" };

        // Act
        Action act = () => CollectionNaming.GetQualifiedQuotedTable(options, "documents");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GetQualifiedQuotedTable_InvalidCollection_Throws()
    {
        // Arrange
        var options = new PgvectorOptions { Schema = "public", TablePrefix = "vec_" };

        // Act
        Action act = () => CollectionNaming.GetQualifiedQuotedTable(options, "'; DROP TABLE--");

        // Assert
        act.Should().Throw<ArgumentException>();
    }
}
