#pragma warning disable CS8620 // NSubstitute + DaprClient nullable generics mismatch
#pragma warning disable CA2007

using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Client;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.Parties.CommandApi;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Projections.Abstractions;
using Hexalith.Parties.Security;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.IntegrationTests.Security;

/// <summary>
/// Tier 2 integration tests that verify the encryption pipeline through the
/// DI-wired WebApplicationFactory, testing real component interactions.
/// </summary>
public sealed class EncryptionPipelineIntegrationTests : IClassFixture<EncryptionTestFactory>
{
    private readonly EncryptionTestFactory _factory;

    public EncryptionPipelineIntegrationTests(EncryptionTestFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Creates a party key by protecting a PartyCreated event (triggers auto key creation).
    /// </summary>
    private static async Task EnsurePartyKeyExistsAsync(IEventPayloadProtectionService protectionService, string tenantId, string partyId)
    {
        PartyCreated createPayload = new()
        {
            Type = PartyType.Person,
            PersonDetails = new PersonDetails { FirstName = "Setup", LastName = "Key" },
        };
        byte[] serialized = JsonSerializer.SerializeToUtf8Bytes(createPayload, EncryptionTestFactory.SerializerOptions);
        AggregateIdentity identity = new(tenantId, "party", partyId);
        await protectionService.ProtectEventPayloadAsync(
            identity, createPayload, typeof(PartyCreated).FullName!, serialized, "json");
    }

    // ─── Task 7.1: CreateParty encrypts personal data fields in store ───

    [Fact]
    public async Task CreateParty_PersonWithChannels_EventsEncryptedInStore()
    {
        // Resolve the DI-wired protection service (tests full DI integration)
        IEventPayloadProtectionService protectionService = _factory.Services.GetRequiredService<IEventPayloadProtectionService>();

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

        byte[] serialized = JsonSerializer.SerializeToUtf8Bytes(payload, EncryptionTestFactory.SerializerOptions);
        AggregateIdentity identity = new("tenant-a", "party", "enc-p1");

        PayloadProtectionResult result = await protectionService.ProtectEventPayloadAsync(
            identity, payload, typeof(PartyCreated).FullName!, serialized, "json");

        // Verify encryption markers
        result.SerializationFormat.ShouldBe("json+pdenc-v1");
        JsonNode? root = JsonNode.Parse(result.PayloadBytes);
        root.ShouldNotBeNull();

        // PersonDetails fields should have $enc markers
        JsonNode? firstName = root["personDetails"]?["firstName"];
        firstName.ShouldNotBeNull();
        firstName["$enc"]?.GetValue<bool>().ShouldBe(true);
        firstName["alg"]?.GetValue<string>().ShouldBe("AES256GCM");
        firstName["kv"].ShouldNotBeNull();
        firstName["n"].ShouldNotBeNull();
        firstName["t"].ShouldNotBeNull();
        firstName["c"].ShouldNotBeNull();

        // Plaintext should not be in the output
        string protectedJson = Encoding.UTF8.GetString(result.PayloadBytes);
        protectedJson.ShouldNotContain("Ada");
        protectedJson.ShouldNotContain("Lovelace");
    }

    // ─── Task 7.2: Encrypted events decrypt correctly at publish time ───

    [Fact]
    public async Task CreateParty_PersonWithChannels_PublishedEventsDecrypted()
    {
        IEventPayloadProtectionService protectionService = _factory.Services.GetRequiredService<IEventPayloadProtectionService>();

        // Ensure key exists by creating the party first
        await EnsurePartyKeyExistsAsync(protectionService, "tenant-a", "enc-p2");

        ContactChannelAdded payload = new()
        {
            ContactChannelId = "cc-int-1",
            Type = ContactChannelType.Email,
            Value = "ada@example.com",
            IsPreferred = true,
        };

        byte[] serialized = JsonSerializer.SerializeToUtf8Bytes(payload, EncryptionTestFactory.SerializerOptions);
        AggregateIdentity identity = new("tenant-a", "party", "enc-p2");

        // Encrypt
        PayloadProtectionResult encrypted = await protectionService.ProtectEventPayloadAsync(
            identity, payload, typeof(ContactChannelAdded).FullName!, serialized, "json");
        encrypted.SerializationFormat.ShouldBe("json+pdenc-v1");

        // Decrypt (simulates publish-time decryption)
        PayloadProtectionResult decrypted = await protectionService.UnprotectEventPayloadAsync(
            identity, typeof(ContactChannelAdded).FullName!, encrypted.PayloadBytes, encrypted.SerializationFormat);

        decrypted.SerializationFormat.ShouldBe("json");
        ContactChannelAdded? roundTrip = JsonSerializer.Deserialize<ContactChannelAdded>(
            decrypted.PayloadBytes, EncryptionTestFactory.SerializerOptions);
        roundTrip.ShouldNotBeNull();
        roundTrip.Value.ShouldBe("ada@example.com");
        roundTrip.ContactChannelId.ShouldBe("cc-int-1");
    }

    // ─── Task 7.3: AddContactChannel encrypts channel value ───

    [Fact]
    public async Task UpdateParty_AddContactChannel_EncryptsChannelValue()
    {
        IEventPayloadProtectionService protectionService = _factory.Services.GetRequiredService<IEventPayloadProtectionService>();

        // Ensure key exists by creating the party first
        await EnsurePartyKeyExistsAsync(protectionService, "tenant-a", "enc-p3");

        ContactChannelAdded payload = new()
        {
            ContactChannelId = "cc-int-2",
            Type = ContactChannelType.Phone,
            Value = "+33612345678",
        };

        byte[] serialized = JsonSerializer.SerializeToUtf8Bytes(payload, EncryptionTestFactory.SerializerOptions);
        AggregateIdentity identity = new("tenant-a", "party", "enc-p3");

        PayloadProtectionResult result = await protectionService.ProtectEventPayloadAsync(
            identity, payload, typeof(ContactChannelAdded).FullName!, serialized, "json");

        result.SerializationFormat.ShouldBe("json+pdenc-v1");
        string protectedJson = Encoding.UTF8.GetString(result.PayloadBytes);
        protectedJson.ShouldNotContain("+33612345678");
        protectedJson.ShouldContain("cc-int-2"); // ID remains plaintext
    }

    // ─── Task 7.4: Circuit breaker blocks publication after key deletion ───

    [Fact]
    public async Task DecryptionFailure_CircuitBreakerActivates_PublicationBlocked()
    {
        IEventPayloadProtectionService protectionService = _factory.Services.GetRequiredService<IEventPayloadProtectionService>();
        IPartyKeyManagementService keyService = _factory.Services.GetRequiredService<IPartyKeyManagementService>();

        // Ensure key exists by creating the party first
        await EnsurePartyKeyExistsAsync(protectionService, "tenant-a", "enc-cb-1");

        // Create and encrypt a payload
        ContactChannelAdded payload = new()
        {
            ContactChannelId = "cc-cb-1",
            Type = ContactChannelType.Email,
            Value = "circuit@example.com",
        };

        AggregateIdentity identity = new("tenant-a", "party", "enc-cb-1");
        byte[] serialized = JsonSerializer.SerializeToUtf8Bytes(payload, EncryptionTestFactory.SerializerOptions);

        PayloadProtectionResult encrypted = await protectionService.ProtectEventPayloadAsync(
            identity, payload, typeof(ContactChannelAdded).FullName!, serialized, "json");

        // Delete the key (simulates crypto-shredding)
        await keyService.DeleteKeyAsync("tenant-a", "enc-cb-1");

        // Get circuit breaker options for threshold
        CryptoShreddingOptions options = _factory.Services
            .GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<CryptoShreddingOptions>>()
            .CurrentValue;

        // Repeatedly attempt decryption to trigger circuit breaker
        for (int i = 0; i < options.CircuitBreakerFailureThreshold; i++)
        {
            await Should.ThrowAsync<Exception>(
                () => protectionService.UnprotectEventPayloadAsync(
                    identity, typeof(ContactChannelAdded).FullName!,
                    encrypted.PayloadBytes, encrypted.SerializationFormat));
        }

        // Next attempt should throw DecryptionCircuitOpenException (circuit is open)
        await Should.ThrowAsync<DecryptionCircuitOpenException>(
            () => protectionService.UnprotectEventPayloadAsync(
                identity, typeof(ContactChannelAdded).FullName!,
                encrypted.PayloadBytes, encrypted.SerializationFormat));
    }

    // ─── Task 7.5: CryptoShredding disabled — no encryption ───

    [Fact]
    public async Task Configuration_CryptoShreddingDisabled_NoEncryption()
    {
        // Create a factory with crypto disabled
        using CryptoDisabledTestFactory disabledFactory = new();

        // Force the factory to create a host (and thus build the DI container)
        _ = disabledFactory.CreateClient();

        IEventPayloadProtectionService protectionService =
            disabledFactory.Services.GetRequiredService<IEventPayloadProtectionService>();

        ContactChannelAdded payload = new()
        {
            ContactChannelId = "cc-dis-1",
            Type = ContactChannelType.Email,
            Value = "disabled@example.com",
        };

        byte[] serialized = JsonSerializer.SerializeToUtf8Bytes(payload, EncryptionTestFactory.SerializerOptions);
        AggregateIdentity identity = new("tenant-a", "party", "enc-dis-1");

        PayloadProtectionResult result = await protectionService.ProtectEventPayloadAsync(
            identity, payload, typeof(ContactChannelAdded).FullName!, serialized, "json");

        // Should pass through without encryption
        result.SerializationFormat.ShouldBe("json");
        string json = Encoding.UTF8.GetString(result.PayloadBytes);
        json.ShouldContain("disabled@example.com");
    }

    // ─── Task 7.6: Mixed-format store — plaintext then encrypted, both readable ───

    [Fact]
    public async Task MixedFormatStore_PlaintextThenEncrypted_BothReadable()
    {
        IEventPayloadProtectionService protectionService = _factory.Services.GetRequiredService<IEventPayloadProtectionService>();

        ContactChannelAdded payload = new()
        {
            ContactChannelId = "cc-mix-1",
            Type = ContactChannelType.Email,
            Value = "mixed@example.com",
        };

        AggregateIdentity identity = new("tenant-a", "party", "enc-mix-1");

        // Ensure key exists by creating the party first
        await EnsurePartyKeyExistsAsync(protectionService, "tenant-a", "enc-mix-1");

        // Simulate a plaintext event (stored before encryption was enabled)
        byte[] plaintextBytes = JsonSerializer.SerializeToUtf8Bytes(payload, EncryptionTestFactory.SerializerOptions);

        // Simulate an encrypted event (stored after encryption was enabled)
        PayloadProtectionResult encrypted = await protectionService.ProtectEventPayloadAsync(
            identity, payload, typeof(ContactChannelAdded).FullName!, plaintextBytes, "json");

        // Unprotect the plaintext event (should pass through)
        PayloadProtectionResult unprotectedPlaintext = await protectionService.UnprotectEventPayloadAsync(
            identity, typeof(ContactChannelAdded).FullName!, plaintextBytes, "json");
        unprotectedPlaintext.SerializationFormat.ShouldBe("json");

        // Unprotect the encrypted event (should decrypt)
        PayloadProtectionResult unprotectedEncrypted = await protectionService.UnprotectEventPayloadAsync(
            identity, typeof(ContactChannelAdded).FullName!, encrypted.PayloadBytes, encrypted.SerializationFormat);
        unprotectedEncrypted.SerializationFormat.ShouldBe("json");

        // Both should produce the same readable data
        ContactChannelAdded? fromPlaintext = JsonSerializer.Deserialize<ContactChannelAdded>(
            unprotectedPlaintext.PayloadBytes, EncryptionTestFactory.SerializerOptions);
        ContactChannelAdded? fromEncrypted = JsonSerializer.Deserialize<ContactChannelAdded>(
            unprotectedEncrypted.PayloadBytes, EncryptionTestFactory.SerializerOptions);

        fromPlaintext.ShouldNotBeNull();
        fromEncrypted.ShouldNotBeNull();
        fromPlaintext.Value.ShouldBe("mixed@example.com");
        fromEncrypted.Value.ShouldBe("mixed@example.com");
    }
}

public sealed class EncryptionTestFactory : WebApplicationFactory<Program>
{
    internal static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ConfigureTestHost(builder, cryptoEnabled: true);
    }

    internal static void ConfigureTestHost(IWebHostBuilder builder, bool cryptoEnabled)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:JwtBearer:Issuer"] = "hexalith-dev",
                ["Authentication:JwtBearer:Audience"] = "hexalith-parties",
                ["Authentication:JwtBearer:SigningKey"] = "DevOnlySigningKey-AtLeast32Chars-MustBeSecure!",
                ["Authentication:JwtBearer:RequireHttpsMetadata"] = "false",
                ["Parties:CryptoShredding:IsEnabled"] = cryptoEnabled.ToString(),
            });
        });

        IActorProxyFactory proxyFactory = Substitute.For<IActorProxyFactory>();
        proxyFactory.CreateActorProxy<IPartyDetailProjectionActor>(
            Arg.Any<ActorId>(), Arg.Any<string>(), Arg.Any<ActorProxyOptions?>())
            .Returns(callInfo =>
            {
                IPartyDetailProjectionActor detailProxy = Substitute.For<IPartyDetailProjectionActor>();
                detailProxy.GetDetailAsync().Returns(Task.FromResult<PartyDetail?>(null));
                return detailProxy;
            });

        IPartyIndexProjectionActor indexProxy = Substitute.For<IPartyIndexProjectionActor>();
        indexProxy.GetEntriesAsync().Returns(Task.FromResult<IReadOnlyDictionary<string, PartyIndexEntry>>(
            new Dictionary<string, PartyIndexEntry>()));
        proxyFactory.CreateActorProxy<IPartyIndexProjectionActor>(
            Arg.Any<ActorId>(), Arg.Any<string>(), Arg.Any<ActorProxyOptions?>())
            .Returns(indexProxy);

        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.CheckHealthAsync(Arg.Any<CancellationToken>()).Returns(true);
        daprClient.GetStateAsync<string?>(
            Arg.Any<string>(), Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns((string?)null);
        daprClient.GetMetadataAsync(Arg.Any<CancellationToken>())
            .Returns(new DaprMetadata(
                id: "test",
                actors: [],
                extended: new Dictionary<string, string>(),
                components: [new DaprComponentsMetadata("pubsub", "pubsub.redis", "v1", [])]));

        // Configure audit state mock (ETag-based operations for KeyOperationAuditService)
        daprClient.GetStateAndETagAsync<List<KeyOperationAuditEntry>>(
            Arg.Any<string>(), Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns((null as List<KeyOperationAuditEntry>, ""));
        daprClient.TrySaveStateAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<List<KeyOperationAuditEntry>>(),
            Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns(true);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ICommandRouter>();
            services.AddSingleton<ICommandRouter>(Substitute.For<ICommandRouter>());
            services.RemoveAll<IActorProxyFactory>();
            services.AddSingleton(proxyFactory);
            services.RemoveAll<DaprClient>();
            services.AddSingleton(daprClient);
        });
    }
}

public sealed class CryptoDisabledTestFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        EncryptionTestFactory.ConfigureTestHost(builder, cryptoEnabled: false);
    }
}

#pragma warning restore CA2007
#pragma warning restore CS8620
