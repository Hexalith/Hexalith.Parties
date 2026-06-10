using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

using FluentValidation;
using FluentValidation.Results;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Security;
using Hexalith.Parties.Server.Aggregates;
using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Contracts.State;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hexalith.Parties.Domain;

internal sealed partial class PartyDomainServiceInvoker(
    IEventPayloadProtectionService payloadProtectionService,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<PartyDomainServiceInvoker> logger,
    IPartyErasureRecordStore? erasureRecordStore = null,
    PartyErasureOrchestrator? erasureOrchestrator = null) : IDomainServiceInvoker
{
    private const string PartyDomain = "party";

    // Allowlist anchor — only types from this assembly may be resolved as command payload types.
    // Prevents Type.GetType from loading arbitrary assemblies via assembly-qualified wire data.
    private static readonly Assembly ContractsAssembly = typeof(CreateParty).Assembly;

    // Symmetric to envelope payload serialization. EventStore producers serialize with default
    // System.Text.Json options + JsonStringEnumConverter; deserializing with anything else risks
    // silently dropping required fields, which would yield false-pass / false-fail validation.
    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.General)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly ConcurrentDictionary<string, Type?> CommandTypeCache = new(StringComparer.Ordinal);

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

        DomainResult? rejection = await TryRejectInvalidPayloadAsync(command, cancellationToken).ConfigureAwait(false);
        if (rejection is not null)
        {
            // Short-circuit before any payload unprotection or aggregate processing. Returning a
            // rejection result keeps the failure on the platform's normal IRejectionEvent path
            // (no dead-letter, no AggregateActor infrastructure-failure routing).
            return rejection;
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
        if (ResolveCommandType(command.CommandType) == typeof(RetryErasureVerification))
        {
            return await InvokeRetryErasureVerificationAsync(command, unprotectedState, cancellationToken)
                .ConfigureAwait(false);
        }

        return await aggregate.ProcessAsync(command, unprotectedState).ConfigureAwait(false);
    }

    private async Task<DomainResult> InvokeRetryErasureVerificationAsync(
        CommandEnvelope envelope,
        object? currentState,
        CancellationToken cancellationToken)
    {
        RetryErasureVerification command = JsonSerializer.Deserialize<RetryErasureVerification>(envelope.Payload, PayloadJsonOptions)
            ?? throw new InvalidOperationException("Validated RetryErasureVerification payload deserialized to null.");

        if (!string.Equals(command.TenantId, envelope.TenantId, StringComparison.Ordinal)
            || !string.Equals(command.PartyId, envelope.AggregateId, StringComparison.Ordinal))
        {
            return RejectionFor(envelope.CommandType, "Route", "RouteMismatch");
        }

        PartyState? state = RehydratePartyState(currentState);
        if (state is null)
        {
            return DomainResult.Rejection([new PartyNotFound { Message = "Party does not exist." }]);
        }

        if (state.ErasureStatus is ErasureStatus.Erased)
        {
            return DomainResult.NoOp();
        }

        if (state.ErasureStatus is not (ErasureStatus.KeyDestroyed or ErasureStatus.VerificationInProgress or ErasureStatus.Verified))
        {
            return DomainResult.Rejection([new PartyErasureInProgress
            {
                PartyId = command.PartyId,
                TenantId = command.TenantId,
                Status = state.ErasureStatus.ToString(),
                Message = "Erasure verification retry is not available for the current party state.",
            }]);
        }

        if (erasureRecordStore is null || erasureOrchestrator is null)
        {
            return RejectionFor(envelope.CommandType, "Contract", "ContractUnavailable");
        }

        ErasureCertificate? certificate = await erasureRecordStore
            .GetCertificateAsync(command.TenantId, command.PartyId, cancellationToken)
            .ConfigureAwait(false);
        if (certificate is null)
        {
            return RejectionFor(envelope.CommandType, "Certificate", "CertificateUnavailable");
        }

        ErasureVerificationReport report = await erasureOrchestrator
            .ExecuteVerificationAsync(command.TenantId, command.PartyId, certificate, cancellationToken)
            .ConfigureAwait(false);
        await erasureRecordStore.SaveVerificationReportAsync(report, cancellationToken).ConfigureAwait(false);

        ErasureVerificationStatus certificateStatus = report.OverallStatus == ErasureVerificationOverallStatus.Complete
            ? ErasureVerificationStatus.Verified
            : report.OverallStatus == ErasureVerificationOverallStatus.Pending
                ? ErasureVerificationStatus.Pending
                : ErasureVerificationStatus.Failed;
        await erasureRecordStore
            .SaveCertificateAsync(certificate with { Timestamp = report.Timestamp, VerificationStatus = certificateStatus }, cancellationToken)
            .ConfigureAwait(false);
        await erasureRecordStore
            .SaveStatusAsync(
                new PartyErasureStatusRecord
                {
                    PartyId = command.PartyId,
                    TenantId = command.TenantId,
                    Status = report.OverallStatus == ErasureVerificationOverallStatus.Complete
                        ? ErasureStatus.Verified.ToString()
                        : report.OverallStatus.ToString(),
                    UpdatedAt = report.Timestamp,
                    ErrorMessage = report.OverallStatus == ErasureVerificationOverallStatus.Complete
                        ? null
                        : "Erasure verification did not complete.",
                },
                cancellationToken)
            .ConfigureAwait(false);

        if (report.OverallStatus != ErasureVerificationOverallStatus.Complete)
        {
            return DomainResult.NoOp();
        }

        List<IEventPayload> events = [];
        if (state.ErasureStatus is ErasureStatus.KeyDestroyed or ErasureStatus.VerificationInProgress)
        {
            events.Add(new ErasureVerified
            {
                PartyId = command.PartyId,
                TenantId = command.TenantId,
                VerifiedAt = report.Timestamp,
                VerificationReportId = string.IsNullOrWhiteSpace(envelope.CorrelationId)
                    ? "retry-erasure-verification"
                    : envelope.CorrelationId,
            });
        }

        events.Add(new PartyErased
        {
            PartyId = command.PartyId,
            TenantId = command.TenantId,
            ErasedAt = report.Timestamp,
            ErasureStatus = ErasureStatus.Erased.ToString(),
            VerificationStatus = report.OverallStatus.ToString(),
        });

        return DomainResult.Success(events);
    }

    private static PartyState? RehydratePartyState(object? currentState)
    {
        if (currentState is null)
        {
            return null;
        }

        if (currentState is PartyState typed)
        {
            return typed;
        }

        if (currentState is DomainServiceCurrentState snapshotAware)
        {
            PartyState? state = snapshotAware.SnapshotState switch
            {
                null when snapshotAware.Events.Count == 0 => null,
                null => new PartyState(),
                PartyState snapshot => snapshot,
                JsonElement json when json.ValueKind == JsonValueKind.Null && snapshotAware.Events.Count == 0 => null,
                JsonElement json when json.ValueKind == JsonValueKind.Object => ReadPartyStateSnapshot(json),
                _ => ReadPartyStateSnapshot(JsonSerializer.SerializeToElement(snapshotAware.SnapshotState, PayloadJsonOptions)),
            };

            if (state is null)
            {
                return null;
            }

            foreach (EventEnvelope historicalEvent in snapshotAware.Events)
            {
                ApplyHistoricalEvent(state, historicalEvent);
            }

            return state;
        }

        if (currentState is JsonElement element && element.ValueKind == JsonValueKind.Object)
        {
            return ReadPartyStateSnapshot(element);
        }

        return null;
    }

    private static PartyState ReadPartyStateSnapshot(JsonElement snapshot)
    {
        PartyState state = new();
        if (TryGetProperty(snapshot, nameof(PartyState.ErasureStatus), out JsonElement erasureStatus)
            && TryReadErasureStatus(erasureStatus, out ErasureStatus status))
        {
            SetPrivateProperty(state, nameof(PartyState.ErasureStatus), status);
        }

        if (TryGetProperty(snapshot, nameof(PartyState.ErasedAt), out JsonElement erasedAt)
            && erasedAt.ValueKind == JsonValueKind.String
            && erasedAt.TryGetDateTimeOffset(out DateTimeOffset value))
        {
            SetPrivateProperty(state, nameof(PartyState.ErasedAt), value);
        }

        if (TryGetProperty(snapshot, nameof(PartyState.IsRestricted), out JsonElement isRestricted)
            && isRestricted.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            SetPrivateProperty(state, nameof(PartyState.IsRestricted), isRestricted.GetBoolean());
        }

        return state;
    }

    private static bool TryGetProperty(JsonElement snapshot, string propertyName, out JsonElement value)
    {
        if (snapshot.TryGetProperty(propertyName, out value))
        {
            return true;
        }

        string camelCase = JsonNamingPolicy.CamelCase.ConvertName(propertyName);
        return snapshot.TryGetProperty(camelCase, out value);
    }

    private static bool TryReadErasureStatus(JsonElement element, out ErasureStatus status)
    {
        if (element.ValueKind == JsonValueKind.String
            && Enum.TryParse(element.GetString(), ignoreCase: true, out status))
        {
            return true;
        }

        if (element.ValueKind == JsonValueKind.Number
            && element.TryGetInt32(out int numeric)
            && Enum.IsDefined(typeof(ErasureStatus), numeric))
        {
            status = (ErasureStatus)numeric;
            return true;
        }

        status = ErasureStatus.Active;
        return false;
    }

    private static void SetPrivateProperty<T>(PartyState state, string propertyName, T value)
    {
        PropertyInfo property = typeof(PartyState).GetProperty(propertyName)
            ?? throw new InvalidOperationException($"PartyState property '{propertyName}' was not found.");
        property.SetValue(state, value);
    }

    private static void ApplyHistoricalEvent(PartyState state, EventEnvelope historicalEvent)
    {
        string eventTypeName = historicalEvent.Metadata.EventTypeName.Split(',', 2)[0].Trim();
        Type? eventType = ContractsAssembly.GetType(eventTypeName, throwOnError: false)
            ?? ContractsAssembly.GetTypes().SingleOrDefault(type => string.Equals(type.Name, eventTypeName, StringComparison.Ordinal));
        if (eventType is null)
        {
            return;
        }

        object? payload = JsonSerializer.Deserialize(historicalEvent.Payload, eventType, PayloadJsonOptions);
        if (payload is null)
        {
            return;
        }

        MethodInfo? apply = typeof(PartyState).GetMethod(nameof(PartyState.Apply), [eventType]);
        _ = apply?.Invoke(state, [payload]);
    }

    private async Task<DomainResult?> TryRejectInvalidPayloadAsync(
        CommandEnvelope command,
        CancellationToken cancellationToken)
    {
        Type? commandType = ResolveCommandType(command.CommandType);
        if (commandType is null)
        {
            // Fail-closed on unresolved command types: a command whose payload type cannot be
            // located in the contracts assembly cannot be validated, and silently skipping
            // validation would let an attacker bypass FluentValidation by mangling CommandType.
            LogUnresolvedCommandType(command.CommandType);
            return RejectionFor(command.CommandType, "CommandType", "UnresolvedCommandType");
        }

        // Use a synchronous scope: validators are stateless and have no async resources.
        // AsyncServiceScope would force an `await using` whose disposal awaiter does not accept
        // ConfigureAwait, breaking the project-wide CA2007 rule.
        using IServiceScope scope = serviceScopeFactory.CreateScope();
        Type validatorType = typeof(IValidator<>).MakeGenericType(commandType);
        if (scope.ServiceProvider.GetService(validatorType) is not IValidator validator)
        {
            // No validator registered for a known contract type: surface as a warning so misconfig
            // is observable, but allow the command through. Validation is opt-in per command type
            // and registering all command types is the responsibility of the actor host startup.
            LogValidatorMissing(commandType.FullName ?? commandType.Name);
            return null;
        }

        if (command.Payload.Length == 0)
        {
            return RejectionFor(command.CommandType, "Payload", "EmptyPayload");
        }

        object? payload;
        try
        {
            payload = JsonSerializer.Deserialize(command.Payload, commandType, PayloadJsonOptions);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or ArgumentException)
        {
            // Note: ex.Message is intentionally not surfaced to the rejection event — it can carry
            // payload fragments (offset context, field excerpts) that would end up in persisted
            // events and operator logs. Log the underlying exception type at debug for diagnostics.
            LogPayloadDeserializationFailure(command.CommandType, ex.GetType().Name);
            return RejectionFor(command.CommandType, "Payload", "InvalidJson");
        }

        if (payload is null)
        {
            return RejectionFor(command.CommandType, "Payload", "InvalidJson");
        }

        IValidationContext context = (IValidationContext)Activator.CreateInstance(
            typeof(ValidationContext<>).MakeGenericType(commandType),
            payload)!;

        ValidationResult result = await validator
            .ValidateAsync(context, cancellationToken)
            .ConfigureAwait(false);

        if (result.IsValid)
        {
            return null;
        }

        IReadOnlyList<PartyValidationFailure> failures = [.. result.Errors.Select(failure => new PartyValidationFailure
        {
            PropertyName = failure.PropertyName ?? string.Empty,
            ErrorCode = failure.ErrorCode ?? "ValidationFailure",
        })];

        return DomainResult.Rejection(
            [new PartyCommandValidationRejected
            {
                CommandType = commandType.FullName ?? commandType.Name,
                Failures = failures,
            }]);
    }

    private static DomainResult RejectionFor(string commandType, string propertyName, string errorCode)
        => DomainResult.Rejection(
            [new PartyCommandValidationRejected
            {
                CommandType = commandType,
                Failures = [new PartyValidationFailure { PropertyName = propertyName, ErrorCode = errorCode }],
            }]);

    private static Type? ResolveCommandType(string commandType)
    {
        if (string.IsNullOrWhiteSpace(commandType))
        {
            return null;
        }

        return CommandTypeCache.GetOrAdd(commandType, ResolveCommandTypeUncached);
    }

    private static Type? ResolveCommandTypeUncached(string commandType)
    {
        // Strip optional assembly suffix and surrounding whitespace/quotes that may sneak in
        // through wire serialization variants.
        string typeName = commandType.Split(',', 2)[0].Trim().Trim('"', '\'');
        if (typeName.Length == 0)
        {
            return null;
        }

        // Restrict resolution to the contracts assembly — never call Type.GetType on raw wire
        // input, which would happily load arbitrary assemblies from the probing path given an
        // assembly-qualified attacker-controlled name like "Foo, EvilAssembly".
        Type? exact = ContractsAssembly.GetType(typeName, throwOnError: false);
        if (exact is not null)
        {
            return exact;
        }

        // Short-name fallback only succeeds when exactly one type in the contracts assembly
        // matches by Name. Multiple matches resolve to null so command-type ambiguity fails-closed.
        Type[] candidates = [.. ContractsAssembly
            .GetTypes()
            .Where(type => string.Equals(type.Name, typeName, StringComparison.Ordinal))
            .Take(2)];
        return candidates.Length == 1 ? candidates[0] : null;
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Rejecting Parties command with unresolved CommandType {CommandType}")]
    private partial void LogUnresolvedCommandType(string commandType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No FluentValidation validator registered for Parties command type {CommandType}; payload validation is being skipped")]
    private partial void LogValidatorMissing(string commandType);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Rejecting Parties command {CommandType}: payload deserialization failed with {ExceptionType}")]
    private partial void LogPayloadDeserializationFailure(string commandType, string exceptionType);
}
