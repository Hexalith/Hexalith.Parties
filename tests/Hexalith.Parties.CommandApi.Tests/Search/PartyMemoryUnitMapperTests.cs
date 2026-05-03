using Hexalith.Memories.Contracts.V1;
using Hexalith.Memories.Client.Rest;
using Hexalith.Parties.CommandApi.Search;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.Parties.CommandApi.Tests.Search;

public class PartyMemoryUnitMapperTests
{
    [Fact]
    public void MapsPartyEventToEventSourceMemoryUnitWithTenantCaseAndPartyMetadata()
    {
        PartyIndexEntry entry = CreateEntry();

        PartyMemoryUnit? unit = PartyMemoryUnitMapper.Map(
            entry,
            new PartyMemoryUnitMappingContext(
                TenantId: "tenant-a",
                CaseId: "case-a",
                AggregateId: "party-1",
                EventType: "PartyCreated",
                CorrelationId: "corr-1",
                CausationId: "cause-1",
                SourceService: "Hexalith.Parties",
                Timestamp: DateTimeOffset.Parse("2026-05-02T10:00:00Z")));

        unit.ShouldNotBeNull();
        unit.SourceUri.ShouldBe("urn:hexalith:parties:tenant-a:party:party-1");
        unit.SourceType.ShouldBe(SourceType.Event);
        unit.TenantId.ShouldBe("tenant-a");
        unit.CaseId.ShouldBe("case-a");
        unit.Content.ShouldContain("Jean Dupont");
        unit.Content.ShouldContain("jean@example.com");
        unit.Content.ShouldContain("FR11111111111");
        unit.Metadata["tenantId"].Value.ShouldBe("tenant-a");
        unit.Metadata["partyId"].Value.ShouldBe("party-1");
        unit.Metadata["aggregateId"].Value.ShouldBe("party-1");
        unit.Metadata["eventType"].Value.ShouldBe("PartyCreated");
        unit.Metadata["correlationId"].Value.ShouldBe("corr-1");
        unit.Metadata["causationId"].Value.ShouldBe("cause-1");
        unit.Metadata["sourceService"].Value.ShouldBe("Hexalith.Parties");
        unit.Metadata["partyType"].Value.ShouldBe("Person");
        unit.Metadata["isActive"].Value.ShouldBe("true");
        unit.Metadata["isErased"].Value.ShouldBe("false");
    }

    [Fact]
    public void ErasedPartyContentIsNotMappedForIndexing()
    {
        PartyIndexEntry erased = CreateEntry() with { IsErased = true };

        PartyMemoryUnit? unit = PartyMemoryUnitMapper.Map(
            erased,
            PartyMemoryUnitMappingContext.ForProjection("tenant-a", "case-a"));

        unit.ShouldBeNull();
    }

    [Fact]
    public async Task PartyMemoryIndexingServiceIndexesPartyCreatedDataAndTracksMapping()
    {
        var client = new RecordingMemoriesClient();
        var service = new PartyMemoryIndexingService(client, NullLogger<PartyMemoryIndexingService>.Instance);
        PartyIndexEntry entry = CreateEntry();

        PartyMemoryIndexingResult? result = await service.IndexAsync(
            entry,
            new PartyMemoryUnitMappingContext(
                TenantId: "tenant-a",
                CaseId: "case-a",
                AggregateId: "party-1",
                EventType: "PartyCreated"),
            CancellationToken.None);

        result.ShouldNotBeNull();
        result.PartyId.ShouldBe("party-1");
        result.SourceUri.ShouldBe("urn:hexalith:parties:tenant-a:party:party-1");
        result.WorkflowInstanceId.ShouldBe("workflow-1");
        result.Indexed.ShouldBeTrue();
        result.FailureReason.ShouldBeNull();
        client.LastTenantId.ShouldBe("tenant-a");
        client.LastCaseId.ShouldBe("case-a");
        client.LastSourceUri.ShouldBe(result.SourceUri);
        client.LastContentText.ShouldNotBeNull();
        client.LastContentText.ShouldContain("Jean Dupont");
        client.LastMetadata.ShouldContainKey("partyId");
    }

    [Fact]
    public async Task PartyMemoryIndexingServiceReturnsBlockedResultWhenMemoriesIngestFails()
    {
        var client = new ThrowingMemoriesClient(new HttpRequestException("memories down"));
        var service = new PartyMemoryIndexingService(client, NullLogger<PartyMemoryIndexingService>.Instance);
        PartyIndexEntry entry = CreateEntry();

        PartyMemoryIndexingResult? result = await service.IndexAsync(
            entry,
            new PartyMemoryUnitMappingContext("tenant-a", "case-a", "party-1", "PartyCreated"),
            CancellationToken.None);

        result.ShouldNotBeNull();
        result.Indexed.ShouldBeFalse();
        result.FailureReason.ShouldNotBeNull();
        result.FailureReason.ShouldContain("HttpRequestException");
        result.WorkflowInstanceId.ShouldBeNull();
    }

    [Fact]
    public void InactiveOrErasedPartyIsNotMappedForIndexing()
    {
        PartyIndexEntry inactive = CreateEntry() with { IsActive = false };
        PartyMemoryUnit? unit = PartyMemoryUnitMapper.Map(
            inactive,
            PartyMemoryUnitMappingContext.ForProjection("tenant-a", "case-a"));
        unit.ShouldBeNull();
    }

    private static PartyIndexEntry CreateEntry()
        => new()
        {
            Id = "party-1",
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = "Jean Dupont",
            SearchableContactChannels =
            [
                new ContactChannel
                {
                    Id = "contact-1",
                    Type = ContactChannelType.Email,
                    Value = "jean@example.com",
                    IsPreferred = true,
                },
            ],
            SearchableIdentifiers =
            [
                new PartyIdentifier
                {
                    Id = "identifier-1",
                    Type = IdentifierType.VAT,
                    Value = "FR11111111111",
                },
            ],
            CreatedAt = DateTimeOffset.Parse("2026-05-01T10:00:00Z"),
            LastModifiedAt = DateTimeOffset.Parse("2026-05-02T10:00:00Z"),
            IsErased = false,
        };

    private sealed class RecordingMemoriesClient()
        : MemoriesClient(
            new HttpClient { BaseAddress = new Uri("https://memories.example") },
            Options.Create(new MemoriesClientOptions()),
            NullLogger<MemoriesClient>.Instance)
    {
        public string? LastTenantId { get; private set; }

        public string? LastCaseId { get; private set; }

        public string? LastSourceUri { get; private set; }

        public string? LastContentText { get; private set; }

        public IReadOnlyDictionary<string, MetadataField> LastMetadata { get; private set; } =
            new Dictionary<string, MetadataField>(StringComparer.Ordinal);

#pragma warning disable HXL001
        public override Task<string> IngestAsync(
            string tenantId,
            string caseId,
            string sourceUri,
            byte[] content,
            string contentType,
            string ingestedBy,
            IReadOnlyDictionary<string, MetadataField>? metadata,
            CancellationToken ct)
        {
            LastTenantId = tenantId;
            LastCaseId = caseId;
            LastSourceUri = sourceUri;
            LastContentText = System.Text.Encoding.UTF8.GetString(content);
            LastMetadata = metadata ?? new Dictionary<string, MetadataField>(StringComparer.Ordinal);
            return Task.FromResult("workflow-1");
        }
#pragma warning restore HXL001
    }

    private sealed class ThrowingMemoriesClient(Exception ex)
        : MemoriesClient(
            new HttpClient { BaseAddress = new Uri("https://memories.example") },
            Options.Create(new MemoriesClientOptions()),
            NullLogger<MemoriesClient>.Instance)
    {
#pragma warning disable HXL001
        public override Task<string> IngestAsync(
            string tenantId,
            string caseId,
            string sourceUri,
            byte[] content,
            string contentType,
            string ingestedBy,
            IReadOnlyDictionary<string, MetadataField>? metadata,
            CancellationToken ct)
            => Task.FromException<string>(ex);
#pragma warning restore HXL001
    }
}
