// -----------------------------------------------------------------------
// <copyright file="CollectionNaming.cs" company="Sassy Solutions">
//     Copyright (c) 2026 Sassy Solutions. Licensed under the MIT License.
//     See LICENSE in the project root for license information.
// </copyright>
// -----------------------------------------------------------------------

using Compendium.Adapters.Pgvector.Options;

namespace Compendium.Adapters.Pgvector.Internal;

/// <summary>
/// Derives the schema-qualified table name used to back a logical collection.
/// </summary>
internal static class CollectionNaming
{
    /// <summary>
    /// Returns the unquoted table name (<c>{prefix}{collection}</c>) for diagnostics.
    /// </summary>
    public static string GetTableName(PgvectorOptions options, string collection)
    {
        ArgumentNullException.ThrowIfNull(options);
        return (options.TablePrefix ?? string.Empty) + collection;
    }

    /// <summary>
    /// Returns the schema-qualified, double-quoted table name safe to interpolate into SQL.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the schema, prefix, or collection produce an invalid identifier.</exception>
    public static string GetQualifiedQuotedTable(PgvectorOptions options, string collection)
    {
        ArgumentNullException.ThrowIfNull(options);
        var table = GetTableName(options, collection);
        return SqlIdentifier.Quote(options.Schema, nameof(options.Schema))
               + "."
               + SqlIdentifier.Quote(table, nameof(collection));
    }
}
