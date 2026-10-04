namespace Hexalith.Parties.Security;

/// <summary>Purpose-protected snapshot wrapper independent of profile erasure keys.</summary>
/// <param name="Marker">The versioned purpose wrapper discriminator.</param>
/// <param name="Payload">Provider-owned protected snapshot content.</param>
public sealed record IdentityHistorySnapshot(string Marker, object Payload);
