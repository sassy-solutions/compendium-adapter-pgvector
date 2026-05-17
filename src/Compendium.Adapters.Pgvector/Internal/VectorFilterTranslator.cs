// -----------------------------------------------------------------------
// <copyright file="VectorFilterTranslator.cs" company="Sassy Solutions">
//     Copyright (c) 2026 Sassy Solutions. Licensed under the MIT License.
//     See LICENSE in the project root for license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Text;
using Compendium.Abstractions.VectorStore.Models;
using Compendium.Adapters.Pgvector.Security;
using Compendium.Core.Results;

namespace Compendium.Adapters.Pgvector.Internal;

/// <summary>
/// Translates a <see cref="VectorFilter"/> tree into a parameterised SQL fragment.
/// Tenant identifiers and field names are validated against deny-lists to ensure
/// no caller-supplied string is ever raw-concatenated into the generated SQL.
/// </summary>
internal sealed class VectorFilterTranslator
{
    private readonly StringBuilder _sql = new();
    private readonly List<(string Name, object? Value)> _parameters = [];
    private int _paramIndex;

    /// <summary>The parameterised <c>WHERE</c> fragment (without the leading <c>WHERE</c>).</summary>
    public string Sql => _sql.ToString();

    /// <summary>Named parameters to bind; names already include the leading <c>@</c>.</summary>
    public IReadOnlyList<(string Name, object? Value)> Parameters => _parameters;

    /// <summary>
    /// Translates the supplied <paramref name="filter"/> into SQL. Returns a failure when an invalid
    /// identifier or unsupported node is encountered.
    /// </summary>
    public static Result<VectorFilterTranslator> Build(VectorFilter? filter, string? tenantOverride)
    {
        var translator = new VectorFilterTranslator();
        var conjuncts = new List<string>();

        // Tenant clause (always emitted when a tenant is supplied — either on the filter or via override).
        var tenantId = tenantOverride ?? filter?.TenantId;
        if (!string.IsNullOrEmpty(tenantId))
        {
            if (!TenantIdentifier.IsValid(tenantId))
            {
                return Error.Validation(
                    "Pgvector.InvalidTenantId",
                    $"Tenant id '{tenantId}' is not a valid identifier (alphanumeric, dashes, underscores; <=255 chars).");
            }

            var p = translator.AddParameter(tenantId);
            conjuncts.Add($"tenant_id = {p}");
        }
        else
        {
            // No tenant supplied — restrict to rows that also have no tenant. This is the safest
            // default; cross-tenant reads are only possible by explicitly passing a tenant.
            conjuncts.Add("tenant_id IS NULL");
        }

        if (filter is not null)
        {
            var nodeResult = translator.TranslateNode(filter);
            if (nodeResult.IsFailure)
            {
                return Result.Failure<VectorFilterTranslator>(nodeResult.Error);
            }

            if (!string.IsNullOrEmpty(nodeResult.Value))
            {
                conjuncts.Add(nodeResult.Value!);
            }
        }

        translator._sql.Append(string.Join(" AND ", conjuncts));
        return Result.Success(translator);
    }

    private Result<string> TranslateNode(VectorFilter node)
    {
        switch (node.Kind)
        {
            case VectorFilterKind.Eq:
            case VectorFilterKind.Ne:
                {
                    if (!IsValidField(node.Field))
                    {
                        return Error.Validation(
                            "Pgvector.InvalidFilterField",
                            $"Filter field '{node.Field}' is not a valid metadata key.");
                    }

                    var op = node.Kind == VectorFilterKind.Eq ? "=" : "<>";
                    var p = AddParameter(node.Value);
                    return Result.Success($"(metadata ->> '{node.Field}') {op} {p}::text");
                }

            case VectorFilterKind.In:
                {
                    if (!IsValidField(node.Field))
                    {
                        return Error.Validation(
                            "Pgvector.InvalidFilterField",
                            $"Filter field '{node.Field}' is not a valid metadata key.");
                    }

                    if (node.Values is null || node.Values.Count == 0)
                    {
                        return Error.Validation(
                            "Pgvector.EmptyInFilter",
                            $"In-filter for field '{node.Field}' requires at least one value.");
                    }

                    var placeholders = new List<string>(node.Values.Count);
                    foreach (var v in node.Values)
                    {
                        placeholders.Add(AddParameter(v) + "::text");
                    }

                    return Result.Success($"(metadata ->> '{node.Field}') IN ({string.Join(", ", placeholders)})");
                }

            case VectorFilterKind.Range:
                {
                    if (!IsValidField(node.Field))
                    {
                        return Error.Validation(
                            "Pgvector.InvalidFilterField",
                            $"Filter field '{node.Field}' is not a valid metadata key.");
                    }

                    if (node.RangeMin is null && node.RangeMax is null)
                    {
                        return Error.Validation(
                            "Pgvector.EmptyRangeFilter",
                            $"Range filter for field '{node.Field}' requires at least one bound.");
                    }

                    var parts = new List<string>(2);
                    if (node.RangeMin is not null)
                    {
                        var op = node.RangeMinInclusive ? ">=" : ">";
                        var p = AddParameter(node.RangeMin);
                        parts.Add($"((metadata ->> '{node.Field}')::numeric {op} {p})");
                    }

                    if (node.RangeMax is not null)
                    {
                        var op = node.RangeMaxInclusive ? "<=" : "<";
                        var p = AddParameter(node.RangeMax);
                        parts.Add($"((metadata ->> '{node.Field}')::numeric {op} {p})");
                    }

                    return Result.Success("(" + string.Join(" AND ", parts) + ")");
                }

            case VectorFilterKind.And:
            case VectorFilterKind.Or:
                {
                    if (node.Children is null || node.Children.Count == 0)
                    {
                        return Error.Validation(
                            "Pgvector.EmptyLogicalFilter",
                            $"Logical filter '{node.Kind}' requires at least one child.");
                    }

                    var sep = node.Kind == VectorFilterKind.And ? " AND " : " OR ";
                    var fragments = new List<string>(node.Children.Count);
                    foreach (var child in node.Children)
                    {
                        var r = TranslateNode(child);
                        if (r.IsFailure)
                        {
                            return Result.Failure<string>(r.Error);
                        }

                        fragments.Add(r.Value!);
                    }

                    return Result.Success("(" + string.Join(sep, fragments) + ")");
                }

            default:
                return Error.Validation(
                    "Pgvector.UnsupportedFilterKind",
                    $"Filter kind '{node.Kind}' is not supported.");
        }
    }

    private string AddParameter(object? value)
    {
        var name = "@p" + _paramIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _paramIndex++;
        _parameters.Add((name, value));
        return name;
    }

    private static bool IsValidField(string? field)
    {
        if (string.IsNullOrEmpty(field))
        {
            return false;
        }

        // Metadata fields can be richer than SQL identifiers (dots, dashes…), but we still want to
        // refuse single-quotes and backslashes which would otherwise need escaping in JSON-path
        // literal context. Keep the character class deliberately tight.
        foreach (var c in field)
        {
            if (c is '\'' or '"' or '\\' or '\n' or '\r' or '\t' or '\0')
            {
                return false;
            }
        }

        return field.Length <= 128;
    }
}
