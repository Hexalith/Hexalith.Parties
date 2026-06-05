using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Projections.Services;

using Microsoft.Extensions.Logging;

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.Tests.Projections;

public sealed class ProjectionRebuildServiceTests
{
    [Fact]
    public async Task ReadAggregateEventsAsync_ValidMetadataAndEvents_ReturnsEventsInOrderAsync()
    {
        var metadata = new { currentSequence = 2L, lastModified = DateTimeOffset.UtcNow };
        PartyCreated evt1 = new()
        {
            Type = PartyType.Person,
            PersonDetails = new PersonDetails
            {
                FirstName = "Alice",
                LastName = "Smith",
            },
        };
        PartyDisplayNameDerived evt2 = new()
        {
            DisplayName = "Alice Smith",
            SortName = "Smith, Alice",
        };

        var envelope1 = CreateEnvelope("party-001", 1, evt1);
        var envelope2 = CreateEnvelope("party-001", 2, evt2);

        MockHttpMessageHandler handler = new();
        handler.AddResponse(
            BuildActorStateUrl("AggregateActor", "test-tenant:party:party-001", "test-tenant:party:party-001:metadata"),
            JsonSerializer.Serialize(metadata));
        handler.AddResponse(
            BuildActorStateUrl("AggregateActor", "test-tenant:party:party-001", "test-tenant:party:party-001:events:1"),
            JsonSerializer.Serialize(envelope1));
        handler.AddResponse(
            BuildActorStateUrl("AggregateActor", "test-tenant:party:party-001", "test-tenant:party:party-001:events:2"),
            JsonSerializer.Serialize(envelope2));

        ProjectionRebuildService sut = CreateService(handler);

        IReadOnlyList<IEventPayload> events = await sut.ReadAggregateEventsAsync("test-tenant", "party-001", CancellationToken.None);

        events.Count.ShouldBe(2);
        events[0].ShouldBeOfType<PartyCreated>();
        events[1].ShouldBeOfType<PartyDisplayNameDerived>();
    }

    [Fact]
    public async Task ReadAggregateEventsAsync_NoMetadata_ReturnsEmptyAsync()
    {
        MockHttpMessageHandler handler = new();
        handler.AddResponse(
            BuildActorStateUrl("AggregateActor", "test-tenant:party:party-001", "test-tenant:party:party-001:metadata"),
            null,
            HttpStatusCode.NotFound);

        ProjectionRebuildService sut = CreateService(handler);

        IReadOnlyList<IEventPayload> events = await sut.ReadAggregateEventsAsync("test-tenant", "party-001", CancellationToken.None);

        events.ShouldBeEmpty();
    }

    [Fact]
    public async Task ReadAggregateEventsAsync_MissingEvent_SkipsAndContinuesAsync()
    {
        var metadata = new { currentSequence = 2L, lastModified = DateTimeOffset.UtcNow };
        PartyCreated evt1 = new()
        {
            Type = PartyType.Person,
            PersonDetails = new PersonDetails
            {
                FirstName = "Bob",
                LastName = "Jones",
            },
        };
        var envelope1 = CreateEnvelope("party-002", 1, evt1);

        MockHttpMessageHandler handler = new();
        handler.AddResponse(
            BuildActorStateUrl("AggregateActor", "test-tenant:party:party-002", "test-tenant:party:party-002:metadata"),
            JsonSerializer.Serialize(metadata));
        handler.AddResponse(
            BuildActorStateUrl("AggregateActor", "test-tenant:party:party-002", "test-tenant:party:party-002:events:1"),
            JsonSerializer.Serialize(envelope1));
        handler.AddResponse(
            BuildActorStateUrl("AggregateActor", "test-tenant:party:party-002", "test-tenant:party:party-002:events:2"),
            null,
            HttpStatusCode.NotFound);

        ProjectionRebuildService sut = CreateService(handler);

        IReadOnlyList<IEventPayload> events = await sut.ReadAggregateEventsAsync("test-tenant", "party-002", CancellationToken.None);

        events.Count.ShouldBe(1);
        events[0].ShouldBeOfType<PartyCreated>();
    }

    [Fact]
    public async Task RebuildDetailProjectionAsync_CheckpointExists_ResumesFromNextSequenceAsync()
    {
        var metadata = new { currentSequence = 2L, lastModified = DateTimeOffset.UtcNow };
        PartyDetail existingDetail = new()
        {
            Id = "party-001",
            Type = PartyType.Person,
            DisplayName = "Alice",
            SortName = "Smith, Alice",
            IsActive = true,
            PersonDetails = new PersonDetails
            {
                FirstName = "Alice",
                LastName = "Smith",
            },
            CreatedAt = DateTimeOffset.UtcNow,
            LastModifiedAt = DateTimeOffset.UtcNow,
        };
        PartyDisplayNameDerived evt2 = new()
        {
            DisplayName = "Alice Smith",
            SortName = "Smith, Alice",
        };
        var envelope2 = CreateEnvelope("party-001", 2, evt2);

        MockHttpMessageHandler handler = new();
        handler.AddResponse(
            BuildActorStateUrl("PartyIndexProjectionActor", "test-tenant:party-index", "test-tenant:rebuild-checkpoint:detail:party-001"),
            JsonSerializer.Serialize(new { partyId = "party-001", sequenceNumber = 1L }));
        handler.AddResponse(
            BuildActorStateUrl("PartyDetailProjectionActor", "test-tenant:party-detail:party-001", "test-tenant:party-detail:party-001"),
            JsonSerializer.Serialize(existingDetail));
        handler.AddResponse(
            BuildActorStateUrl("AggregateActor", "test-tenant:party:party-001", "test-tenant:party:party-001:metadata"),
            JsonSerializer.Serialize(metadata));
        handler.AddResponse(
            BuildActorStateUrl("AggregateActor", "test-tenant:party:party-001", "test-tenant:party:party-001:events:2"),
            JsonSerializer.Serialize(envelope2));
        handler.AddResponse(
            BuildActorStateRootUrl("PartyDetailProjectionActor", "test-tenant:party-detail:party-001"),
            null,
            HttpStatusCode.NoContent,
            HttpMethod.Put);
        handler.AddResponse(
            BuildActorStateRootUrl("PartyIndexProjectionActor", "test-tenant:party-index"),
            null,
            HttpStatusCode.NoContent,
            HttpMethod.Put);

        ProjectionRebuildService sut = CreateService(handler);

        await sut.RebuildDetailProjectionAsync("test-tenant", "party-001", CancellationToken.None);

        handler.Requests.ShouldContain(request => request.Method == HttpMethod.Get && request.Path == BuildActorStateUrl(
            "AggregateActor",
            "test-tenant:party:party-001",
            "test-tenant:party:party-001:events:2"));
        handler.Requests.ShouldNotContain(request => request.Method == HttpMethod.Get && request.Path == BuildActorStateUrl(
            "AggregateActor",
            "test-tenant:party:party-001",
            "test-tenant:party:party-001:events:1"));
    }

    [Fact]
    public async Task RebuildIndexProjectionAsync_WhenIndexStateMissing_UsesManifestFallbackAsync()
    {
        var metadata = new { currentSequence = 1L, lastModified = DateTimeOffset.UtcNow };
        PartyCreated evt1 = new()
        {
            Type = PartyType.Person,
            PersonDetails = new PersonDetails
            {
                FirstName = "Bob",
                LastName = "Jones",
            },
        };
        var envelope1 = CreateEnvelope("party-002", 1, evt1);

        MockHttpMessageHandler handler = new();
        handler.AddResponse(
            BuildActorStateUrl("PartyIndexProjectionActor", "test-tenant:party-index", "test-tenant:rebuild-checkpoint:index"),
            null,
            HttpStatusCode.NotFound);
        handler.AddResponse(
            BuildActorStateUrl("PartyIndexProjectionActor", "test-tenant:party-index", "test-tenant:party-index:all"),
            null,
            HttpStatusCode.NotFound);
        handler.AddResponse(
            BuildActorStateUrl("PartyIndexProjectionActor", "test-tenant:party-index", "test-tenant:party-index:manifest"),
            JsonSerializer.Serialize(new[] { "party-002" }));
        handler.AddResponse(
            BuildActorStateUrl("AggregateActor", "test-tenant:party:party-002", "test-tenant:party:party-002:metadata"),
            JsonSerializer.Serialize(metadata));
        handler.AddResponse(
            BuildActorStateUrl("AggregateActor", "test-tenant:party:party-002", "test-tenant:party:party-002:events:1"),
            JsonSerializer.Serialize(envelope1));
        handler.AddResponse(
            BuildActorStateRootUrl("PartyIndexProjectionActor", "test-tenant:party-index"),
            null,
            HttpStatusCode.NoContent,
            HttpMethod.Put);

        ProjectionRebuildService sut = CreateService(handler);

        await sut.RebuildIndexProjectionAsync("test-tenant", CancellationToken.None);

        handler.Requests.ShouldContain(request => request.Method == HttpMethod.Get && request.Path == BuildActorStateUrl(
            "PartyIndexProjectionActor",
            "test-tenant:party-index",
            "test-tenant:party-index:manifest"));
        handler.Requests.ShouldContain(request => request.Method == HttpMethod.Put && request.Path == BuildActorStateRootUrl(
            "PartyIndexProjectionActor",
            "test-tenant:party-index"));
    }

    [Fact]
    public async Task RebuildIndexProjectionAsync_MissingIndexAndManifest_FailsClosedWithoutWritingAsync()
    {
        MockHttpMessageHandler handler = new();
        handler.AddResponse(
            BuildActorStateUrl("PartyIndexProjectionActor", "test-tenant:party-index", "test-tenant:rebuild-checkpoint:index"),
            null,
            HttpStatusCode.NotFound);
        handler.AddResponse(
            BuildActorStateUrl("PartyIndexProjectionActor", "test-tenant:party-index", "test-tenant:party-index:all"),
            null,
            HttpStatusCode.NotFound);
        handler.AddResponse(
            BuildActorStateUrl("PartyIndexProjectionActor", "test-tenant:party-index", "test-tenant:party-index:manifest"),
            null,
            HttpStatusCode.NotFound);

        ProjectionRebuildService sut = CreateService(handler);

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => sut.RebuildIndexProjectionAsync("test-tenant", CancellationToken.None));

        exception.Message.ShouldBe("Projection rebuild cannot enumerate trusted party ids.");
        handler.Requests.ShouldNotContain(request => request.Method == HttpMethod.Put);
    }

    [Fact]
    public async Task RebuildDetailProjectionAsync_TenantRebuildWithoutTrustedPartyIds_FailsClosedWithoutWritingAsync()
    {
        MockHttpMessageHandler handler = new();
        handler.AddResponse(
            BuildActorStateUrl("PartyIndexProjectionActor", "test-tenant:party-index", "test-tenant:rebuild-checkpoint:detail"),
            null,
            HttpStatusCode.NotFound);
        handler.AddResponse(
            BuildActorStateUrl("PartyIndexProjectionActor", "test-tenant:party-index", "test-tenant:party-index:all"),
            null,
            HttpStatusCode.NotFound);
        handler.AddResponse(
            BuildActorStateUrl("PartyIndexProjectionActor", "test-tenant:party-index", "test-tenant:party-index:manifest"),
            null,
            HttpStatusCode.NotFound);

        ProjectionRebuildService sut = CreateService(handler);

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => sut.RebuildDetailProjectionAsync("test-tenant", null, CancellationToken.None));

        exception.Message.ShouldBe("Projection rebuild cannot enumerate trusted party ids.");
        handler.Requests.ShouldNotContain(request => request.Method == HttpMethod.Put);
    }

    [Fact]
    public async Task ReadAggregateEventsAsync_UnknownEventType_SkipsPayloadAsync()
    {
        var metadata = new { currentSequence = 1L, lastModified = DateTimeOffset.UtcNow };
        var envelope = new
        {
            aggregateId = "party-unknown",
            tenantId = "test-tenant",
            domain = "party",
            sequenceNumber = 1L,
            eventTypeName = "Hexalith.Parties.Contracts.Events.UnknownPartyEvent",
            serializationFormat = "json",
            payload = JsonSerializer.SerializeToUtf8Bytes(new { value = "ignored" }),
        };

        MockHttpMessageHandler handler = new();
        handler.AddResponse(
            BuildActorStateUrl("AggregateActor", "test-tenant:party:party-unknown", "test-tenant:party:party-unknown:metadata"),
            JsonSerializer.Serialize(metadata));
        handler.AddResponse(
            BuildActorStateUrl("AggregateActor", "test-tenant:party:party-unknown", "test-tenant:party:party-unknown:events:1"),
            JsonSerializer.Serialize(envelope));

        ProjectionRebuildService sut = CreateService(handler);

        IReadOnlyList<IEventPayload> events = await sut.ReadAggregateEventsAsync("test-tenant", "party-unknown", CancellationToken.None);

        events.ShouldBeEmpty();
    }

    [Fact]
    public async Task ReadAggregateEventsAsync_ErasedEncryptedPayload_SkipsPayloadAsync()
    {
        var metadata = new { currentSequence = 1L, lastModified = DateTimeOffset.UtcNow };
        PartyCreated evt = new()
        {
            Type = PartyType.Person,
            PersonDetails = new PersonDetails
            {
                FirstName = "Erased",
                LastName = "Party",
            },
        };
        var envelope = CreateEnvelope("party-erased", 1, evt, "json+pdenc-v1");
        IEventPayloadProtectionService protectionService = Substitute.For<IEventPayloadProtectionService>();
        protectionService.UnprotectEventPayloadAsync(
                Arg.Any<AggregateIdentity>(),
                Arg.Any<string>(),
                Arg.Any<byte[]>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<PayloadProtectionResult>(new InvalidOperationException("key unavailable")));

        MockHttpMessageHandler handler = new();
        handler.AddResponse(
            BuildActorStateUrl("AggregateActor", "test-tenant:party:party-erased", "test-tenant:party:party-erased:metadata"),
            JsonSerializer.Serialize(metadata));
        handler.AddResponse(
            BuildActorStateUrl("AggregateActor", "test-tenant:party:party-erased", "test-tenant:party:party-erased:events:1"),
            JsonSerializer.Serialize(envelope));

        ProjectionRebuildService sut = CreateService(handler, protectionService);

        IReadOnlyList<IEventPayload> events = await sut.ReadAggregateEventsAsync("test-tenant", "party-erased", CancellationToken.None);

        events.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetProcessingRecordsAsync_ReturnsBoundedAuditMetadataWithoutPayloadTextAsync()
    {
        var metadata = new { currentSequence = 2L, lastModified = DateTimeOffset.UtcNow };
        ProcessingRestricted restricted = new()
        {
            PartyId = "party-audit",
            TenantId = "test-tenant",
            RestrictedAt = DateTimeOffset.Parse("2026-05-04T00:00:00Z"),
            Reason = "Ada Lovelace complaint contains ada@example.test",
            RestrictedBy = "dpo-user",
            CorrelationId = "corr-restrict",
        };
        ConsentRecorded consent = new()
        {
            PartyId = "party-audit",
            TenantId = "test-tenant",
            ConsentId = "email-ada@example.test:marketing",
            ChannelId = "email-ada@example.test",
            Purpose = "marketing-Ada-Lovelace",
            LawfulBasis = LawfulBasis.Consent,
            GrantedAt = DateTimeOffset.Parse("2026-05-03T00:00:00Z"),
            GrantedBy = "dpo-user",
        };
        var envelope1 = CreateEnvelope("party-audit", 1, consent, userId: "dpo-user", correlationId: "corr-consent");
        var envelope2 = CreateEnvelope("party-audit", 2, restricted, userId: "dpo-user", correlationId: "corr-restrict");

        MockHttpMessageHandler handler = new();
        handler.AddResponse(
            BuildActorStateUrl("AggregateActor", "test-tenant:party:party-audit", "test-tenant:party:party-audit:metadata"),
            JsonSerializer.Serialize(metadata));
        handler.AddResponse(
            BuildActorStateUrl("AggregateActor", "test-tenant:party:party-audit", "test-tenant:party:party-audit:events:1"),
            JsonSerializer.Serialize(envelope1));
        handler.AddResponse(
            BuildActorStateUrl("AggregateActor", "test-tenant:party:party-audit", "test-tenant:party:party-audit:events:2"),
            JsonSerializer.Serialize(envelope2));

        ProjectionRebuildService sut = CreateService(handler);

        IReadOnlyList<ProcessingActivityRecord> records = await sut.GetProcessingRecordsAsync("test-tenant", "party-audit", CancellationToken.None);

        records.Count.ShouldBe(2);
        records[0].ShouldSatisfyAllConditions(
            r => r.PartyId.ShouldBe("party-audit"),
            r => r.TenantId.ShouldBe("test-tenant"),
            r => r.ActorId.ShouldBe("dpo-user"),
            r => r.CorrelationId.ShouldBe("corr-consent"),
            r => r.OperationCategory.ShouldBe("Consent"),
            r => r.Outcome.ShouldBe("Succeeded"),
            r => r.Summary.ShouldBe("Consent recorded."));
        records[1].OperationCategory.ShouldBe("Restriction");
        records[1].Summary.ShouldBe("Processing restricted.");

        string serialized = JsonSerializer.Serialize(records);
        serialized.ShouldNotContain("Ada", Case.Insensitive);
        serialized.ShouldNotContain("ada@example.test", Case.Insensitive);
        serialized.ShouldNotContain("marketing-Ada-Lovelace", Case.Insensitive);
    }

    [Fact]
    public async Task RebuildIndexProjectionAsync_CanceledBeforeStateAccess_DoesNotWriteAsync()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        MockHttpMessageHandler handler = new();
        ProjectionRebuildService sut = CreateService(handler);

        await Should.ThrowAsync<OperationCanceledException>(
            () => sut.RebuildIndexProjectionAsync("test-tenant", cts.Token));

        handler.Requests.ShouldNotContain(request => request.Method == HttpMethod.Put);
    }

    [Fact]
    public async Task RebuildDetailProjectionAsync_StateWriteFailure_StopsBeforeCheckpointWriteAsync()
    {
        var metadata = new { currentSequence = 1L, lastModified = DateTimeOffset.UtcNow };
        PartyCreated evt = new()
        {
            Type = PartyType.Person,
            PersonDetails = new PersonDetails
            {
                FirstName = "Write",
                LastName = "Failure",
            },
        };
        var envelope = CreateEnvelope("party-write-failure", 1, evt);

        MockHttpMessageHandler handler = new();
        handler.AddResponse(
            BuildActorStateUrl("PartyIndexProjectionActor", "test-tenant:party-index", "test-tenant:rebuild-checkpoint:detail:party-write-failure"),
            null,
            HttpStatusCode.NotFound);
        handler.AddResponse(
            BuildActorStateUrl("AggregateActor", "test-tenant:party:party-write-failure", "test-tenant:party:party-write-failure:metadata"),
            JsonSerializer.Serialize(metadata));
        handler.AddResponse(
            BuildActorStateUrl("AggregateActor", "test-tenant:party:party-write-failure", "test-tenant:party:party-write-failure:events:1"),
            JsonSerializer.Serialize(envelope));
        handler.AddResponse(
            BuildActorStateRootUrl("PartyDetailProjectionActor", "test-tenant:party-detail:party-write-failure"),
            null,
            HttpStatusCode.InternalServerError,
            HttpMethod.Put);

        ProjectionRebuildService sut = CreateService(handler);

        await Should.ThrowAsync<HttpRequestException>(
            () => sut.RebuildDetailProjectionAsync("test-tenant", "party-write-failure", CancellationToken.None));

        handler.Requests.ShouldNotContain(request =>
            request.Method == HttpMethod.Put
            && request.Path == BuildActorStateRootUrl("PartyIndexProjectionActor", "test-tenant:party-index"));
    }

    private static object CreateEnvelope(
        string aggregateId,
        long seq,
        IEventPayload payload,
        string serializationFormat = "json",
        string userId = "system",
        string correlationId = "corr-test")
    {
        string typeName = payload.GetType().FullName!;
        byte[] payloadBytes = JsonSerializer.SerializeToUtf8Bytes(payload, payload.GetType(), new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });

        return new
        {
            aggregateId,
            tenantId = "test-tenant",
            domain = "party",
            sequenceNumber = seq,
            eventTypeName = typeName,
            serializationFormat,
            payload = payloadBytes,
            userId,
            correlationId,
            causationId = correlationId,
        };
    }

    private static string BuildActorStateUrl(string actorType, string actorId, string stateKey)
    {
        return $"/v1.0/actors/{actorType}/{Uri.EscapeDataString(actorId)}/state/{Uri.EscapeDataString(stateKey)}";
    }

    private static string BuildActorStateRootUrl(string actorType, string actorId)
    {
        return $"/v1.0/actors/{actorType}/{Uri.EscapeDataString(actorId)}/state";
    }

    private static ProjectionRebuildService CreateService(
        MockHttpMessageHandler handler,
        IEventPayloadProtectionService? protectionService = null)
    {
        HttpClient httpClient = new(handler) { BaseAddress = new Uri("http://localhost:3500") };
        ILogger<ProjectionRebuildService> logger = Substitute.For<ILogger<ProjectionRebuildService>>();
        if (protectionService is null)
        {
            protectionService = Substitute.For<IEventPayloadProtectionService>();
            protectionService.UnprotectEventPayloadAsync(
                Arg.Any<AggregateIdentity>(),
                Arg.Any<string>(),
                Arg.Any<byte[]>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    if (callInfo[2] is not byte[] payloadBytes || callInfo[3] is not string serializationFormat)
                    {
                        throw new InvalidOperationException("Expected serialized payload bytes and format.");
                    }

                    return Task.FromResult(new PayloadProtectionResult(payloadBytes, serializationFormat));
                });
        }

        return new ProjectionRebuildService(httpClient, protectionService, logger);
    }

    internal sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, (string? Content, HttpStatusCode StatusCode)> _responses = new(StringComparer.OrdinalIgnoreCase);

        public List<(HttpMethod Method, string Path, string? Body)> Requests { get; } = [];

        public void AddResponse(string url, string? content, HttpStatusCode statusCode = HttpStatusCode.OK, HttpMethod? method = null)
        {
            _responses[$"{(method ?? HttpMethod.Get).Method}:{url}"] = (content, statusCode);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string path = request.RequestUri?.PathAndQuery ?? string.Empty;
            string? body = request.Content is null
                ? null
                : request.Content.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();
            Requests.Add((request.Method, path, body));

            if (_responses.TryGetValue($"{request.Method.Method}:{path}", out (string? Content, HttpStatusCode StatusCode) response))
            {
                HttpResponseMessage httpResponse = new(response.StatusCode);
                if (response.Content is not null)
                {
                    httpResponse.Content = new StringContent(response.Content, System.Text.Encoding.UTF8, "application/json");
                }

                return Task.FromResult(httpResponse);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
