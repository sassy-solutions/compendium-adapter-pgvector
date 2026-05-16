// -----------------------------------------------------------------------
// <copyright file="PgvectorOptionsTests.cs" company="Sassy Solutions">
//     Copyright (c) 2026 Sassy Solutions. Licensed under the MIT License.
//     See LICENSE in the project root for license information.
// </copyright>
// -----------------------------------------------------------------------

using System.ComponentModel.DataAnnotations;
using Compendium.Adapters.Pgvector.Options;

namespace Compendium.Adapters.Pgvector.Tests.Options;

/// <summary>
/// Demonstrates the convention every adapter test follows :
/// <list type="bullet">
///   <item>file copyright header</item>
///   <item>class named <c>{SUT}Tests</c></item>
///   <item>method named <c>{Method}_{Scenario}_{Expected}</c></item>
///   <item>explicit <c>// Arrange / // Act / // Assert</c> comments</item>
///   <item>FluentAssertions only — never <c>Assert.*</c></item>
/// </list>
/// </summary>
public class PgvectorOptionsTests
{
    [Fact]
    public void PgvectorOptions_Defaults_AreSensible()
    {
        // Arrange / Act
        var options = new PgvectorOptions();

        // Assert
        options.Timeout.Should().Be(TimeSpan.FromSeconds(30));
        options.BaseUrl.Should().BeEmpty();
        options.ApiKey.Should().BeEmpty();
    }

    [Theory]
    [InlineData("", "key", false)]
    [InlineData("   ", "key", false)]
    [InlineData("not-a-url", "key", false)]
    [InlineData("https://api.example.com", "", false)]
    [InlineData("https://api.example.com", "valid-key", true)]
    public void PgvectorOptions_DataAnnotations_ValidateAsExpected(
        string baseUrl,
        string apiKey,
        bool expectedValid)
    {
        // Arrange
        var options = new PgvectorOptions { BaseUrl = baseUrl, ApiKey = apiKey };
        var ctx = new ValidationContext(options);
        var results = new List<ValidationResult>();

        // Act
        var actual = Validator.TryValidateObject(options, ctx, results, validateAllProperties: true);

        // Assert
        actual.Should().Be(expectedValid);
    }

    [Fact]
    public void PgvectorOptions_SectionName_IsCanonical()
    {
        // Assert
        PgvectorOptions.SectionName.Should().Be("Compendium:Adapters:Pgvector");
    }
}
