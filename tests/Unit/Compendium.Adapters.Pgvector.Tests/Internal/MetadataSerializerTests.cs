// -----------------------------------------------------------------------
// <copyright file="MetadataSerializerTests.cs" company="Sassy Solutions">
//     Copyright (c) 2026 Sassy Solutions. Licensed under the MIT License.
//     See LICENSE in the project root for license information.
// </copyright>
// -----------------------------------------------------------------------

using Compendium.Adapters.Pgvector.Internal;

namespace Compendium.Adapters.Pgvector.Tests.Internal;

public class MetadataSerializerTests
{
    [Fact]
    public void Serialise_Null_ReturnsEmptyObject()
    {
        // Arrange / Act
        var actual = MetadataSerializer.Serialise(null);

        // Assert
        actual.Should().Be("{}");
    }

    [Fact]
    public void Serialise_Empty_ReturnsEmptyObject()
    {
        // Arrange
        IReadOnlyDictionary<string, object> metadata = new Dictionary<string, object>();

        // Act
        var actual = MetadataSerializer.Serialise(metadata);

        // Assert
        actual.Should().Be("{}");
    }

    [Fact]
    public void Serialise_ScalarValues_RoundTrips()
    {
        // Arrange
        var metadata = new Dictionary<string, object>
        {
            ["title"] = "doc1",
            ["page"] = 42L,
            ["enabled"] = true,
            ["score"] = 3.14,
        };

        // Act
        var json = MetadataSerializer.Serialise(metadata);
        var actual = MetadataSerializer.Deserialise(json);

        // Assert
        actual.Should().ContainKey("title").WhoseValue.Should().Be("doc1");
        actual.Should().ContainKey("page").WhoseValue.Should().Be(42L);
        actual.Should().ContainKey("enabled").WhoseValue.Should().Be(true);
        // double round-trip through JSON keeps the value but type may shift to double — assert numerically.
        actual["score"].Should().BeOfType<double>().Which.Should().BeApproximately(3.14, 1e-9);
    }

    [Fact]
    public void Deserialise_Null_ReturnsEmpty()
    {
        // Arrange / Act
        var actual = MetadataSerializer.Deserialise(null);

        // Assert
        actual.Should().BeEmpty();
    }

    [Fact]
    public void Deserialise_EmptyString_ReturnsEmpty()
    {
        // Arrange / Act
        var actual = MetadataSerializer.Deserialise(string.Empty);

        // Assert
        actual.Should().BeEmpty();
    }

    [Fact]
    public void Deserialise_NestedObject_ReturnsNestedDictionary()
    {
        // Arrange
        const string json = "{\"meta\":{\"author\":\"a\",\"version\":1}}";

        // Act
        var actual = MetadataSerializer.Deserialise(json);

        // Assert
        actual.Should().ContainKey("meta");
        var nested = actual["meta"].Should().BeAssignableTo<IDictionary<string, object>>().Subject;
        nested.Should().ContainKey("author").WhoseValue.Should().Be("a");
        nested["version"].Should().Be(1L);
    }

    [Fact]
    public void Deserialise_ArrayOfStrings_ReturnsArray()
    {
        // Arrange
        const string json = "{\"tags\":[\"a\",\"b\",\"c\"]}";

        // Act
        var actual = MetadataSerializer.Deserialise(json);

        // Assert
        var tags = actual["tags"].Should().BeAssignableTo<IEnumerable<object>>().Subject;
        tags.Should().Equal("a", "b", "c");
    }

    [Fact]
    public void Deserialise_NullJsonValue_ReturnsNull()
    {
        // Arrange
        const string json = "{\"k\":null}";

        // Act
        var actual = MetadataSerializer.Deserialise(json);

        // Assert
        actual.Should().ContainKey("k");
        actual["k"].Should().BeNull();
    }

    [Fact]
    public void Deserialise_IntegerThenDouble_BothNumericTypesRoundTrip()
    {
        // Arrange — 42 fits in long; 1.5 falls through to double.
        const string json = "{\"i\":42,\"d\":1.5}";

        // Act
        var actual = MetadataSerializer.Deserialise(json);

        // Assert
        actual["i"].Should().BeOfType<long>().Which.Should().Be(42L);
        actual["d"].Should().BeOfType<double>().Which.Should().BeApproximately(1.5, 1e-9);
    }
}
