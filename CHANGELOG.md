# Changelog

All notable changes to `Compendium.Adapters.Pgvector` are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- `PgvectorVectorStore` implementing `Compendium.Abstractions.VectorStore.IVectorStore` over PostgreSQL + pgvector via raw Npgsql.
  - `EnsureCollectionAsync` — idempotent extension + per-collection table + ANN index creation (HNSW by default, IVFFlat opt-in). Persists collection metadata (dimension, distance metric) in a per-schema `compendium_pgvector_collections` table; rejects dimension/metric mismatches with structured errors.
  - `UpsertAsync` — single-row `INSERT ... ON CONFLICT DO UPDATE` with JSONB metadata. Tenant-aware: every record's tenant id is validated before binding.
  - `DeleteAsync` — id-list deletion scoped by tenant (either `IS NULL` or `= @tenant_id`); never crosses tenant boundaries.
  - `SearchAsync` — top-k similarity search using the collection's configured operator (`<->`, `<=>`, `<#>`). Tenant scope honoured through `VectorFilter.ForTenant`; missing tenant restricts to `tenant_id IS NULL`.
- `PgvectorOptions` — connection string, schema, table prefix, ANN index choice (HNSW/IVFFlat) and tuning knobs (`HnswM`, `HnswEfConstruction`, `IvfFlatLists`), command timeout, pool size. Data-annotation-validated, `ValidateOnStart`.
- `ServiceCollectionExtensions.AddCompendiumPgvector(...)` — DI registration. Two overloads: `IConfiguration` binding to `Compendium:Adapters:Pgvector` section, or an inline `Action<PgvectorOptions>` callback. Both register `PgvectorVectorStore` as the concrete type and as `IVectorStore`.
- `TenantIdentifier` — security-hardened tenant id validator (alphanumeric + dash + underscore, ≤ 255 chars). Mirrors `compendium-adapter-postgresql/RowLevelSecurityExtensions` to keep the multi-tenant posture consistent across adapters.
- `SqlIdentifier` — PostgreSQL identifier validator + quoter. Rejects (rather than escapes) anything outside `[a-zA-Z_][a-zA-Z0-9_]{0,62}`.
- `DistanceMetricMap` — `DistanceMetric` ↔ pgvector operator + opclass + persisted label.
- `MetadataSerializer` — JSONB round-trip for `IReadOnlyDictionary<string, object>` metadata.
- `VectorFilterTranslator` — translates `VectorFilter` (Eq/Ne/In/Range/And/Or + tenant) into parameterised SQL fragments with strict field-name validation.
- `samples/01-rag-roundtrip` — minimal runnable program that ensures a collection, upserts five vectors, searches the top three.
- `tests/Unit/Compendium.Adapters.Pgvector.Tests` — 143 unit tests covering options validation, tenant id validator (with the same SQL-injection corpus as `compendium-adapter-postgresql`), SQL identifier validation, distance-metric mapping, JSONB metadata round-trip, vector-filter translation, DI registration, and `PgvectorVectorStore` argument-validation paths.
- `tests/Integration/Compendium.Adapters.Pgvector.IntegrationTests` — Testcontainers-based suite (`pgvector/pgvector:pg17`) covering extension bootstrap, idempotent ensure-collection, dimension-mismatch detection, upsert/search/delete round-trip, tenant isolation, and collection-not-found behaviour. Skips cleanly when Docker is unavailable via `[RequiresDockerFact]`.

### Dependencies

- `Compendium.Abstractions.VectorStore` 1.0.1
- `Compendium.Abstractions` 1.0.1
- `Compendium.Core` 1.0.1
- `Npgsql` 9.0.4
- `Pgvector` 0.3.2
