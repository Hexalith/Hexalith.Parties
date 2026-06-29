using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Security;

using Microsoft.Extensions.Options;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace Hexalith.Parties.Security.Tests;

public sealed class CryptoKeyManagementCompatibilityHarnessTests
{
    private static readonly JsonSerializerOptions s_jsonOptions = PartiesJsonOptions.Default;

    [Fact]
    public async Task ProtectedPartyPayload_RoundTripsThroughRealServiceAndEventStoreContractAsync()
    {
        CryptoKeyManagementHarnessServices harness = CreateHarness();
        AggregateIdentity identity = PartyIdentity();
        await EnsurePartyKeyAsync(harness, identity).ConfigureAwait(true);
        ContactChannelAdded payload = ContactPayload("readable@example.test");
        byte[] serialized = JsonSerializer.SerializeToUtf8Bytes(payload, s_jsonOptions);

        PayloadProtectionResult protectedResult = await harness.ProtectionService.ProtectEventPayloadAsync(
            identity,
            payload,
            typeof(ContactChannelAdded).FullName!,
            serialized,
            "json").ConfigureAwait(true);

        protectedResult.SerializationFormat.ShouldBe("json+pdenc-v1");
        protectedResult.Metadata.State.ShouldBe(PayloadProtectionState.Unprotected);
        protectedResult.Metadata.MetadataVersion.ShouldBe(EventStorePayloadProtectionMetadata.CurrentMetadataVersion);
        Encoding.UTF8.GetString(protectedResult.PayloadBytes).ShouldNotContain("readable@example.test");

        PayloadUnprotectionOutcome outcome = await ((IEventPayloadProtectionService)harness.ProtectionService)
            .TryUnprotectEventPayloadAsync(
                identity,
                typeof(ContactChannelAdded).FullName!,
                protectedResult.PayloadBytes,
                protectedResult.SerializationFormat,
                protectedResult.Metadata)
            .ConfigureAwait(true);

        outcome.IsReadable.ShouldBeTrue();
        outcome.SerializationFormat.ShouldBe("json");
        ContactChannelAdded? roundTrip = JsonSerializer.Deserialize<ContactChannelAdded>(outcome.PayloadBytes!, s_jsonOptions);
        roundTrip.ShouldNotBeNull();
        roundTrip.Value.ShouldBe("readable@example.test");
    }

    [Fact]
    public async Task LegacyUnprotectedPayload_PassesThroughWithUnprotectedMetadataAsync()
    {
        CryptoKeyManagementHarnessServices harness = CreateHarness();
        AggregateIdentity identity = PartyIdentity();
        PartyDeactivated payload = new();
        byte[] serialized = JsonSerializer.SerializeToUtf8Bytes(payload, s_jsonOptions);

        PayloadProtectionResult protectedResult = await harness.ProtectionService.ProtectEventPayloadAsync(
            identity,
            payload,
            typeof(PartyDeactivated).FullName!,
            serialized,
            "json").ConfigureAwait(true);

        protectedResult.PayloadBytes.ShouldBe(serialized);
        protectedResult.SerializationFormat.ShouldBe("json");
        protectedResult.Metadata.State.ShouldBe(PayloadProtectionState.Unprotected);

        PayloadUnprotectionOutcome outcome = await ((IEventPayloadProtectionService)harness.ProtectionService)
            .TryUnprotectEventPayloadAsync(
                identity,
                typeof(PartyDeactivated).FullName!,
                protectedResult.PayloadBytes,
                protectedResult.SerializationFormat,
                protectedResult.Metadata)
            .ConfigureAwait(true);

        outcome.IsReadable.ShouldBeTrue();
        outcome.PayloadBytes.ShouldBe(serialized);
        outcome.Metadata.State.ShouldBe(PayloadProtectionState.Unprotected);
    }

    [Fact]
    public async Task RestrictedParty_RemainsReadableBecauseRestrictionIsPolicyNotCryptoDestructionAsync()
    {
        CryptoKeyManagementHarnessServices harness = CreateHarness();
        AggregateIdentity identity = PartyIdentity();
        await EnsurePartyKeyAsync(harness, identity).ConfigureAwait(true);
        ContactChannelAdded payload = ContactPayload("restricted-readable@example.test");
        byte[] serialized = JsonSerializer.SerializeToUtf8Bytes(payload, s_jsonOptions);

        PayloadProtectionResult protectedResult = await harness.ProtectionService.ProtectEventPayloadAsync(
            identity,
            payload,
            typeof(ContactChannelAdded).FullName!,
            serialized,
            "json").ConfigureAwait(true);

        ProcessingRestricted restriction = new()
        {
            PartyId = identity.AggregateId,
            TenantId = identity.TenantId,
            RestrictedAt = DateTimeOffset.UtcNow,
            Reason = "Accuracy contested",
            RestrictedBy = "dpo",
            CorrelationId = "corr-restriction",
        };
        byte[] restrictionBytes = JsonSerializer.SerializeToUtf8Bytes(restriction, s_jsonOptions);

        PayloadProtectionResult restrictionResult = await harness.ProtectionService.ProtectEventPayloadAsync(
            identity,
            restriction,
            typeof(ProcessingRestricted).FullName!,
            restrictionBytes,
            "json").ConfigureAwait(true);

        restrictionResult.SerializationFormat.ShouldBe("json");

        PayloadProtectionResult unprotectedResult = await harness.ProtectionService.UnprotectEventPayloadAsync(
            identity,
            typeof(ContactChannelAdded).FullName!,
            protectedResult.PayloadBytes,
            protectedResult.SerializationFormat).ConfigureAwait(true);

        ContactChannelAdded? roundTrip = JsonSerializer.Deserialize<ContactChannelAdded>(unprotectedResult.PayloadBytes, s_jsonOptions);
        roundTrip.ShouldNotBeNull();
        roundTrip.Value.ShouldBe("restricted-readable@example.test");
    }

    [Fact]
    public async Task DestroyedKeyPayload_BecomesBoundedUnreadableAndRedactsWithoutRestoringPersonalFieldsAsync()
    {
        CryptoKeyManagementHarnessServices harness = CreateHarness();
        AggregateIdentity identity = PartyIdentity();
        await EnsurePartyKeyAsync(harness, identity).ConfigureAwait(true);
        ContactChannelAdded payload = ContactPayload("erase-me@example.test");
        byte[] serialized = JsonSerializer.SerializeToUtf8Bytes(payload, s_jsonOptions);

        PayloadProtectionResult protectedResult = await harness.ProtectionService.ProtectEventPayloadAsync(
            identity,
            payload,
            typeof(ContactChannelAdded).FullName!,
            serialized,
            "json").ConfigureAwait(true);

        ErasureCertificate certificate = await harness.KeyManagementService.DeleteKeyAsync(
            identity.TenantId,
            identity.AggregateId).ConfigureAwait(true);

        PayloadUnprotectionOutcome outcome = await ((IEventPayloadProtectionService)harness.ProtectionService)
            .TryUnprotectEventPayloadAsync(
                identity,
                typeof(ContactChannelAdded).FullName!,
                protectedResult.PayloadBytes,
                protectedResult.SerializationFormat,
                protectedResult.Metadata)
            .ConfigureAwait(true);

        outcome.IsUnreadable.ShouldBeTrue();
        outcome.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.ProviderUnavailable);
        certificate.VerificationStatus.ShouldBe(ErasureVerificationStatus.Verified);

        PayloadProtectionResult redacted = PartyPayloadProtectionService.RedactProtectedPayload(
            protectedResult.PayloadBytes,
            protectedResult.SerializationFormat);

        redacted.SerializationFormat.ShouldBe("json-redacted");
        string redactedJson = Encoding.UTF8.GetString(redacted.PayloadBytes);
        redactedJson.ShouldNotContain("erase-me@example.test");
        redactedJson.ShouldNotContain("\"$enc\"");
        using JsonDocument doc = JsonDocument.Parse(redacted.PayloadBytes);
        doc.RootElement.GetProperty("value").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task MissingKeyPayload_BecomesBoundedUnreadableWithoutLeakingKeyDetailsAsync()
    {
        CryptoKeyManagementHarnessServices protectingHarness = CreateHarness();
        AggregateIdentity identity = PartyIdentity();
        await EnsurePartyKeyAsync(protectingHarness, identity).ConfigureAwait(true);
        ContactChannelAdded payload = ContactPayload("missing-key@example.test");
        byte[] serialized = JsonSerializer.SerializeToUtf8Bytes(payload, s_jsonOptions);

        PayloadProtectionResult protectedResult = await protectingHarness.ProtectionService.ProtectEventPayloadAsync(
            identity,
            payload,
            typeof(ContactChannelAdded).FullName!,
            serialized,
            "json").ConfigureAwait(true);

        CryptoKeyManagementHarnessServices missingKeyHarness = CreateHarness();
        PayloadUnprotectionOutcome outcome = await ((IEventPayloadProtectionService)missingKeyHarness.ProtectionService)
            .TryUnprotectEventPayloadAsync(
                identity,
                typeof(ContactChannelAdded).FullName!,
                protectedResult.PayloadBytes,
                protectedResult.SerializationFormat,
                protectedResult.Metadata)
            .ConfigureAwait(true);

        outcome.IsUnreadable.ShouldBeTrue();
        outcome.PayloadBytes.ShouldBeNull();
        outcome.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.ProviderUnavailable);
        ProtectedDataLeakSentinel.AssertNoLeak(protectingHarness.CapturedMessages);
        ProtectedDataLeakSentinel.AssertNoLeak(missingKeyHarness.CapturedMessages);
    }

    [Fact]
    public async Task TamperedProtectedPayload_UsesBoundedUnreadableOutcomeWithoutProviderTextPolicyAsync()
    {
        CryptoKeyManagementHarnessServices harness = CreateHarness();
        AggregateIdentity identity = PartyIdentity();
        await EnsurePartyKeyAsync(harness, identity).ConfigureAwait(true);
        ContactChannelAdded payload = ContactPayload("tamper-me@example.test");
        byte[] serialized = JsonSerializer.SerializeToUtf8Bytes(payload, s_jsonOptions);

        PayloadProtectionResult protectedResult = await harness.ProtectionService.ProtectEventPayloadAsync(
            identity,
            payload,
            typeof(ContactChannelAdded).FullName!,
            serialized,
            "json").ConfigureAwait(true);

        JsonNode root = JsonNode.Parse(protectedResult.PayloadBytes)!;
        string ciphertext = root["value"]!["c"]!.GetValue<string>();
        byte[] ciphertextBytes = Convert.FromBase64String(ciphertext);
        ciphertextBytes[0] ^= 0xFF;
        root["value"]!["c"] = Convert.ToBase64String(ciphertextBytes);
        byte[] tamperedBytes = JsonSerializer.SerializeToUtf8Bytes(root, s_jsonOptions);

        PayloadUnprotectionOutcome outcome = await ((IEventPayloadProtectionService)harness.ProtectionService)
            .TryUnprotectEventPayloadAsync(
                identity,
                typeof(ContactChannelAdded).FullName!,
                tamperedBytes,
                protectedResult.SerializationFormat,
                protectedResult.Metadata)
            .ConfigureAwait(true);

        outcome.IsUnreadable.ShouldBeTrue();
        outcome.PayloadBytes.ShouldBeNull();
        outcome.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.ProviderUnavailable);
    }

    [Fact]
    public async Task ProviderUnavailable_IsBoundedAndDoesNotLeakProviderExceptionTextAsync()
    {
        IPartyKeyManagementService failingKeyManagement = Substitute.For<IPartyKeyManagementService>();
        failingKeyManagement.GetKeyVersionAsync("tenant-harness", "party-harness", 1, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException(ProtectedDataLeakSentinel.ProtectedProviderExceptionText));
        CryptoKeyManagementHarnessServices harness = CreateHarness(keyManagementService: failingKeyManagement);
        AggregateIdentity identity = PartyIdentity();
        byte[] protectedBytes = Encoding.UTF8.GetBytes(
            """
            {"value":{"$enc":true,"alg":"AES256GCM","kv":1,"n":"AAAAAAAAAAAAAAAA","t":"AAAAAAAAAAAAAAAAAAAAAA==","c":"AAAA"}}
            """);

        PayloadUnprotectionOutcome outcome = await ((IEventPayloadProtectionService)harness.ProtectionService)
            .TryUnprotectEventPayloadAsync(
                identity,
                typeof(ContactChannelAdded).FullName!,
                protectedBytes,
                "json+pdenc-v1",
                EventStorePayloadProtectionMetadata.ProviderOpaque(ProtectedDataLeakSentinel.ProtectedProviderPrivateBlob))
            .ConfigureAwait(true);

        outcome.IsUnreadable.ShouldBeTrue();
        outcome.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.ProviderUnavailable);
        ProtectedDataLeakSentinel.AssertNoLeak(harness.CapturedMessages);
    }

    [Fact]
    public async Task EvidenceArtifacts_DoNotEchoProtectedDataSentinelsAsync()
    {
        CryptoKeyManagementHarnessServices harness = CreateHarness();
        AggregateIdentity identity = PartyIdentity();
        await EnsurePartyKeyAsync(harness, identity).ConfigureAwait(true);
        ContactChannelAdded payload = ContactPayload(ProtectedDataLeakSentinel.ProtectedPayloadPlaintext);
        byte[] serialized = JsonSerializer.SerializeToUtf8Bytes(payload, s_jsonOptions);

        PayloadProtectionResult protectedResult = await harness.ProtectionService.ProtectEventPayloadAsync(
            identity,
            payload,
            typeof(ContactChannelAdded).FullName!,
            serialized,
            "json").ConfigureAwait(true);

        await harness.ProtectionService.UnprotectEventPayloadAsync(
            identity,
            typeof(ContactChannelAdded).FullName!,
            protectedResult.PayloadBytes,
            protectedResult.SerializationFormat).ConfigureAwait(true);

        await harness.LifecycleService.OnPartyCreatedAsync(identity.TenantId, identity.AggregateId).ConfigureAwait(true);
        ErasureCertificate certificate = await harness.KeyManagementService.DeleteKeyAsync(
            identity.TenantId,
            identity.AggregateId).ConfigureAwait(true);

        ErasureVerificationService verification = new(
            [
                (_, _, _) => Task.FromResult(new ErasureVerificationStoreResult
                {
                    StoreName = "detail-projection",
                    Status = ErasureStoreCleanupStatus.Pending,
                    Timestamp = DateTimeOffset.UtcNow,
                    ErrorMessage = ProtectedDataLeakSentinel.ProtectedProviderExceptionText,
                }),
            ],
            harness.VerificationLogger);
        ErasureVerificationReport report = await verification.VerifyErasureAsync(
            identity.TenantId,
            identity.AggregateId,
            certificate).ConfigureAwait(true);

        harness.CircuitBreaker.RecordFailure(identity.TenantId, identity.AggregateId, 1, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5));
        Should.Throw<DecryptionCircuitOpenException>(() => harness.CircuitBreaker.ThrowIfOpen(identity.TenantId, identity.AggregateId));

        EventStorePayloadProtectionMetadata sensitiveMetadata = new(
            PayloadProtectionState.Protected,
            EventStorePayloadProtectionMetadata.CurrentMetadataVersion,
            "aes-gcm-256",
            ProtectedDataLeakSentinel.ProtectedKeyAlias,
            "application/json",
            new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["provider-private"] = ProtectedDataLeakSentinel.ProtectedProviderPrivateBlob,
                ["state-store-key"] = ProtectedDataLeakSentinel.ProtectedStateStoreKey,
                ["connection"] = ProtectedDataLeakSentinel.ProtectedConnectionString,
            }));

        PartyDataPortabilityPackage export = new()
        {
            PartyId = identity.AggregateId,
            TenantId = identity.TenantId,
            Status = "PersonalDataUnavailable",
            ExportedAt = DateTimeOffset.UtcNow,
            ExportedBy = "system",
            CorrelationId = "corr-safe",
            Party = null,
            ProcessingRecords =
            [
                new ProcessingActivityRecord
                {
                    SequenceNumber = 1,
                    PartyId = identity.AggregateId,
                    TenantId = identity.TenantId,
                    ActorId = "redacted",
                    CorrelationId = "corr-safe",
                    EventType = "ProtectedPayloadUnreadable",
                    OperationCategory = "Export",
                    Outcome = "PersonalDataUnavailable",
                    Timestamp = DateTimeOffset.UtcNow,
                    Summary = "Personal data is unavailable.",
                },
            ],
        };

        string safeEvidence = JsonSerializer.Serialize(
            new
            {
                ProtectedFormat = protectedResult.SerializationFormat,
                protectedResult.Metadata.State,
                UnreadableReason = UnreadableProtectedDataReasonCodes.ProviderUnavailable,
                certificate.VerificationStatus,
                report.OverallStatus,
                SanitizedReport = report,
                Export = export,
                MetadataState = sensitiveMetadata.State,
                MetadataVersion = sensitiveMetadata.MetadataVersion,
            },
            s_jsonOptions);

        ProtectedDataLeakSentinel.AssertNoLeak(harness.CapturedMessages);
        ProtectedDataLeakSentinel.AssertNoLeak([safeEvidence]);
    }

    private static CryptoKeyManagementHarnessServices CreateHarness(IPartyKeyManagementService? keyManagementService = null)
    {
        LocalDevKeyStorageBackend backend = new();
        IKeyOperationAuditService auditService = Substitute.For<IKeyOperationAuditService>();
        CorrelationContextAccessor correlation = new();
        IPartyKeyManagementService keys = keyManagementService ?? new PartyKeyManagementService(backend, auditService, correlation);
        IPartyKeyRetryScheduler retryScheduler = Substitute.For<IPartyKeyRetryScheduler>();
        CapturingLogger<PartyKeyLifecycleService> lifecycleLogger = new();
        PartyKeyLifecycleService lifecycle = new(keys, retryScheduler, lifecycleLogger);
        CapturingLogger<DecryptionCircuitBreaker> circuitLogger = new();
        DecryptionCircuitBreaker circuitBreaker = new(circuitLogger);
        IOptionsMonitor<CryptoShreddingOptions> options = Substitute.For<IOptionsMonitor<CryptoShreddingOptions>>();
        options.CurrentValue.Returns(new CryptoShreddingOptions());
        CapturingLogger<PartyPayloadProtectionService> protectionLogger = new();
        PartyPayloadProtectionService protection = new(
            keys,
            backend,
            lifecycle,
            circuitBreaker,
            options,
            protectionLogger);
        CapturingLogger<ErasureVerificationService> verificationLogger = new();

        return new CryptoKeyManagementHarnessServices(
            backend,
            keys,
            lifecycle,
            circuitBreaker,
            protection,
            lifecycleLogger,
            circuitLogger,
            protectionLogger,
            verificationLogger);
    }

    private static Task EnsurePartyKeyAsync(CryptoKeyManagementHarnessServices harness, AggregateIdentity identity) =>
        harness.KeyManagementService.CreateKeyAsync(identity.TenantId, identity.AggregateId);

    private static ContactChannelAdded ContactPayload(string value) =>
        new()
        {
            ContactChannelId = "contact-harness",
            Type = ContactChannelType.Email,
            Value = value,
            IsPreferred = true,
        };

    private static AggregateIdentity PartyIdentity() => new("tenant-harness", "party", "party-harness");

}
