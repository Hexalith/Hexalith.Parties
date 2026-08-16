using Hexalith.Memories.Contracts.V1;
using Hexalith.Memories.Client.Rest;
using Hexalith.Parties.Search;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Testing;

using Microsoft.Extensions.Logging;
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
        unit.Metadata["timestamp"].Value.ShouldBe("2026-05-02T10:00:00.0000000+00:00");
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
        mappings[0].CaseId.ShouldBe("case-a");
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
    public async Task PartyMemoryIndexingServiceTransientFailureLogRetainsNoSensitiveDataOrException()
    {
        const string tenantId = "tenant-secret";
        const string partyId = "party-secret";
        const string caseId = "case-secret";
        const string providerFailure = "provider leaked tenant-secret party-secret case-secret";
        var client = new ThrowingMemoriesClient(new HttpRequestException(providerFailure));
        var logger = new RecordingLogger<PartyMemoryIndexingService>();
        var service = new PartyMemoryIndexingService(
            client,
            new RecordingMappingStore(),
            CreateMonitor(new PartyMemorySearchOptions()),
            logger);

        PartyMemoryIndexingResult? result = await service.IndexAsync(
            CreateEntry() with { Id = partyId },
            new PartyMemoryUnitMappingContext(tenantId, caseId, "PartyCreated", AggregateId: partyId),
            CancellationToken.None);

        result.ShouldNotBeNull().Indexed.ShouldBeFalse();
        AssertSanitizedDiagnostics(logger, tenantId, partyId, caseId, providerFailure);
        logger.Records.ShouldHaveSingleItem().Message.ShouldContain(nameof(HttpRequestException));
    }

    [Fact]
    public async Task PartyMemoryIndexingServiceEmptyWorkflowLogRetainsNoSensitiveData()
    {
        const string tenantId = "tenant-secret";
        const string partyId = "party-secret";
        const string caseId = "case-secret";
        var client = new RecordingMemoriesClient { WorkflowInstanceId = " " };
        var logger = new RecordingLogger<PartyMemoryIndexingService>();
        var service = new PartyMemoryIndexingService(
            client,
            new RecordingMappingStore(),
            CreateMonitor(new PartyMemorySearchOptions()),
            logger);

        PartyMemoryIndexingResult? result = await service.IndexAsync(
            CreateEntry() with { Id = partyId },
            new PartyMemoryUnitMappingContext(tenantId, caseId, "PartyCreated", AggregateId: partyId),
            CancellationToken.None);

        result.ShouldNotBeNull().Indexed.ShouldBeFalse();
        AssertSanitizedDiagnostics(logger, tenantId, partyId, caseId);
        logger.Records.ShouldHaveSingleItem().Message.ShouldContain("no workflow/memory-unit id");
    }

    [Fact]
    public async Task PartyMemoryIndexingServiceCompensationUsesCapturedCaseAndLogsOnlyBoundedMetadata()
    {
        const string tenantId = "tenant-secret";
        const string partyId = "party-secret";
        const string caseId = "case-secret";
        const string memoryUnitId = "memory-unit-secret";
        const string providerFailure = "provider leaked tenant-secret party-secret case-secret memory-unit-secret";
        var client = new RecordingMemoriesClient { WorkflowInstanceId = memoryUnitId };
        var logger = new RecordingLogger<PartyMemoryIndexingService>();
        var service = new PartyMemoryIndexingService(
            client,
            new ThrowingMappingStore(new InvalidOperationException(providerFailure)),
            CreateMonitor(new PartyMemorySearchOptions
            {
                Endpoint = new Uri("http://127.0.0.1:1/"),
                // Deliberately differs from the ingest snapshot. Compensation must use the
                // captured unit CaseId, not this mutable live option.
                CaseId = null,
                ApiToken = "invalid\r\nmemory-unit-secret",
            }),
            logger);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        PartyMemoryIndexingResult? result = await service.IndexAsync(
            CreateEntry() with { Id = partyId },
            new PartyMemoryUnitMappingContext(tenantId, caseId, "PartyCreated", AggregateId: partyId),
            timeout.Token);

        result.ShouldNotBeNull().Indexed.ShouldBeFalse();
        AssertSanitizedDiagnostics(logger, tenantId, partyId, caseId, memoryUnitId, providerFailure);
        (LogLevel level, string message, Exception? loggedException) = logger.Records.ShouldHaveSingleItem();
        level.ShouldBe(LogLevel.Error);
        message.ShouldContain(nameof(HttpRequestException));
        message.ShouldContain(nameof(InvalidOperationException));
        message.ShouldNotContain("skipped");
        loggedException.ShouldBeNull();
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

    private static void AssertSanitizedDiagnostics(
        RecordingLogger<PartyMemoryIndexingService> logger,
        params string[] sensitiveValues)
    {
        logger.Records.ShouldNotBeEmpty();
        foreach ((_, string message, Exception? loggedException) in logger.Records)
        {
            loggedException.ShouldBeNull();
            foreach (string sensitiveValue in sensitiveValues)
            {
                message.ShouldNotContain(sensitiveValue);
            }
        }
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

    private sealed class TestOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue { get; } = value;

        public T Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    private sealed class RecordingMappingStore : IPartyMemoryUnitMappingStore
    {
        private readonly Dictionary<string, List<PartyMemoryUnitMappingEntry>> _mappings = new(StringComparer.Ordinal);

        public Task RecordMappingAsync(
            string tenantId,
            string partyId,
            string memoryUnitId,
            string sourceUri,
            string caseId,
            CancellationToken cancellationToken)
        {
            string key = $"{tenantId}:{partyId}";
            if (!_mappings.TryGetValue(key, out List<PartyMemoryUnitMappingEntry>? list))
            {
                list = [];
                _mappings[key] = list;
            }

            // Match production: replace matching identities only within the same case.
            // Legacy entries without a CaseId are adopted by the current case, while
            // cross-case entries remain available for later cleanup.
            int idx = list.FindIndex(e =>
                (string.IsNullOrWhiteSpace(e.CaseId)
                    || string.Equals(e.CaseId, caseId, StringComparison.Ordinal))
                && (string.Equals(e.MemoryUnitId, memoryUnitId, StringComparison.Ordinal)
                    || string.Equals(e.SourceUri, sourceUri, StringComparison.Ordinal)));
            if (idx >= 0)
            {
                list[idx] = new PartyMemoryUnitMappingEntry(memoryUnitId, sourceUri, caseId);
            }
            else
            {
                list.Add(new PartyMemoryUnitMappingEntry(memoryUnitId, sourceUri, caseId));
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

    private sealed class ThrowingMappingStore(Exception failure) : IPartyMemoryUnitMappingStore
    {
        public Task RecordMappingAsync(
            string tenantId,
            string partyId,
            string memoryUnitId,
            string sourceUri,
            string caseId,
            CancellationToken cancellationToken)
            => Task.FromException(failure);

        public Task<IReadOnlyList<PartyMemoryUnitMappingEntry>> GetMappingsAsync(
            string tenantId,
            string partyId,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<PartyMemoryUnitMappingEntry>>([]);

        public Task ClearMappingsAsync(string tenantId, string partyId, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task ReplaceMappingsAsync(
            string tenantId,
            string partyId,
            IReadOnlyList<PartyMemoryUnitMappingEntry> entries,
            CancellationToken cancellationToken)
            => Task.CompletedTask;
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

        public string WorkflowInstanceId { get; init; } = "workflow-1";

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
            return Task.FromResult(WorkflowInstanceId);
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
