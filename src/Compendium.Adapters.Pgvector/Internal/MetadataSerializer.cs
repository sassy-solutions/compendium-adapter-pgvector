// -----------------------------------------------------------------------
// <copyright file="MetadataSerializer.cs" company="Sassy Solutions">
//     Copyright (c) 2026 Sassy Solutions. Licensed under the MIT License.
//     See LICENSE in the project root for license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Text.Json;

namespace Compendium.Adapters.Pgvector.Internal;

/// <summary>
/// Round-trips a <see cref="IReadOnlyDictionary{TKey, TValue}"/> of metadata through PostgreSQL JSONB.
/// </summary>
internal static class MetadataSerializer
{
    private static readonly JsonSerializerOptions s_serialise = new()
    {
        WriteIndented = false,
    };

    /// <summary>
    /// Serialises the supplied metadata to a JSON string suitable for binding to a <c>jsonb</c> parameter.
    /// Returns <c>"{}"</c> when <paramref name="metadata"/> is null or empty.
    /// </summary>
    public static string Serialise(IReadOnlyDictionary<string, object>? metadata)
    {
        if (metadata is null || metadata.Count == 0)
        {
            return "{}";
        }

        return JsonSerializer.Serialize(metadata, s_serialise);
    }

    /// <summary>
    /// Deserialises a JSONB payload back into a metadata dictionary.
    /// Returns an empty dictionary when <paramref name="json"/> is null/empty.
    /// </summary>
    public static IReadOnlyDictionary<string, object> Deserialise(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return new Dictionary<string, object>();
        }

        using var doc = JsonDocument.Parse(json);
        var result = new Dictionary<string, object>(StringComparer.Ordinal);

        foreach (var property in doc.RootElement.EnumerateObject())
        {
            result[property.Name] = ConvertElement(property.Value);
        }

        return result;
    }

    private static object ConvertElement(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString() ?? string.Empty,
        JsonValueKind.Number => element.TryGetInt64(out var l)
            ? l
            : (element.TryGetDouble(out var d) ? d : (object)element.GetDecimal()),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null!,
        JsonValueKind.Array => element.EnumerateArray().Select(ConvertElement).ToArray(),
        JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => ConvertElement(p.Value)),
        _ => element.ToString(),
    };
}
