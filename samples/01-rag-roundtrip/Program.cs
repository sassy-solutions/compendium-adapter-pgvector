// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="Sassy Solutions">
//     Copyright (c) 2026 Sassy Solutions. Licensed under the MIT License.
//     See LICENSE in the project root for license information.
// </copyright>
// -----------------------------------------------------------------------
//
// Sample 01 — RAG round-trip
// =========================
// Demonstrates the minimal happy path of Compendium.Adapters.Pgvector:
//   1. ensure a 3-dimensional collection (cosine distance, HNSW index),
//   2. upsert five hand-crafted vectors,
//   3. search for the three nearest neighbours to a query vector,
//   4. print the matches.
//
// Connection-string convention:
//   export PGVECTOR_CONNECTION="Host=localhost;Database=pgvec;Username=u;Password=p"
//
// You need a PostgreSQL instance with the `vector` extension installed.
// The fastest way is the official image:
//   docker run --rm -p 5432:5432 -e POSTGRES_PASSWORD=p -e POSTGRES_USER=u -e POSTGRES_DB=pgvec pgvector/pgvector:pg17
// then:
//   PGVECTOR_CONNECTION="Host=localhost;Database=pgvec;Username=u;Password=p" dotnet run --project samples/01-rag-roundtrip

using Compendium.Abstractions.VectorStore.Models;
using Compendium.Adapters.Pgvector;
using Compendium.Adapters.Pgvector.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

var connectionString = Environment.GetEnvironmentVariable("PGVECTOR_CONNECTION")
    ?? throw new InvalidOperationException(
        "Set the PGVECTOR_CONNECTION environment variable to a PostgreSQL connection string with the pgvector extension installed.");

var options = Options.Create(new PgvectorOptions
{
    ConnectionString = connectionString,
    Schema = "public",
    TablePrefix = "sample_",
    DefaultIndex = PgvectorIndexType.Hnsw,
});

using var loggerFactory = LoggerFactory.Create(b => b.AddSimpleConsole(o => o.SingleLine = true));
var logger = loggerFactory.CreateLogger<PgvectorVectorStore>();

await using var store = new PgvectorVectorStore(options, logger);

const string collection = "rag_roundtrip";

// 1. Ensure the collection exists.
var ensure = await store.EnsureCollectionAsync(collection, dimension: 3, DistanceMetric.Cosine);
if (ensure.IsFailure)
{
    Console.Error.WriteLine($"EnsureCollection failed: {ensure.Error.Code} — {ensure.Error.Message}");
    return 1;
}

// 2. Upsert five vectors.
var records = new List<VectorRecord>
{
    new("alpha",   new float[] { 1f,  0f,  0f }, new Dictionary<string, object> { ["title"] = "alpha" }),
    new("beta",    new float[] { 0f,  1f,  0f }, new Dictionary<string, object> { ["title"] = "beta" }),
    new("gamma",   new float[] { 0f,  0f,  1f }, new Dictionary<string, object> { ["title"] = "gamma" }),
    new("ne-x",    new float[] { 1f,  1f,  0f }, new Dictionary<string, object> { ["title"] = "ne-x" }),
    new("origin",  new float[] { 0.5f, 0.5f, 0.5f }, new Dictionary<string, object> { ["title"] = "origin" }),
};

var upsert = await store.UpsertAsync(collection, records);
if (upsert.IsFailure)
{
    Console.Error.WriteLine($"Upsert failed: {upsert.Error.Code} — {upsert.Error.Message}");
    return 1;
}

// 3. Search for the three closest to a query that leans toward alpha+beta.
var query = new float[] { 0.9f, 0.1f, 0f };
var search = await store.SearchAsync(collection, query, topK: 3);
if (search.IsFailure)
{
    Console.Error.WriteLine($"Search failed: {search.Error.Code} — {search.Error.Message}");
    return 1;
}

// 4. Print results.
Console.WriteLine("Top 3 nearest neighbours:");
foreach (var match in search.Value!)
{
    var title = match.Metadata.TryGetValue("title", out var t) ? t : "(no title)";
    Console.WriteLine($"  id={match.Id,-8} score={match.Score,8:F4}  title={title}");
}

return 0;
