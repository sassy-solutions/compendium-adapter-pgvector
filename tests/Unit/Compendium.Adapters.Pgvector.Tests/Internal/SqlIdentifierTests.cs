// -----------------------------------------------------------------------
// <copyright file="SqlIdentifierTests.cs" company="Sassy Solutions">
//     Copyright (c) 2026 Sassy Solutions. Licensed under the MIT License.
//     See LICENSE in the project root for license information.
// </copyright>
// -----------------------------------------------------------------------

using Compendium.Adapters.Pgvector.Internal;

namespace Compendium.Adapters.Pgvector.Tests.Internal;

public class SqlIdentifierTests
{
    [Theory]
    [InlineData("documents")]
    [InlineData("_private")]
    [InlineData("schema_1")]
    [InlineData("a")]
    public void IsValid_WellFormed_ReturnsTrue(string id)
    {
        // Arrange / Act
        var actual = SqlIdentifier.IsValid(id);

        // Assert
        actual.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1leading_digit")]
    [InlineData("with space")]
    [InlineData("with-dash")]
    [InlineData("with.dot")]
    [InlineData("with\"quote")]
    [InlineData("'; DROP TABLE--")]
    [InlineData("a;b")]
    public void IsValid_Invalid_ReturnsFalse(string? id)
    {
        // Arrange / Act
        var actual = SqlIdentifier.IsValid(id);

        // Assert
        actual.Should().BeFalse();
    }

    [Fact]
    public void IsValid_TooLong_ReturnsFalse()
    {
        // Arrange
        var id = new string('a', SqlIdentifier.MaxLength + 1);

        // Act
        var actual = SqlIdentifier.IsValid(id);

        // Assert
        actual.Should().BeFalse();
    }

    [Fact]
    public void IsValid_AtMaxLength_ReturnsTrue()
    {
        // Arrange
        var id = new string('a', SqlIdentifier.MaxLength);

        // Act
        var actual = SqlIdentifier.IsValid(id);

        // Assert
        actual.Should().BeTrue();
    }

    [Fact]
    public void Quote_ValidIdentifier_ReturnsDoubleQuoted()
    {
        // Arrange / Act
        var actual = SqlIdentifier.Quote("documents", "name");

        // Assert
        actual.Should().Be("\"documents\"");
    }

    [Fact]
    public void Quote_Invalid_Throws()
    {
        // Arrange / Act
        Action act = () => SqlIdentifier.Quote("'; DROP TABLE--", "name");

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("*Invalid SQL identifier*");
    }
}
