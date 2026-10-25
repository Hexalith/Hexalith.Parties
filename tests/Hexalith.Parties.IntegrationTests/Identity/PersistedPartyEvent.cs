namespace Hexalith.Parties.IntegrationTests.Identity;

/// <summary>One durable stream event. History payloads are ciphertext.</summary>
/// <param name="Sequence">The original stream position.</param>
/// <param name="EventTypeName">The persisted event contract name.</param>
/// <param name="Payload">Plain profile JSON or protected history bytes.</param>
/// <param name="Protected">Whether <paramref name="Payload"/> is history ciphertext.</param>
/// <param name="Timestamp">The original event timestamp.</param>
/// <param name="EvidenceId">The custody unit that protects a history event.</param>
/// <param name="SealedDigest">SHA-256 of the ciphertext, when protected.</param>
internal sealed record PersistedPartyEvent(
    long Sequence,
    string EventTypeName,
    byte[] Payload,
    bool Protected,
    DateTimeOffset Timestamp,
    string? EvidenceId,
    string? SealedDigest);
