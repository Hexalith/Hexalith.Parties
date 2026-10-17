using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.State;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace Hexalith.Parties.Security.Tests;

public sealed class IdentityHistoryProtectionTests
{
    [Fact]
    public async Task SerializedHistorySnapshot_IsDeniedInsteadOfProfileOnlyProtection()
    {
        PartyState state = State();
        var custody = Substitute.For<IIdentityHistoryCustody>();
        var service = Service(Substitute.For<IPartyKeyManagementService>(), Substitute.For<IKeyStorageBackend>(), custody);
        JsonElement serialized = JsonSerializer.SerializeToElement(state, PartiesJsonOptions.Default);
        await Should.ThrowAsync<InvalidOperationException>(() => service.ProtectSnapshotStateAsync(new("tenant-a", "party", "party-1"), serialized, TestContext.Current.CancellationToken));
        await custody.DidNotReceiveWithAnyArgs().ProtectSnapshotAsync(default!, default!, default);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task TypedSnapshot_RetainedHistoryRequiresCustodyAndSurvivesProfileKeyErasure(bool persist, bool erased)
    {
        IPartyKeyManagementService keys = Substitute.For<IPartyKeyManagementService>();
        IKeyStorageBackend backend = Substitute.For<IKeyStorageBackend>();
        backend.ListKeyVersionsAsync("tenant-a", "party-1", Arg.Any<CancellationToken>()).Returns([1]);
        keys.GetKeyVersionAsync("tenant-a", "party-1", 1, Arg.Any<CancellationToken>()).Returns(_ => Enumerable.Repeat((byte)7, 32).ToArray());
        IIdentityHistoryCustody custody = Substitute.For<IIdentityHistoryCustody>();
        // Synthetic encryption verifies persistence and profile separation, not live custody qualification.
        byte[] historyKey = RandomNumberGenerator.GetBytes(32);
        custody.ProtectSnapshotAsync(Arg.Any<AggregateIdentity>(), Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(call => new SnapshotProtectionResult(EncryptSnapshot(call.Arg<object>(), historyKey), HistoryMetadata()));
        custody.UnprotectSnapshotAsync(Arg.Any<AggregateIdentity>(), Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(call => DecryptSnapshot(call.Arg<byte[]>(), historyKey));
        var service = Service(keys, backend, custody);
        var adapter = new EventStorePartyPayloadProtectionAdapter(service, Substitute.For<IPartyErasureRecordStore>());
        SnapshotProtectionResult result = await adapter.ProtectSnapshotAsync(new("tenant-a", "party", "party-1"), State(), TestContext.Current.CancellationToken);
        result.Metadata.State.ShouldBe(PayloadProtectionState.Protected);
        result.Metadata.Scheme.ShouldBe("party-actor-history-v1");
        result.State.ShouldBeOfType<IdentityHistorySnapshot>();
        JsonElement persisted = JsonSerializer.SerializeToElement(result.State, JsonSerializerOptions.Web);
        byte[] persistedPayload = persisted.GetProperty("payload").GetBytesFromBase64();
        string readablePayload = Encoding.UTF8.GetString(persistedPayload);
        readablePayload.ShouldNotContain("humanActorBindings");
        readablePayload.ShouldNotContain("humanActorTransitions");
        readablePayload.ShouldNotContain("01HX0000000000000000000001");
        readablePayload.ShouldNotContain("operator-evidence-private");
        readablePayload.ShouldNotContain("history-intent-digest-private");
        persisted.GetRawText().ShouldNotContain("01HX0000000000000000000001");
        if (erased)
        {
            keys.GetKeyVersionAsync("tenant-a", "party-1", 1, Arg.Any<CancellationToken>()).Returns<byte[]>(_ => throw new PartyEncryptionKeyDestroyedException("tenant-a", "party-1"));
        }

        SnapshotUnprotectionOutcome restored = await adapter.TryUnprotectSnapshotAsync(new("tenant-a", "party", "party-1"),
            persist ? persisted : result.State, result.Metadata, TestContext.Current.CancellationToken);
        restored.IsReadable.ShouldBeTrue();
        JsonElement json = restored.State.ShouldBeOfType<JsonElement>();
        json.GetProperty("humanActorBindings").GetArrayLength().ShouldBe(1);
        json.GetProperty("humanBindingVersion").GetInt64().ShouldBe(1);
        json.GetProperty("humanActorBindings")[0].GetProperty("evidence").GetProperty("bindingVersion").GetInt64().ShouldBe(1);
        json.GetProperty("humanActorBindings")[0].GetProperty("evidence").GetProperty("actorId").GetString().ShouldBe("01HX0000000000000000000001");
        json.GetProperty("humanActorBindings")[0].GetProperty("evidence").GetProperty("provenanceId").GetString().ShouldBe("operator-evidence-private");
        json.GetProperty("humanActorTransitions")[0].GetProperty("intentDigest").GetString().ShouldBe("history-intent-digest-private");
        json.GetProperty("person").GetProperty("firstName").GetString().ShouldBe(erased ? null : "Private");
        json.GetProperty("person").GetProperty("lastName").GetString().ShouldBe(erased ? null : "Name");
        await custody.Received(1).UnprotectSnapshotAsync(Arg.Any<AggregateIdentity>(), Arg.Any<object>(), Arg.Any<CancellationToken>());
        CryptographicOperations.ZeroMemory(historyKey);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("same")]
    [InlineData("clone")]
    [InlineData("cosmetic-wrapper")]
    [InlineData("unprotected")]
    [InlineData("legacy")]
    [InlineData("opaque-metadata")]
    [InlineData("wrong-purpose")]
    [InlineData("key-alias")]
    [InlineData("compatibility-flags")]
    [InlineData("old-version")]
    [InlineData("future-version")]
    [InlineData("missing-metadata")]
    [InlineData("null-state")]
    [InlineData("empty-bytes")]
    [InlineData("string-state")]
    [InlineData("same-with-metadata")]
    [InlineData("clone-with-metadata")]
    [InlineData("json-bytes")]
    [InlineData("cosmetic-json-bytes")]
    [InlineData("prefixed-json-bytes")]
    [InlineData("base64-json-bytes")]
    [InlineData("deep-json-bytes")]
    public async Task SnapshotCustody_IneffectiveResultsCannotReportProtectedMetadata(string kind)
    {
        IPartyKeyManagementService keys = Substitute.For<IPartyKeyManagementService>();
        IKeyStorageBackend backend = Substitute.For<IKeyStorageBackend>();
        backend.ListKeyVersionsAsync("tenant-a", "party-1", Arg.Any<CancellationToken>()).Returns([1]);
        keys.GetKeyVersionAsync("tenant-a", "party-1", 1, Arg.Any<CancellationToken>()).Returns(_ => Enumerable.Repeat((byte)7, 32).ToArray());
        IIdentityHistoryCustody custody = Substitute.For<IIdentityHistoryCustody>();
        custody.ProtectSnapshotAsync(Arg.Any<AggregateIdentity>(), Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(call => InvalidSnapshotResult(kind, call.Arg<object>()));
        var adapter = new EventStorePartyPayloadProtectionAdapter(Service(keys, backend, custody), Substitute.For<IPartyErasureRecordStore>());

        InvalidOperationException failure = await Should.ThrowAsync<InvalidOperationException>(() =>
            adapter.ProtectSnapshotAsync(new("tenant-a", "party", "party-1"), State(), TestContext.Current.CancellationToken));

        failure.Message.ShouldBe("Independent identity history snapshot protection did not produce protected purpose evidence.");
        await custody.Received(1).ProtectSnapshotAsync(Arg.Any<AggregateIdentity>(), Arg.Any<object>(), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SnapshotCustody_MissingProviderIsDenied()
    {
        IPartyKeyManagementService keys = Substitute.For<IPartyKeyManagementService>();
        IKeyStorageBackend backend = Substitute.For<IKeyStorageBackend>();
        backend.ListKeyVersionsAsync("tenant-a", "party-1", Arg.Any<CancellationToken>()).Returns([1]);
        keys.GetKeyVersionAsync("tenant-a", "party-1", 1, Arg.Any<CancellationToken>()).Returns(_ => Enumerable.Repeat((byte)7, 32).ToArray());
        var service = Service(keys, backend, null);

        InvalidOperationException failure = await Should.ThrowAsync<InvalidOperationException>(() =>
            service.ProtectSnapshotStateAsync(new("tenant-a", "party", "party-1"), State(), TestContext.Current.CancellationToken));

        failure.Message.ShouldBe("Independent identity history snapshot protection is unavailable.");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SnapshotCustody_PreexistingWrapperCannotBypassWriteValidation(bool serialized)
    {
        var custody = Substitute.For<IIdentityHistoryCustody>();
        var adapter = new EventStorePartyPayloadProtectionAdapter(
            Service(Substitute.For<IPartyKeyManagementService>(), Substitute.For<IKeyStorageBackend>(), custody),
            Substitute.For<IPartyErasureRecordStore>());
        var wrapper = new IdentityHistorySnapshot("identity-history-snapshot-v1", State());
        object input = serialized ? JsonSerializer.SerializeToElement(wrapper, PartiesJsonOptions.Default) : wrapper;

        await Should.ThrowAsync<InvalidOperationException>(() =>
            adapter.ProtectSnapshotAsync(new("tenant-a", "party", "party-1"), input, TestContext.Current.CancellationToken));

        await custody.DidNotReceiveWithAnyArgs().ProtectSnapshotAsync(default!, default!, default);
    }

    [Fact]
    public async Task SnapshotCustody_CancellationCannotProduceProtectedMetadata()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        IPartyKeyManagementService keys = Substitute.For<IPartyKeyManagementService>();
        IKeyStorageBackend backend = Substitute.For<IKeyStorageBackend>();
        backend.ListKeyVersionsAsync("tenant-a", "party-1", Arg.Any<CancellationToken>()).Returns([1]);
        keys.GetKeyVersionAsync("tenant-a", "party-1", 1, Arg.Any<CancellationToken>()).Returns(_ => Enumerable.Repeat((byte)7, 32).ToArray());
        IIdentityHistoryCustody custody = Substitute.For<IIdentityHistoryCustody>();
        custody.ProtectSnapshotAsync(Arg.Any<AggregateIdentity>(), Arg.Any<object>(), cancellation.Token)
            .Returns(_ =>
            {
                cancellation.Cancel();
                return new SnapshotProtectionResult(new byte[] { 0xff, 0x10, 0x80 }, HistoryMetadata());
            });
        var adapter = new EventStorePartyPayloadProtectionAdapter(Service(keys, backend, custody), Substitute.For<IPartyErasureRecordStore>());

        OperationCanceledException failure = await Should.ThrowAsync<OperationCanceledException>(() =>
            adapter.ProtectSnapshotAsync(new("tenant-a", "party", "party-1"), State(), cancellation.Token));

        failure.CancellationToken.ShouldBe(cancellation.Token);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("object")]
    [InlineData("empty-bytes")]
    [InlineData("json-bytes")]
    [InlineData("invalid-base64")]
    public async Task SnapshotCustody_InvalidPersistedCiphertextIsDeniedBeforeProviderRead(string kind)
    {
        IIdentityHistoryCustody custody = Substitute.For<IIdentityHistoryCustody>();
        var adapter = new EventStorePartyPayloadProtectionAdapter(
            Service(Substitute.For<IPartyKeyManagementService>(), Substitute.For<IKeyStorageBackend>(), custody),
            Substitute.For<IPartyErasureRecordStore>());
        object payload = kind switch
        {
            "null" => null!,
            "object" => JsonSerializer.SerializeToElement(State(), PartiesJsonOptions.Default),
            "empty-bytes" => Array.Empty<byte>(),
            "json-bytes" => JsonSerializer.SerializeToUtf8Bytes(State(), PartiesJsonOptions.Default),
            "invalid-base64" => JsonSerializer.SerializeToElement("invalid-base64!"),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

        SnapshotUnprotectionOutcome outcome = await adapter.TryUnprotectSnapshotAsync(new("tenant-a", "party", "party-1"),
            new IdentityHistorySnapshot("identity-history-snapshot-v1", payload), HistoryMetadata(), TestContext.Current.CancellationToken);

        outcome.IsUnreadable.ShouldBeTrue();
        await custody.DidNotReceiveWithAnyArgs().UnprotectSnapshotAsync(default!, default!, default);
    }

    [Fact]
    public async Task SnapshotCustody_ProviderBufferCannotMutateAcceptedSnapshot()
    {
        IPartyKeyManagementService keys = Substitute.For<IPartyKeyManagementService>();
        IKeyStorageBackend backend = Substitute.For<IKeyStorageBackend>();
        backend.ListKeyVersionsAsync("tenant-a", "party-1", Arg.Any<CancellationToken>()).Returns([1]);
        keys.GetKeyVersionAsync("tenant-a", "party-1", 1, Arg.Any<CancellationToken>()).Returns(_ => Enumerable.Repeat((byte)7, 32).ToArray());
        IIdentityHistoryCustody custody = Substitute.For<IIdentityHistoryCustody>();
        byte[] providerBuffer = [0xff, 0x10, 0x80];
        custody.ProtectSnapshotAsync(Arg.Any<AggregateIdentity>(), Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(new SnapshotProtectionResult(providerBuffer, HistoryMetadata()));
        var service = Service(keys, backend, custody);

        object result = await service.ProtectSnapshotStateAsync(new("tenant-a", "party", "party-1"), State(), TestContext.Current.CancellationToken);
        Array.Clear(providerBuffer);
        byte[] persisted = JsonSerializer.SerializeToElement(result, JsonSerializerOptions.Web).GetProperty("payload").GetBytesFromBase64();

        persisted.ShouldBe(new byte[] { 0xff, 0x10, 0x80 });
    }

    [Fact]
    public async Task SnapshotCustody_ReadCancellationDoesNotReleaseHistory()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        IIdentityHistoryCustody custody = Substitute.For<IIdentityHistoryCustody>();
        custody.UnprotectSnapshotAsync(Arg.Any<AggregateIdentity>(), Arg.Any<object>(), cancellation.Token)
            .Returns(_ =>
            {
                cancellation.Cancel();
                return JsonSerializer.SerializeToElement(State(), PartiesJsonOptions.Default);
            });
        var service = Service(Substitute.For<IPartyKeyManagementService>(), Substitute.For<IKeyStorageBackend>(), custody);

        OperationCanceledException failure = await Should.ThrowAsync<OperationCanceledException>(() =>
            service.UnprotectSnapshotStateAsync(new("tenant-a", "party", "party-1"),
                new IdentityHistorySnapshot("identity-history-snapshot-v1", new byte[] { 0xff, 0x10, 0x80 }), cancellation.Token));

        failure.CancellationToken.ShouldBe(cancellation.Token);
    }

    [Theory]
    [InlineData("authentication-failure")]
    [InlineData("access-refused")]
    [InlineData("unavailable-history")]
    public async Task SnapshotCustody_UnreadableProviderOutcomeDoesNotReleaseStateOrDecryptProfile(string kind)
    {
        IIdentityHistoryCustody custody = Substitute.For<IIdentityHistoryCustody>();
        IPartyKeyManagementService keys = Substitute.For<IPartyKeyManagementService>();
        custody.UnprotectSnapshotAsync(Arg.Any<AggregateIdentity>(), Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns<object?>(_ => kind switch
            {
                "authentication-failure" => throw new AuthenticationTagMismatchException(),
                "access-refused" => throw new UnauthorizedAccessException(),
                "unavailable-history" => null,
                _ => throw new ArgumentOutOfRangeException(nameof(kind)),
            });
        var adapter = new EventStorePartyPayloadProtectionAdapter(
            Service(keys, Substitute.For<IKeyStorageBackend>(), custody), Substitute.For<IPartyErasureRecordStore>());

        SnapshotUnprotectionOutcome outcome = await adapter.TryUnprotectSnapshotAsync(new("tenant-a", "party", "party-1"),
            new IdentityHistorySnapshot("identity-history-snapshot-v1", new byte[] { 0xff, 0x10, 0x80 }),
            HistoryMetadata(), TestContext.Current.CancellationToken);

        outcome.IsUnreadable.ShouldBeTrue();
        outcome.State.ShouldBeNull();
        await custody.Received(1).UnprotectSnapshotAsync(Arg.Any<AggregateIdentity>(), Arg.Any<object>(), TestContext.Current.CancellationToken);
        await keys.DidNotReceiveWithAnyArgs().GetKeyVersionAsync(default!, default!, default, default);
    }

    private static object InvalidSnapshotResult(string kind, object profile)
    {
        byte[] opaque = [0xff, 0x10, 0x80];
        EventStorePayloadProtectionMetadata metadata = HistoryMetadata();
        return kind switch
        {
            "null" => null!,
            "same" => profile,
            "clone" => JsonSerializer.SerializeToElement(profile, PartiesJsonOptions.Default),
            "cosmetic-wrapper" => new Dictionary<string, object> { ["protected"] = true, ["payload"] = profile },
            "unprotected" => new SnapshotProtectionResult(opaque, EventStorePayloadProtectionMetadata.Unprotected()),
            "legacy" => new SnapshotProtectionResult(opaque, EventStorePayloadProtectionMetadataCarrier.Legacy()),
            "opaque-metadata" => new SnapshotProtectionResult(opaque, metadata with { State = PayloadProtectionState.ProviderOpaque }),
            "wrong-purpose" => new SnapshotProtectionResult(opaque, metadata with { Scheme = "parties-aes-gcm-json-fields" }),
            "key-alias" => new SnapshotProtectionResult(opaque, metadata with { KeyAlias = "required-key-reference" }),
            "compatibility-flags" => new SnapshotProtectionResult(opaque, metadata with { CompatibilityFlags = new Dictionary<string, string> { ["required-key-reference"] = "key-1" } }),
            "old-version" => new SnapshotProtectionResult(opaque, metadata with { MetadataVersion = 0 }),
            "future-version" => new SnapshotProtectionResult(opaque, metadata with { MetadataVersion = metadata.MetadataVersion + 1 }),
            "missing-metadata" => new SnapshotProtectionResult(opaque, null!),
            "null-state" => new SnapshotProtectionResult(null!, metadata),
            "empty-bytes" => new SnapshotProtectionResult(Array.Empty<byte>(), metadata),
            "string-state" => new SnapshotProtectionResult("ciphertext", metadata),
            "same-with-metadata" => new SnapshotProtectionResult(profile, metadata),
            "clone-with-metadata" => new SnapshotProtectionResult(JsonSerializer.SerializeToElement(profile, PartiesJsonOptions.Default), metadata),
            "json-bytes" => new SnapshotProtectionResult(JsonSerializer.SerializeToUtf8Bytes(profile, PartiesJsonOptions.Default), metadata),
            "cosmetic-json-bytes" => new SnapshotProtectionResult(JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object> { ["payload"] = profile }, PartiesJsonOptions.Default), metadata),
            "prefixed-json-bytes" => new SnapshotProtectionResult(Encoding.UTF8.GetBytes("plaintext:" + JsonSerializer.Serialize(profile, PartiesJsonOptions.Default)), metadata),
            "base64-json-bytes" => new SnapshotProtectionResult(Encoding.UTF8.GetBytes(Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(profile, PartiesJsonOptions.Default))), metadata),
            "deep-json-bytes" => new SnapshotProtectionResult(Encoding.UTF8.GetBytes(new string('[', 80) + JsonSerializer.Serialize(profile, PartiesJsonOptions.Default) + new string(']', 80)), metadata),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }

    private static EventStorePayloadProtectionMetadata HistoryMetadata()
        => new(PayloadProtectionState.Protected, EventStorePayloadProtectionMetadata.CurrentMetadataVersion,
            "party-actor-history-v1", null, "application/octet-stream", null);

    private static byte[] EncryptSnapshot(object profile, byte[] key)
    {
        byte[] plaintext = JsonSerializer.SerializeToUtf8Bytes(profile, PartiesJsonOptions.Default);
        byte[] result = new byte[28 + plaintext.Length];
        RandomNumberGenerator.Fill(result.AsSpan(0, 12));
        try
        {
            using var aes = new AesGcm(key, 16);
            aes.Encrypt(result.AsSpan(0, 12), plaintext, result.AsSpan(28), result.AsSpan(12, 16));
            return result;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    private static object DecryptSnapshot(byte[] payload, byte[] key)
    {
        byte[] plaintext = new byte[payload.Length - 28];
        try
        {
            using var aes = new AesGcm(key, 16);
            aes.Decrypt(payload.AsSpan(0, 12), payload.AsSpan(28), payload.AsSpan(12, 16), plaintext);
            return JsonSerializer.Deserialize<JsonElement>(plaintext);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    [Fact]
    public async Task ProtectedHistoryFormat_UsesCustodyWithoutProfileKeysAndRejectsNoOpProtection()
    {
        IIdentityHistoryCustody custody = Substitute.For<IIdentityHistoryCustody>();
        var inner = Service(Substitute.For<IPartyKeyManagementService>(), Substitute.For<IKeyStorageBackend>(), custody);
        var adapter = new EventStorePartyPayloadProtectionAdapter(inner, Substitute.For<IPartyErasureRecordStore>());
        var metadata = new EventStorePayloadProtectionMetadata(PayloadProtectionState.Protected, 1, "party-actor-history-v1", null, "application/json", null);
        custody.UnprotectEventAsync(Arg.Any<AggregateIdentity>(), Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new PayloadProtectionResult("{}"u8.ToArray(), "json"));
        PayloadUnprotectionOutcome read = await adapter.TryUnprotectEventPayloadAsync(new("tenant-a", "party", "party-1"), typeof(HumanActorBindingEstablished).FullName!,
            "opaque"u8.ToArray(), "json+identity-history-v1", metadata, TestContext.Current.CancellationToken);
        read.IsUnreadable.ShouldBeFalse();
        HumanActorBindingEstablished history = new(State().HumanActorBindings[0], DateTimeOffset.UnixEpoch, 0);
        custody.ProtectEventAsync(Arg.Any<AggregateIdentity>(), Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<IdentityHistoryCustodyEvidence>(), Arg.Any<CancellationToken>())
            .Returns(new PayloadProtectionResult("{}"u8.ToArray(), "json"));
        await Should.ThrowAsync<InvalidOperationException>(() => inner.ProtectEventPayloadAsync(new("tenant-a", "party", "party-1"), history,
            typeof(HumanActorBindingEstablished).FullName!, "{}"u8.ToArray(), "json", TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("[]", false)]
    [InlineData("5", false)]
    [InlineData("{\"marker\":17}", true)]
    [InlineData("{\"marker\":\"unknown-history\"}", true)]
    public async Task LegacyNonObjectAndMalformedMarker_AreHandledSafely(string json, bool denied)
    {
        var service = Service(Substitute.For<IPartyKeyManagementService>(), Substitute.For<IKeyStorageBackend>(), null);
        JsonElement state = JsonSerializer.Deserialize<JsonElement>(json);
        if (denied)
        {
            await Should.ThrowAsync<InvalidOperationException>(() => service.UnprotectSnapshotStateAsync(new("tenant-a", "party", "party-1"), state, TestContext.Current.CancellationToken));
        }
        else
        {
            await service.UnprotectSnapshotStateAsync(new("tenant-a", "party", "party-1"), state, TestContext.Current.CancellationToken);
        }
    }

    private static PartyState State()
    {
        var state = new PartyState();
        DateTimeOffset start = DateTimeOffset.UnixEpoch;
        state.Apply(new PartyCreated { Type = PartyType.Person, CreatedAt = start, PersonDetails = new PersonDetails { FirstName = "Private", LastName = "Name" } });
        state.Apply(new HumanActorBindingEstablished(new(new("tenant-a", "party-1", "01HX0000000000000000000001", 1, 1, start, start.AddDays(1), "writer-evidence-private", "operator-evidence-private",
            new("synthetic", "party-actor-history-v1", start.AddDays(1), 1, "custody", true, true, true)), "logical", "history-intent-digest-private"), start, 0));
        return state;
    }
    private static PartyPayloadProtectionService Service(IPartyKeyManagementService keys, IKeyStorageBackend backend, IIdentityHistoryCustody? custody)
    {
        IOptionsMonitor<CryptoShreddingOptions> options = Substitute.For<IOptionsMonitor<CryptoShreddingOptions>>();
        options.CurrentValue.Returns(new CryptoShreddingOptions());
        return new(keys, backend, new PartyKeyLifecycleService(keys, Substitute.For<IPartyKeyRetryScheduler>(), NullLogger<PartyKeyLifecycleService>.Instance),
            new DecryptionCircuitBreaker(NullLogger<DecryptionCircuitBreaker>.Instance), options, NullLogger<PartyPayloadProtectionService>.Instance, custody);
    }
}
