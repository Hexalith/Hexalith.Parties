using System.Reflection;
using System.Text.Json;

using Dapr.Client;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.Memories.Client.Rest;
using Hexalith.Memories.Contracts.V1;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Projections.Configuration;
using Hexalith.Parties.Projections.Handlers;
using Hexalith.Parties.Projections.Models;
using Hexalith.Parties.Search;
using Hexalith.Parties.Testing;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

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

        bool result = await indexer.NotifyIndexedAsync(
            "tenant-a",
            CreateEntry(),
            "PartyCreated",
            DateTimeOffset.UnixEpoch,
            TestContext.Current.CancellationToken);

        client.IngestCount.ShouldBe(0);
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task NotifyIndexedAsync_WhenEnabledWithoutCaseId_ReturnsRetryableWithoutIngestAsync()
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
                CaseId = null,
            });

        bool result = await indexer.NotifyIndexedAsync(
            "tenant-a",
            CreateEntry(),
            "PartyCreated",
            DateTimeOffset.UnixEpoch,
            TestContext.Current.CancellationToken);

        client.IngestCount.ShouldBe(0);
        result.ShouldBeFalse();
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

        DateTimeOffset eventTimestamp = DateTimeOffset.Parse("2026-08-04T12:34:56Z");
        bool result = await indexer.NotifyIndexedAsync(
            "tenant-a",
            CreateEntry(),
            "PartyCreated",
            eventTimestamp,
            TestContext.Current.CancellationToken);

        result.ShouldBeTrue();
        client.IngestCount.ShouldBe(1);
        client.LastTenantId.ShouldBe("tenant-a");
        client.LastCaseId.ShouldBe("case-a");
        client.LastMetadata.ShouldNotBeNull();
        client.LastMetadata["eventType"].Value.ShouldBe("PartyCreated");
        client.LastMetadata["timestamp"].Value.ShouldBe(eventTimestamp.ToString("O"));
    }

    [Fact]
    public async Task ProjectionRetry_AfterSerializedReadBackPreservesExactIngestedProvenanceAsync()
    {
        var services = new ServiceCollection();
        services.AddDaprClient();
        using ServiceProvider provider = services.BuildServiceProvider();
        JsonSerializerOptions serializerOptions = provider.GetRequiredService<DaprClient>().JsonSerializerOptions;
        var client = new RecordingMemoriesClient { FailNextIngest = true };
        var intentState = new PartyMemoryTestReadModelStore();
        var mappingStore = new RecordingMappingStore();
        var searchOptions = new PartyMemorySearchOptions
        {
            Enabled = true,
            Endpoint = new Uri("https://memories.example/"),
            CaseId = "case-a",
            RequireApiToken = false,
        };
        IOptions<PartySdkReadModelOptions> projectionOptions = Options.Create(
            new PartySdkReadModelOptions { ReadModelStateStoreName = "statestore" });
        IReadModelStore store = Substitute.For<IReadModelStore>();
        string indexKey = PartySdkReadModelAddresses.Index("tenant-a");
        byte[]? persisted = null;
        int writes = 0;
        store.GetAsync<PartyIndexSdkReadModel>("statestore", indexKey, Arg.Any<CancellationToken>())
            .Returns(_ => new ReadModelEntry<PartyIndexSdkReadModel>(
                persisted is null ? null : JsonSerializer.Deserialize<PartyIndexSdkReadModel>(persisted, serializerOptions),
                persisted is null ? null : "etag-1"));
        store.TrySaveAsync("statestore", indexKey, Arg.Any<PartyIndexSdkReadModel>(), Arg.Any<string>(),
            Arg.Any<CancellationToken>()).Returns(call =>
            {
                writes++;
                persisted = JsonSerializer.SerializeToUtf8Bytes(call.Arg<PartyIndexSdkReadModel>(), serializerOptions);
                return true;
            });
        DateTimeOffset timestamp = new DateTimeOffset(2026, 8, 4, 12, 34, 56, TimeSpan.FromMinutes(330)).AddTicks(1234567);
        ProjectionRequest request = new("tenant-a", "party", "party-1",
        [
            Delivery(new PartyCreated
            {
                Type = PartyType.Person,
                PersonDetails = new PersonDetails { FirstName = "Canonical", LastName = "Party" },
            }, 1, DateTimeOffset.UnixEpoch),
            Delivery(new PartyDeactivated(), 2, timestamp),
            Delivery(new PartyNotFound(), 3, timestamp.AddMinutes(1)),
        ]);
        var handler = new PartyIndexSdkProjectionHandler(store, projectionOptions,
            CreateIndexer(client, mappingStore, searchOptions, intentState: intentState));

        DomainProjectionHandlerResult first = await handler.ProjectAsync(request, "first", TestContext.Current.CancellationToken);

        first.Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        persisted.ShouldNotBeNull();
        JsonSerializer.Deserialize<PartyIndexSdkReadModel>(persisted, serializerOptions)!
            .LastIndexedEvents[request.AggregateId].Timestamp.ToString("O")
            .ShouldBe("2026-08-04T12:34:56.1234567+05:30");
        client.LastMetadata.ShouldNotBeNull();
        client.LastMetadata["eventType"].Value.ShouldBe(typeof(PartyDeactivated).FullName);
        client.LastMetadata["timestamp"].Value.ShouldBe("2026-08-04T12:34:56.1234567+05:30");
        byte[] committed = persisted.ToArray();

        handler = new PartyIndexSdkProjectionHandler(store, projectionOptions,
            CreateIndexer(client, mappingStore, searchOptions, intentState: intentState));
        DomainProjectionHandlerResult retry = await handler.ProjectAsync(request, "retry", TestContext.Current.CancellationToken);

        retry.Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        writes.ShouldBe(1);
        persisted.ShouldBe(committed);
        client.IngestCount.ShouldBe(2);
        client.LastMetadata.ShouldNotBeNull();
        client.LastMetadata["eventType"].Value.ShouldBe(typeof(PartyDeactivated).FullName);
        client.LastMetadata["timestamp"].Value.ShouldBe("2026-08-04T12:34:56.1234567+05:30");

        static ProjectionEventDto Delivery(object payload, long sequence, DateTimeOffset eventTimestamp)
            => new(payload.GetType().FullName!,
                JsonSerializer.SerializeToUtf8Bytes(payload, payload.GetType(), PartiesJsonOptions.Default),
                "json", sequence, eventTimestamp, "correlation-1");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProjectionRetry_AfterCaseRepairIndexesLatestPersistedTargetAndHonorsErasureAsync(bool erasedBeforeRepair)
    {
        var client = new RecordingMemoriesClient();
        var mappings = new RecordingMappingStore();
        var monitor = new StaticOptionsMonitor(MissingCaseOptions());
        IReadModelStore store = Substitute.For<IReadModelStore>();
        string key = PartySdkReadModelAddresses.Index("tenant-a");
        byte[]? persisted = null;
        int writes = 0;
        store.GetAsync<PartyIndexSdkReadModel>("statestore", key, Arg.Any<CancellationToken>())
            .Returns(_ => new ReadModelEntry<PartyIndexSdkReadModel>(
                persisted is null ? null : JsonSerializer.Deserialize<PartyIndexSdkReadModel>(persisted, PartiesJsonOptions.Default),
                persisted is null ? null : "etag"));
        store.TrySaveAsync("statestore", key, Arg.Any<PartyIndexSdkReadModel>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                writes++;
                persisted = JsonSerializer.SerializeToUtf8Bytes(call.Arg<PartyIndexSdkReadModel>(), PartiesJsonOptions.Default);
                return true;
            });
        var projectionOptions = Options.Create(new PartySdkReadModelOptions());
        PartyIndexSdkProjectionHandler Handler() => new(store, projectionOptions,
            CreateIndexer(client, mappings, monitor.CurrentValue, optionsMonitor: monitor));
        ProjectionRequest created = new("tenant-a", "party", "party-1",
        [
            Delivery(new PartyCreated
            {
                Type = PartyType.Person,
                PersonDetails = new PersonDetails { FirstName = "Canonical", LastName = "Party" },
            }, 1),
        ]);

        DomainProjectionHandlerResult first = await Handler().ProjectAsync(created, "missing-case", TestContext.Current.CancellationToken);

        first.Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        first.ReasonCode.ShouldBe("search-reconciliation-required");
        client.IngestCount.ShouldBe(0);
        (await mappings.GetMappingsAsync("tenant-a", "party-1", TestContext.Current.CancellationToken)).ShouldBeEmpty();
        persisted.ShouldNotBeNull();
        JsonSerializer.Deserialize<PartyIndexSdkReadModel>(persisted, PartiesJsonOptions.Default)!.LastSequenceNumbers["party-1"].ShouldBe(1);

        ProjectionRequest newer = created with { Events = [Delivery(new PartyDeactivated(), 2)] };
        (await Handler().ProjectAsync(newer, "newer-target", TestContext.Current.CancellationToken)).Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        if (erasedBeforeRepair)
        {
            ProjectionRequest erased = created with
            {
                Events = [Delivery(new PartyErased { PartyId = "party-1", TenantId = "tenant-a", ErasedAt = DateTimeOffset.UnixEpoch.AddMinutes(3) }, 3)],
            };
            (await Handler().ProjectAsync(erased, "erased-target", TestContext.Current.CancellationToken)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        }
        byte[] committed = persisted.ToArray();
        monitor.CurrentValue = MissingCaseOptions("repaired-case");

        // A fresh handler redelivers the old event after a newer target has been committed.
        DomainProjectionHandlerResult repaired = await Handler().ProjectAsync(created, "repaired", TestContext.Current.CancellationToken);

        repaired.Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        writes.ShouldBe(erasedBeforeRepair ? 3 : 2);
        persisted.ShouldBe(committed);
        PartyIndexSdkReadModel canonical = JsonSerializer.Deserialize<PartyIndexSdkReadModel>(persisted, PartiesJsonOptions.Default)!;
        if (erasedBeforeRepair)
        {
            canonical.ErasureSequenceNumbers["party-1"].ShouldBe(3);
            canonical.Entries.ShouldNotContainKey("party-1");
            client.IngestCount.ShouldBe(0);
            (await mappings.GetMappingsAsync("tenant-a", "party-1", TestContext.Current.CancellationToken)).ShouldBeEmpty();
            return;
        }
        canonical.LastSequenceNumbers["party-1"].ShouldBe(2);
        canonical.Entries["party-1"].IsActive.ShouldBeFalse();
        client.IngestCount.ShouldBe(1);
        client.LastCaseId.ShouldBe("repaired-case");
        client.LastMetadata.ShouldNotBeNull();
        client.LastMetadata["isActive"].Value.ShouldBe("false");
        client.LastMetadata["eventType"].Value.ShouldBe(typeof(PartyDeactivated).FullName);
        PartyMemoryUnitMappingEntry mapping = (await mappings.GetMappingsAsync("tenant-a", "party-1", TestContext.Current.CancellationToken)).ShouldHaveSingleItem();
        mapping.MemoryUnitId.ShouldBe("workflow-1");
        mapping.CaseId.ShouldBe("repaired-case");
        mapping.SourceUri.ShouldBe(PartyMemoryUrn.Build("tenant-a", "party-1"));

        static ProjectionEventDto Delivery(object payload, long sequence) => new(payload.GetType().FullName!,
            JsonSerializer.SerializeToUtf8Bytes(payload, payload.GetType(), PartiesJsonOptions.Default),
            "json", sequence, DateTimeOffset.UnixEpoch.AddMinutes(sequence), "correlation-1");
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

        bool result = await indexer.NotifyIndexedAsync(
            "tenant-a",
            CreateEntry(),
            "PartyCreated",
            DateTimeOffset.UnixEpoch,
            TestContext.Current.CancellationToken);

        result.ShouldBeFalse();
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
                NullLogger<PartyMemoryIndexingService>.Instance, Hexalith.Parties.Tests.Search.PartyMemoryTestServices.CreateIntentStore()),
            new PartyMemoryCleanupService(
                new HttpClient { BaseAddress = new Uri("https://memories.example/") },
                mappingStore,
                NullLogger<PartyMemoryCleanupService>.Instance, Hexalith.Parties.Tests.Search.PartyMemoryTestServices.CreateIntentStore()),
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
        message.ShouldContain("indexing failed");
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
            "case-a",
            CancellationToken.None);
        var handler = new CapturingHandler(new HttpResponseMessage(System.Net.HttpStatusCode.NoContent));
        var cleanup = new PartyMemoryCleanupService(
            new HttpClient(handler) { BaseAddress = new Uri("https://memories.example/") },
            mappingStore,
            NullLogger<PartyMemoryCleanupService>.Instance, Hexalith.Parties.Tests.Search.PartyMemoryTestServices.CreateIntentStore());
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
                NullLogger<PartyMemoryIndexingService>.Instance, Hexalith.Parties.Tests.Search.PartyMemoryTestServices.CreateIntentStore()),
            cleanup,
            CreateMonitor(new PartyMemorySearchOptions
            {
                Enabled = true,
                Endpoint = new Uri("https://memories.example/"),
                CaseId = "case-a",
                RequireApiToken = false,
            }),
            NullLogger<PartyMemoryIndexEntrySearchIndexer>.Instance);

        bool result = await indexer.NotifyRemovedAsync("tenant-a", "party-1", TestContext.Current.CancellationToken);

        result.ShouldBeTrue();
        handler.RequestCount.ShouldBe(1);
        (await mappingStore.GetMappingsAsync("tenant-a", "party-1", CancellationToken.None)).Count.ShouldBe(0);
    }

    [Fact]
    public async Task NotifyRemovedAsync_WhenIndexingDisabled_StillCleansPersistedCaseAsync()
    {
        var mappingStore = new RecordingMappingStore();
        await mappingStore.RecordMappingAsync(
            "tenant-a",
            "party-1",
            "memory-1",
            "urn:hexalith:parties:tenant-a:party:party-1",
            "case-at-ingestion",
            CancellationToken.None);
        var handler = new CapturingHandler(new HttpResponseMessage(System.Net.HttpStatusCode.NoContent)) { CaseId = "case-at-ingestion" };
        var indexer = new PartyMemoryIndexEntrySearchIndexer(
            indexingService: null,
            new PartyMemoryCleanupService(
                new HttpClient(handler) { BaseAddress = new Uri("https://memories.example/") },
                mappingStore,
                NullLogger<PartyMemoryCleanupService>.Instance, Hexalith.Parties.Tests.Search.PartyMemoryTestServices.CreateIntentStore()),
            CreateMonitor(new PartyMemorySearchOptions { Enabled = false, CaseId = "case-current" }),
            NullLogger<PartyMemoryIndexEntrySearchIndexer>.Instance);

        bool result = await indexer.NotifyRemovedAsync("tenant-a", "party-1", TestContext.Current.CancellationToken);

        result.ShouldBeTrue();
        handler.LastUri.ShouldNotBeNull();
        handler.LastUri.AbsolutePath.ShouldContain("/cases/case-at-ingestion/");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task NotifyIndexedAsync_WithoutCaseId_SuppressesRetainedPairWithoutIngestAsync(string? caseId)
    {
        var client = new RecordingMemoriesClient();
        var logger = new PartyMemoryIndexEntryWarningLogger();
        var indexer = CreateIndexer(client, new RecordingMappingStore(), MissingCaseOptions(caseId), logger);

        for (int attempt = 0; attempt < 10; attempt++)
        {
            (await NotifyMissingCaseAsync(indexer, "private-tenant", "private-party")).ShouldBeFalse();
        }

        client.IngestCount.ShouldBe(0);
        string message = WarningMessages(logger).ShouldHaveSingleItem();
        message.ShouldBe("Memories party indexing skipped: search is enabled but no CaseId is configured.");
        message.ShouldNotContain("private-tenant");
        message.ShouldNotContain("private-party");
        var record = logger.Records.ShouldHaveSingleItem();
        record.Exception.ShouldBeNull();
        KeyValuePair<string, object?> property = record.State.ShouldHaveSingleItem();
        property.Key.ShouldBe("{OriginalFormat}");
        property.Value.ShouldBe(message);
        WarningTrackingCounts(indexer).ShouldAllBe(count => count == 1);
    }

    [Fact]
    public async Task NotifyIndexedAsync_WithoutCaseId_KeepsExactTenantAndPartyPairsDistinctAsync()
    {
        var logger = new PartyMemoryIndexEntryWarningLogger();
        var indexer = CreateIndexer(new RecordingMemoriesClient(), new RecordingMappingStore(), MissingCaseOptions(), logger);
        (string TenantId, string PartyId)[] pairs =
        [
            ("tenant-a", "party-1"),
            ("tenant-b", "party-1"),
            ("tenant-a:branch", "party-2"),
            ("tenant-a", "branch:party-2"),
            ("Tenant-a", "party-1"),
            ("tenant-a", "Party-1"),
        ];

        foreach ((string tenantId, string partyId) in pairs)
        {
            (await NotifyMissingCaseAsync(indexer, tenantId, partyId)).ShouldBeFalse();
            (await NotifyMissingCaseAsync(indexer, tenantId, partyId)).ShouldBeFalse();
        }

        WarningMessages(logger).Length.ShouldBe(pairs.Length);
        WarningTrackingCounts(indexer).ShouldAllBe(count => count == pairs.Length);
    }

    [Fact]
    public async Task NotifyIndexedAsync_WithoutCaseId_BoundsHistoryAndEvictsOldestPairAsync()
    {
        const int capacity = 1024;
        var logger = new PartyMemoryIndexEntryWarningLogger();
        var client = new RecordingMemoriesClient();
        var indexer = CreateIndexer(client, new RecordingMappingStore(), MissingCaseOptions(), logger);

        for (int party = 0; party < capacity * 3; party++)
        {
            (await NotifyMissingCaseAsync(indexer, "capacity-tenant", $"party-{party}")).ShouldBeFalse();
        }

        WarningMessages(logger).Length.ShouldBe(capacity * 3);
        WarningTrackingCounts(indexer).ShouldAllBe(count => count == capacity);
        (await NotifyMissingCaseAsync(indexer, "capacity-tenant", $"party-{(capacity * 3) - 1}")).ShouldBeFalse();
        WarningMessages(logger).Length.ShouldBe(capacity * 3);
        (await NotifyMissingCaseAsync(indexer, "capacity-tenant", "party-0")).ShouldBeFalse();
        WarningMessages(logger).Length.ShouldBe((capacity * 3) + 1);
        WarningTrackingCounts(indexer).ShouldAllBe(count => count == capacity);
        client.IngestCount.ShouldBe(0);
    }

    [Fact]
    public async Task NotifyIndexedAsync_WithoutCaseId_RetainedDuplicateDoesNotRefreshEvictionOrderAsync()
    {
        var logger = new PartyMemoryIndexEntryWarningLogger();
        var indexer = CreateIndexer(new RecordingMemoriesClient(), new RecordingMappingStore(), MissingCaseOptions(), logger);
        for (int party = 0; party < 1024; party++)
        {
            (await NotifyMissingCaseAsync(indexer, "fifo-tenant", $"party-{party}")).ShouldBeFalse();
        }

        (await NotifyMissingCaseAsync(indexer, "fifo-tenant", "party-0")).ShouldBeFalse();
        WarningMessages(logger).Length.ShouldBe(1024);
        (await NotifyMissingCaseAsync(indexer, "fifo-tenant", "party-1024")).ShouldBeFalse();
        (await NotifyMissingCaseAsync(indexer, "fifo-tenant", "party-0")).ShouldBeFalse();

        WarningMessages(logger).Length.ShouldBe(1026);
        WarningTrackingCounts(indexer).ShouldAllBe(count => count == 1024);
    }

    [Fact]
    public async Task NotifyIndexedAsync_WithoutCaseId_DuplicateChurnRetainsNoAdditionalHistoryAsync()
    {
        var logger = new PartyMemoryIndexEntryWarningLogger();
        var indexer = CreateIndexer(new RecordingMemoriesClient(), new RecordingMappingStore(), MissingCaseOptions(), logger);

        for (int attempt = 0; attempt < 4096; attempt++)
        {
            (await NotifyMissingCaseAsync(indexer, "repeat-tenant", "repeat-party")).ShouldBeFalse();
        }

        WarningMessages(logger).ShouldHaveSingleItem();
        WarningTrackingCounts(indexer).ShouldAllBe(count => count == 1);
    }

    [Fact]
    public async Task NotifyIndexedAsync_WithoutCaseId_DoesNotShareWarningHistoryAcrossInstancesAsync()
    {
        var firstLogger = new PartyMemoryIndexEntryWarningLogger();
        var secondLogger = new PartyMemoryIndexEntryWarningLogger();
        var first = CreateIndexer(new RecordingMemoriesClient(), new RecordingMappingStore(), MissingCaseOptions(), firstLogger);
        var second = CreateIndexer(new RecordingMemoriesClient(), new RecordingMappingStore(), MissingCaseOptions(), secondLogger);

        (await NotifyMissingCaseAsync(first, "host-tenant", "host-party")).ShouldBeFalse();
        (await NotifyMissingCaseAsync(second, "host-tenant", "host-party")).ShouldBeFalse();

        WarningMessages(firstLogger).ShouldHaveSingleItem();
        WarningMessages(secondLogger).ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NotifyIndexedAsync_WithoutCaseId_ConcurrentAdmissionsRemainBoundedAsync(bool distinctPairs)
    {
        const int notifications = 2048;
        var logger = new PartyMemoryIndexEntryWarningLogger();
        var client = new RecordingMemoriesClient();
        var indexer = CreateIndexer(client, new RecordingMappingStore(), MissingCaseOptions(), logger);
        TaskCompletionSource start = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool>[] pending = Enumerable.Range(0, notifications).Select(party => Task.Run(async () =>
        {
            await start.Task.ConfigureAwait(true);
            return await NotifyMissingCaseAsync(indexer, "concurrent-tenant", distinctPairs ? $"party-{party}" : "same-party").ConfigureAwait(true);
        }, TestContext.Current.CancellationToken)).ToArray();

        start.SetResult();
        bool[] outcomes = await Task.WhenAll(pending);

        outcomes.ShouldAllBe(converged => !converged);
        WarningMessages(logger).Length.ShouldBe(distinctPairs ? notifications : 1);
        WarningTrackingCounts(indexer).ShouldAllBe(count => count == (distinctPairs ? 1024 : 1));
        client.IngestCount.ShouldBe(0);
    }

    [Fact]
    public async Task NotifyIndexedAsync_WithoutCaseId_MixedConcurrentChurnSuppressesRetainedPairsAsync()
    {
        var logger = new PartyMemoryIndexEntryWarningLogger();
        var indexer = CreateIndexer(new RecordingMemoriesClient(), new RecordingMappingStore(), MissingCaseOptions(), logger);
        for (int party = 0; party < 1024; party++)
        {
            (await NotifyMissingCaseAsync(indexer, "mixed-tenant", $"party-{party}")).ShouldBeFalse();
        }

        TaskCompletionSource start = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool>[] pending = Enumerable.Range(0, 1280).Select(party => Task.Run(async () =>
        {
            await start.Task.ConfigureAwait(true);
            // Only 256 newcomers: the most recently admitted pair cannot be evicted.
            string partyId = party % 5 == 0 ? $"new-party-{party}" : "party-1023";
            return await NotifyMissingCaseAsync(indexer, "mixed-tenant", partyId).ConfigureAwait(true);
        }, TestContext.Current.CancellationToken)).ToArray();

        start.SetResult();
        bool[] outcomes = await Task.WhenAll(pending);

        outcomes.ShouldAllBe(converged => !converged);
        WarningMessages(logger).Length.ShouldBe(1280);
        WarningTrackingCounts(indexer).ShouldAllBe(count => count == 1024);
        (await NotifyMissingCaseAsync(indexer, "mixed-tenant", "party-1023")).ShouldBeFalse();
        WarningMessages(logger).Length.ShouldBe(1280);
        (await NotifyMissingCaseAsync(indexer, "mixed-tenant", "party-0")).ShouldBeFalse();
        WarningMessages(logger).Length.ShouldBe(1281);
        WarningTrackingCounts(indexer).ShouldAllBe(count => count == 1024);
    }

    [Fact]
    public async Task NotifyIndexedAsync_WhenCaseIdIsReconfigured_IndexesPreviouslySuppressedPairAsync()
    {
        var logger = new PartyMemoryIndexEntryWarningLogger();
        var client = new RecordingMemoriesClient();
        var monitor = new StaticOptionsMonitor(MissingCaseOptions());
        var indexer = CreateIndexer(client, new RecordingMappingStore(), monitor.CurrentValue, logger, monitor);

        (await NotifyMissingCaseAsync(indexer, "reload-tenant", "reload-party")).ShouldBeFalse();
        (await NotifyMissingCaseAsync(indexer, "reload-tenant", "reload-party")).ShouldBeFalse();
        client.IngestCount.ShouldBe(0);
        WarningMessages(logger).ShouldHaveSingleItem();

        monitor.CurrentValue = MissingCaseOptions("repaired-case");
        (await NotifyMissingCaseAsync(indexer, "reload-tenant", "reload-party")).ShouldBeTrue();
        client.IngestCount.ShouldBe(1);
        client.LastCaseId.ShouldBe("repaired-case");

        monitor.CurrentValue = MissingCaseOptions();
        (await NotifyMissingCaseAsync(indexer, "reload-tenant", "reload-party")).ShouldBeFalse();
        client.IngestCount.ShouldBe(1);
        WarningMessages(logger).ShouldHaveSingleItem();
        WarningTrackingCounts(indexer).ShouldAllBe(count => count == 1);
    }

    [Fact]
    public async Task NotifyRemovedAsync_WhenEnabledWithoutCaseId_CleansPersistedCaseAfterSuppressedNotificationsAsync()
    {
        var logger = new PartyMemoryIndexEntryWarningLogger();
        var mappingStore = new RecordingMappingStore();
        await mappingStore.RecordMappingAsync("cleanup-tenant", "cleanup-party", "memory-1", "urn:cleanup", "persisted-case", TestContext.Current.CancellationToken);
        var handler = new CapturingHandler(new HttpResponseMessage(System.Net.HttpStatusCode.NoContent)) { TenantId = "cleanup-tenant", CaseId = "persisted-case" };
        var indexer = new PartyMemoryIndexEntrySearchIndexer(
            indexingService: null,
            new PartyMemoryCleanupService(
                new HttpClient(handler) { BaseAddress = new Uri("https://memories.example/") },
                mappingStore,
                NullLogger<PartyMemoryCleanupService>.Instance, Hexalith.Parties.Tests.Search.PartyMemoryTestServices.CreateIntentStore()),
            CreateMonitor(MissingCaseOptions()),
            logger);
        (await NotifyMissingCaseAsync(indexer, "cleanup-tenant", "cleanup-party")).ShouldBeFalse();
        (await NotifyMissingCaseAsync(indexer, "cleanup-tenant", "cleanup-party")).ShouldBeFalse();
        WarningMessages(logger).ShouldHaveSingleItem();

        bool removed = await indexer.NotifyRemovedAsync("cleanup-tenant", "cleanup-party", TestContext.Current.CancellationToken);

        removed.ShouldBeTrue();
        handler.RequestCount.ShouldBe(1);
        handler.LastUri.ShouldNotBeNull();
        handler.LastUri.AbsolutePath.ShouldContain("/cases/persisted-case/");
        (await mappingStore.GetMappingsAsync("cleanup-tenant", "cleanup-party", TestContext.Current.CancellationToken)).ShouldBeEmpty();
    }

    [Fact]
    public async Task NotifyIndexedAsync_WhenDisabled_DoesNotRetainWarningsAsync()
    {
        var logger = new PartyMemoryIndexEntryWarningLogger();
        var indexer = CreateIndexer(new RecordingMemoriesClient(), new RecordingMappingStore(), new PartyMemorySearchOptions { Enabled = false }, logger);

        (await NotifyMissingCaseAsync(indexer, "disabled-tenant", "disabled-party")).ShouldBeTrue();

        WarningMessages(logger).ShouldBeEmpty();
        WarningTrackingCounts(indexer).ShouldAllBe(count => count == 0);
    }

    private static Task<bool> NotifyMissingCaseAsync(PartyMemoryIndexEntrySearchIndexer indexer, string tenantId, string partyId)
        => indexer.NotifyIndexedAsync(tenantId, CreateEntry() with { Id = partyId }, "PartyCreated", DateTimeOffset.UnixEpoch, TestContext.Current.CancellationToken);

    private static PartyMemorySearchOptions MissingCaseOptions(string? caseId = null)
        => new() { Enabled = true, Endpoint = new Uri("https://memories.example/"), CaseId = caseId };

    private static string[] WarningMessages(PartyMemoryIndexEntryWarningLogger logger)
        => logger.Warnings;

    // Inspect both structures after notifications finish: log counts cannot prove the memory bound.
    private static int[] WarningTrackingCounts(PartyMemoryIndexEntrySearchIndexer indexer)
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        FieldInfo? retainedField = typeof(PartyMemoryIndexEntrySearchIndexer).GetField("_caseIdMissingWarned", flags);
        FieldInfo? orderField = typeof(PartyMemoryIndexEntrySearchIndexer).GetField("_caseIdMissingWarningOrder", flags);
        retainedField.ShouldNotBeNull();
        orderField.ShouldNotBeNull();
        var retained = retainedField.GetValue(indexer).ShouldBeOfType<HashSet<(string TenantId, string PartyId)>>();
        var order = orderField.GetValue(indexer).ShouldBeOfType<Queue<(string TenantId, string PartyId)>>();
        order.Count.ShouldBe(retained.Count);
        order.Distinct().Count().ShouldBe(order.Count);
        retained.SetEquals(order).ShouldBeTrue();
        return [retained.Count, order.Count];
    }

    private static PartyMemoryIndexEntrySearchIndexer CreateIndexer(
        MemoriesClient client,
        IPartyMemoryUnitMappingStore mappingStore,
        PartyMemorySearchOptions options,
        ILogger<PartyMemoryIndexEntrySearchIndexer>? logger = null,
        IOptionsMonitor<PartyMemorySearchOptions>? optionsMonitor = null,
        PartyMemoryTestReadModelStore? intentState = null)
    {
        IOptionsMonitor<PartyMemorySearchOptions> monitor = optionsMonitor ?? CreateMonitor(options);
        var intentStore = PartyMemoryRecoveryTests.Intents(intentState ?? new PartyMemoryTestReadModelStore());
        return new PartyMemoryIndexEntrySearchIndexer(
            new PartyMemoryIndexingService(
                client,
                mappingStore,
                monitor,
                NullLogger<PartyMemoryIndexingService>.Instance, intentStore),
            new PartyMemoryCleanupService(
                new HttpClient { BaseAddress = new Uri("https://memories.example/") },
                mappingStore,
                NullLogger<PartyMemoryCleanupService>.Instance, intentStore),
            monitor,
            logger ?? NullLogger<PartyMemoryIndexEntrySearchIndexer>.Instance);
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
        public PartyMemorySearchOptions CurrentValue { get; set; } = current;

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
            string caseId,
            CancellationToken cancellationToken)
        {
            string key = $"{tenantId}:{partyId}";
            if (!_mappings.TryGetValue(key, out List<PartyMemoryUnitMappingEntry>? list))
            {
                list = [];
                _mappings[key] = list;
            }

            list.Add(new PartyMemoryUnitMappingEntry(memoryUnitId, sourceUri, caseId));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<PartyMemoryUnitMappingEntry>> GetMappingsAsync(
            string tenantId,
            string partyId,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<PartyMemoryUnitMappingEntry>>(
                _mappings.TryGetValue($"{tenantId}:{partyId}", out List<PartyMemoryUnitMappingEntry>? list) ? [.. list] : []);

        public Task ClearMappingsAsync(string tenantId, string partyId, CancellationToken cancellationToken)
        {
            _mappings.Remove($"{tenantId}:{partyId}");
            return Task.CompletedTask;
        }

        public Task<int> RemoveMappingsAsync(
            string tenantId,
            string partyId,
            IReadOnlyList<PartyMemoryUnitMappingEntry> entries,
            CancellationToken cancellationToken)
        {
            string key = $"{tenantId}:{partyId}";
            if (!_mappings.TryGetValue(key, out List<PartyMemoryUnitMappingEntry>? current))
            {
                return Task.FromResult(0);
            }

            _ = current.RemoveAll(entries.Contains);
            if (current.Count == 0)
            {
                _mappings.Remove(key);
            }

            return Task.FromResult(current.Count);
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

        public IReadOnlyDictionary<string, MetadataField>? LastMetadata { get; private set; }

        public bool FailNextIngest { get; set; }

#pragma warning disable HXL001
        public override Task<string> IngestAsync(
            string tenantId,
            string caseId,
            string sourceUri,
            byte[] content,
            string contentType,
            string ingestedBy,
            IReadOnlyDictionary<string, MetadataField>? metadata,
            string? idempotencyToken,
            CancellationToken ct)
        {
            IngestCount++;
            LastTenantId = tenantId;
            LastCaseId = caseId;
            LastMetadata = metadata;
            if (FailNextIngest)
            {
                FailNextIngest = false;
                return Task.FromException<string>(new MemoriesRemoteException(System.Net.HttpStatusCode.Unauthorized,
                    new ErrorResponse("UNAUTHORIZED", "Submission rejected.", "Repair credentials.")));
            }

            return Task.FromResult("workflow-1");
        }
        public override Task<IngestionWorkflowStatus> GetIngestionWorkflowStatusAsync(string instanceId, CancellationToken ct)
            => Task.FromResult(PartyMemoryRecoveryHandler.Completed(instanceId, "workflow-1", LastTenantId!, LastCaseId!));
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
            string? idempotencyToken,
            CancellationToken ct)
            => Task.FromException<string>(ex);
#pragma warning restore HXL001
    }

    private sealed class CapturingHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        public Uri? LastUri { get; private set; }
        public string TenantId { get; set; } = "tenant-a";
        public string CaseId { get; set; } = "case-a";
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath.Contains("/ingest/", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = System.Net.Http.Json.JsonContent.Create(PartyMemoryRecoveryHandler.Completed("memory-1", "memory-1", TenantId, CaseId)),
                });
            }

            if (request.Method == HttpMethod.Get)
            {
                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)
                {
                    Content = System.Net.Http.Json.JsonContent.Create(new { code = "MEMORY_UNIT_NOT_FOUND" }),
                });
            }

            RequestCount++;
            LastUri = request.RequestUri;
            return Task.FromResult(response);
        }
    }
}
