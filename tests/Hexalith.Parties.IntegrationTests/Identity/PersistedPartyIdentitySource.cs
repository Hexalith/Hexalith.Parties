using System.Security.Cryptography;
using System.Text.Json;

using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.Parties.Contracts;

namespace Hexalith.Parties.IntegrationTests.Identity;

/// <summary>Serves authoritative and retained reads from the disposable directory.</summary>
internal sealed class PersistedPartyIdentitySource(PersistedPartyIdentityDirectory directory, TestOnlyIdentityHistoryCustody custody, MutableUtcClock clock)
    : IAuthoritativeEventStreamReader, IRetainedIdentityHistoryReader
{
    /// <inheritdoc />
    public Task<AuthoritativeStreamReadResult> ReadAsync(AggregateIdentity identity, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(identity);
        directory.CountAuthoritativeRead();
        if (directory.ReadFault == "outage")
        {
            return Task.FromResult(new AuthoritativeStreamReadResult(null, "source-unavailable"));
        }

        IReadOnlyList<PersistedPartyEvent> stored = directory.Read(identity.TenantId, identity.AggregateId);
        var events = new List<StreamReadEvent>(stored.Count);
        foreach (PersistedPartyEvent item in stored)
        {
            byte[] payload;
            try
            {
                payload = item.Protected
                    ? custody.UnprotectEventAsync(identity, item.EventTypeName, item.Payload, "json+identity-history-v1", cancellationToken).GetAwaiter().GetResult().PayloadBytes
                    : item.Payload.ToArray();
            }
            catch (CryptographicException)
            {
                return Task.FromResult(new AuthoritativeStreamReadResult(null, "source-unreadable"));
            }

            events.Add(new StreamReadEvent(item.Sequence, item.EventTypeName, payload, "json", 1, "persisted-message", "persisted-correlation", "persisted-causation", item.Timestamp, "persisted-writer", EventStorePayloadProtectionMetadata.Unprotected()));
        }

        DateTimeOffset now = clock.UtcNow;
        DateTimeOffset observedAt = directory.ReadFault switch
        {
            "future" => now.AddHours(1),
            "stale" => DateTimeOffset.UnixEpoch,
            _ => now,
        };
        long head = directory.ReadFault == "gap" ? events.Count + 1 : events.Count;
        var stream = new AuthoritativeEventStream(identity, head, observedAt, events, "persisted-observation");
        return Task.FromResult(new AuthoritativeStreamReadResult(stream, null));
    }

    /// <inheritdoc />
    public async Task<RetainedIdentityHistoryReadResult> ReadAsync(AggregateIdentity identity, string purpose, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(identity);
        directory.CountRetainedRead();
        if (!string.Equals(purpose, RetainedIdentityHistoryReadRequest.AttributionPurpose, StringComparison.Ordinal))
        {
            return new RetainedIdentityHistoryReadResult(null, "purpose-unavailable");
        }

        RetainedIdentityHistoryStream? storedOverride = directory.ReadRetainedOverride(identity.TenantId, identity.AggregateId);
        if (storedOverride is not null)
        {
            return new RetainedIdentityHistoryReadResult(Restamp(storedOverride), null);
        }

        IReadOnlyList<PersistedPartyEvent> stored = directory.Read(identity.TenantId, identity.AggregateId);
        var readable = new List<StreamReadEvent>();
        var excluded = new List<long>();
        var expired = new List<ExpiredIdentityHistoryCertificate>();
        foreach (PersistedPartyEvent item in stored)
        {
            if (!item.Protected)
            {
                excluded.Add(item.Sequence);
                continue;
            }

            try
            {
                byte[] plain = (await custody.UnprotectEventAsync(identity, item.EventTypeName, item.Payload, "json+identity-history-v1", cancellationToken).ConfigureAwait(false)).PayloadBytes;
                readable.Add(new StreamReadEvent(item.Sequence, item.EventTypeName, plain, "json", 1, string.Empty, null, null, item.Timestamp, null, EventStorePayloadProtectionMetadata.Unprotected()));
            }
            catch (CryptographicException) when (item.EvidenceId is not null && !custody.IsPending(item.EvidenceId) && custody.EveryCopyIsUnreadable(item.EvidenceId))
            {
                DateTimeOffset now = clock.UtcNow;
                expired.Add(new ExpiredIdentityHistoryCertificate(identity, purpose, "party-actor-retention-v1", item.Sequence, item.EventTypeName, item.SealedDigest ?? new string('A', 64), "destroyed-" + item.Sequence, 1, "persisted-lifecycle", now, now.AddMinutes(1)));
            }
            catch (CryptographicException)
            {
                return new RetainedIdentityHistoryReadResult(null, "history-unreadable");
            }
        }

        long head = readable.Count + excluded.Count + expired.Count;
        DateTimeOffset observed = clock.UtcNow;
        var stream = new RetainedIdentityHistoryStream(identity, purpose, head, observed, readable, excluded, "persisted-history-observation")
        {
            AuthorityRevision = "persisted-source-r1",
            ValidUntil = observed.AddMinutes(1),
            ExpiredEvents = expired,
        };
        return new RetainedIdentityHistoryReadResult(stream, null);
    }

    /// <summary>Builds the snapshot-free command tail from durable bytes.</summary>
    public DomainServiceCurrentState? CommandState(string tenantId, string partyId)
    {
        IReadOnlyList<PersistedPartyEvent> stored = directory.Read(tenantId, partyId);
        if (stored.Count == 0)
        {
            return null;
        }

        EventEnvelope[] envelopes = stored.Select(item => new EventEnvelope(
            new EventMetadata("01HX00000000000000000000" + item.Sequence.ToString("X4")[..4].PadLeft(4, '0'), partyId, "party", tenantId, "party", item.Sequence, item.Sequence, item.Timestamp, "persisted-correlation", "persisted-causation", "persisted-writer", "persisted", item.EventTypeName, 1, item.Protected ? "json+identity-history-v1" : "json"),
            item.Payload.ToArray(),
            null)).ToArray();
        return new DomainServiceCurrentState(null, envelopes, 0, envelopes.Length);
    }

    private RetainedIdentityHistoryStream Restamp(RetainedIdentityHistoryStream stream)
    {
        DateTimeOffset now = clock.UtcNow;
        ExpiredIdentityHistoryCertificate[] expired = stream.ExpiredEvents.Select(item => item with { ObservedAt = now, ValidUntil = now.AddMinutes(1) }).ToArray();
        return stream with
        {
            ObservedAt = now,
            ValidUntil = now.AddMinutes(1),
            ExpiredEvents = expired,
            AuthorityRevision = string.IsNullOrWhiteSpace(stream.AuthorityRevision) ? "persisted-source-r1" : stream.AuthorityRevision,
        };
    }
}
