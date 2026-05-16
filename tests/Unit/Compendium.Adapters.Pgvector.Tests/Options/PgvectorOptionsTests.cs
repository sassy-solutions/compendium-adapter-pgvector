// -----------------------------------------------------------------------
// <copyright file="PgvectorOptionsTests.cs" company="Sassy Solutions">
//     Copyright (c) 2026 Sassy Solutions. Licensed under the MIT License.
//     See LICENSE in the project root for license information.
// </copyright>
// -----------------------------------------------------------------------

using System.ComponentModel.DataAnnotations;
using Compendium.Adapters.Pgvector.Options;

namespace Compendium.Adapters.Pgvector.Tests.Options;

public class PgvectorOptionsTests
{
    [Fact]
    public void PgvectorOptions_Defaults_AreSensible()
    {
        // Arrange / Act
        var options = new PgvectorOptions();

        // Assert
        options.ConnectionString.Should().BeEmpty();
        options.Schema.Should().Be("public");
        options.TablePrefix.Should().Be("vec_");
        options.DefaultIndex.Should().Be(PgvectorIndexType.Hnsw);
        options.HnswM.Should().Be(16);
        options.HnswEfConstruction.Should().Be(64);
        options.IvfFlatLists.Should().Be(100);
        options.BatchUpsertThreshold.Should().Be(256);
        options.CommandTimeoutSeconds.Should().Be(60);
        options.MaxPoolSize.Should().Be(100);
    }

    [Fact]
    public void PgvectorOptions_SectionName_IsCanonical()
    {
        // Assert
        PgvectorOptions.SectionName.Should().Be("Compendium:Adapters:Pgvector");
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("Host=localhost", true)]
    public void PgvectorOptions_DataAnnotations_RejectMissingConnectionString(string connectionString, bool expectedValid)
    {
        // Arrange
        var options = new PgvectorOptions { ConnectionString = connectionString };
        var ctx = new ValidationContext(options);
        var results = new List<ValidationResult>();

        // Act
        var actual = Validator.TryValidateObject(options, ctx, results, validateAllProperties: true);

        // Assert
        actual.Should().Be(expectedValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1001)]
    public void PgvectorOptions_HnswM_OutOfRange_Fails(int value)
    {
        // Arrange
        var options = new PgvectorOptions { ConnectionString = "Host=localhost", HnswM = value };
        var ctx = new ValidationContext(options);
        var results = new List<ValidationResult>();

        // Act
        var actual = Validator.TryValidateObject(options, ctx, results, validateAllProperties: true);

        // Assert
        actual.Should().BeFalse();
        results.Should().Contain(r => r.MemberNames.Contains(nameof(PgvectorOptions.HnswM)));
    }

    [Fact]
    public void PgvectorOptions_CommandTimeout_TooLow_Fails()
    {
        // Arrange
        var options = new PgvectorOptions { ConnectionString = "Host=localhost", CommandTimeoutSeconds = 0 };
        var ctx = new ValidationContext(options);
        var results = new List<ValidationResult>();

        // Act
        var actual = Validator.TryValidateObject(options, ctx, results, validateAllProperties: true);

        // Assert
        actual.Should().BeFalse();
    }

    [Fact]
    public void PgvectorOptions_MaxPoolSize_AtUpperBound_Passes()
    {
        // Arrange
        var options = new PgvectorOptions { ConnectionString = "Host=localhost", MaxPoolSize = 10_000 };
        var ctx = new ValidationContext(options);
        var results = new List<ValidationResult>();

        // Act
        var actual = Validator.TryValidateObject(options, ctx, results, validateAllProperties: true);

        // Assert
        actual.Should().BeTrue();
    }
}
