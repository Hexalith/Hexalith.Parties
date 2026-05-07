using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.Parties.Security;
using Hexalith.Parties.Server.Aggregates;

using Microsoft.Extensions.Logging;

namespace Hexalith.Parties.Domain;

internal sealed partial class PartyDomainServiceInvoker(
    IEventPayloadProtectionService payloadProtectionService,
    ILogger<PartyDomainServiceInvoker> logger) : IDomainServiceInvoker
{
    private const string PartyDomain = "party";

    public async Task<DomainResult> InvokeAsync(
        CommandEnvelope command,
        object? currentState,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.Equals(command.Domain, PartyDomain, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainServiceNotFoundException(command.TenantId, command.Domain);
        }

        object? unprotectedState = await UnprotectCurrentStateAsync(command, currentState, cancellationToken)
            .ConfigureAwait(false);

        // Allocate the aggregate per invocation so the framework's ProcessAsync cannot
        // accidentally retain transient state across concurrent calls. PartyAggregate.Handle
        // methods are static — the allocation cost is negligible.
        PartyAggregate aggregate = new();
        // Re-check cancellation right before dispatch: the unprotect/replay loop above can be
        // long-running for parties with thousands of events, and the framework's ProcessAsync
        // may not natively observe cancellation.
        cancellationToken.ThrowIfCancellationRequested();
        return await aggregate.ProcessAsync(command, unprotectedState).ConfigureAwait(false);
    }

    private async Task<object?> UnprotectCurrentStateAsync(
        CommandEnvelope command,
        object? currentState,
        CancellationToken cancellationToken)
    {
        if (currentState is not DomainServiceCurrentState state)
        {
            return currentState;
        }

        object? snapshotState = state.SnapshotState is null
            ? null
            : await UnprotectSnapshotOrRedactAsync(command, state.SnapshotState, cancellationToken).ConfigureAwait(false);

        var events = new List<EventEnvelope>(state.Events.Count);
        foreach (EventEnvelope envelope in state.Events)
        {
            // Per-iteration cancellation: streams of thousands of events were previously
            // un-cancellable inside this loop because only the network call observed CT.
            cancellationToken.ThrowIfCancellationRequested();

            PayloadProtectionResult result = await UnprotectEnvelopeOrRedactAsync(command, envelope, cancellationToken).ConfigureAwait(false);

            // The "no-op" fast path is signalled by the protection service returning the same
            // byte[] reference and the same serialization format. Reference-equality on byte[]
            // is fragile (a future defensive-clone change would silently break the optimization
            // and force re-allocation on every event), but format equality keeps us safe in the
            // common case where decryption was a no-op for un-protected events.
            bool isNoOp = ReferenceEquals(result.PayloadBytes, envelope.Payload)
                && string.Equals(result.SerializationFormat, envelope.Metadata.SerializationFormat, StringComparison.Ordinal);

            events.Add(isNoOp
                ? envelope
                : new EventEnvelope(
                    envelope.Metadata with { SerializationFormat = result.SerializationFormat },
                    result.PayloadBytes,
                    envelope.Extensions));
        }

        return state with { SnapshotState = snapshotState, Events = events };
    }

    /// <summary>
    /// Unprotect an event payload, or fall back to redaction (encrypted markers replaced with
    /// JSON null) when decryption fails. After a party's encryption key has been destroyed
    /// (post-erasure), the lifecycle commands <c>MarkPartyEncryptionKeyDeleted</c>,
    /// <c>MarkErasureVerified</c>, and <c>CompletePartyErasure</c> only need erasure-status flags
    /// from non-encrypted events; redacting earlier encrypted events keeps Apply-replay working.
    /// </summary>
    private async Task<PayloadProtectionResult> UnprotectEnvelopeOrRedactAsync(
        CommandEnvelope command,
        EventEnvelope envelope,
        CancellationToken cancellationToken)
    {
        try
        {
            return await payloadProtectionService
                .UnprotectEventPayloadAsync(
                    command.AggregateIdentity,
                    envelope.Metadata.EventTypeName,
                    envelope.Payload,
                    envelope.Metadata.SerializationFormat,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (PartyEncryptionKeyDestroyedException.IsMatch(ex))
        {
            LogRehydrationFallbackToRedaction(
                command.AggregateIdentity.TenantId,
                command.AggregateIdentity.AggregateId,
                envelope.Metadata.EventTypeName,
                ex.GetType().Name,
                ex.Message);
            return PartyPayloadProtectionService.RedactProtectedPayload(envelope.Payload, envelope.Metadata.SerializationFormat);
        }
    }

    private async Task<object?> UnprotectSnapshotOrRedactAsync(
        CommandEnvelope command,
        object snapshotState,
        CancellationToken cancellationToken)
    {
        try
        {
            return await payloadProtectionService
                .UnprotectSnapshotStateAsync(command.AggregateIdentity, snapshotState, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (PartyEncryptionKeyDestroyedException.IsMatch(ex))
        {
            LogSnapshotRehydrationFallbackToRedaction(
                command.AggregateIdentity.TenantId,
                command.AggregateIdentity.AggregateId,
                ex.GetType().Name,
                ex.Message);
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falling back to redacted event payload during rehydration for {TenantId}/{PartyId} event {EventTypeName}: {ExceptionType}: {Error}")]
    private partial void LogRehydrationFallbackToRedaction(string tenantId, string partyId, string eventTypeName, string exceptionType, string error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falling back to null snapshot during rehydration for {TenantId}/{PartyId}: {ExceptionType}: {Error}")]
    private partial void LogSnapshotRehydrationFallbackToRedaction(string tenantId, string partyId, string exceptionType, string error);
}
