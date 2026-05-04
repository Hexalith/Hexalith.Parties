// <copyright file="PartyEncryptionKeyDestroyedException.cs" company="ITANEO">
// Copyright (c) ITANEO (https://www.itaneo.com). All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Hexalith.Parties.Security;

/// <summary>
/// Thrown when a party's encryption key has been destroyed (post-erasure or explicit shred) and
/// no plaintext or unprotect operation is possible. Catch sites that recover via the redaction
/// fallback path should match this typed exception rather than message-text matching, which is
/// fragile to localization, message rewording, and runtime resource-string changes. Transient
/// KMS errors, key-version mismatches, and HSM permission failures must continue to propagate
/// as their own types so projections and command handlers don't silently corrupt with null
/// personal-data fields on a recoverable failure.
/// </summary>
public sealed class PartyEncryptionKeyDestroyedException : KeyNotFoundException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PartyEncryptionKeyDestroyedException"/> class.
    /// </summary>
    /// <param name="tenantId">The tenant id whose key has been destroyed.</param>
    /// <param name="partyId">The party id whose key has been destroyed.</param>
    public PartyEncryptionKeyDestroyedException(string tenantId, string partyId)
        : base($"Encryption key for tenant '{tenantId}' party '{partyId}' has been destroyed (post-erasure).")
    {
        TenantId = tenantId;
        PartyId = partyId;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PartyEncryptionKeyDestroyedException"/> class.
    /// </summary>
    /// <param name="tenantId">The tenant id whose key has been destroyed.</param>
    /// <param name="partyId">The party id whose key has been destroyed.</param>
    /// <param name="innerException">The underlying KMS / storage exception.</param>
    public PartyEncryptionKeyDestroyedException(string tenantId, string partyId, Exception innerException)
        : base($"Encryption key for tenant '{tenantId}' party '{partyId}' has been destroyed (post-erasure).", innerException)
    {
        TenantId = tenantId;
        PartyId = partyId;
    }

    /// <summary>Gets the tenant id whose key has been destroyed.</summary>
    public string TenantId { get; }

    /// <summary>Gets the party id whose key has been destroyed.</summary>
    public string PartyId { get; }

    /// <summary>
    /// Returns true when <paramref name="exception"/> is exactly a <see cref="PartyEncryptionKeyDestroyedException"/>.
    /// Uses exact-type match (not <c>is</c> pattern) because the exception's base type is
    /// <see cref="KeyNotFoundException"/>, which is widely thrown by generic dictionary/cache lookups;
    /// a permissive base-type check would silently route unrelated programming bugs into the
    /// post-erasure redaction path. Future variants (e.g., key-shredded, key-expired) must be
    /// declared as siblings rather than subclasses so they receive distinct handling.
    /// </summary>
    /// <param name="exception">Candidate exception, may be null.</param>
    /// <returns><c>true</c> when the exception is exactly this type; otherwise <c>false</c>.</returns>
    public static bool IsMatch(Exception? exception) =>
        exception is not null && exception.GetType() == typeof(PartyEncryptionKeyDestroyedException);
}
