using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Security;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.Security.Tests;

public sealed class PartyPayloadProtectionServiceTests
{
    // Use camelCase to match the service's s_jsonOptions
    private static readonly JsonSerializerOptions s_serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IPartyKeyManagementService _keyManagementService = Substitute.For<IPartyKeyManagementService>();
    private readonly IKeyStorageBackend _keyStorageBackend = Substitute.For<IKeyStorageBackend>();
    private readonly IPartyKeyRetryScheduler _retryScheduler = Substitute.For<IPartyKeyRetryScheduler>();

    private static IOptionsMonitor<CryptoShreddingOptions> CreateDefaultOptions(CryptoShreddingOptions? options = null)
    {
        IOptionsMonitor<CryptoShreddingOptions> monitor = Substitute.For<IOptionsMonitor<CryptoShreddingOptions>>();
        monitor.CurrentValue.Returns(options ?? new CryptoShreddingOptions());
        return monitor;
    }

    private PartyPayloadProtectionService CreateService(CryptoShreddingOptions? options = null)
        => new(
            _keyManagementService,
            _keyStorageBackend,
            new PartyKeyLifecycleService(_keyManagementService, _retryScheduler, NullLogger<PartyKeyLifecycleService>.Instance),
            new DecryptionCircuitBreaker(NullLogger<DecryptionCircuitBreaker>.Instance),
            CreateDefaultOptions(options),
            NullLogger<PartyPayloadProtectionService>.Instance);

    private void SetupKey(string tenantId, string partyId, int version, byte[] keyMaterial)
    {
        _keyStorageBackend.ListKeyVersionsAsync(tenantId, partyId, Arg.Any<CancellationToken>()).Returns([version]);
        _keyManagementService.GetKeyVersionAsync(tenantId, partyId, version, Arg.Any<CancellationToken>())
            .Returns(_ => (byte[])keyMaterial.Clone());
    }

    private static byte[] MakeKey(byte fill = 7) => Enumerable.Repeat(fill, 32).ToArray();

    private static AggregateIdentity PartyIdentity(string tenantId = "acme", string partyId = "p1")
        => new(tenantId, "party", partyId);

    // ─── Task 4.1: PersonCreated encrypts PersonalData fields ───

    [Fact]
    public async Task ProtectEventPayload_PersonCreated_EncryptsPersonalDataFields()
    {
        SetupKey("acme", "p1", 2, MakeKey());
        PartyCreated payload = new()
        {
            Type = PartyType.Person,
            PersonDetails = new PersonDetails
            {
                FirstName = "Ada",
                LastName = "Lovelace",
                DateOfBirth = new DateTimeOffset(1815, 12, 10, 0, 0, 0, TimeSpan.Zero),
                Prefix = "Ms",
                Suffix = "PhD",
            },
        };

        byte[] serialized = JsonSerializer.SerializeToUtf8Bytes(payload, s_serializerOptions);
        PartyPayloadProtectionService service = CreateService();

        PayloadProtectionResult result = await service.ProtectEventPayloadAsync(
            PartyIdentity(), payload, typeof(PartyCreated).FullName!, serialized, "json");

        result.SerializationFormat.ShouldBe("json+pdenc-v1");
        string protectedJson = Encoding.UTF8.GetString(result.PayloadBytes);

        // Personal data fields should be encrypted
        protectedJson.ShouldNotContain("Ada");
        protectedJson.ShouldNotContain("Lovelace");
        protectedJson.ShouldNotContain("1815");

        // Non-PII fields should remain plaintext
        protectedJson.ShouldContain("\"type\"");

        // Verify encrypted JSON structure
        JsonNode? root = JsonNode.Parse(result.PayloadBytes);
        root.ShouldNotBeNull();
        JsonNode? firstName = root["personDetails"]?["firstName"];
        firstName.ShouldNotBeNull();
        firstName["$enc"]?.GetValue<bool>().ShouldBe(true);
        firstName["alg"]?.GetValue<string>().ShouldBe("AES256GCM");
        firstName["kv"]?.GetValue<int>().ShouldBe(2);
        firstName["n"].ShouldNotBeNull();
        firstName["t"].ShouldNotBeNull();
        firstName["c"].ShouldNotBeNull();
    }

    // ─── Task 4.2: ContactChannelAdded encrypts Value ───

    [Fact]
    public async Task ProtectEventPayload_ContactChannelAdded_EncryptsValue()
    {
        SetupKey("acme", "p1", 1, MakeKey());
        ContactChannelAdded payload = new()
        {
            ContactChannelId = "cc1",
            Type = ContactChannelType.Email,
            Value = "ada@example.com",
            IsPreferred = true,
        };

        byte[] serialized = JsonSerializer.SerializeToUtf8Bytes(payload, s_serializerOptions);
        PartyPayloadProtectionService service = CreateService();

        PayloadProtectionResult result = await service.ProtectEventPayloadAsync(
            PartyIdentity(), payload, typeof(ContactChannelAdded).FullName!, serialized, "json");

        result.SerializationFormat.ShouldBe("json+pdenc-v1");
        string protectedJson = Encoding.UTF8.GetString(result.PayloadBytes);
        protectedJson.ShouldNotContain("ada@example.com");
        protectedJson.ShouldContain("cc1"); // ID remains plaintext
        protectedJson.ShouldContain("\"type\""); // Type field remains plaintext
    }

    // ─── Task 4.3: IdentifierAdded encrypts Value ───

    [Fact]
    public async Task ProtectEventPayload_IdentifierAdded_EncryptsValue()
    {
        SetupKey("acme", "p1", 1, MakeKey());
        IdentifierAdded payload = new()
        {
            IdentifierId = "id1",
            Type = IdentifierType.VAT,
            Value = "FR123456789",
        };

        byte[] serialized = JsonSerializer.SerializeToUtf8Bytes(payload, s_serializerOptions);
        PartyPayloadProtectionService service = CreateService();

        PayloadProtectionResult result = await service.ProtectEventPayloadAsync(
            PartyIdentity(), payload, typeof(IdentifierAdded).FullName!, serialized, "json");

        result.SerializationFormat.ShouldBe("json+pdenc-v1");
        string protectedJson = Encoding.UTF8.GetString(result.PayloadBytes);
        protectedJson.ShouldNotContain("FR123456789");
        protectedJson.ShouldContain("id1"); // ID remains plaintext
    }

    // ─── Task 4.4: Organization (non-natural person) does NOT encrypt entity fields ───

    [Fact]
    public async Task ProtectEventPayload_OrganizationCreated_DoesNotEncryptEntityFields()
    {
        byte[] keyMaterial = MakeKey(9);
        _keyStorageBackend.ListKeyVersionsAsync("acme", "org1", Arg.Any<CancellationToken>())
            .Returns(
                (IReadOnlyList<int>)[],
                [1]);
        _keyManagementService.CreateKeyAsync("acme", "org1", Arg.Any<CancellationToken>())
            .Returns(new PartyKeyInfo
            {
                KeyId = "acme/parties/org1/v1",
                Version = 1,
                TenantId = "acme",
                PartyId = "org1",
                Algorithm = EncryptionAlgorithm.AES256GCM,
                CreatedAt = DateTimeOffset.UtcNow,
            });
        _keyManagementService.GetKeyVersionAsync("acme", "org1", 1, Arg.Any<CancellationToken>())
            .Returns(_ => (byte[])keyMaterial.Clone());

        PartyCreated payload = new()
        {
            Type = PartyType.Organization,
            OrganizationDetails = new OrganizationDetails
            {
                LegalName = "Acme Corp",
                TradingName = "Acme",
                LegalForm = "SAS",
                RegistrationNumber = "123456",
                IsNaturalPerson = false,
            },
        };

        byte[] serialized = JsonSerializer.SerializeToUtf8Bytes(payload, s_serializerOptions);
        PartyPayloadProtectionService service = CreateService();

        PayloadProtectionResult result = await service.ProtectEventPayloadAsync(
            new AggregateIdentity("acme", "party", "org1"), payload, typeof(PartyCreated).FullName!, serialized, "json");

        result.SerializationFormat.ShouldBe("json");
        string json = Encoding.UTF8.GetString(result.PayloadBytes);
        json.ShouldContain("Acme Corp");
        json.ShouldContain("Acme");
        json.ShouldContain("SAS");
    }

    // ─── Task 4.5: Organization IsNaturalPerson=true encrypts all string fields ───

    [Fact]
    public async Task ProtectEventPayload_OrganizationIsNaturalPerson_EncryptsAllStringFields()
    {
        SetupKey("acme", "np1", 1, MakeKey());
        PartyCreated payload = new()
        {
            Type = PartyType.Organization,
            OrganizationDetails = new OrganizationDetails
            {
                LegalName = "Jean Dupont EURL",
                TradingName = "Dupont Consulting",
                LegalForm = "EURL",
                RegistrationNumber = "999999",
                IsNaturalPerson = true,
            },
        };

        byte[] serialized = JsonSerializer.SerializeToUtf8Bytes(payload, s_serializerOptions);
        PartyPayloadProtectionService service = CreateService();

        PayloadProtectionResult result = await service.ProtectEventPayloadAsync(
            PartyIdentity(partyId: "np1"), payload, typeof(PartyCreated).FullName!, serialized, "json");

        result.SerializationFormat.ShouldBe("json+pdenc-v1");
        string protectedJson = Encoding.UTF8.GetString(result.PayloadBytes);
        protectedJson.ShouldNotContain("Jean Dupont EURL");
        protectedJson.ShouldNotContain("Dupont Consulting");
        protectedJson.ShouldNotContain("EURL");
        protectedJson.ShouldNotContain("999999");
    }

    // ─── Task 4.6: Full roundtrip protect → unprotect ───

    [Fact]
    public async Task UnprotectEventPayload_EncryptedPayload_DecryptsCorrectly()
    {
        SetupKey("acme", "p1", 2, MakeKey());
        PartyCreated payload = new()
        {
            Type = PartyType.Person,
            PersonDetails = new PersonDetails
            {
                FirstName = "Ada",
                LastName = "Lovelace",
                DateOfBirth = new DateTimeOffset(1815, 12, 10, 0, 0, 0, TimeSpan.Zero),
                Prefix = null,
                Suffix = null,
            },
        };

        byte[] serialized = JsonSerializer.SerializeToUtf8Bytes(payload, s_serializerOptions);
        AggregateIdentity identity = PartyIdentity();
        PartyPayloadProtectionService service = CreateService();

        PayloadProtectionResult protectedResult = await service.ProtectEventPayloadAsync(
            identity, payload, typeof(PartyCreated).FullName!, serialized, "json");

        PayloadProtectionResult unprotectedResult = await service.UnprotectEventPayloadAsync(
            identity, typeof(PartyCreated).FullName!, protectedResult.PayloadBytes, protectedResult.SerializationFormat);

        unprotectedResult.SerializationFormat.ShouldBe("json");
        PartyCreated? roundTrip = JsonSerializer.Deserialize<PartyCreated>(unprotectedResult.PayloadBytes, s_serializerOptions);
        roundTrip.ShouldNotBeNull();
        roundTrip.PersonDetails.ShouldNotBeNull();
        roundTrip.PersonDetails.FirstName.ShouldBe("Ada");
        roundTrip.PersonDetails.LastName.ShouldBe("Lovelace");
        roundTrip.PersonDetails.DateOfBirth.ShouldBe(payload.PersonDetails!.DateOfBirth);
        roundTrip.Type.ShouldBe(PartyType.Person);
    }

    // ─── Task 4.7: Different key version uses correct version ───

    [Fact]
    public async Task UnprotectEventPayload_DifferentKeyVersion_UsesCorrectVersion()
    {
        byte[] keyV1 = MakeKey(1);
        byte[] keyV2 = MakeKey(2);

        // Encrypt with v1
        _keyStorageBackend.ListKeyVersionsAsync("acme", "p1", Arg.Any<CancellationToken>()).Returns([1]);
        _keyManagementService.GetKeyVersionAsync("acme", "p1", 1, Arg.Any<CancellationToken>())
            .Returns(_ => (byte[])keyV1.Clone());

        ContactChannelAdded payload = new()
        {
            ContactChannelId = "cc1",
            Type = ContactChannelType.Email,
            Value = "v1@example.com",
        };

        byte[] serialized = JsonSerializer.SerializeToUtf8Bytes(payload, s_serializerOptions);
        AggregateIdentity identity = PartyIdentity();
        PartyPayloadProtectionService service = CreateService();

        PayloadProtectionResult encrypted = await service.ProtectEventPayloadAsync(
            identity, payload, typeof(ContactChannelAdded).FullName!, serialized, "json");

        // "Rotate" key — now v2 is current, but v1 still retrievable
        _keyStorageBackend.ListKeyVersionsAsync("acme", "p1", Arg.Any<CancellationToken>()).Returns([1, 2]);
        _keyManagementService.GetKeyVersionAsync("acme", "p1", 2, Arg.Any<CancellationToken>())
            .Returns(_ => (byte[])keyV2.Clone());
        // v1 still available for decryption
        _keyManagementService.GetKeyVersionAsync("acme", "p1", 1, Arg.Any<CancellationToken>())
            .Returns(_ => (byte[])keyV1.Clone());

        PayloadProtectionResult decrypted = await service.UnprotectEventPayloadAsync(
            identity, typeof(ContactChannelAdded).FullName!, encrypted.PayloadBytes, encrypted.SerializationFormat);

        ContactChannelAdded? roundTrip = JsonSerializer.Deserialize<ContactChannelAdded>(decrypted.PayloadBytes, s_serializerOptions);
        roundTrip.ShouldNotBeNull();
        roundTrip.Value.ShouldBe("v1@example.com");
    }

    // ─── Task 4.8: Non-party domain passthrough ───

    [Fact]
    public async Task ProtectEventPayload_NonPartyDomain_Passthrough()
    {
        ContactChannelAdded payload = new()
        {
            ContactChannelId = "cc1",
            Type = ContactChannelType.Email,
            Value = "test@example.com",
        };

        byte[] serialized = JsonSerializer.SerializeToUtf8Bytes(payload, s_serializerOptions);
        AggregateIdentity identity = new("acme", "customer", "c1"); // non-party domain
        PartyPayloadProtectionService service = CreateService();

        PayloadProtectionResult result = await service.ProtectEventPayloadAsync(
            identity, payload, typeof(ContactChannelAdded).FullName!, serialized, "json");

        result.SerializationFormat.ShouldBe("json");
        result.PayloadBytes.ShouldBe(serialized);
    }

    // ─── Task 4.9: No PersonalData fields passthrough ───

    [Fact]
    public async Task ProtectEventPayload_NoPersonalData_Passthrough()
    {
        SetupKey("acme", "p1", 1, MakeKey());
        PartyDeactivated payload = new();

        byte[] serialized = JsonSerializer.SerializeToUtf8Bytes(payload, s_serializerOptions);
        PartyPayloadProtectionService service = CreateService();

        PayloadProtectionResult result = await service.ProtectEventPayloadAsync(
            PartyIdentity(), payload, typeof(PartyDeactivated).FullName!, serialized, "json");

        result.SerializationFormat.ShouldBe("json");
    }

    // ─── Task 4.10: Snapshot protection encrypts PersonalData fields ───

    [Fact]
    public async Task ProtectSnapshotState_PartyState_EncryptsPersonalDataFields()
    {
        SetupKey("acme", "p1", 1, MakeKey());
        // Use a simple object with [PersonalData] fields as a stand-in for PartyState
        PartyCreated state = new()
        {
            Type = PartyType.Person,
            PersonDetails = new PersonDetails
            {
                FirstName = "Alan",
                LastName = "Turing",
                DateOfBirth = new DateTimeOffset(1912, 6, 23, 0, 0, 0, TimeSpan.Zero),
                Prefix = null,
                Suffix = null,
            },
        };

        PartyPayloadProtectionService service = CreateService();

        object protectedState = await service.ProtectSnapshotStateAsync(PartyIdentity(), state);

        protectedState.ShouldBeOfType<PartyPayloadProtectionService.ProtectedSnapshotState>();
        PartyPayloadProtectionService.ProtectedSnapshotState snapshot = (PartyPayloadProtectionService.ProtectedSnapshotState)protectedState;
        snapshot.Marker.ShouldBe("$protectedSnapshot");
        snapshot.SerializationFormat.ShouldBe("json+pdenc-v1");
        snapshot.Payload.ShouldNotBeEmpty();

        // Verify plaintext is not in the payload
        string decoded = Encoding.UTF8.GetString(Convert.FromBase64String(snapshot.Payload));
        decoded.ShouldNotContain("Alan");
        decoded.ShouldNotContain("Turing");
    }

    // ─── Task 4.11: Snapshot roundtrip ───

    [Fact]
    public async Task UnprotectSnapshotState_ProtectedSnapshot_RestoresOriginalState()
    {
        SetupKey("acme", "p1", 1, MakeKey());
        PartyCreated original = new()
        {
            Type = PartyType.Person,
            PersonDetails = new PersonDetails
            {
                FirstName = "Grace",
                LastName = "Hopper",
                DateOfBirth = new DateTimeOffset(1906, 12, 9, 0, 0, 0, TimeSpan.Zero),
                Prefix = null,
                Suffix = null,
            },
        };

        PartyPayloadProtectionService service = CreateService();

        object protectedState = await service.ProtectSnapshotStateAsync(PartyIdentity(), original);
        object restoredState = await service.UnprotectSnapshotStateAsync(PartyIdentity(), protectedState);

        restoredState.ShouldBeOfType<PartyCreated>();
        PartyCreated restored = (PartyCreated)restoredState;
        restored.PersonDetails.ShouldNotBeNull();
        restored.PersonDetails.FirstName.ShouldBe("Grace");
        restored.PersonDetails.LastName.ShouldBe("Hopper");
        restored.PersonDetails.DateOfBirth.ShouldBe(original.PersonDetails!.DateOfBirth);
    }

    // ─── Task 4.12: Nonce uniqueness verification at scale ───

    [Fact]
    public async Task ProtectEventPayload_1000EncryptionsOfSameData_AllProduceDifferentCiphertext()
    {
        SetupKey("acme", "p1", 1, MakeKey());
        ContactChannelAdded payload = new()
        {
            ContactChannelId = "cc1",
            Type = ContactChannelType.Email,
            Value = "nonce-test@example.com",
        };

        byte[] serialized = JsonSerializer.SerializeToUtf8Bytes(payload, s_serializerOptions);
        PartyPayloadProtectionService service = CreateService();
        HashSet<string> nonces = new(1000);

        for (int i = 0; i < 1000; i++)
        {
            PayloadProtectionResult result = await service.ProtectEventPayloadAsync(
                PartyIdentity(), payload, typeof(ContactChannelAdded).FullName!, serialized, "json");

            JsonNode? root = JsonNode.Parse(result.PayloadBytes);
            string nonce = root!["value"]!["n"]!.GetValue<string>();
            nonces.Add(nonce).ShouldBeTrue($"Duplicate nonce detected at iteration {i}");
        }

        nonces.Count.ShouldBe(1000);
    }

    // ─── Task 4.13: CryptoShredding disabled passthrough ───

    [Fact]
    public async Task ProtectEventPayload_CryptoShreddingDisabled_Passthrough()
    {
        SetupKey("acme", "p1", 1, MakeKey());
        ContactChannelAdded payload = new()
        {
            ContactChannelId = "cc1",
            Type = ContactChannelType.Email,
            Value = "disabled@example.com",
        };

        byte[] serialized = JsonSerializer.SerializeToUtf8Bytes(payload, s_serializerOptions);
        CryptoShreddingOptions options = new() { IsEnabled = false };
        PartyPayloadProtectionService service = CreateService(options);

        PayloadProtectionResult result = await service.ProtectEventPayloadAsync(
            PartyIdentity(), payload, typeof(ContactChannelAdded).FullName!, serialized, "json");

        result.SerializationFormat.ShouldBe("json");
        string json = Encoding.UTF8.GetString(result.PayloadBytes);
        json.ShouldContain("disabled@example.com");
    }

    // ─── Task 4.14: CryptoShredding disabled still decrypts existing encrypted ───

    [Fact]
    public async Task UnprotectEventPayload_CryptoShreddingDisabled_StillDecryptsExistingEncrypted()
    {
        SetupKey("acme", "p1", 1, MakeKey());
        ContactChannelAdded payload = new()
        {
            ContactChannelId = "cc1",
            Type = ContactChannelType.Email,
            Value = "mixed@example.com",
        };

        byte[] serialized = JsonSerializer.SerializeToUtf8Bytes(payload, s_serializerOptions);

        // First encrypt with enabled config
        PartyPayloadProtectionService enabledService = CreateService();
        PayloadProtectionResult encrypted = await enabledService.ProtectEventPayloadAsync(
            PartyIdentity(), payload, typeof(ContactChannelAdded).FullName!, serialized, "json");

        // Then decrypt with disabled config — should still work
        CryptoShreddingOptions disabled = new() { IsEnabled = false };
        PartyPayloadProtectionService disabledService = CreateService(disabled);
        PayloadProtectionResult decrypted = await disabledService.UnprotectEventPayloadAsync(
            PartyIdentity(), typeof(ContactChannelAdded).FullName!, encrypted.PayloadBytes, encrypted.SerializationFormat);

        decrypted.SerializationFormat.ShouldBe("json");
        ContactChannelAdded? roundTrip = JsonSerializer.Deserialize<ContactChannelAdded>(decrypted.PayloadBytes, s_serializerOptions);
        roundTrip.ShouldNotBeNull();
        roundTrip.Value.ShouldBe("mixed@example.com");
    }

    // ─── Task 4.15: Tampered ciphertext throws CryptographicException ───

    [Fact]
    public async Task UnprotectEventPayload_TamperedCiphertext_ThrowsCryptographicException()
    {
        SetupKey("acme", "p1", 1, MakeKey());
        ContactChannelAdded payload = new()
        {
            ContactChannelId = "cc1",
            Type = ContactChannelType.Email,
            Value = "tamper@example.com",
        };

        byte[] serialized = JsonSerializer.SerializeToUtf8Bytes(payload, s_serializerOptions);
        PartyPayloadProtectionService service = CreateService();

        PayloadProtectionResult encrypted = await service.ProtectEventPayloadAsync(
            PartyIdentity(), payload, typeof(ContactChannelAdded).FullName!, serialized, "json");

        // Tamper with the ciphertext
        JsonNode? root = JsonNode.Parse(encrypted.PayloadBytes);
        string ciphertext = root!["value"]!["c"]!.GetValue<string>();
        byte[] ciphertextBytes = Convert.FromBase64String(ciphertext);
        ciphertextBytes[0] ^= 0xFF; // Flip bits
        root["value"]!["c"] = Convert.ToBase64String(ciphertextBytes);

        byte[] tamperedBytes = JsonSerializer.SerializeToUtf8Bytes(root);

        await Should.ThrowAsync<CryptographicException>(
            () => service.UnprotectEventPayloadAsync(
                PartyIdentity(), typeof(ContactChannelAdded).FullName!, tamperedBytes, "json+pdenc-v1"));
    }

    // ─── Task 4.16: Corrupted base64 throws with context ───

    [Fact]
    public async Task UnprotectEventPayload_CorruptedBase64_ThrowsFormatExceptionWithContext()
    {
        SetupKey("acme", "p1", 1, MakeKey());
        ContactChannelAdded payload = new()
        {
            ContactChannelId = "cc1",
            Type = ContactChannelType.Email,
            Value = "corrupt@example.com",
        };

        byte[] serialized = JsonSerializer.SerializeToUtf8Bytes(payload, s_serializerOptions);
        PartyPayloadProtectionService service = CreateService();

        PayloadProtectionResult encrypted = await service.ProtectEventPayloadAsync(
            PartyIdentity(), payload, typeof(ContactChannelAdded).FullName!, serialized, "json");

        // Corrupt the base64 in the nonce field
        JsonNode? root = JsonNode.Parse(encrypted.PayloadBytes);
        root!["value"]!["n"] = "!!!not-valid-base64!!!";
        byte[] corruptedBytes = JsonSerializer.SerializeToUtf8Bytes(root);

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => service.UnprotectEventPayloadAsync(
                PartyIdentity(), typeof(ContactChannelAdded).FullName!, corruptedBytes, "json+pdenc-v1"));

        ex.Message.ShouldContain("Corrupted encrypted field");
        ex.Message.ShouldContain("acme");
        ex.Message.ShouldContain("p1");
        ex.InnerException.ShouldBeOfType<FormatException>();
    }

    // ─── Task 4.17: Snapshot TypeName survives version change ───

    [Fact]
    public async Task ProtectSnapshotState_TypeNameSurvivesVersionChange()
    {
        SetupKey("acme", "p1", 1, MakeKey());
        PartyCreated state = new()
        {
            Type = PartyType.Person,
            PersonDetails = new PersonDetails
            {
                FirstName = "Test",
                LastName = "Version",
                DateOfBirth = null,
                Prefix = null,
                Suffix = null,
            },
        };

        PartyPayloadProtectionService service = CreateService();

        object protectedState = await service.ProtectSnapshotStateAsync(PartyIdentity(), state);

        PartyPayloadProtectionService.ProtectedSnapshotState snapshot =
            (PartyPayloadProtectionService.ProtectedSnapshotState)protectedState;

        // TypeName should use FullName (not AssemblyQualifiedName)
        snapshot.TypeName.ShouldNotContain("Version=");
        snapshot.TypeName.ShouldNotContain("PublicKeyToken=");
        snapshot.TypeName.ShouldContain("PartyCreated");

        // Roundtrip should work
        object restored = await service.UnprotectSnapshotStateAsync(PartyIdentity(), protectedState);
        restored.ShouldBeOfType<PartyCreated>();
    }

    // ─── Task 4.18: COMPLIANCE — Personal Data Registry Test ───

    [Fact]
    public void AllEventPayloadTypes_PersonalDataClassificationVerified()
    {
        // Enumerate all IEventPayload implementations in the Contracts assembly
        Type[] eventPayloadTypes = typeof(PartyCreated).Assembly
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(IEventPayload).IsAssignableFrom(t))
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .ToArray();

        // Expected classification: type → whether a representative instance contains protected data
        Dictionary<Type, bool> expectedClassification = new()
        {
            // Domain events with personal data
            [typeof(PartyCreated)] = true, // PersonDetails has [PersonalData] fields
            [typeof(PersonDetailsUpdated)] = true,
            [typeof(ContactChannelAdded)] = true,
            [typeof(ContactChannelUpdated)] = true, // Value has [PersonalData] (nullable but classified)
            [typeof(IdentifierAdded)] = true,

            // Domain events without personal data
            [typeof(ConsentRecorded)] = false,
            [typeof(ConsentRevoked)] = false,
            [typeof(OrganizationDetailsUpdated)] = false, // IsNaturalPerson=false default → no personal data
            [typeof(PartyDeactivated)] = false,
            [typeof(PartyReactivated)] = false,
            [typeof(PartyMerged)] = false,
            [typeof(PreferredContactChannelChanged)] = false,
            [typeof(ContactChannelRemoved)] = false,
            [typeof(IdentifierRemoved)] = false,
            [typeof(IsNaturalPersonChanged)] = false,
            [typeof(PartyDisplayNameDerived)] = false, // No [PersonalData] on event fields
            [typeof(PartyEncryptionKeyCreated)] = false,
            [typeof(PartyEncryptionKeyDeleted)] = false,
            [typeof(PartyEncryptionKeyRotated)] = false,
            [typeof(ErasePartyRequested)] = false,
            [typeof(PartyErased)] = false,
            [typeof(ProcessingRestricted)] = false,
            [typeof(RestrictionLifted)] = false,
            [typeof(ErasureVerified)] = false,

            // Rejection events (all without personal data)
            [typeof(CompositeOperationConflict)] = false,
            [typeof(ConsentNotFound)] = false,
            [typeof(ContactChannelNotFound)] = false,
            [typeof(IdentifierNotFound)] = false,
            [typeof(InvalidConsentPurpose)] = false,
            [typeof(PartyCannotAddDuplicateIdentifier)] = false,
            [typeof(PartyCannotBeCreatedWithoutOrganizationDetails)] = false,
            [typeof(PartyCannotAddDuplicateChannel)] = false,
            [typeof(PartyCannotBeCreatedWithInvalidId)] = false,
            [typeof(PartyCannotBeDeactivatedWhenInactive)] = false,
            [typeof(PartyCannotBeCreatedWithoutType)] = false,
            [typeof(PartyCannotBeReactivatedWhenActive)] = false,
            [typeof(PartyCannotBeCreatedWithoutPersonDetails)] = false,
            [typeof(PartyNotFound)] = false,
            [typeof(PartyErasureInProgress)] = false,
            [typeof(PartyNotRestricted)] = false,
            [typeof(PartyProcessingRestricted)] = false,
            [typeof(PartyTypeMismatch)] = false,
            // Carries CommandType + property-name/error-code metadata only — no PII.
            // PartyDomainServiceInvoker explicitly excludes raw payload fragments and validator
            // messages from the rejection event to keep this classification stable.
            [typeof(PartyCommandValidationRejected)] = false,
        };

        // Verify every IEventPayload type is classified
        foreach (Type eventType in eventPayloadTypes)
        {
            expectedClassification.ShouldContainKey(
                eventType,
                $"New IEventPayload type '{eventType.Name}' found but not classified in the personal data registry. " +
                "Add it to the expectedClassification dictionary to indicate whether it carries personal data.");
        }

        // Verify classifications match actual behavior
        Dictionary<Type, object> testInstances = CreateTestInstances();
        foreach (KeyValuePair<Type, bool> kvp in expectedClassification)
        {
            if (!testInstances.TryGetValue(kvp.Key, out object? instance))
            {
                continue; // Skip types we can't easily instantiate
            }

            bool actual = PersonalDataGraphInspector.ContainsProtectedData(instance);
            actual.ShouldBe(
                kvp.Value,
                $"Type '{kvp.Key.Name}' expected ContainsProtectedData={kvp.Value} but got {actual}");
        }
    }

    private static Dictionary<Type, object> CreateTestInstances()
    {
        return new Dictionary<Type, object>
        {
            [typeof(PartyCreated)] = new PartyCreated
            {
                Type = PartyType.Person,
                PersonDetails = new PersonDetails { FirstName = "A", LastName = "B", DateOfBirth = null, Prefix = null, Suffix = null },
            },
            [typeof(PersonDetailsUpdated)] = new PersonDetailsUpdated
            {
                PersonDetails = new PersonDetails { FirstName = "A", LastName = "B", DateOfBirth = null, Prefix = null, Suffix = null },
            },
            [typeof(ContactChannelAdded)] = new ContactChannelAdded
            {
                ContactChannelId = "cc1", Type = ContactChannelType.Email, Value = "a@b.com",
            },
            [typeof(ContactChannelUpdated)] = new ContactChannelUpdated
            {
                ContactChannelId = "cc1", Value = "a@b.com",
            },
            [typeof(IdentifierAdded)] = new IdentifierAdded
            {
                IdentifierId = "id1", Type = IdentifierType.VAT, Value = "X",
            },
            [typeof(OrganizationDetailsUpdated)] = new OrganizationDetailsUpdated
            {
                OrganizationDetails = new OrganizationDetails
                {
                    LegalName = "Corp", IsNaturalPerson = false,
                },
            },
            [typeof(PartyDeactivated)] = new PartyDeactivated(),
            [typeof(PartyReactivated)] = new PartyReactivated(),
            [typeof(PartyMerged)] = new PartyMerged { SurvivorPartyId = "s1", MergedPartyId = "m1" },
            [typeof(PreferredContactChannelChanged)] = new PreferredContactChannelChanged { ContactChannelId = "cc1" },
            [typeof(ContactChannelRemoved)] = new ContactChannelRemoved { ContactChannelId = "cc1" },
            [typeof(IdentifierRemoved)] = new IdentifierRemoved { IdentifierId = "id1" },
            [typeof(IsNaturalPersonChanged)] = new IsNaturalPersonChanged { IsNaturalPerson = true },
            [typeof(PartyDisplayNameDerived)] = new PartyDisplayNameDerived { DisplayName = "X", SortName = "X" },
            [typeof(PartyEncryptionKeyCreated)] = new PartyEncryptionKeyCreated
            {
                PartyId = "p1", TenantId = "t1", KeyVersion = 1, CreatedAt = DateTimeOffset.UtcNow,
            },
            [typeof(PartyEncryptionKeyDeleted)] = new PartyEncryptionKeyDeleted
            {
                PartyId = "p1", TenantId = "t1", DeletedAt = DateTimeOffset.UtcNow,
            },
            [typeof(PartyEncryptionKeyRotated)] = new PartyEncryptionKeyRotated
            {
                PartyId = "p1", NewKeyVersion = 2, PreviousKeyVersion = 1, RotatedAt = DateTimeOffset.UtcNow,
            },
            [typeof(ErasePartyRequested)] = new ErasePartyRequested
            {
                PartyId = "p1", TenantId = "t1", RequestedAt = DateTimeOffset.UtcNow, RequestedBy = "admin",
            },
            [typeof(PartyErased)] = new PartyErased
            {
                PartyId = "p1", TenantId = "t1", ErasedAt = DateTimeOffset.UtcNow,
            },
            [typeof(ErasureVerified)] = new ErasureVerified
            {
                PartyId = "p1", TenantId = "t1", VerifiedAt = DateTimeOffset.UtcNow, VerificationReportId = "r1",
            },
        };
    }

    // ─── Task 4.19: Crypto-shredding proof — key deleted ───

    [Fact]
    public async Task UnprotectEventPayload_KeyDeleted_ThrowsAndPlaintextUnrecoverable()
    {
        byte[] keyMaterial = MakeKey();
        SetupKey("acme", "p1", 1, keyMaterial);

        ContactChannelAdded payload = new()
        {
            ContactChannelId = "cc1",
            Type = ContactChannelType.Email,
            Value = "shred-me@example.com",
        };

        byte[] serialized = JsonSerializer.SerializeToUtf8Bytes(payload, s_serializerOptions);
        PartyPayloadProtectionService service = CreateService();

        PayloadProtectionResult encrypted = await service.ProtectEventPayloadAsync(
            PartyIdentity(), payload, typeof(ContactChannelAdded).FullName!, serialized, "json");

        // "Delete" the key
        _keyManagementService.GetKeyVersionAsync("acme", "p1", 1, Arg.Any<CancellationToken>())
            .Returns<byte[]>(_ => throw new InvalidOperationException("Key has been destroyed."));

        // Attempt to decrypt — should throw
        await Should.ThrowAsync<InvalidOperationException>(
            () => service.UnprotectEventPayloadAsync(
                PartyIdentity(), typeof(ContactChannelAdded).FullName!, encrypted.PayloadBytes, encrypted.SerializationFormat));

        // Verify plaintext is not recoverable from the encrypted payload
        string encPayload = Encoding.UTF8.GetString(encrypted.PayloadBytes);
        encPayload.ShouldNotContain("shred-me@example.com");
    }

    // ─── Task 4.20: Snapshot with old key version decrypts correctly ───

    [Fact]
    public async Task UnprotectSnapshotState_EncryptedWithOldKeyVersion_DecryptsCorrectly()
    {
        byte[] keyV1 = MakeKey(1);

        // Encrypt snapshot with v1
        _keyStorageBackend.ListKeyVersionsAsync("acme", "p1", Arg.Any<CancellationToken>()).Returns([1]);
        _keyManagementService.GetKeyVersionAsync("acme", "p1", 1, Arg.Any<CancellationToken>())
            .Returns(_ => (byte[])keyV1.Clone());

        PartyCreated state = new()
        {
            Type = PartyType.Person,
            PersonDetails = new PersonDetails
            {
                FirstName = "Snapshot",
                LastName = "OldKey",
                DateOfBirth = null,
                Prefix = null,
                Suffix = null,
            },
        };

        PartyPayloadProtectionService service = CreateService();
        object protectedState = await service.ProtectSnapshotStateAsync(PartyIdentity(), state);

        // "Rotate" key to v2 — but v1 still retrievable
        _keyStorageBackend.ListKeyVersionsAsync("acme", "p1", Arg.Any<CancellationToken>()).Returns([1, 2]);
        _keyManagementService.GetKeyVersionAsync("acme", "p1", 1, Arg.Any<CancellationToken>())
            .Returns(_ => (byte[])keyV1.Clone());

        object restored = await service.UnprotectSnapshotStateAsync(PartyIdentity(), protectedState);

        restored.ShouldBeOfType<PartyCreated>();
        PartyCreated restoredState = (PartyCreated)restored;
        restoredState.PersonDetails.ShouldNotBeNull();
        restoredState.PersonDetails.FirstName.ShouldBe("Snapshot");
        restoredState.PersonDetails.LastName.ShouldBe("OldKey");
    }

    // ─── Task 4.21: Performance benchmark ───

    [Fact]
    public async Task ProtectEventPayload_RealisticComposite_CompletesUnder50ms()
    {
        SetupKey("acme", "p1", 1, MakeKey());

        // Realistic composite with 10+ encrypted fields
        PartyCreated payload = new()
        {
            Type = PartyType.Person,
            PersonDetails = new PersonDetails
            {
                FirstName = "Performance",
                LastName = "Test",
                DateOfBirth = new DateTimeOffset(1990, 1, 1, 0, 0, 0, TimeSpan.Zero),
                Prefix = "Dr",
                Suffix = "III",
            },
        };

        byte[] serialized = JsonSerializer.SerializeToUtf8Bytes(payload, s_serializerOptions);
        PartyPayloadProtectionService service = CreateService();

        // Warm up
        await service.ProtectEventPayloadAsync(
            PartyIdentity(), payload, typeof(PartyCreated).FullName!, serialized, "json");

        // Benchmark
        Stopwatch sw = Stopwatch.StartNew();
        for (int i = 0; i < 10; i++)
        {
            await service.ProtectEventPayloadAsync(
                PartyIdentity(), payload, typeof(PartyCreated).FullName!, serialized, "json");
        }

        sw.Stop();
        double avgMs = sw.Elapsed.TotalMilliseconds / 10;
        avgMs.ShouldBeLessThan(50, $"Average encryption time was {avgMs:F2}ms, expected < 50ms");
    }

    // ─── Task 4.22: IsNaturalPerson reclassification scope expansion ───

    [Fact]
    public async Task ProtectEventPayload_IsNaturalPersonReclassification_EncryptionScopeExpands()
    {
        SetupKey("acme", "org1", 1, MakeKey());

        // Before reclassification: standard org, entity fields in plaintext
        PartyCreated orgPayload = new()
        {
            Type = PartyType.Organization,
            OrganizationDetails = new OrganizationDetails
            {
                LegalName = "Jean Dupont EURL",
                TradingName = "Dupont",
                LegalForm = "EURL",
                IsNaturalPerson = false,
            },
        };

        byte[] serialized = JsonSerializer.SerializeToUtf8Bytes(orgPayload, s_serializerOptions);
        PartyPayloadProtectionService service = CreateService();

        PayloadProtectionResult beforeResult = await service.ProtectEventPayloadAsync(
            PartyIdentity(partyId: "org1"), orgPayload, typeof(PartyCreated).FullName!, serialized, "json");

        // Before: plaintext (no personal data in standard org)
        beforeResult.SerializationFormat.ShouldBe("json");
        Encoding.UTF8.GetString(beforeResult.PayloadBytes).ShouldContain("Jean Dupont EURL");

        // After reclassification: IsNaturalPerson=true, string fields now encrypted
        OrganizationDetailsUpdated afterPayload = new()
        {
            OrganizationDetails = new OrganizationDetails
            {
                LegalName = "Jean Dupont EURL",
                TradingName = "Dupont",
                LegalForm = "EURL",
                IsNaturalPerson = true,
            },
        };

        byte[] afterSerialized = JsonSerializer.SerializeToUtf8Bytes(afterPayload, s_serializerOptions);

        PayloadProtectionResult afterResult = await service.ProtectEventPayloadAsync(
            PartyIdentity(partyId: "org1"), afterPayload, typeof(OrganizationDetailsUpdated).FullName!, afterSerialized, "json");

        afterResult.SerializationFormat.ShouldBe("json+pdenc-v1");
        string afterJson = Encoding.UTF8.GetString(afterResult.PayloadBytes);
        afterJson.ShouldNotContain("Jean Dupont EURL");
    }
}
