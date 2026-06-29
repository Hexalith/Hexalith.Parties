using System.Buffers.Text;
using System.Collections.ObjectModel;
using System.Security;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Parties.Contracts.Security;

namespace Hexalith.Parties.Security;

public sealed class EventStorePartyPayloadProtectionAdapter(
    PartyPayloadProtectionService inner,
    IPartyErasureRecordStore erasureRecordStore) : IEventPayloadProtectionService
{
    private const string ProtectedScheme = "parties-aes-gcm-json-fields";

    private static readonly EventStorePayloadProtectionMetadata s_protectedMetadata = new(
        PayloadProtectionState.Protected,
        EventStorePayloadProtectionMetadata.CurrentMetadataVersion,
        ProtectedScheme,
        KeyAlias: null,
        ContentHint: "application/json",
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["format"] = PartyPayloadProtectionService.ProtectedSerializationFormat,
            ["field-envelope"] = "pdenc-v1",
        }));

    public async Task<PayloadProtectionResult> ProtectEventPayloadAsync(
        AggregateIdentity identity,
        IEventPayload eventPayload,
        string eventTypeName,
        byte[] payloadBytes,
        string serializationFormat,
        CancellationToken cancellationToken = default)
    {
        PayloadProtectionResult result = await inner
            .ProtectEventPayloadAsync(identity, eventPayload, eventTypeName, payloadBytes, serializationFormat, cancellationToken)
            .ConfigureAwait(false);

        return IsProtectedFormat(result.SerializationFormat)
            ? result with { Metadata = s_protectedMetadata }
            : result;
    }

    public Task<PayloadProtectionResult> UnprotectEventPayloadAsync(
        AggregateIdentity identity,
        string eventTypeName,
        byte[] payloadBytes,
        string serializationFormat,
        CancellationToken cancellationToken = default)
        => inner.UnprotectEventPayloadAsync(identity, eventTypeName, payloadBytes, serializationFormat, cancellationToken);

    public Task<PayloadProtectionResult> UnprotectEventPayloadAsync(
        AggregateIdentity identity,
        string eventTypeName,
        byte[] payloadBytes,
        string serializationFormat,
        EventStorePayloadProtectionMetadata? metadata,
        CancellationToken cancellationToken = default)
        => ((IEventPayloadProtectionService)inner).UnprotectEventPayloadAsync(
            identity,
            eventTypeName,
            payloadBytes,
            serializationFormat,
            metadata,
            cancellationToken);

    public Task<object> ProtectSnapshotStateAsync(
        AggregateIdentity identity,
        object state,
        CancellationToken cancellationToken = default)
        => inner.ProtectSnapshotStateAsync(identity, state, cancellationToken);

    public Task<object> UnprotectSnapshotStateAsync(
        AggregateIdentity identity,
        object state,
        CancellationToken cancellationToken = default)
        => inner.UnprotectSnapshotStateAsync(identity, state, cancellationToken);

    public async Task<SnapshotProtectionResult> ProtectSnapshotAsync(
        AggregateIdentity identity,
        object state,
        CancellationToken cancellationToken = default)
    {
        object protectedState = await inner.ProtectSnapshotStateAsync(identity, state, cancellationToken).ConfigureAwait(false);
        EventStorePayloadProtectionMetadata metadata = protectedState is PartyPayloadProtectionService.ProtectedSnapshotState
            ? s_protectedMetadata
            : EventStorePayloadProtectionMetadata.Unprotected();
        return new SnapshotProtectionResult(protectedState, metadata);
    }

    public Task<object> UnprotectSnapshotAsync(
        AggregateIdentity identity,
        object state,
        EventStorePayloadProtectionMetadata? metadata,
        CancellationToken cancellationToken = default)
        => ((IEventPayloadProtectionService)inner).UnprotectSnapshotAsync(identity, state, metadata, cancellationToken);

    public async Task<PayloadUnprotectionOutcome> TryUnprotectEventPayloadAsync(
        AggregateIdentity identity,
        string eventTypeName,
        byte[] payloadBytes,
        string serializationFormat,
        EventStorePayloadProtectionMetadata? metadata,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventTypeName);
        ArgumentNullException.ThrowIfNull(payloadBytes);
        ArgumentException.ThrowIfNullOrWhiteSpace(serializationFormat);

        EventStorePayloadProtectionMetadata resolvedMetadata = metadata ?? EventStorePayloadProtectionMetadataCarrier.Legacy();
        UnreadableProtectedDataReason? metadataFailure = ValidateMetadata(resolvedMetadata);
        if (metadataFailure is not null)
        {
            return PayloadUnprotectionOutcome.Unreadable(metadataFailure.Value, resolvedMetadata);
        }

        ProtectedPayloadShape shape = InspectPayloadShape(payloadBytes);
        UnreadableProtectedDataReason? mismatch = ClassifyBytesMetadataMismatch(serializationFormat, resolvedMetadata, shape);
        if (mismatch is not null)
        {
            return PayloadUnprotectionOutcome.Unreadable(mismatch.Value, resolvedMetadata);
        }

        try
        {
            PayloadProtectionResult result = await ((IEventPayloadProtectionService)inner)
                .UnprotectEventPayloadAsync(
                    identity,
                    eventTypeName,
                    payloadBytes,
                    serializationFormat,
                    resolvedMetadata,
                    cancellationToken)
                .ConfigureAwait(false);
            return PayloadUnprotectionOutcome.FromResult(result with { Metadata = resolvedMetadata });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            UnreadableProtectedDataReason reason = await ClassifyFailureAsync(identity, ex, cancellationToken).ConfigureAwait(false);
            return PayloadUnprotectionOutcome.Unreadable(reason, resolvedMetadata);
        }
    }

    public async Task<SnapshotUnprotectionOutcome> TryUnprotectSnapshotAsync(
        AggregateIdentity identity,
        object state,
        EventStorePayloadProtectionMetadata? metadata,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(state);

        EventStorePayloadProtectionMetadata resolvedMetadata = metadata ?? EventStorePayloadProtectionMetadataCarrier.Legacy();
        UnreadableProtectedDataReason? metadataFailure = ValidateMetadata(resolvedMetadata);
        if (metadataFailure is not null)
        {
            return SnapshotUnprotectionOutcome.Unreadable(metadataFailure.Value, resolvedMetadata);
        }

        if (resolvedMetadata.State == PayloadProtectionState.Protected
            && state is not PartyPayloadProtectionService.ProtectedSnapshotState)
        {
            return SnapshotUnprotectionOutcome.Unreadable(UnreadableProtectedDataReason.ConsistencyMismatch, resolvedMetadata);
        }

        try
        {
            object unprotected = await ((IEventPayloadProtectionService)inner)
                .UnprotectSnapshotAsync(identity, state, resolvedMetadata, cancellationToken)
                .ConfigureAwait(false);
            return SnapshotUnprotectionOutcome.Readable(unprotected, resolvedMetadata);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            UnreadableProtectedDataReason reason = await ClassifyFailureAsync(identity, ex, cancellationToken).ConfigureAwait(false);
            return SnapshotUnprotectionOutcome.Unreadable(reason, resolvedMetadata);
        }
    }

    private static UnreadableProtectedDataReason? ValidateMetadata(EventStorePayloadProtectionMetadata metadata)
        => metadata switch
        {
            { State: PayloadProtectionState.ProviderOpaque } => UnreadableProtectedDataReasonMapper.FromProviderOpaqueMetadata(metadata),
            { MetadataVersion: > EventStorePayloadProtectionMetadata.CurrentMetadataVersion } => UnreadableProtectedDataReason.UnknownMetadataVersion,
            { MetadataVersion: < 1 } => UnreadableProtectedDataReason.MalformedMetadata,
            _ => null,
        };

    private static UnreadableProtectedDataReason? ClassifyBytesMetadataMismatch(
        string serializationFormat,
        EventStorePayloadProtectionMetadata metadata,
        ProtectedPayloadShape shape)
    {
        bool protectedFormat = IsProtectedFormat(serializationFormat);

        if (shape == ProtectedPayloadShape.Malformed)
        {
            return protectedFormat || metadata.State == PayloadProtectionState.Protected
                ? UnreadableProtectedDataReason.BytesMetadataMismatch
                : null;
        }

        if (metadata.State == PayloadProtectionState.Protected)
        {
            return protectedFormat && shape == ProtectedPayloadShape.ContainsEncryptedMarker
                ? null
                : UnreadableProtectedDataReason.BytesMetadataMismatch;
        }

        if (metadata.State == PayloadProtectionState.Unprotected)
        {
            if (protectedFormat)
            {
                return shape == ProtectedPayloadShape.ContainsEncryptedMarker
                    ? null
                    : UnreadableProtectedDataReason.BytesMetadataMismatch;
            }

            return shape == ProtectedPayloadShape.ContainsEncryptedMarker
                ? UnreadableProtectedDataReason.BytesMetadataMismatch
                : null;
        }

        return null;
    }

    private static ProtectedPayloadShape InspectPayloadShape(byte[] payloadBytes)
    {
        try
        {
            JsonNode? root = JsonNode.Parse(payloadBytes);
            return ContainsEncryptedMarker(root) switch
            {
                true => ValidateEncryptedMarkers(root)
                    ? ProtectedPayloadShape.ContainsEncryptedMarker
                    : ProtectedPayloadShape.Malformed,
                false => ProtectedPayloadShape.NoEncryptedMarker,
            };
        }
        catch (JsonException)
        {
            return ProtectedPayloadShape.Malformed;
        }
    }

    private static bool ContainsEncryptedMarker(JsonNode? node)
        => node switch
        {
            JsonObject obj => IsEncryptedMarker(obj)
                || obj.Any(property => ContainsEncryptedMarker(property.Value)),
            JsonArray array => array.Any(ContainsEncryptedMarker),
            _ => false,
        };

    private static bool ValidateEncryptedMarkers(JsonNode? node)
        => node switch
        {
            JsonObject obj when IsEncryptedMarker(obj) => IsValidEncryptedMarker(obj),
            JsonObject obj => obj.All(property => ValidateEncryptedMarkers(property.Value)),
            JsonArray array => array.All(ValidateEncryptedMarkers),
            _ => true,
        };

    private static bool IsEncryptedMarker(JsonObject obj)
        => obj.TryGetPropertyValue("$enc", out JsonNode? marker)
            && marker is JsonValue value
            && value.TryGetValue(out bool flag)
            && flag;

    private static bool IsValidEncryptedMarker(JsonObject obj)
        => IsExpectedString(obj, "alg", "AES256GCM")
            && obj.TryGetPropertyValue("kv", out JsonNode? version)
            && version is JsonValue versionValue
            && versionValue.TryGetValue(out int keyVersion)
            && keyVersion > 0
            && IsBase64String(obj, "n")
            && IsBase64String(obj, "t")
            && IsBase64String(obj, "c");

    private static bool IsExpectedString(JsonObject obj, string propertyName, string expected)
        => obj.TryGetPropertyValue(propertyName, out JsonNode? value)
            && value is JsonValue jsonValue
            && jsonValue.TryGetValue(out string? actual)
            && string.Equals(actual, expected, StringComparison.Ordinal);

    private static bool IsBase64String(JsonObject obj, string propertyName)
    {
        if (!obj.TryGetPropertyValue(propertyName, out JsonNode? node)
            || node is not JsonValue value
            || !value.TryGetValue(out string? encoded)
            || string.IsNullOrWhiteSpace(encoded))
        {
            return false;
        }

        // Validate the shape without decoding. The ciphertext field ("c") is the size of the
        // protected personal-data field and is data-controlled, so a stackalloc sized to its
        // base64 length would overflow the stack on a large field — and this runs on every
        // unprotection (the projection/replay read path). Base64.IsValid is allocation-free and
        // bounded.
        return Base64.IsValid(encoded.AsSpan());
    }

    private async Task<UnreadableProtectedDataReason> ClassifyFailureAsync(
        AggregateIdentity identity,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (PartyEncryptionKeyDestroyedException.IsMatch(exception))
        {
            return await IsKnownDestroyedAsync(identity, cancellationToken).ConfigureAwait(false)
                ? UnreadableProtectedDataReason.KeyInvalidatedOrDeleted
                : UnreadableProtectedDataReason.MissingKey;
        }

        return exception switch
        {
            CryptographicException => UnreadableProtectedDataReason.BytesMetadataMismatch,
            FormatException => UnreadableProtectedDataReason.BytesMetadataMismatch,
            JsonException => UnreadableProtectedDataReason.BytesMetadataMismatch,
            UnauthorizedAccessException => UnreadableProtectedDataReason.ProviderDenied,
            SecurityException => UnreadableProtectedDataReason.ProviderDenied,
            DecryptionCircuitOpenException => UnreadableProtectedDataReason.ProviderUnavailable,
            KeyNotFoundException => UnreadableProtectedDataReason.MissingKey,
            _ => UnreadableProtectedDataReason.ProviderUnavailable,
        };
    }

    private async Task<bool> IsKnownDestroyedAsync(AggregateIdentity identity, CancellationToken cancellationToken)
    {
        try
        {
            PartyErasureStatusRecord? status = await erasureRecordStore
                .GetStatusAsync(identity.TenantId, identity.AggregateId, cancellationToken)
                .ConfigureAwait(false);

            if (status is not null && IsDestroyedStatus(status.Status))
            {
                return true;
            }

            ErasureCertificate? certificate = await erasureRecordStore
                .GetCertificateAsync(identity.TenantId, identity.AggregateId, cancellationToken)
                .ConfigureAwait(false);

            return certificate?.VerificationStatus == ErasureVerificationStatus.Verified;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsDestroyedStatus(string status)
        => Enum.TryParse(status, ignoreCase: true, out ErasureStatus parsed)
            && parsed is ErasureStatus.KeyDestroyed or ErasureStatus.Verified or ErasureStatus.Erased;

    private static bool IsProtectedFormat(string serializationFormat)
        => string.Equals(
            serializationFormat,
            PartyPayloadProtectionService.ProtectedSerializationFormat,
            StringComparison.Ordinal);

    private enum ProtectedPayloadShape
    {
        NoEncryptedMarker,
        ContainsEncryptedMarker,
        Malformed,
    }
}
