// -----------------------------------------------------------------------
// <copyright file="PgvectorOptions.cs" company="Sassy Solutions">
//     Copyright (c) 2026 Sassy Solutions. Licensed under the MIT License.
//     See LICENSE in the project root for license information.
// </copyright>
// -----------------------------------------------------------------------

using System.ComponentModel.DataAnnotations;

namespace Compendium.Adapters.Pgvector.Options;

/// <summary>
/// Index type used by the <c>vector</c> column for approximate nearest neighbour search.
/// </summary>
public enum PgvectorIndexType
{
    /// <summary>
    /// Hierarchical Navigable Small World — default; best query latency, slower build.
    /// </summary>
    Hnsw = 0,

    /// <summary>
    /// Inverted-file flat — cheaper build, sensitive to <c>lists</c> parameter and recall.
    /// </summary>
    IvfFlat = 1,
}

/// <summary>
/// Configuration for <see cref="PgvectorVectorStore"/>.
/// Bound from <c>Compendium:Adapters:Pgvector</c> by default.
/// </summary>
public sealed class PgvectorOptions
{
    /// <summary>
    /// Configuration section name used by <c>IConfiguration.GetSection(...)</c>.
    /// </summary>
    public const string SectionName = "Compendium:Adapters:Pgvector";

    /// <summary>
    /// PostgreSQL connection string. Required.
    /// </summary>
    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Schema in which collection tables are created. Default <c>public</c>.
    /// Must match the identifier regex (alphanumeric + underscore, max 63 chars).
    /// </summary>
    public string Schema { get; set; } = "public";

    /// <summary>
    /// Prefix applied to every collection-derived table name. Default <c>vec_</c>.
    /// </summary>
    public string TablePrefix { get; set; } = "vec_";

    /// <summary>
    /// Default ANN index built when a collection is first created. Default <see cref="PgvectorIndexType.Hnsw"/>.
    /// </summary>
    public PgvectorIndexType DefaultIndex { get; set; } = PgvectorIndexType.Hnsw;

    /// <summary>
    /// HNSW <c>m</c> parameter (graph degree). Default 16.
    /// </summary>
    [Range(2, 1000)]
    public int HnswM { get; set; } = 16;

    /// <summary>
    /// HNSW <c>ef_construction</c> parameter (build-time candidate-list size). Default 64.
    /// </summary>
    [Range(4, 1000)]
    public int HnswEfConstruction { get; set; } = 64;

    /// <summary>
    /// IVFFlat <c>lists</c> parameter. Default 100 (per pgvector docs: sqrt(rows)).
    /// </summary>
    [Range(1, 10_000)]
    public int IvfFlatLists { get; set; } = 100;

    /// <summary>
    /// Threshold above which <see cref="PgvectorVectorStore.UpsertAsync"/> switches from a row-at-a-time
    /// <c>INSERT … ON CONFLICT DO UPDATE</c> path to a staging-table + <c>MERGE</c> path. Default 256.
    /// </summary>
    [Range(1, 100_000)]
    public int BatchUpsertThreshold { get; set; } = 256;

    /// <summary>
    /// Command timeout in seconds applied to every Npgsql command. Default 60.
    /// </summary>
    [Range(1, 3_600)]
    public int CommandTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Maximum size of the Npgsql connection pool. Default 100.
    /// </summary>
    [Range(1, 10_000)]
    public int MaxPoolSize { get; set; } = 100;
}
