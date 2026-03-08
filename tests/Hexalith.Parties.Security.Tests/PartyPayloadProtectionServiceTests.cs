using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Security;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.Security.Tests;

public sealed class PartyPayloadProtectionServiceTests
{
    private readonly IPartyKeyManagementService _keyManagementService = Substitute.For<IPartyKeyManagementService>();
    private readonly IKeyStorageBackend _keyStorageBackend = Substitute.For<IKeyStorageBackend>();
    private readonly IPartyKeyRetryScheduler _retryScheduler = Substitute.For<IPartyKeyRetryScheduler>();

    private PartyPayloadProtectionService CreateService()
        => new(
            _keyManagementService,
            _keyStorageBackend,
            new PartyKeyLifecycleService(_keyManagementService, _retryScheduler, NullLogger<PartyKeyLifecycleService>.Instance),
            NullLogger<PartyPayloadProtectionService>.Instance);

    [Fact]
    public async Task ProtectEventPayloadAsync_PersonPartyEvent_EncryptsProtectedFieldsAndRoundTrips()
    {
        byte[] keyMaterial = Enumerable.Repeat((byte)7, 32).ToArray();
        _keyStorageBackend.ListKeyVersionsAsync("acme", "p1", Arg.Any<CancellationToken>()).Returns([2]);
        _keyManagementService.GetKeyVersionAsync("acme", "p1", 2, Arg.Any<CancellationToken>())
            .Returns(_ => (byte[])keyMaterial.Clone());

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

        byte[] serialized = JsonSerializer.SerializeToUtf8Bytes(payload);
        AggregateIdentity identity = new("acme", "party", "p1");
        PartyPayloadProtectionService service = CreateService();

        PayloadProtectionResult protectedPayload = await service.ProtectEventPayloadAsync(
            identity,
            payload,
            typeof(PartyCreated).FullName!,
            serialized,
            "json");

        protectedPayload.SerializationFormat.ShouldBe("json+pdenc-v1");
        string protectedJson = Encoding.UTF8.GetString(protectedPayload.PayloadBytes);
        protectedJson.ShouldNotContain("Ada");
        protectedJson.ShouldNotContain("Lovelace");

        PayloadProtectionResult unprotectedPayload = await service.UnprotectEventPayloadAsync(
            identity,
            typeof(PartyCreated).FullName!,
            protectedPayload.PayloadBytes,
            protectedPayload.SerializationFormat);

        unprotectedPayload.SerializationFormat.ShouldBe("json");
        PartyCreated? roundTrip = JsonSerializer.Deserialize<PartyCreated>(unprotectedPayload.PayloadBytes);
        roundTrip.ShouldNotBeNull();
        roundTrip.PersonDetails.ShouldNotBeNull();
        roundTrip.PersonDetails.FirstName.ShouldBe("Ada");
        roundTrip.PersonDetails.LastName.ShouldBe("Lovelace");
        roundTrip.PersonDetails.DateOfBirth.ShouldBe(payload.PersonDetails!.DateOfBirth);
    }

    [Fact]
    public async Task ProtectEventPayloadAsync_OrganizationPartyWithoutNaturalPerson_KeepsEntityFieldsReadable()
    {
        byte[] keyMaterial = Enumerable.Repeat((byte)9, 32).ToArray();
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

        byte[] serialized = JsonSerializer.SerializeToUtf8Bytes(payload);
        PartyPayloadProtectionService service = CreateService();

        PayloadProtectionResult result = await service.ProtectEventPayloadAsync(
            new AggregateIdentity("acme", "party", "org1"),
            payload,
            typeof(PartyCreated).FullName!,
            serialized,
            "json");

        result.SerializationFormat.ShouldBe("json");
        string json = Encoding.UTF8.GetString(result.PayloadBytes);
        json.ShouldContain("Acme Corp");
        await _keyManagementService.Received(1).CreateKeyAsync("acme", "org1", Arg.Any<CancellationToken>());
    }
}