// -----------------------------------------------------------------------
// <copyright file="DistanceMetricMap.cs" company="Sassy Solutions">
//     Copyright (c) 2026 Sassy Solutions. Licensed under the MIT License.
//     See LICENSE in the project root for license information.
// </copyright>
// -----------------------------------------------------------------------

using Compendium.Abstractions.VectorStore.Models;

namespace Compendium.Adapters.Pgvector.Internal;

/// <summary>
/// Translates <see cref="DistanceMetric"/> values into the pgvector operators, opclasses,
/// and score conventions used by the adapter.
/// </summary>
internal static class DistanceMetricMap
{
    /// <summary>
    /// Returns the pgvector distance operator for the given <paramref name="metric"/>.
    /// </summary>
    /// <remarks>
    /// pgvector operators:
    /// <list type="bullet">
    ///   <item><c>&lt;-&gt;</c> — L2 distance.</item>
    ///   <item><c>&lt;=&gt;</c> — cosine distance (1 - cosine similarity).</item>
    ///   <item><c>&lt;#&gt;</c> — negative inner product.</item>
    /// </list>
    /// </remarks>
    public static string Operator(DistanceMetric metric) => metric switch
    {
        DistanceMetric.L2 => "<->",
        DistanceMetric.Cosine => "<=>",
        DistanceMetric.InnerProduct => "<#>",
        _ => throw new ArgumentOutOfRangeException(nameof(metric), metric, "Unsupported distance metric."),
    };

    /// <summary>
    /// Returns the pgvector index opclass for the given <paramref name="metric"/>.
    /// </summary>
    public static string OpClass(DistanceMetric metric) => metric switch
    {
        DistanceMetric.L2 => "vector_l2_ops",
        DistanceMetric.Cosine => "vector_cosine_ops",
        DistanceMetric.InnerProduct => "vector_ip_ops",
        _ => throw new ArgumentOutOfRangeException(nameof(metric), metric, "Unsupported distance metric."),
    };

    /// <summary>
    /// Returns the human-readable label persisted in the <c>compendium_pgvector_collections</c>
    /// metadata table.
    /// </summary>
    public static string Label(DistanceMetric metric) => metric switch
    {
        DistanceMetric.L2 => "l2",
        DistanceMetric.Cosine => "cosine",
        DistanceMetric.InnerProduct => "inner_product",
        _ => throw new ArgumentOutOfRangeException(nameof(metric), metric, "Unsupported distance metric."),
    };

    /// <summary>
    /// Parses the label produced by <see cref="Label"/> back into a <see cref="DistanceMetric"/>.
    /// </summary>
    public static bool TryParseLabel(string? label, out DistanceMetric metric)
    {
        switch (label)
        {
            case "l2":
                metric = DistanceMetric.L2;
                return true;
            case "cosine":
                metric = DistanceMetric.Cosine;
                return true;
            case "inner_product":
                metric = DistanceMetric.InnerProduct;
                return true;
            default:
                metric = default;
                return false;
        }
    }
}
