using System.Collections;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.Security;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.Parties.Security;

public sealed partial class PartyPayloadProtectionService(
    IPartyKeyManagementService keyManagementService,
    IKeyStorageBackend keyStorageBackend,
    IPartyKeyLifecycleService cryptoLifecycleService,
    DecryptionCircuitBreaker circuitBreaker,
    IOptionsMonitor<CryptoShreddingOptions> cryptoOptions,
    ILogger<PartyPayloadProtectionService> logger) : IEventPayloadProtectionService
{
    internal const string ProtectedSerializationFormat = "json+pdenc-v1";
    internal const string RedactedSerializationFormat = "json-redacted";
    private const string ProtectedSnapshotMarker = "$protectedSnapshot";
    private const string EncryptedFieldMarker = "$enc";

    private static readonly JsonSerializerOptions s_jsonOptions = PartiesJsonOptions.Default;

    public async Task<PayloadProtectionResult> ProtectEventPayloadAsync(
        AggregateIdentity identity,
        IEventPayload eventPayload,
        string eventTypeName,
        byte[] payloadBytes,
        string serializationFormat,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(eventPayload);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventTypeName);
        ArgumentNullException.ThrowIfNull(payloadBytes);
        ArgumentException.ThrowIfNullOrWhiteSpace(serializationFormat);

        if (!ShouldHandle(identity))
        {
            return new PayloadProtectionResult(payloadBytes, serializationFormat);
        }

        // Read options snapshot once per call for consistent behavior within a single event
        CryptoShreddingOptions options = cryptoOptions.CurrentValue;

        if (!options.IsEnabled)
        {
            // When disabled: skip encryption on persist but key lifecycle still operates
            return new PayloadProtectionResult(payloadBytes, serializationFormat);
        }

        bool containsProtectedData = PersonalDataGraphInspector.ContainsProtectedData(eventPayload);
        bool ensureKeyExists = eventPayload is PartyCreated;
        if (!containsProtectedData && !ensureKeyExists)
        {
            return new PayloadProtectionResult(payloadBytes, serializationFormat);
        }

        (byte[] key, int version) = await GetCurrentKeyAsync(identity, ensureKeyExists, cancellationToken).ConfigureAwait(false);
        try
        {
            if (!containsProtectedData)
            {
                return new PayloadProtectionResult(payloadBytes, serializationFormat);
            }

            JsonNode? root = JsonNode.Parse(payloadBytes);
            if (root is null)
            {
                LogProtectionFailure(identity.TenantId, identity.AggregateId, "JSON payload deserialized to null — refusing to store personal data unencrypted.");
                throw new InvalidOperationException(
                    $"Cannot protect event payload for {identity.TenantId}/{identity.AggregateId}: JSON deserialization returned null. " +
                    "Refusing to persist personal data without encryption (fail-closed).");
            }

            bool changed = ProtectNode(eventPayload, root, identity, eventTypeName, key, version, pathPrefix: string.Empty);
            if (!changed)
            {
                return new PayloadProtectionResult(payloadBytes, serializationFormat);
            }

            byte[] protectedBytes = JsonSerializer.SerializeToUtf8Bytes(root, s_jsonOptions);
            LogProtectedPayload(identity.TenantId, identity.AggregateId, eventTypeName, version);
            return new PayloadProtectionResult(protectedBytes, ProtectedSerializationFormat);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    public async Task<PayloadProtectionResult> UnprotectEventPayloadAsync(
        AggregateIdentity identity,
        string eventTypeName,
        byte[] payloadBytes,
        string serializationFormat,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventTypeName);
        ArgumentNullException.ThrowIfNull(payloadBytes);
        ArgumentException.ThrowIfNullOrWhiteSpace(serializationFormat);

        if (!ShouldHandle(identity) || !string.Equals(serializationFormat, ProtectedSerializationFormat, StringComparison.Ordinal))
        {
            // Already-redacted payloads must not flow through the unprotect path: there is no
            // ciphertext to decrypt, and re-protection of a redacted payload would silently
            // re-encrypt nulled-out leaves. Treat as no-op so callers receive the redacted bytes
            // verbatim.
            return new PayloadProtectionResult(payloadBytes, serializationFormat);
        }

        circuitBreaker.ThrowIfOpen(identity.TenantId, identity.AggregateId);

        JsonNode? root = JsonNode.Parse(payloadBytes);
        if (root is null)
        {
            return new PayloadProtectionResult(payloadBytes, serializationFormat);
        }

        try
        {
            JsonNode? unprotected = await UnprotectNodeAsync(identity, eventTypeName, root, cancellationToken).ConfigureAwait(false);
            byte[] unprotectedBytes = JsonSerializer.SerializeToUtf8Bytes(unprotected, s_jsonOptions);
            LogUnprotectedPayload(identity.TenantId, identity.AggregateId, eventTypeName);
            circuitBreaker.RecordSuccess(identity.TenantId, identity.AggregateId);
            return new PayloadProtectionResult(unprotectedBytes, "json");
        }
        catch (Exception ex) when (ex is not DecryptionCircuitOpenException)
        {
            CryptoShreddingOptions cbOptions = cryptoOptions.CurrentValue;
            circuitBreaker.RecordFailure(
                identity.TenantId,
                identity.AggregateId,
                cbOptions.CircuitBreakerFailureThreshold,
                cbOptions.CircuitBreakerBreakDuration,
                cbOptions.CircuitBreakerMaxOpenDuration);
            throw;
        }
    }

    /// <summary>
    /// Returns a redacted payload where every encrypted marker (<c>{"$enc":true,...}</c>) is
    /// replaced with JSON <c>null</c>. Used as a fail-safe for state rehydration after a party's
    /// encryption key has been destroyed: subsequent commands (e.g. <c>MarkErasureVerified</c>,
    /// <c>CompletePartyErasure</c>) only inspect lifecycle/erasure flags from non-encrypted events,
    /// so redacting personal-data fields is sufficient to allow Apply-replay to continue.
    /// </summary>
    public static PayloadProtectionResult RedactProtectedPayload(byte[] payloadBytes, string serializationFormat)
    {
        ArgumentNullException.ThrowIfNull(payloadBytes);
        ArgumentException.ThrowIfNullOrWhiteSpace(serializationFormat);

        if (!string.Equals(serializationFormat, ProtectedSerializationFormat, StringComparison.Ordinal))
        {
            return new PayloadProtectionResult(payloadBytes, serializationFormat);
        }

        if (payloadBytes.Length == 0)
        {
            // ArgumentNullException.ThrowIfNull does not reject empty arrays; JsonNode.Parse
            // throws JsonException on a length-zero buffer, which would escape the redaction
            // path and abort projection delivery. Treat as a no-op.
            return new PayloadProtectionResult(payloadBytes, serializationFormat);
        }

        JsonNode? root = JsonNode.Parse(payloadBytes);
        if (root is null)
        {
            return new PayloadProtectionResult(payloadBytes, serializationFormat);
        }

        // Build a fresh JsonNode tree rather than mutate-in-place. JsonNode parents are
        // single-owner: assigning a recursively-rebuilt subtree back to its original parent
        // can throw "Cannot reassign" when a non-marker JsonObject contains a non-marker
        // JsonObject. Constructing a new tree avoids the risk entirely.
        JsonNode? redacted = RebuildWithoutEncryptedMarkers(root);

        // Whole-payload $enc at the root would collapse to JSON literal "null" bytes which
        // deserialize to a null event and silently drop the lifecycle event. Substitute an
        // empty object so the event deserializes to a default-valued instance and the
        // skip-and-log path can decide whether the redacted event is still useful.
        redacted ??= new JsonObject();

        byte[] redactedBytes = JsonSerializer.SerializeToUtf8Bytes(redacted, s_jsonOptions);
        // Use a distinct format marker so downstream consumers (audit/compliance pipelines)
        // can detect that this payload was redacted, rather than indistinguishable from a
        // plain JSON event that was never encrypted.
        return new PayloadProtectionResult(redactedBytes, RedactedSerializationFormat);
    }

    private static JsonNode? RebuildWithoutEncryptedMarkers(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj when IsEncryptedMarker(obj):
                return null;

            case JsonObject obj:
                // Walk the original object directly. JsonNode property values are single-owner,
                // but the recursion always produces a fresh node (or null), so writing into
                // rebuiltObj never re-parents an existing one. Avoid DeepClone() per recursion
                // step — that would be O(N²) in tree depth on nested encrypted structures.
                JsonObject rebuiltObj = new();
                foreach (KeyValuePair<string, JsonNode?> property in obj)
                {
                    rebuiltObj[property.Key] = RebuildWithoutEncryptedMarkers(property.Value);
                }

                return rebuiltObj;

            case JsonArray array:
                JsonArray rebuiltArr = new();
                foreach (JsonNode? item in array)
                {
                    rebuiltArr.Add(RebuildWithoutEncryptedMarkers(item));
                }

                return rebuiltArr;

            case JsonValue value:
                return value.DeepClone();

            default:
                return node?.DeepClone();
        }
    }

    public async Task<object> ProtectSnapshotStateAsync(
        AggregateIdentity identity,
        object state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(state);

        if (!ShouldHandle(identity) || !PersonalDataGraphInspector.ContainsProtectedData(state))
        {
            return state;
        }

        (byte[] key, int version) = await GetCurrentKeyAsync(identity, createIfMissing: false, cancellationToken).ConfigureAwait(false);
        try
        {
            JsonNode? root = JsonSerializer.SerializeToNode(state, state.GetType(), s_jsonOptions);
            if (root is null)
            {
                return state;
            }

            bool changed = ProtectNode(state, root, identity, state.GetType().FullName ?? state.GetType().Name, key, version, pathPrefix: string.Empty);
            if (!changed)
            {
                return state;
            }

            return new ProtectedSnapshotState
            {
                Marker = ProtectedSnapshotMarker,
                TypeName = state.GetType().FullName ?? state.GetType().Name,
                Payload = Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(root, s_jsonOptions)),
                SerializationFormat = ProtectedSerializationFormat,
            };
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    public async Task<object> UnprotectSnapshotStateAsync(
        AggregateIdentity identity,
        object state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(state);

        ProtectedSnapshotState? protectedState = state switch
        {
            ProtectedSnapshotState direct => direct,
            JsonElement element => element.Deserialize<ProtectedSnapshotState>(s_jsonOptions),
            _ => null,
        };

        if (protectedState is null
            || !string.Equals(protectedState.Marker, ProtectedSnapshotMarker, StringComparison.Ordinal)
            || !string.Equals(protectedState.SerializationFormat, ProtectedSerializationFormat, StringComparison.Ordinal))
        {
            return state;
        }

        Type? targetType = Type.GetType(protectedState.TypeName)
            ?? ResolveVersionTolerantType(protectedState.TypeName);
        if (targetType is null)
        {
            throw new InvalidOperationException($"Unable to resolve snapshot state type '{protectedState.TypeName}'.");
        }

        byte[] protectedBytes = Convert.FromBase64String(protectedState.Payload);
        JsonNode? protectedRoot = JsonNode.Parse(protectedBytes);
        if (protectedRoot is null)
        {
            throw new InvalidOperationException("Protected snapshot payload is empty.");
        }

        JsonNode? unprotected = await UnprotectNodeAsync(identity, targetType.FullName ?? targetType.Name, protectedRoot, cancellationToken).ConfigureAwait(false);
        byte[] unprotectedBytes = JsonSerializer.SerializeToUtf8Bytes(unprotected, s_jsonOptions);
        object? restored = JsonSerializer.Deserialize(unprotectedBytes, targetType, s_jsonOptions);
        return restored ?? state;
    }

    private static bool ShouldHandle(AggregateIdentity identity)
        => string.Equals(identity.Domain, "party", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Strips assembly version info from an assembly-qualified type name and retries resolution.
    /// Supports snapshots persisted with older NuGet versions.
    /// </summary>
    private static Type? ResolveVersionTolerantType(string typeName)
    {
        // Extract just the full type name (everything before the first comma, if present)
        int commaIndex = typeName.IndexOf(',', StringComparison.Ordinal);
        string fullNameOnly = commaIndex >= 0 ? typeName[..commaIndex].Trim() : typeName;

        Type? type = Type.GetType(fullNameOnly);
        if (type is not null)
        {
            return type;
        }

        // Try searching loaded assemblies by full name
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            type = assembly.GetType(fullNameOnly);
            if (type is not null)
            {
                return type;
            }
        }

        return null;
    }

    private async Task<(byte[] Key, int Version)> GetCurrentKeyAsync(AggregateIdentity identity, bool createIfMissing, CancellationToken cancellationToken)
    {
        try
        {
            IReadOnlyList<int> versions = await keyStorageBackend.ListKeyVersionsAsync(identity.TenantId, identity.AggregateId, cancellationToken).ConfigureAwait(false);

            if (versions.Count == 0)
            {
                if (createIfMissing)
                {
                    await cryptoLifecycleService.OnPartyCreatedAsync(identity.TenantId, identity.AggregateId, cancellationToken).ConfigureAwait(false);
                    versions = await keyStorageBackend.ListKeyVersionsAsync(identity.TenantId, identity.AggregateId, cancellationToken).ConfigureAwait(false);
                }
                else if (await cryptoLifecycleService.IsCryptoPendingAsync(identity.TenantId, identity.AggregateId, cancellationToken).ConfigureAwait(false))
                {
                    await cryptoLifecycleService.RetryPendingKeyCreationAsync(identity.TenantId, identity.AggregateId, cancellationToken).ConfigureAwait(false);
                    versions = await keyStorageBackend.ListKeyVersionsAsync(identity.TenantId, identity.AggregateId, cancellationToken).ConfigureAwait(false);
                }

                if (versions.Count == 0)
                {
                    // The lifecycle marker is best-effort: a state-store outage here must not
                    // mask the typed PartyEncryptionKeyDestroyedException, which catch sites use
                    // to drive the redaction-fallback path. Swallow + log the marker failure and
                    // proceed to throw the typed exception.
                    try
                    {
                        await cryptoLifecycleService
                            .MarkCryptoPendingAsync(identity.TenantId, identity.AggregateId, "No encryption key is available for this party.", cancellationToken)
                            .ConfigureAwait(false);
                    }
                    catch (Exception markerEx)
                    {
                        LogProtectionFailure(identity.TenantId, identity.AggregateId, $"Crypto-pending marker write failed: {markerEx.GetType().Name}");
                    }

                    throw new PartyEncryptionKeyDestroyedException(identity.TenantId, identity.AggregateId);
                }
            }

            int version = versions.Max();
            byte[] key = await keyManagementService.GetKeyVersionAsync(identity.TenantId, identity.AggregateId, version, cancellationToken).ConfigureAwait(false);
            await cryptoLifecycleService.ClearCryptoPendingAsync(identity.TenantId, identity.AggregateId, cancellationToken).ConfigureAwait(false);
            return (key, version);
        }
        catch (Exception ex)
        {
            await cryptoLifecycleService.MarkCryptoPendingAsync(identity.TenantId, identity.AggregateId, "Protected payload key operation failed.", cancellationToken).ConfigureAwait(false);
            LogProtectionFailure(identity.TenantId, identity.AggregateId, ex.GetType().Name);
            throw;
        }
    }

    private bool ProtectNode(object instance, JsonNode node, AggregateIdentity identity, string eventTypeName, byte[] key, int version, string pathPrefix)
    {
        Type type = instance.GetType();
        bool changed = false;

        if (node is JsonObject obj)
        {
            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!property.CanRead)
                {
                    continue;
                }

                (string jsonName, JsonNode? childNode) = GetPropertyNode(obj, property);
                object? value;
                try
                {
                    value = property.GetValue(instance);
                }
                catch (Exception ex)
                {
                    LogPropertyAccessFailure(identity.TenantId, identity.AggregateId, property.Name, ex.GetType().Name);
                    continue;
                }

                if (childNode is null || value is null)
                {
                    continue;
                }

                string path = string.IsNullOrEmpty(pathPrefix) ? property.Name : $"{pathPrefix}.{property.Name}";
                if (PersonalDataGraphInspector.ShouldProtectProperty(instance, property, value))
                {
                    obj[jsonName] = EncryptNode(childNode, identity, eventTypeName, path, key, version);
                    changed = true;
                    continue;
                }

                changed |= ProtectNestedValue(value, childNode, identity, eventTypeName, key, version, path);
            }
        }

        return changed;
    }

    private bool ProtectNestedValue(object value, JsonNode childNode, AggregateIdentity identity, string eventTypeName, byte[] key, int version, string path)
    {
        if (PersonalDataGraphInspector.IsScalarType(value.GetType()))
        {
            return false;
        }

        if (value is IEnumerable enumerable && value is not string && childNode is JsonArray array)
        {
            bool changed = false;
            int index = 0;
            foreach (object? item in enumerable)
            {
                if (item is not null && index < array.Count && array[index] is JsonNode itemNode)
                {
                    changed |= ProtectNode(item, itemNode, identity, eventTypeName, key, version, $"{path}[{index}]");
                }

                index++;
            }

            return changed;
        }

        return ProtectNode(value, childNode, identity, eventTypeName, key, version, path);
    }

    private static JsonObject EncryptNode(JsonNode node, AggregateIdentity identity, string eventTypeName, string path, byte[] key, int version)
    {
        byte[] plaintext = Encoding.UTF8.GetBytes(node.ToJsonString(s_jsonOptions));
        byte[] nonce = RandomNumberGenerator.GetBytes(12);
        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[16];
        using var aes = new AesGcm(key, 16);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        CryptographicOperations.ZeroMemory(plaintext);

        return new JsonObject
        {
            [EncryptedFieldMarker] = true,
            ["alg"] = "AES256GCM",
            ["kv"] = version,
            ["n"] = Convert.ToBase64String(nonce),
            ["t"] = Convert.ToBase64String(tag),
            ["c"] = Convert.ToBase64String(ciphertext),
        };
    }

    private async Task<JsonNode?> UnprotectNodeAsync(AggregateIdentity identity, string eventTypeName, JsonNode node, CancellationToken cancellationToken)
    {
        switch (node)
        {
            case JsonObject obj when IsEncryptedMarker(obj):
                return await DecryptNodeAsync(identity, eventTypeName, obj, cancellationToken).ConfigureAwait(false);

            case JsonObject obj:
                if (IsSuspiciousEncryptedObject(obj))
                {
                    LogSuspiciousEncryptedField(identity.TenantId, identity.AggregateId, eventTypeName);
                }

                foreach (KeyValuePair<string, JsonNode?> property in obj.ToList())
                {
                    if (property.Value is not null)
                    {
                        obj[property.Key] = await UnprotectNodeAsync(identity, eventTypeName, property.Value, cancellationToken).ConfigureAwait(false);
                    }
                }

                return obj;

            case JsonArray array:
                for (int i = 0; i < array.Count; i++)
                {
                    if (array[i] is not null)
                    {
                        array[i] = await UnprotectNodeAsync(identity, eventTypeName, array[i]!, cancellationToken).ConfigureAwait(false);
                    }
                }

                return array;

            default:
                return node;
        }
    }

    private async Task<JsonNode?> DecryptNodeAsync(AggregateIdentity identity, string eventTypeName, JsonObject marker, CancellationToken cancellationToken)
    {
        int version = marker["kv"]?.GetValue<int>()
            ?? throw new InvalidOperationException("Encrypted field metadata is missing key version.");
        byte[] key = await keyManagementService.GetKeyVersionAsync(identity.TenantId, identity.AggregateId, version, cancellationToken).ConfigureAwait(false);
        try
        {
            byte[] nonce;
            byte[] tag;
            byte[] ciphertext;
            try
            {
                nonce = Convert.FromBase64String(marker["n"]?.GetValue<string>() ?? throw new InvalidOperationException("Encrypted field metadata is missing nonce."));
                tag = Convert.FromBase64String(marker["t"]?.GetValue<string>() ?? throw new InvalidOperationException("Encrypted field metadata is missing tag."));
                ciphertext = Convert.FromBase64String(marker["c"]?.GetValue<string>() ?? throw new InvalidOperationException("Encrypted field metadata is missing ciphertext."));
            }
            catch (FormatException ex)
            {
                LogCorruptedEncryptedField(identity.TenantId, identity.AggregateId, eventTypeName, ex.GetType().Name);
                throw new InvalidOperationException(
                    "Corrupted encrypted field in protected event payload: invalid base64 encoding.",
                    ex);
            }

            byte[] plaintext = new byte[ciphertext.Length];
            using var aes = new AesGcm(key, 16);
            aes.Decrypt(nonce, ciphertext, tag, plaintext);

            try
            {
                return JsonNode.Parse(plaintext);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plaintext);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    private static bool IsEncryptedMarker(JsonObject obj)
    {
        // Tolerant marker check: corrupted or adversarial payloads may store $enc as a string
        // ("true") or number (1) instead of a JSON bool. JsonValue.GetValue<bool> throws
        // InvalidOperationException on a type mismatch, which would escape the redaction path
        // and abort projection delivery on a single bad event.
        if (!obj.TryGetPropertyValue(EncryptedFieldMarker, out JsonNode? marker) || marker is null)
        {
            return false;
        }

        return marker is JsonValue value && value.TryGetValue(out bool flag) && flag;
    }

    private static bool IsSuspiciousEncryptedObject(JsonObject obj)
        => !obj.ContainsKey(EncryptedFieldMarker)
            && obj.ContainsKey("alg")
            && obj.ContainsKey("kv");

    private static (string PropertyName, JsonNode? Node) GetPropertyNode(JsonObject obj, PropertyInfo property)
    {
        ArgumentNullException.ThrowIfNull(obj);
        ArgumentNullException.ThrowIfNull(property);

        if (obj.TryGetPropertyValue(property.Name, out JsonNode? pascalNode))
        {
            return (property.Name, pascalNode);
        }

        string camelName = PersonalDataGraphInspector.GetJsonPropertyName(property);
        return (camelName, obj[camelName]);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Protected party payload for {TenantId}/{PartyId} event {EventTypeName} using key version {Version}")]
    private partial void LogProtectedPayload(string tenantId, string partyId, string eventTypeName, int version);

    [LoggerMessage(Level = LogLevel.Information, Message = "Unprotected party payload for {TenantId}/{PartyId} event {EventTypeName}")]
    private partial void LogUnprotectedPayload(string tenantId, string partyId, string eventTypeName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Party payload protection failed for {TenantId}/{PartyId}: {Error}")]
    private partial void LogProtectionFailure(string tenantId, string partyId, string error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "PropertyInfo.GetValue failed for {TenantId}/{PartyId} property '{PropertyName}': {Error}")]
    private partial void LogPropertyAccessFailure(string tenantId, string partyId, string propertyName, string error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Corrupted encrypted field for {TenantId}/{PartyId} in event {EventTypeName}: {Error}")]
    private partial void LogCorruptedEncryptedField(string tenantId, string partyId, string eventTypeName, string error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Suspicious encrypted field for {TenantId}/{PartyId} in event {EventTypeName}: JSON object has 'alg'/'kv' fields but missing '$enc' marker (possible corruption)")]
    private partial void LogSuspiciousEncryptedField(string tenantId, string partyId, string eventTypeName);

    internal sealed record ProtectedSnapshotState
    {
        public required string Marker { get; init; }

        public required string TypeName { get; init; }

        public required string Payload { get; init; }

        public required string SerializationFormat { get; init; }
    }
}
