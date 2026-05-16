// -----------------------------------------------------------------------
// <copyright file="TenantIdentifier.cs" company="Sassy Solutions">
//     Copyright (c) 2026 Sassy Solutions. Licensed under the MIT License.
//     See LICENSE in the project root for license information.
// </copyright>
// -----------------------------------------------------------------------

// NOTE: This validator mirrors
// Compendium.Adapters.PostgreSQL.Security.RowLevelSecurityExtensions (see
// https://github.com/sassy-solutions/compendium-adapter-postgresql).
// Same regex, same length cap, same posture: defence-in-depth against
// tenant-id-driven SQL injection. The adapter never raw-concats a tenant id
// into SQL — every code path either parameterises the value or rejects it.

using System.Text.RegularExpressions;

namespace Compendium.Adapters.Pgvector.Security;

/// <summary>
/// Validates tenant identifiers before they are bound to SQL parameters or
/// embedded in error metadata. Rejects anything that could carry a SQL
/// injection payload.
/// </summary>
public static partial class TenantIdentifier
{
    /// <summary>
    /// Maximum length of an accepted tenant identifier.
    /// </summary>
    public const int MaxLength = 255;

    [GeneratedRegex(@"^[a-zA-Z0-9_-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex TenantIdRegex();

    /// <summary>
    /// Returns <c>true</c> when the supplied <paramref name="tenantId"/> is a non-empty string of length
    /// 1–<see cref="MaxLength"/> containing only <c>[a-zA-Z0-9_-]</c>.
    /// </summary>
    /// <param name="tenantId">The tenant identifier to validate.</param>
    /// <returns>Whether the tenant id is well-formed.</returns>
    public static bool IsValid(string? tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return false;
        }

        if (tenantId.Length > MaxLength)
        {
            return false;
        }

        return TenantIdRegex().IsMatch(tenantId);
    }
}
