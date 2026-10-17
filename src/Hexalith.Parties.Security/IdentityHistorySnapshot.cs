namespace Hexalith.Parties.Security;

/// <summary>Purpose-protected snapshot wrapper independent of profile erasure keys.</summary>
/// <param name="Marker">The versioned purpose wrapper discriminator.</param>
/// <param name="Payload">Detached purpose-ciphertext bytes, persisted as base64 by JSON serialization.</param>
public sealed record IdentityHistorySnapshot(string Marker, object Payload);
