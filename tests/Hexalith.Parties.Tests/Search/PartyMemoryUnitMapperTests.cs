using Hexalith.Memories.Contracts.V1;
using Hexalith.Memories.Client.Rest;
using Hexalith.Parties.Search;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.Parties.Tests.Search;

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
                EventType: "PartyCreated",
                AggregateId: "party-1",
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
        unit.Metadata["partyType"].Value.ShouldBe("person");
        unit.Metadata["isActive"].Value.ShouldBe("true");
        unit.Metadata["isErased"].Value.ShouldBe("false");
    }

    [Fact]
    public void ErasedPartyContentIsNotMappedForIndexing()
    {
        PartyIndexEntry erased = CreateEntry() with { IsErased = true };

        PartyMemoryUnit? unit = PartyMemoryUnitMapper.Map(
            erased,
            PartyMemoryUnitMappingContext.ForProjection("tenant-a", "case-a", "PartyErased"));

        unit.ShouldBeNull();
    }

    [Fact]
    public async Task PartyMemoryIndexingServiceIndexesPartyCreatedDataAndTracksMapping()
    {
        var client = new RecordingMemoriesClient();
        var mappingStore = new RecordingMappingStore();
        var optionsMonitor = CreateMonitor(new PartyMemorySearchOptions
        {
            Enabled = true,
            Endpoint = new Uri("https://memories.example/"),
            CaseId = "case-a",
            TenantId = "tenant-a",
            RequireApiToken = false,
        });
        var service = new PartyMemoryIndexingService(client, mappingStore, optionsMonitor, NullLogger<PartyMemoryIndexingService>.Instance);
        PartyIndexEntry entry = CreateEntry();

        PartyMemoryIndexingResult? result = await service.IndexAsync(
            entry,
            new PartyMemoryUnitMappingContext(
                TenantId: "tenant-a",
                CaseId: "case-a",
                EventType: "PartyCreated",
                AggregateId: "party-1"),
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
        // The indexing service must record the per-party → memory-unit-id mapping so that
        // erasure cleanup can later iterate per-unit DELETEs (AC5 resolved decision #2).
        IReadOnlyList<PartyMemoryUnitMappingEntry> mappings = await mappingStore.GetMappingsAsync("tenant-a", "party-1", CancellationToken.None);
        mappings.Count.ShouldBe(1);
        mappings[0].MemoryUnitId.ShouldBe("workflow-1");
        mappings[0].SourceUri.ShouldBe(result.SourceUri);
    }

    [Fact]
    public async Task PartyMemoryIndexingServiceReturnsBlockedResultWhenMemoriesIngestFails()
    {
        var client = new ThrowingMemoriesClient(new HttpRequestException("memories down"));
        var mappingStore = new RecordingMappingStore();
        var optionsMonitor = CreateMonitor(new PartyMemorySearchOptions
        {
            Enabled = true,
            Endpoint = new Uri("https://memories.example/"),
            CaseId = "case-a",
            TenantId = "tenant-a",
            RequireApiToken = false,
        });
        var service = new PartyMemoryIndexingService(client, mappingStore, optionsMonitor, NullLogger<PartyMemoryIndexingService>.Instance);
        PartyIndexEntry entry = CreateEntry();

        PartyMemoryIndexingResult? result = await service.IndexAsync(
            entry,
            new PartyMemoryUnitMappingContext("tenant-a", "case-a", "PartyCreated", AggregateId: "party-1"),
            CancellationToken.None);

        result.ShouldNotBeNull();
        result.Indexed.ShouldBeFalse();
        result.FailureReason.ShouldNotBeNull();
        result.FailureReason.ShouldContain("HttpRequestException");
        result.WorkflowInstanceId.ShouldBeNull();
    }

    [Fact]
    public void ErasedPartyIsNotMappedForIndexing()
    {
        PartyIndexEntry erased = CreateEntry() with { IsErased = true };
        PartyMemoryUnit? unit = PartyMemoryUnitMapper.Map(
            erased,
            PartyMemoryUnitMappingContext.ForProjection("tenant-a", "case-a", "PartyErased"));
        unit.ShouldBeNull();
    }

    [Fact]
    public void InactivePartyIsMappedWithLifecycleStateInMetadataOnly()
    {
        // AC1 requires indexing the active/erased state — not only active parties.
        // Inactive (deactivated) parties remain searchable when callers pass
        // ActiveFilter=false; the metadata captures the lifecycle so hydration can
        // apply the filter authoritatively. Per P20, the content blob deliberately omits
        // a "State: inactive" line so a literal "inactive" query cannot match an inactive
        // party via semantic embeddings — that filter is applied in hydration only.
        PartyIndexEntry inactive = CreateEntry() with { IsActive = false };
        PartyMemoryUnit? unit = PartyMemoryUnitMapper.Map(
            inactive,
            PartyMemoryUnitMappingContext.ForProjection("tenant-a", "case-a", "PartyDeactivated"));

        unit.ShouldNotBeNull();
        unit.Metadata["isActive"].Value.ShouldBe("false");
        unit.Content.ShouldNotContain("State: inactive");
        unit.Content.ShouldNotContain("State: active");
    }

    [Fact]
    public void DisplayNameWithUnicodeLineSeparatorIsSanitized()
    {
        // P16: line/paragraph-separator chars (`\u2028`, `\u2029`, `\v`, `\f`, NEL) must
        // be neutralized so an attacker cannot smuggle forged structured lines into the
        // content blob. The previous SanitizeLine only replaced `\r\n`/`\r`/`\n`.
        PartyIndexEntry trickyName = CreateEntry() with
        {
            DisplayName = "Alice\u2028Identifier SSN: 999-99-9999",
        };
        PartyMemoryUnit? unit = PartyMemoryUnitMapper.Map(
            trickyName,
            PartyMemoryUnitMappingContext.ForProjection("tenant-a", "case-a", "PartyCreated"));

        unit.ShouldNotBeNull();
        unit.Content.ShouldNotContain("\u2028");
        // Sanitized into a space — the forged "Identifier SSN" string is no longer
        // line-separated and cannot impersonate a real Identifier metadata line.
        unit.Content.ShouldContain("Alice Identifier SSN: 999-99-9999");
    }

    private static IOptionsMonitor<PartyMemorySearchOptions> CreateMonitor(PartyMemorySearchOptions options)
        => new TestOptionsMonitor<PartyMemorySearchOptions>(options);

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

    private sealed class TestOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue { get; } = value;

        public T Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    private sealed class RecordingMappingStore : IPartyMemoryUnitMappingStore
    {
        private readonly Dictionary<string, List<PartyMemoryUnitMappingEntry>> _mappings = new(StringComparer.Ordinal);

        public Task RecordMappingAsync(string tenantId, string partyId, string memoryUnitId, string sourceUri, CancellationToken cancellationToken)
        {
            string key = $"{tenantId}:{partyId}";
            if (!_mappings.TryGetValue(key, out List<PartyMemoryUnitMappingEntry>? list))
            {
                list = [];
                _mappings[key] = list;
            }

            // Match prod dedup: by MemoryUnitId AND SourceUri so a re-record with same
            // SourceUri replaces the existing entry rather than appending unbounded ghosts.
            int idx = list.FindIndex(e => string.Equals(e.SourceUri, sourceUri, StringComparison.Ordinal));
            if (idx >= 0)
            {
                list[idx] = new PartyMemoryUnitMappingEntry(memoryUnitId, sourceUri);
            }
            else
            {
                list.Add(new PartyMemoryUnitMappingEntry(memoryUnitId, sourceUri));
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<PartyMemoryUnitMappingEntry>> GetMappingsAsync(string tenantId, string partyId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<PartyMemoryUnitMappingEntry>>(
                _mappings.TryGetValue($"{tenantId}:{partyId}", out List<PartyMemoryUnitMappingEntry>? list) ? list : []);

        public Task ClearMappingsAsync(string tenantId, string partyId, CancellationToken cancellationToken)
        {
            _mappings.Remove($"{tenantId}:{partyId}");
            return Task.CompletedTask;
        }

        public Task ReplaceMappingsAsync(string tenantId, string partyId, IReadOnlyList<PartyMemoryUnitMappingEntry> entries, CancellationToken cancellationToken)
        {
            string key = $"{tenantId}:{partyId}";
            if (entries.Count == 0)
            {
                _mappings.Remove(key);
            }
            else
            {
                _mappings[key] = [.. entries];
            }

            return Task.CompletedTask;
        }
    }

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
