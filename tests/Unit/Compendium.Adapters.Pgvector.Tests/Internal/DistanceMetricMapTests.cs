// -----------------------------------------------------------------------
// <copyright file="DistanceMetricMapTests.cs" company="Sassy Solutions">
//     Copyright (c) 2026 Sassy Solutions. Licensed under the MIT License.
//     See LICENSE in the project root for license information.
// </copyright>
// -----------------------------------------------------------------------

using Compendium.Abstractions.VectorStore.Models;
using Compendium.Adapters.Pgvector.Internal;

namespace Compendium.Adapters.Pgvector.Tests.Internal;

public class DistanceMetricMapTests
{
    [Theory]
    [InlineData(DistanceMetric.L2, "<->")]
    [InlineData(DistanceMetric.Cosine, "<=>")]
    [InlineData(DistanceMetric.InnerProduct, "<#>")]
    public void Operator_KnownMetric_ReturnsPgvectorOperator(DistanceMetric metric, string expected)
    {
        // Arrange / Act
        var actual = DistanceMetricMap.Operator(metric);

        // Assert
        actual.Should().Be(expected);
    }

    [Theory]
    [InlineData(DistanceMetric.L2, "vector_l2_ops")]
    [InlineData(DistanceMetric.Cosine, "vector_cosine_ops")]
    [InlineData(DistanceMetric.InnerProduct, "vector_ip_ops")]
    public void OpClass_KnownMetric_ReturnsExpectedOpClass(DistanceMetric metric, string expected)
    {
        // Arrange / Act
        var actual = DistanceMetricMap.OpClass(metric);

        // Assert
        actual.Should().Be(expected);
    }

    [Theory]
    [InlineData(DistanceMetric.L2, "l2")]
    [InlineData(DistanceMetric.Cosine, "cosine")]
    [InlineData(DistanceMetric.InnerProduct, "inner_product")]
    public void Label_KnownMetric_ReturnsExpectedLabel(DistanceMetric metric, string expected)
    {
        // Arrange / Act
        var actual = DistanceMetricMap.Label(metric);

        // Assert
        actual.Should().Be(expected);
    }

    [Theory]
    [InlineData("l2", DistanceMetric.L2)]
    [InlineData("cosine", DistanceMetric.Cosine)]
    [InlineData("inner_product", DistanceMetric.InnerProduct)]
    public void TryParseLabel_Known_ReturnsTrueAndExpectedMetric(string label, DistanceMetric expected)
    {
        // Arrange / Act
        var ok = DistanceMetricMap.TryParseLabel(label, out var metric);

        // Assert
        ok.Should().BeTrue();
        metric.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("euclidean")]
    [InlineData("manhattan")]
    public void TryParseLabel_Unknown_ReturnsFalse(string? label)
    {
        // Arrange / Act
        var ok = DistanceMetricMap.TryParseLabel(label, out _);

        // Assert
        ok.Should().BeFalse();
    }

    [Fact]
    public void Operator_UnsupportedMetric_Throws()
    {
        // Arrange / Act
        Action act = () => DistanceMetricMap.Operator((DistanceMetric)int.MaxValue);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void OpClass_UnsupportedMetric_Throws()
    {
        // Arrange / Act
        Action act = () => DistanceMetricMap.OpClass((DistanceMetric)int.MaxValue);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Label_UnsupportedMetric_Throws()
    {
        // Arrange / Act
        Action act = () => DistanceMetricMap.Label((DistanceMetric)int.MaxValue);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
