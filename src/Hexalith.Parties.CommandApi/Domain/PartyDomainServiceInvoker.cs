using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.Parties.Security;
using Hexalith.Parties.Server.Aggregates;

using Microsoft.Extensions.Logging;

namespace Hexalith.Parties.CommandApi.Domain;

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
            PayloadProtectionResult result = await UnprotectEnvelopeOrRedactAsync(command, envelope, cancellationToken).ConfigureAwait(false);

            events.Add(result.PayloadBytes == envelope.Payload
                    && string.Equals(result.SerializationFormat, envelope.Metadata.SerializationFormat, StringComparison.Ordinal)
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
        catch (InvalidOperationException ex) when (IsKeyDestroyedFailure(ex))
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
        catch (InvalidOperationException ex) when (IsKeyDestroyedFailure(ex))
        {
            LogSnapshotRehydrationFallbackToRedaction(
                command.AggregateIdentity.TenantId,
                command.AggregateIdentity.AggregateId,
                ex.GetType().Name,
                ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Recognises the specific InvalidOperationException thrown when a party's encryption key
    /// has been destroyed (post-erasure). All other failures propagate so transient KMS or
    /// structural errors are not silently swallowed by the redaction fallback path.
    /// </summary>
    private static bool IsKeyDestroyedFailure(InvalidOperationException ex)
        => ex.Message.Contains("No encryption key", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("key destroyed", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("key has been deleted", StringComparison.OrdinalIgnoreCase);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falling back to redacted event payload during rehydration for {TenantId}/{PartyId} event {EventTypeName}: {ExceptionType}: {Error}")]
    private partial void LogRehydrationFallbackToRedaction(string tenantId, string partyId, string eventTypeName, string exceptionType, string error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falling back to null snapshot during rehydration for {TenantId}/{PartyId}: {ExceptionType}: {Error}")]
    private partial void LogSnapshotRehydrationFallbackToRedaction(string tenantId, string partyId, string exceptionType, string error);
}
