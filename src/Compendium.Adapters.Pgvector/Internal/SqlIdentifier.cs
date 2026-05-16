// -----------------------------------------------------------------------
// <copyright file="SqlIdentifier.cs" company="Sassy Solutions">
//     Copyright (c) 2026 Sassy Solutions. Licensed under the MIT License.
//     See LICENSE in the project root for license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Text.RegularExpressions;

namespace Compendium.Adapters.Pgvector.Internal;

/// <summary>
/// Validates and quotes PostgreSQL identifiers (schema, table, column, collection names).
/// Identifiers that fail validation are rejected outright — they are never escaped or
/// silently transformed — to make injection attempts visible at the boundary.
/// </summary>
internal static partial class SqlIdentifier
{
    /// <summary>
    /// PostgreSQL identifier length limit (NAMEDATALEN-1 with default NAMEDATALEN=64).
    /// </summary>
    public const int MaxLength = 63;

    [GeneratedRegex(@"^[a-zA-Z_][a-zA-Z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex IdentifierRegex();

    /// <summary>
    /// Returns <c>true</c> when <paramref name="identifier"/> starts with a letter or underscore,
    /// contains only <c>[a-zA-Z0-9_]</c>, and is &lt;= <see cref="MaxLength"/> chars.
    /// </summary>
    public static bool IsValid(string? identifier)
    {
        if (string.IsNullOrEmpty(identifier))
        {
            return false;
        }

        if (identifier.Length > MaxLength)
        {
            return false;
        }

        return IdentifierRegex().IsMatch(identifier);
    }

    /// <summary>
    /// Returns the identifier wrapped in double-quotes if it is valid; throws otherwise.
    /// This is the only sanctioned way to embed an identifier into SQL emitted by this adapter.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when <paramref name="identifier"/> is invalid.</exception>
    public static string Quote(string identifier, string parameterName)
    {
        if (!IsValid(identifier))
        {
            throw new ArgumentException(
                $"Invalid SQL identifier '{identifier}'. Must match [a-zA-Z_][a-zA-Z0-9_]* and be {MaxLength} chars or fewer.",
                parameterName);
        }

        return "\"" + identifier + "\"";
    }
}
