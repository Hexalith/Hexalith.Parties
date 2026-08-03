using Hexalith.Memories.Client.Rest;
using Hexalith.Memories.Contracts.V1;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Search;
using Hexalith.Parties.Tests.Gateway;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.Parties.Tests.Search;

public sealed class PartyMemoryIndexEntrySearchIndexerTests
{
    [Fact]
    public async Task NotifyIndexedAsync_WhenDisabled_DoesNotIngestAsync()
    {
        var client = new RecordingMemoriesClient();
        var mappingStore = new RecordingMappingStore();
        var indexer = CreateIndexer(
            client,
            mappingStore,
            new PartyMemorySearchOptions { Enabled = false, CaseId = "case-a" });

        await indexer.NotifyIndexedAsync(
            "tenant-a",
            CreateEntry(),
            "PartyCreated",
            DateTimeOffset.UnixEpoch,
            TestContext.Current.CancellationToken);

        client.IngestCount.ShouldBe(0);
    }

    [Fact]
    public async Task NotifyIndexedAsync_WhenEnabledWithCaseId_IngestsAsync()
    {
        var client = new RecordingMemoriesClient();
        var mappingStore = new RecordingMappingStore();
        var indexer = CreateIndexer(
            client,
            mappingStore,
            new PartyMemorySearchOptions
            {
                Enabled = true,
                Endpoint = new Uri("https://memories.example/"),
                CaseId = "case-a",
                RequireApiToken = false,
            });

        await indexer.NotifyIndexedAsync(
            "tenant-a",
            CreateEntry(),
            "PartyCreated",
            DateTimeOffset.UnixEpoch,
            TestContext.Current.CancellationToken);

        client.IngestCount.ShouldBe(1);
        client.LastTenantId.ShouldBe("tenant-a");
        client.LastCaseId.ShouldBe("case-a");
    }

    [Fact]
    public async Task NotifyIndexedAsync_WhenIndexingThrows_DoesNotPropagateAsync()
    {
        var client = new ThrowingMemoriesClient(new InvalidOperationException("boom"));
        var mappingStore = new RecordingMappingStore();
        var indexer = CreateIndexer(
            client,
            mappingStore,
            new PartyMemorySearchOptions
            {
                Enabled = true,
                Endpoint = new Uri("https://memories.example/"),
                CaseId = "case-a",
                RequireApiToken = false,
            });

        await Should.NotThrowAsync(() => indexer.NotifyIndexedAsync(
            "tenant-a",
            CreateEntry(),
            "PartyCreated",
            DateTimeOffset.UnixEpoch,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task NotifyIndexedAsync_WhenIndexingThrows_LogsOnlyBoundedMetadataAsync()
    {
        var logger = new RecordingLogger<PartyMemoryIndexEntrySearchIndexer>();
        var client = new ThrowingMemoriesClient(
            new InvalidOperationException("tenant-a/party-1 readmodel:key must not leak"));
        var mappingStore = new RecordingMappingStore();
        PartyMemorySearchOptions options = new()
        {
            Enabled = true,
            Endpoint = new Uri("https://memories.example/"),
            CaseId = "case-a",
            RequireApiToken = false,
        };
        IOptionsMonitor<PartyMemorySearchOptions> monitor = CreateMonitor(options);
        var indexer = new PartyMemoryIndexEntrySearchIndexer(
            new PartyMemoryIndexingService(
                client,
                mappingStore,
                monitor,
                NullLogger<PartyMemoryIndexingService>.Instance),
            new PartyMemoryCleanupService(
                new HttpClient { BaseAddress = new Uri("https://memories.example/") },
                mappingStore,
                NullLogger<PartyMemoryCleanupService>.Instance),
            monitor,
            logger);

        await indexer.NotifyIndexedAsync(
            "tenant-a",
            CreateEntry(),
            "PartyCreated",
            DateTimeOffset.UnixEpoch,
            TestContext.Current.CancellationToken);

        (Microsoft.Extensions.Logging.LogLevel level, string message, Exception? exception) =
            logger.Records.ShouldHaveSingleItem();
        level.ShouldBe(Microsoft.Extensions.Logging.LogLevel.Warning);
        message.ShouldContain(nameof(InvalidOperationException));
        message.ShouldNotContain("tenant-a");
        message.ShouldNotContain("party-1");
        message.ShouldNotContain("readmodel:key");
        exception.ShouldBeNull();
    }

    [Fact]
    public async Task NotifyRemovedAsync_WhenEnabled_DeletesByPartyAsync()
    {
        var mappingStore = new RecordingMappingStore();
        await mappingStore.RecordMappingAsync(
            "tenant-a",
            "party-1",
            "memory-1",
            "urn:hexalith:parties:tenant-a:party:party-1",
            CancellationToken.None);
        var handler = new CapturingHandler(new HttpResponseMessage(System.Net.HttpStatusCode.NoContent));
        var cleanup = new PartyMemoryCleanupService(
            new HttpClient(handler) { BaseAddress = new Uri("https://memories.example/") },
            mappingStore,
            NullLogger<PartyMemoryCleanupService>.Instance);
        var indexer = new PartyMemoryIndexEntrySearchIndexer(
            new PartyMemoryIndexingService(
                new RecordingMemoriesClient(),
                mappingStore,
                CreateMonitor(new PartyMemorySearchOptions
                {
                    Enabled = true,
                    Endpoint = new Uri("https://memories.example/"),
                    CaseId = "case-a",
                    RequireApiToken = false,
                }),
                NullLogger<PartyMemoryIndexingService>.Instance),
            cleanup,
            CreateMonitor(new PartyMemorySearchOptions
            {
                Enabled = true,
                Endpoint = new Uri("https://memories.example/"),
                CaseId = "case-a",
                RequireApiToken = false,
            }),
            NullLogger<PartyMemoryIndexEntrySearchIndexer>.Instance);

        await indexer.NotifyRemovedAsync("tenant-a", "party-1", TestContext.Current.CancellationToken);

        handler.RequestCount.ShouldBe(1);
        (await mappingStore.GetMappingsAsync("tenant-a", "party-1", CancellationToken.None)).Count.ShouldBe(0);
    }

    private static PartyMemoryIndexEntrySearchIndexer CreateIndexer(
        MemoriesClient client,
        IPartyMemoryUnitMappingStore mappingStore,
        PartyMemorySearchOptions options)
    {
        IOptionsMonitor<PartyMemorySearchOptions> monitor = CreateMonitor(options);
        return new PartyMemoryIndexEntrySearchIndexer(
            new PartyMemoryIndexingService(
                client,
                mappingStore,
                monitor,
                NullLogger<PartyMemoryIndexingService>.Instance),
            new PartyMemoryCleanupService(
                new HttpClient { BaseAddress = new Uri("https://memories.example/") },
                mappingStore,
                NullLogger<PartyMemoryCleanupService>.Instance),
            monitor,
            NullLogger<PartyMemoryIndexEntrySearchIndexer>.Instance);
    }

    private static IOptionsMonitor<PartyMemorySearchOptions> CreateMonitor(PartyMemorySearchOptions options)
        => new StaticOptionsMonitor(options);

    private static PartyIndexEntry CreateEntry()
        => new()
        {
            Id = "party-1",
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = "Jean Dupont",
            SortName = "Dupont Jean",
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
        };

    private sealed class StaticOptionsMonitor(PartyMemorySearchOptions current) : IOptionsMonitor<PartyMemorySearchOptions>
    {
        public PartyMemorySearchOptions CurrentValue { get; } = current;

        public PartyMemorySearchOptions Get(string? name) => CurrentValue;

        public IDisposable OnChange(Action<PartyMemorySearchOptions, string?> listener) => EmptyDisposable.Instance;
    }

    private sealed class EmptyDisposable : IDisposable
    {
        public static EmptyDisposable Instance { get; } = new();

        public void Dispose()
        {
        }
    }

    private sealed class RecordingMappingStore : IPartyMemoryUnitMappingStore
    {
        private readonly Dictionary<string, List<PartyMemoryUnitMappingEntry>> _mappings = new(StringComparer.Ordinal);

        public Task RecordMappingAsync(
            string tenantId,
            string partyId,
            string memoryUnitId,
            string sourceUri,
            CancellationToken cancellationToken)
        {
            string key = $"{tenantId}:{partyId}";
            if (!_mappings.TryGetValue(key, out List<PartyMemoryUnitMappingEntry>? list))
            {
                list = [];
                _mappings[key] = list;
            }

            list.Add(new PartyMemoryUnitMappingEntry(memoryUnitId, sourceUri));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<PartyMemoryUnitMappingEntry>> GetMappingsAsync(
            string tenantId,
            string partyId,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<PartyMemoryUnitMappingEntry>>(
                _mappings.TryGetValue($"{tenantId}:{partyId}", out List<PartyMemoryUnitMappingEntry>? list) ? list : []);

        public Task ClearMappingsAsync(string tenantId, string partyId, CancellationToken cancellationToken)
        {
            _mappings.Remove($"{tenantId}:{partyId}");
            return Task.CompletedTask;
        }

        public Task ReplaceMappingsAsync(
            string tenantId,
            string partyId,
            IReadOnlyList<PartyMemoryUnitMappingEntry> entries,
            CancellationToken cancellationToken)
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
        public int IngestCount { get; private set; }

        public string? LastTenantId { get; private set; }

        public string? LastCaseId { get; private set; }

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
            IngestCount++;
            LastTenantId = tenantId;
            LastCaseId = caseId;
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

    private sealed class CapturingHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(response);
        }
    }
}
