using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Hexalith.Commons.UniqueIds;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Events;
using Hexalith.Parties.Contracts.Authorization;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.IntegrationTests.HealthChecks;
using Hexalith.Parties.Queries;

using Microsoft.IdentityModel.Tokens;

using Shouldly;

namespace Hexalith.Parties.IntegrationTests.Gateway;

/// <summary>Proves authenticated projection output and exact persisted actor bytes in the seeded mTLS profile.</summary>
[Trait("Category", "E2E")]
[Trait("Tier", "3")]
[Collection("Non-parallel")]
public sealed class EventStoreGatewayE2ETests
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };
    [Fact]
    public async Task AuthenticatedCreate_ProjectsDetailsAndPersistsMatchingEventEnvelopesAsync()
    {
        RequireRuntimeProfile();
        var fixture = new PartiesAspireTopologyFixture(completeMtlsProfile: true,
            isolatedIdentities: string.Equals(Environment.GetEnvironmentVariable("PARTIES_MTLS_ISOLATED_IDENTITIES"), "true", StringComparison.OrdinalIgnoreCase));
        await using var lifetime = fixture.ConfigureAwait(true);
        await fixture.InitializeAsync(TestContext.Current.CancellationToken);
        fixture.IsAvailable.ShouldBeTrue(fixture.UnavailableReason);
        Console.WriteLine($"Runtime identity profile: namespace={fixture.Session.Namespace}; suffix={fixture.Session.AppIdSuffix}");
        await CreateAndVerifyAsync(fixture, UniqueIdHelper.GenerateSortableUniqueStringId(), "Synthetic first", TestContext.Current.CancellationToken);
    }

    /// <summary>Requires the real runtime; an opt-in failure must never turn into a qualifying skip.</summary>
    internal static void RequireRuntimeProfile()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("PARTIES_MTLS_TOPOLOGY_TESTS"), "true", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Skip("Set PARTIES_MTLS_TOPOLOGY_TESTS=true to require seeded complete mTLS runtime proof.");
        }
    }

    /// <summary>Creates a synthetic party and asserts the query and persisted bytes independently of status.</summary>
    internal static async Task CreateAndVerifyAsync(PartiesAspireTopologyFixture fixture, string partyId, string firstName, CancellationToken cancellationToken)
    {
        string commandId = UniqueIdHelper.GenerateSortableUniqueStringId();
        var identity = new AggregateIdentity(PartiesTenantBootstrapHost.TenantId, "party", partyId);
        using var client = new HttpClient { BaseAddress = fixture.EventStoreClient.BaseAddress, Timeout = TimeSpan.FromSeconds(60) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateToken());
        var command = new CreatePartyComposite
        {
            PartyId = partyId, Type = PartyType.Person,
            PersonDetails = new PersonDetails { FirstName = firstName, LastName = "Synthetic last" },
        };
        using HttpResponseMessage submit = await client.PostAsJsonAsync("/api/v1/commands", new
        {
            messageId = commandId, correlationId = commandId, tenant = identity.TenantId, domain = identity.Domain,
            aggregateId = partyId, commandType = typeof(CreatePartyComposite).FullName, payload = command,
        }, cancellationToken).ConfigureAwait(false);
        submit.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        using JsonDocument status = await PollStatusAsync(client, commandId, cancellationToken).ConfigureAwait(false);
        status.RootElement.GetProperty("status").GetString().ShouldBe("Completed");
        status.RootElement.GetProperty("aggregateId").GetString().ShouldBe(partyId);

        PartyDetail detail = await PollProjectionAsync(client, partyId, firstName, cancellationToken).ConfigureAwait(false);
        detail.Id.ShouldBe(partyId);
        detail.Type.ShouldBe(PartyType.Person);
        detail.PersonDetails.ShouldNotBeNull().FirstName.ShouldBe(firstName);
        detail.PersonDetails.LastName.ShouldBe("Synthetic last");
        detail.IsActive.ShouldBeTrue();
        detail.CreatedAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddMinutes(-5));
        detail.Freshness.ShouldNotBeNull().Status.ShouldBe(ProjectionFreshnessStatus.Current);

        (AggregateMetadata metadata, EventEnvelope[] events) = await fixture.ReadStoredStreamAsync(identity, cancellationToken).ConfigureAwait(false);
        metadata.CurrentSequence.ShouldBe(events.LongLength);
        events.ShouldNotBeEmpty();
        for (int index = 0; index < events.Length; index++)
        {
            EventEnvelope envelope = events[index];
            envelope.Identity.ShouldBe(identity);
            envelope.SequenceNumber.ShouldBe(index + 1);
            envelope.CorrelationId.ShouldBe(commandId);
            envelope.CausationId.ShouldBe(commandId);
            envelope.UserId.ShouldBe(PartiesTenantBootstrapHost.UserId);
            envelope.SerializationFormat.ShouldBe("json");
        }
        EventEnvelope createdEnvelope = events.Single(envelope => envelope.EventTypeName == typeof(PartyCreated).FullName);
        PartyCreated created = JsonSerializer.Deserialize<PartyCreated>(createdEnvelope.Payload, SerializerOptions)!;
        created.Type.ShouldBe(command.Type);
        created.PersonDetails.ShouldBe(command.PersonDetails);
        created.CreatedAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddMinutes(-5));
        created.CreatedAt.ShouldBeLessThanOrEqualTo(createdEnvelope.Timestamp);
        detail.CreatedAt.ShouldBe(createdEnvelope.Timestamp);
    }

    private static async Task<JsonDocument> PollStatusAsync(HttpClient client, string commandId, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));
        while (true)
        {
            using HttpResponseMessage response = await client.GetAsync($"/api/v1/commands/status/{commandId}", timeout.Token).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                JsonDocument status = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false));
                string? outcome = status.RootElement.GetProperty("status").GetString();
                if (outcome is "Completed" or "Rejected" or "PublishFailed" or "TimedOut") { return status; }
                status.Dispose();
            }
            await Task.Delay(TimeSpan.FromMilliseconds(250), timeout.Token).ConfigureAwait(false);
        }
    }

    private static async Task<PartyDetail> PollProjectionAsync(HttpClient client, string partyId, string firstName, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));
        while (true)
        {
            using HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/queries", new
            {
                tenant = PartiesTenantBootstrapHost.TenantId, domain = "party", aggregateId = partyId,
                queryType = PartyDetailQueryTypes.PartyDetailQueryType, projectionType = PartyDetailQueryTypes.ProjectionType,
                projectionActorType = PartyDetailQueryTypes.ActorTypeName, entityId = partyId,
            }, timeout.Token).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                using JsonDocument document = await response.Content.ReadFromJsonAsync<JsonDocument>(timeout.Token).ConfigureAwait(false)
                    ?? throw new InvalidOperationException("The projected query returned no response.");
                PartyDetail? detail = document.RootElement.GetProperty("payload").Deserialize<PartyDetail>(SerializerOptions);
                if (detail is { Freshness.Status: ProjectionFreshnessStatus.Current }
                    && detail.Id == partyId && detail.PersonDetails?.FirstName == firstName) { return detail; }
            }
            else if (response.StatusCode != HttpStatusCode.NotFound && response.StatusCode != HttpStatusCode.ServiceUnavailable)
            {
                throw new InvalidOperationException($"The authenticated query failed with HTTP {(int)response.StatusCode}.");
            }
            await Task.Delay(TimeSpan.FromMilliseconds(250), timeout.Token).ConfigureAwait(false);
        }
    }

    private static string GenerateToken()
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new(PartiesClaimTypes.Subject, PartiesTenantBootstrapHost.UserId),
                new(PartiesClaimTypes.EventStoreTenant, PartiesTenantBootstrapHost.TenantId),
                new("tenants", JsonSerializer.Serialize(new[] { PartiesTenantBootstrapHost.TenantId })),
                new("domains", JsonSerializer.Serialize(new[] { "party" })),
                new("permissions", JsonSerializer.Serialize(new[] { "commands:*", "command:submit", "query:read" })),
            ]),
            NotBefore = DateTime.UtcNow.AddMinutes(-1), Expires = DateTime.UtcNow.AddMinutes(30), IssuedAt = DateTime.UtcNow,
            Issuer = PartiesAspireTopologyFixture.JwtIssuer, Audience = "hexalith-eventstore",
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(PartiesAspireTopologyFixture.JwtSigningKey)), SecurityAlgorithms.HmacSha256Signature),
        };
        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(descriptor));
    }
}
