# `compendium-adapter-pgvector`

pgvector adapter for the [Compendium](https://github.com/sassy-solutions/compendium) framework. Implements `IVectorStore` from `Compendium.Abstractions.VectorStore` over PostgreSQL + the [pgvector](https://github.com/pgvector/pgvector) extension via raw [Npgsql](https://github.com/npgsql/npgsql).

Extracted from `sassy-solutions/compendium` per [ADR-0006](https://github.com/sassy-solutions/compendium/blob/main/docs/adr/0006-multi-repo-adapter-split.md) (multi-repo adapter split). Built from [`template-compendium-adapter-dotnet`](https://github.com/sassy-solutions/template-compendium-adapter-dotnet).

## What's in this package

| Component | Implements | Purpose |
|---|---|---|
| `PgvectorVectorStore` | `IVectorStore` | Embedding storage + ANN similarity search, JSONB metadata, tenant isolation |
| `PgvectorOptions` | — | Connection / schema / index-tuning configuration |
| `TenantIdentifier` | — | Validates tenant ids against a strict alphanumeric+dash+underscore regex before any SQL bind |
| `ServiceCollectionExtensions` | — | DI helpers (`AddCompendiumPgvector(...)`) |

## Install

```bash
dotnet add package Compendium.Adapters.Pgvector
```

## Quick start

```csharp
using Compendium.Abstractions.VectorStore;
using Compendium.Abstractions.VectorStore.Models;
using Compendium.Adapters.Pgvector.DependencyInjection;

services.AddCompendiumPgvector(o =>
{
    o.ConnectionString = "Host=localhost;Database=app;Username=u;Password=p";
    o.Schema = "public";
});

// IVectorStore is now resolvable from DI.
var store = services.BuildServiceProvider().GetRequiredService<IVectorStore>();
await store.EnsureCollectionAsync("documents", dimension: 1536, DistanceMetric.Cosine);
await store.UpsertAsync("documents", new[]
{
    new VectorRecord("doc-1", embedding, metadata, tenantId: "tenant-1"),
});

var matches = await store.SearchAsync(
    "documents",
    queryEmbedding,
    topK: 5,
    VectorFilter.Eq("category", "support").ForTenant("tenant-1"));
```

A runnable example lives under [`samples/01-rag-roundtrip`](samples/01-rag-roundtrip/Program.cs).

## Configuration options

Bind to the `Compendium:Adapters:Pgvector` section, or pass an inline callback.

| Option | Default | Purpose |
|---|---|---|
| `ConnectionString` | _(required)_ | Npgsql connection string. |
| `Schema` | `public` | Schema in which collection tables are created. Must be a valid PostgreSQL identifier. |
| `TablePrefix` | `vec_` | Prefix applied to every collection-derived table name. |
| `DefaultIndex` | `Hnsw` | `Hnsw` (best query latency) or `IvfFlat` (cheaper build). |
| `HnswM` | `16` | HNSW graph degree. |
| `HnswEfConstruction` | `64` | HNSW build-time candidate-list size. |
| `IvfFlatLists` | `100` | IVFFlat `lists` parameter (roughly `sqrt(rows)` per pgvector docs). |
| `BatchUpsertThreshold` | `256` | Reserved for the future COPY-based fast path. |
| `CommandTimeoutSeconds` | `60` | Npgsql command timeout applied to every operation. |
| `MaxPoolSize` | `100` | Npgsql connection-pool ceiling. |

## Tenancy

Every record can carry an optional `TenantId`. The adapter enforces tenant isolation on every read/write:

- **Upsert**: the tenant id is stored on the row; invalid ids (anything outside `[a-zA-Z0-9_-]{1,255}`) are rejected before binding to SQL.
- **Search**: when no `VectorFilter.ForTenant(...)` scope is supplied, queries are restricted to rows with `tenant_id IS NULL`. With a tenant filter, queries restrict to that tenant only. Cross-tenant reads are impossible without explicitly passing a tenant id.
- **Delete**: scoped to either `tenant_id IS NULL` or `tenant_id = @tenant_id`. There is no "delete all tenants" overload.

The `TenantIdentifier.IsValid` helper mirrors `compendium-adapter-postgresql/RowLevelSecurityExtensions` to keep the security posture consistent across adapters.

## Distance metrics

The pgvector operator and index opclass are selected per collection at `EnsureCollectionAsync` time and persisted in the per-schema `compendium_pgvector_collections` table.

| `DistanceMetric` | Operator | Opclass |
|---|---|---|
| `L2` | `<->` | `vector_l2_ops` |
| `Cosine` | `<=>` | `vector_cosine_ops` |
| `InnerProduct` | `<#>` | `vector_ip_ops` |

## Production checklist

- **TLS** — pass `SslMode=VerifyFull` (or `Require` at minimum) in the connection string for any non-loopback deployment.
- **Connection pooling** — keep `MaxPoolSize` aligned with PostgreSQL's `max_connections`. Use `MinPoolSize` if you need pre-warmed connections in latency-sensitive workloads.
- **Dimensions per model** — pick the dimension once: changing it requires recreating the collection. Common values: 384 (e5-small, bge-small), 768 (e5-base, sentence-transformers), 1024 (Cohere embed v3), 1536 (OpenAI text-embedding-3-small), 3072 (OpenAI text-embedding-3-large).
- **Index choice (HNSW vs IVFFlat)** —
  - HNSW (default): faster queries, slower index build, higher memory footprint. Best for read-heavy RAG workloads with up to ~10M vectors.
  - IVFFlat: cheaper build, lower memory, sensitive to `lists` parameter. Recommended for very large collections (>10M) where build time matters and you can recall-tune.
- **`maintenance_work_mem`** — pgvector's HNSW build is memory-bound. Bump to ≥ 1 GB on PG sessions that create the index.
- **Backups** — pgvector data lives in regular PostgreSQL tables; standard `pg_dump`/streaming replication works without special handling.
- **Multi-tenancy** — prefer the `tenant_id` column model (default). For very large workloads with strict isolation requirements, deploy per-tenant schemas and route via separate `PgvectorOptions` instances.

## Versioning

This package continues the version sequence of `Compendium.Adapters.Pgvector` originally published from the framework monorepo. Versions are driven by git tags via [MinVer](https://github.com/adamralph/minver) — see [`docs/RELEASE.md`](docs/RELEASE.md). The first tag from this repo will be set when release infrastructure (and the rotated `NUGET_API_KEY`) is in place.

## Repository conventions

| Aspect | Choice |
|---|---|
| Target | .NET 9, C# 13 |
| DB driver | [Npgsql 9.0.x](https://www.nuget.org/packages/Npgsql) + [Pgvector 0.3.x](https://www.nuget.org/packages/Pgvector) |
| Test framework | xUnit 2.9.3 + FluentAssertions 6.12.1 + NSubstitute 5.1.0 |
| Integration tests | [Testcontainers](https://dotnet.testcontainers.org) 4.11.0 with `pgvector/pgvector:pg17` |
| Coverage gate | 60 % line coverage on the unit-testable surface; integration suite covers DB-bound paths |
| Result pattern | `Result<T>` from `Compendium.Core` |

## Build & test locally

```bash
# Unit tests — no Docker required.
dotnet test --filter "FullyQualifiedName!~IntegrationTests"

# Integration tests — Docker must be running (TestContainers pulls pgvector/pgvector:pg17).
dotnet test --filter "FullyQualifiedName~IntegrationTests"
```

The integration suite covers behaviour that can only be observed against a live pgvector backend: extension bootstrap, collection table + ANN index creation, JSONB round-trip, vector-distance ordering, tenant isolation, idempotent delete.

## License

[MIT](LICENSE) — Copyright © 2026 Sassy Solutions.
