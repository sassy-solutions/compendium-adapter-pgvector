// -----------------------------------------------------------------------
// <copyright file="PgvectorVectorStore.cs" company="Sassy Solutions">
//     Copyright (c) 2026 Sassy Solutions. Licensed under the MIT License.
//     See LICENSE in the project root for license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Compendium.Abstractions.VectorStore;
using Compendium.Abstractions.VectorStore.Models;
using Compendium.Adapters.Pgvector.Internal;
using Compendium.Adapters.Pgvector.Options;
using Compendium.Adapters.Pgvector.Security;
using Compendium.Core.Results;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;
using PgvectorClient = Pgvector;

namespace Compendium.Adapters.Pgvector;

/// <summary>
/// pgvector-backed <see cref="IVectorStore"/>. Each logical collection is materialised as a
/// dedicated table under the configured schema with a pgvector ANN index.
/// </summary>
/// <remarks>
/// <para>Schema (per collection):</para>
/// <code>
/// CREATE TABLE {schema}.{prefix}{collection} (
///   id          text PRIMARY KEY,
///   tenant_id   text NULL,
///   embedding   vector({dim}) NOT NULL,
///   metadata    jsonb NOT NULL DEFAULT '{}'::jsonb,
///   created_at  timestamptz NOT NULL DEFAULT now(),
///   updated_at  timestamptz NOT NULL DEFAULT now()
/// );
/// CREATE INDEX ... USING hnsw (embedding {opclass}) WITH (m=..., ef_construction=...);
/// CREATE INDEX ... ON ... (tenant_id) WHERE tenant_id IS NOT NULL;
/// </code>
/// <para>
/// Collection metadata (dimension, configured distance metric) is recorded in the per-schema
/// <c>compendium_pgvector_collections</c> table so subsequent calls can validate dimension and pick
/// the right operator for similarity search.
/// </para>
/// </remarks>
[SuppressMessage(
    "Design",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Resolved by DI as IVectorStore.")]
public sealed class PgvectorVectorStore : IVectorStore, IAsyncDisposable
{
    private const string CollectionsTable = "compendium_pgvector_collections";

    private readonly PgvectorOptions _options;
    private readonly ILogger<PgvectorVectorStore> _logger;
    private readonly NpgsqlDataSource _dataSource;
    private readonly bool _ownsDataSource;

    /// <summary>
    /// Creates a new <see cref="PgvectorVectorStore"/> that owns its own <see cref="NpgsqlDataSource"/>.
    /// </summary>
    public PgvectorVectorStore(IOptions<PgvectorOptions> options, ILogger<PgvectorVectorStore> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options.Value ?? throw new ArgumentException("Options.Value is null.", nameof(options));
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_options.ConnectionString))
        {
            throw new ArgumentException("PgvectorOptions.ConnectionString must be configured.", nameof(options));
        }

        var builder = new NpgsqlDataSourceBuilder(_options.ConnectionString);
        builder.UseVector();
        _dataSource = builder.Build();
        _ownsDataSource = true;
    }

    /// <summary>
    /// Creates a new <see cref="PgvectorVectorStore"/> over an existing <see cref="NpgsqlDataSource"/>.
    /// Useful for tests or for sharing a data source with other Compendium adapters.
    /// </summary>
    public PgvectorVectorStore(
        NpgsqlDataSource dataSource,
        IOptions<PgvectorOptions> options,
        ILogger<PgvectorVectorStore> logger)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options.Value ?? throw new ArgumentException("Options.Value is null.", nameof(options));
        _logger = logger;
        _dataSource = dataSource;
        _ownsDataSource = false;
    }

    /// <inheritdoc />
    public async Task<Result> EnsureCollectionAsync(
        string collection,
        int dimension,
        DistanceMetric metric,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(collection))
        {
            return Error.Validation("Pgvector.InvalidCollection", "Collection name cannot be null or whitespace.");
        }

        if (dimension <= 0)
        {
            return Error.Validation("Pgvector.InvalidDimension", $"Dimension must be positive, got {dimension}.");
        }

        if (!SqlIdentifier.IsValid(_options.Schema))
        {
            return Error.Validation("Pgvector.InvalidSchema", $"Schema '{_options.Schema}' is not a valid identifier.");
        }

        var unqualifiedTable = CollectionNaming.GetTableName(_options, collection);
        if (!SqlIdentifier.IsValid(unqualifiedTable))
        {
            return Error.Validation(
                "Pgvector.InvalidCollection",
                $"Collection '{collection}' produces table '{unqualifiedTable}' which is not a valid PostgreSQL identifier.");
        }

        try
        {
            await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

            // Ensure extension + schema-level bookkeeping table.
            await ExecuteAsync(conn, "CREATE EXTENSION IF NOT EXISTS vector;", cancellationToken).ConfigureAwait(false);
            await conn.ReloadTypesAsync().ConfigureAwait(false);

            var schemaQuoted = SqlIdentifier.Quote(_options.Schema, "Schema");
            var collectionsTableQuoted = schemaQuoted + "." + SqlIdentifier.Quote(CollectionsTable, "CollectionsTable");

            await ExecuteAsync(conn, $"""
                CREATE TABLE IF NOT EXISTS {collectionsTableQuoted} (
                    name        text PRIMARY KEY,
                    dimension   integer NOT NULL,
                    metric      text NOT NULL
                );
                """, cancellationToken).ConfigureAwait(false);

            // Check whether the collection is already registered. If so, validate the dimension/metric.
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $"SELECT dimension, metric FROM {collectionsTableQuoted} WHERE name = @name;";
                cmd.CommandTimeout = _options.CommandTimeoutSeconds;
                cmd.Parameters.AddWithValue("name", unqualifiedTable);

                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var existingDim = reader.GetInt32(0);
                    var existingMetric = reader.GetString(1);

                    if (existingDim != dimension)
                    {
                        return VectorStoreErrors.DimensionMismatch(dimension, existingDim);
                    }

                    if (!DistanceMetricMap.TryParseLabel(existingMetric, out var parsed) || parsed != metric)
                    {
                        return Error.Conflict(
                            "Pgvector.MetricMismatch",
                            $"Collection '{collection}' already exists with metric '{existingMetric}', cannot be re-created with '{DistanceMetricMap.Label(metric)}'.");
                    }

                    // Already exists and is compatible — idempotent success.
                    return Result.Success();
                }
            }

            // Create the collection table + indexes.
            var tableQuoted = CollectionNaming.GetQualifiedQuotedTable(_options, collection);
            var indexName = SqlIdentifier.Quote(unqualifiedTable + "_embedding_idx", "indexName");
            var tenantIdxName = SqlIdentifier.Quote(unqualifiedTable + "_tenant_idx", "tenantIdxName");
            var opclass = DistanceMetricMap.OpClass(metric);

            var indexClause = _options.DefaultIndex == PgvectorIndexType.Hnsw
                ? $"CREATE INDEX IF NOT EXISTS {indexName} ON {tableQuoted} USING hnsw (embedding {opclass}) WITH (m = {_options.HnswM}, ef_construction = {_options.HnswEfConstruction});"
                : $"CREATE INDEX IF NOT EXISTS {indexName} ON {tableQuoted} USING ivfflat (embedding {opclass}) WITH (lists = {_options.IvfFlatLists});";

            await ExecuteAsync(conn, $$"""
                CREATE TABLE IF NOT EXISTS {{tableQuoted}} (
                    id          text PRIMARY KEY,
                    tenant_id   text NULL,
                    embedding   vector({{dimension.ToString(CultureInfo.InvariantCulture)}}) NOT NULL,
                    metadata    jsonb NOT NULL DEFAULT '{}'::jsonb,
                    created_at  timestamptz NOT NULL DEFAULT now(),
                    updated_at  timestamptz NOT NULL DEFAULT now()
                );
                """, cancellationToken).ConfigureAwait(false);

            await ExecuteAsync(conn, indexClause, cancellationToken).ConfigureAwait(false);
            await ExecuteAsync(
                conn,
                $"CREATE INDEX IF NOT EXISTS {tenantIdxName} ON {tableQuoted} (tenant_id) WHERE tenant_id IS NOT NULL;",
                cancellationToken).ConfigureAwait(false);

            // Record collection metadata.
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $"""
                    INSERT INTO {collectionsTableQuoted} (name, dimension, metric)
                    VALUES (@name, @dimension, @metric)
                    ON CONFLICT (name) DO NOTHING;
                    """;
                cmd.CommandTimeout = _options.CommandTimeoutSeconds;
                cmd.Parameters.AddWithValue("name", unqualifiedTable);
                cmd.Parameters.AddWithValue("dimension", dimension);
                cmd.Parameters.AddWithValue("metric", DistanceMetricMap.Label(metric));
                await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            _logger.LogInformation(
                "Pgvector collection '{Collection}' ensured (dim={Dimension}, metric={Metric}, index={Index}).",
                collection,
                dimension,
                metric,
                _options.DefaultIndex);

            return Result.Success();
        }
        catch (ArgumentException ex)
        {
            return Error.Validation("Pgvector.InvalidIdentifier", ex.Message);
        }
        catch (PostgresException ex)
        {
            _logger.LogError(ex, "Pgvector EnsureCollection failed for '{Collection}'.", collection);
            return Error.Failure("Pgvector.EnsureCollectionFailed", $"PostgreSQL error: {ex.MessageText}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Pgvector EnsureCollection failed for '{Collection}'.", collection);
            return Error.Failure("Pgvector.EnsureCollectionFailed", ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<Result> UpsertAsync(
        string collection,
        IReadOnlyList<VectorRecord> records,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(collection))
        {
            return Error.Validation("Pgvector.InvalidCollection", "Collection name cannot be null or whitespace.");
        }

        ArgumentNullException.ThrowIfNull(records);

        if (records.Count == 0)
        {
            return Result.Success();
        }

        // Validate every record tenant id before touching the DB.
        foreach (var record in records)
        {
            if (record is null)
            {
                return Error.Validation("Pgvector.InvalidRecord", "Records cannot contain null entries.");
            }

            if (string.IsNullOrWhiteSpace(record.Id))
            {
                return Error.Validation("Pgvector.InvalidRecordId", "VectorRecord.Id cannot be null or whitespace.");
            }

            if (record.TenantId is not null && !TenantIdentifier.IsValid(record.TenantId))
            {
                return Error.Validation(
                    "Pgvector.InvalidTenantId",
                    $"Record '{record.Id}' has invalid tenant id '{record.TenantId}'.");
            }
        }

        try
        {
            var tableQuoted = CollectionNaming.GetQualifiedQuotedTable(_options, collection);
            await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

            await using var transaction = await conn.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            // Row-at-a-time INSERT ... ON CONFLICT DO UPDATE. The threshold-driven fast path is reserved
            // for a future enhancement (Npgsql COPY); the row path is correct and safe for batches in
            // the typical RAG sizing (< 10k records / batch).
            var sql = $"""
                INSERT INTO {tableQuoted} (id, tenant_id, embedding, metadata, updated_at)
                VALUES (@id, @tenant_id, @embedding, @metadata::jsonb, now())
                ON CONFLICT (id) DO UPDATE SET
                    tenant_id = EXCLUDED.tenant_id,
                    embedding = EXCLUDED.embedding,
                    metadata  = EXCLUDED.metadata,
                    updated_at = now();
                """;

            foreach (var record in records)
            {
                await using var cmd = conn.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = sql;
                cmd.CommandTimeout = _options.CommandTimeoutSeconds;
                cmd.Parameters.AddWithValue("id", record.Id);
                cmd.Parameters.AddWithValue("tenant_id", (object?)record.TenantId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("embedding", new PgvectorClient.Vector(record.Embedding));

                var metadataParam = cmd.CreateParameter();
                metadataParam.ParameterName = "metadata";
                metadataParam.NpgsqlDbType = NpgsqlDbType.Jsonb;
                metadataParam.Value = MetadataSerializer.Serialise(record.Metadata);
                cmd.Parameters.Add(metadataParam);

                await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }
        catch (ArgumentException ex)
        {
            return Error.Validation("Pgvector.InvalidIdentifier", ex.Message);
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01") // undefined_table
        {
            return VectorStoreErrors.CollectionNotFound(collection);
        }
        catch (PostgresException ex)
        {
            _logger.LogError(ex, "Pgvector Upsert failed for '{Collection}'.", collection);
            return Error.Failure("Pgvector.UpsertFailed", $"PostgreSQL error: {ex.MessageText}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Pgvector Upsert failed for '{Collection}'.", collection);
            return Error.Failure("Pgvector.UpsertFailed", ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(
        string collection,
        IReadOnlyList<string> ids,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(collection))
        {
            return Error.Validation("Pgvector.InvalidCollection", "Collection name cannot be null or whitespace.");
        }

        ArgumentNullException.ThrowIfNull(ids);

        if (ids.Count == 0)
        {
            return Result.Success();
        }

        if (tenantId is not null && !TenantIdentifier.IsValid(tenantId))
        {
            return Error.Validation(
                "Pgvector.InvalidTenantId",
                $"Tenant id '{tenantId}' is not a valid identifier.");
        }

        try
        {
            var tableQuoted = CollectionNaming.GetQualifiedQuotedTable(_options, collection);
            await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            await using var cmd = conn.CreateCommand();
            cmd.CommandTimeout = _options.CommandTimeoutSeconds;

            // tenantId is provided → enforce equality; otherwise restrict to NULL tenant rows.
            cmd.CommandText = tenantId is null
                ? $"DELETE FROM {tableQuoted} WHERE id = ANY(@ids) AND tenant_id IS NULL;"
                : $"DELETE FROM {tableQuoted} WHERE id = ANY(@ids) AND tenant_id = @tenant_id;";

            cmd.Parameters.Add(new NpgsqlParameter("ids", NpgsqlDbType.Array | NpgsqlDbType.Text)
            {
                Value = ids.ToArray(),
            });

            if (tenantId is not null)
            {
                cmd.Parameters.AddWithValue("tenant_id", tenantId);
            }

            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }
        catch (ArgumentException ex)
        {
            return Error.Validation("Pgvector.InvalidIdentifier", ex.Message);
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            return VectorStoreErrors.CollectionNotFound(collection);
        }
        catch (PostgresException ex)
        {
            _logger.LogError(ex, "Pgvector Delete failed for '{Collection}'.", collection);
            return Error.Failure("Pgvector.DeleteFailed", $"PostgreSQL error: {ex.MessageText}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Pgvector Delete failed for '{Collection}'.", collection);
            return Error.Failure("Pgvector.DeleteFailed", ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<VectorMatch>>> SearchAsync(
        string collection,
        ReadOnlyMemory<float> query,
        int topK,
        VectorFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(collection))
        {
            return Error.Validation("Pgvector.InvalidCollection", "Collection name cannot be null or whitespace.");
        }

        if (topK <= 0)
        {
            return Error.Validation("Pgvector.InvalidTopK", $"topK must be positive, got {topK}.");
        }

        if (query.Length == 0)
        {
            return Error.Validation("Pgvector.EmptyQueryVector", "Query embedding cannot be empty.");
        }

        try
        {
            var tableQuoted = CollectionNaming.GetQualifiedQuotedTable(_options, collection);
            await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

            // Look up the collection's configured metric so we use the correct operator.
            var metric = await LoadMetricAsync(conn, collection, cancellationToken).ConfigureAwait(false);
            if (metric.IsFailure)
            {
                return Result.Failure<IReadOnlyList<VectorMatch>>(metric.Error);
            }

            var op = DistanceMetricMap.Operator(metric.Value);

            var translatorResult = VectorFilterTranslator.Build(filter, tenantOverride: null);
            if (translatorResult.IsFailure)
            {
                return Result.Failure<IReadOnlyList<VectorMatch>>(
                    VectorStoreErrors.InvalidFilter(translatorResult.Error.Message));
            }

            var translator = translatorResult.Value!;

            var sql = $"""
                SELECT id, tenant_id, metadata::text, embedding {op} @query AS score
                FROM {tableQuoted}
                WHERE {translator.Sql}
                ORDER BY embedding {op} @query
                LIMIT @top_k;
                """;

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandTimeout = _options.CommandTimeoutSeconds;
            cmd.Parameters.AddWithValue("query", new PgvectorClient.Vector(query));
            cmd.Parameters.AddWithValue("top_k", topK);
            foreach (var (name, value) in translator.Parameters)
            {
                cmd.Parameters.AddWithValue(name.TrimStart('@'), value ?? DBNull.Value);
            }

            var results = new List<VectorMatch>();
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var id = reader.GetString(0);
                var matchTenant = reader.IsDBNull(1) ? null : reader.GetString(1);
                var metadataJson = reader.IsDBNull(2) ? null : reader.GetString(2);
                var score = reader.GetDouble(3);

                results.Add(new VectorMatch(
                    id,
                    (float)score,
                    MetadataSerializer.Deserialise(metadataJson),
                    matchTenant));
            }

            return Result.Success<IReadOnlyList<VectorMatch>>(results);
        }
        catch (ArgumentException ex)
        {
            return Error.Validation("Pgvector.InvalidIdentifier", ex.Message);
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            return VectorStoreErrors.CollectionNotFound(collection);
        }
        catch (PostgresException ex)
        {
            _logger.LogError(ex, "Pgvector Search failed for '{Collection}'.", collection);
            return Error.Failure("Pgvector.SearchFailed", $"PostgreSQL error: {ex.MessageText}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Pgvector Search failed for '{Collection}'.", collection);
            return Error.Failure("Pgvector.SearchFailed", ex.Message);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_ownsDataSource)
        {
            await _dataSource.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task<Result<DistanceMetric>> LoadMetricAsync(
        NpgsqlConnection connection,
        string collection,
        CancellationToken cancellationToken)
    {
        var schemaQuoted = SqlIdentifier.Quote(_options.Schema, "Schema");
        var collectionsTableQuoted = schemaQuoted + "." + SqlIdentifier.Quote(CollectionsTable, "CollectionsTable");
        var unqualifiedTable = CollectionNaming.GetTableName(_options, collection);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT metric FROM {collectionsTableQuoted} WHERE name = @name;";
        cmd.CommandTimeout = _options.CommandTimeoutSeconds;
        cmd.Parameters.AddWithValue("name", unqualifiedTable);

        try
        {
            var raw = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (raw is not string label || !DistanceMetricMap.TryParseLabel(label, out var metric))
            {
                return VectorStoreErrors.CollectionNotFound(collection);
            }

            return Result.Success(metric);
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            return VectorStoreErrors.CollectionNotFound(collection);
        }
    }

    private async Task ExecuteAsync(NpgsqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.CommandTimeout = _options.CommandTimeoutSeconds;
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
