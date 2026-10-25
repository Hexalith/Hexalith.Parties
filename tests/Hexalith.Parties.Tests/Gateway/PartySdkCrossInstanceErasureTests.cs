using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Client.Queries;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Testing.Fakes;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Projections.Configuration;
using Hexalith.Parties.Projections.Handlers;
using Hexalith.Parties.Projections.Models;
using Hexalith.Parties.Projections.Services;
using Hexalith.Parties.Queries;
using Hexalith.Parties.Search;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.Tests.Gateway;

/// <summary>Verifies independent host caches cannot disclose personal data during canonical outages.</summary>
public sealed class PartySdkCrossInstanceErasureTests
{
    private const string StoreName = "statestore";
    private static readonly DateTimeOffset s_now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
    // This fixture owns the complete synthetic store, including any private SDK recovery state.
    private static readonly IOptions<PartySdkReadModelOptions> s_options = Options.Create(new PartySdkReadModelOptions
    {
        ReadModelStateStoreName = StoreName,
        HistoricalPrivateRecoveryReconciliationAttested = true,
        FreshnessAgingSeconds = 30,
        FreshnessStaleSeconds = 300,
    });

    /// <summary>Rejects remote personal caches after real shared erasure, independently of status probes.</summary>
    /// <param name="queryType">The public query discriminator.</param>
    [Theory]
    [InlineData("GetParty")]
    [InlineData("PartyDetail")]
    [InlineData("ExportPartyData")]
    [InlineData("GetProcessingRecords")]
    [InlineData("PartyIndex")]
    [InlineData("PartySearch")]
    public async Task RemoteErasureThenCanonicalOutageRejectsPersonalCacheAsync(string queryType)
    {
        var store = new InMemoryReadModelStore();
        await SeedAsync(store, "tenant-a", "party-2", "unaffected-a");
        await SeedAsync(store, "tenant-a", "party-1", "private-a");
        await SeedAsync(store, "tenant-b", "party-1", "private-b");
        var cacheA = new PartySdkLastKnownReadModelCache();
        var cacheB = new PartySdkLastKnownReadModelCache();
        var readsB = new PartySdkFaultingReadModelStore(store);
        PartySdkQueryService hostA = Service(store, cacheA);
        PartySdkQueryService hostB = Service(readsB, cacheB);
        (await ExecuteAsync(hostA, queryType)).Success.ShouldBeTrue();
        (await ExecuteAsync(hostB, queryType)).Success.ShouldBeTrue();
        AssertUnaffectedPayload(await ExecuteAsync(hostB, queryType, "tenant-b"), queryType);
        QueryResult sameTenant = await ExecuteAsync(hostB, "GetParty", "tenant-a", "party-2");
        sameTenant.GetPayload().Deserialize<PartyDetail>(PartiesJsonOptions.Default)!.DisplayName.ShouldBe("unaffected-a-display");
        AssertUnaffectedCache(cacheB, queryType);

        // The eraser does not know about B's cache. Do not manually evict it.
        await new PartySdkReadModelEraser(store, store, s_options).EraseAsync("tenant-a", "party-1", TestContext.Current.CancellationToken);
        await AssertErasedStoreAsync(store);
        readsB.UnavailableKeys.Add(IsIndex(queryType)
            ? PartySdkReadModelAddresses.Index("tenant-a")
            : PartySdkReadModelAddresses.Detail("tenant-a", "party-1"));

        QueryResult failed = await ExecuteAsync(hostB, queryType);
        AssertBoundedFailure(failed);
        if (IsIndex(queryType))
        {
            cacheB.TryGetIndex("tenant-a", out _).ShouldBeFalse();
        }
        else
        {
            cacheB.TryGetDetail("tenant-a", "party-1", out _).ShouldBeFalse();
        }

        // Inspect previously populated unrelated caches before a healthy read can repopulate them.
        AssertUnaffectedCache(cacheB, queryType);
        AssertUnaffectedPayload(await ExecuteAsync(hostB, queryType, "tenant-b"), queryType);
        sameTenant = await ExecuteAsync(hostB, "GetParty", "tenant-a", "party-2");
        sameTenant.Success.ShouldBeTrue();
        sameTenant.Metadata!.IsDegraded.ShouldBe(false);
        sameTenant.GetPayload().Deserialize<PartyDetail>(PartiesJsonOptions.Default)!.DisplayName.ShouldBe("unaffected-a-display");
        readsB.UnavailableKeys.Clear();
        QueryResult recovered = await ExecuteAsync(hostB, queryType);
        if (queryType is "GetParty" or "PartyDetail")
        {
            recovered.Success.ShouldBeFalse();
            recovered.ErrorMessage.ShouldBe(PartySdkQueryFailureReason.ReadModelUnavailable);
            recovered.PayloadBytes.ShouldBeNull();
            recovered.Metadata.ShouldBeNull();
        }
        else
        {
            recovered.Success.ShouldBeTrue();
            recovered.Metadata!.IsDegraded.ShouldBe(false);
            AssertNoPersonalData(recovered);
        }
    }

    /// <summary>Uses the same privacy policy during outages even without any known erasure.</summary>
    /// <param name="queryType">The public query discriminator.</param>
    [Theory]
    [InlineData("GetParty")]
    [InlineData("PartyDetail")]
    [InlineData("ExportPartyData")]
    [InlineData("GetProcessingRecords")]
    [InlineData("PartyIndex")]
    [InlineData("PartySearch")]
    public async Task OrdinaryOutageDoesNotInventErasureOrAnEmptyIndexAsync(string queryType)
    {
        var store = new InMemoryReadModelStore();
        await SeedAsync(store, "tenant-a", "party-1", "private-a");
        var reads = new PartySdkFaultingReadModelStore(store);
        var cache = new PartySdkLastKnownReadModelCache();
        PartySdkQueryService service = Service(reads, cache);
        (await ExecuteAsync(service, queryType)).Success.ShouldBeTrue();
        reads.UnavailableKeys.Add(IsIndex(queryType)
            ? PartySdkReadModelAddresses.Index("tenant-a")
            : PartySdkReadModelAddresses.Detail("tenant-a", "party-1"));

        AssertBoundedFailure(await ExecuteAsync(service, queryType));
        (await store.GetAsync<PartyDetailSdkReadModel>(StoreName, PartySdkReadModelAddresses.Detail("tenant-a", "party-1"), TestContext.Current.CancellationToken))
            .Value!.Detail!.IsErased.ShouldBeFalse();
    }

    /// <summary>Redacts malformed erased cached content without replacing retained timestamps.</summary>
    /// <param name="queryType">The detail or export query discriminator.</param>
    [Theory]
    [InlineData("GetParty")]
    [InlineData("PartyDetail")]
    [InlineData("ExportPartyData")]
    public async Task ErasedCacheDefensivelyRedactsEveryPersonalFieldAndPreservesTimeAsync(string queryType)
    {
        var store = new InMemoryReadModelStore();
        await SeedAsync(store, "tenant-a", "party-1", "private-a");
        string key = PartySdkReadModelAddresses.Detail("tenant-a", "party-1");
        PartyDetailSdkReadModel existing = (await store.GetAsync<PartyDetailSdkReadModel>(StoreName, key, TestContext.Current.CancellationToken).ConfigureAwait(true)).Value!;
        DateTimeOffset erasedAt = s_now.AddHours(-2);
        DateTimeOffset modifiedAt = s_now.AddHours(-1);
        await store.SaveAsync(StoreName, key, existing with
        {
            Detail = existing.Detail! with { IsErased = true, ErasedAt = erasedAt, LastModifiedAt = modifiedAt },
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
        var reads = new PartySdkFaultingReadModelStore(store);
        var cache = new PartySdkLastKnownReadModelCache();
        PartySdkQueryService service = Service(reads, cache);
        QueryResult canonical = await ExecuteAsync(service, queryType);
        canonical.Success.ShouldBe(queryType == "ExportPartyData");
        reads.UnavailableKeys.Add(key);

        QueryResult result = await ExecuteAsync(service, queryType);
        cache.TryGetDetail("tenant-a", "party-1", out PartyDetailSdkReadModel? retained).ShouldBeTrue();
        PartyDetail redacted = retained!.Detail!;
        redacted.IsErased.ShouldBeTrue();
        redacted.ErasedAt.ShouldBe(erasedAt);
        redacted.LastModifiedAt.ShouldBe(modifiedAt);
        redacted.PersonDetails.ShouldBeNull();
        redacted.OrganizationDetails.ShouldBeNull();
        redacted.ContactChannels.ShouldBeEmpty();
        redacted.Identifiers.ShouldBeEmpty();
        redacted.ConsentRecords.ShouldBeEmpty();
        redacted.NameHistory.ShouldBeEmpty();
        if (queryType != "ExportPartyData")
        {
            result.Success.ShouldBeFalse();
            result.ErrorMessage.ShouldBe(PartySdkQueryFailureReason.ReadModelUnavailable);
            result.PayloadBytes.ShouldBeNull();
            result.Metadata.ShouldBeNull();
        }
        else
        {
            result.Success.ShouldBeTrue();
            AssertDegraded(result);
            AssertNoPersonalData(result);
            PartyDataPortabilityPackage package = result.GetPayload().Deserialize<PartyDataPortabilityPackage>(PartiesJsonOptions.Default)!;
            package.Status.ShouldBe("Erased");
            package.Party.ShouldBeNull();
            package.ProcessingRecords.ShouldHaveSingleItem();
        }
    }

    /// <summary>Preserves empty cached indexes as degraded established responses.</summary>
    /// <param name="queryType">The index query discriminator.</param>
    [Theory]
    [InlineData("PartyIndex")]
    [InlineData("PartySearch")]
    public async Task EmptyIndexCacheRemainsAvailableWithDegradedMetadataAsync(string queryType)
    {
        var store = new InMemoryReadModelStore();
        string key = PartySdkReadModelAddresses.Index("tenant-a");
        await store.SaveAsync(StoreName, key, new PartyIndexSdkReadModel { ProjectedAt = s_now, ProjectionVersion = "empty" }, TestContext.Current.CancellationToken).ConfigureAwait(true);
        var reads = new PartySdkFaultingReadModelStore(store);
        PartySdkQueryService service = Service(reads, new PartySdkLastKnownReadModelCache());
        (await ExecuteAsync(service, queryType)).Success.ShouldBeTrue();
        reads.UnavailableKeys.Add(key);

        QueryResult result = await ExecuteAsync(service, queryType);
        result.Success.ShouldBeTrue();
        AssertDegraded(result);
        result.GetPayload().GetProperty("items").GetArrayLength().ShouldBe(0);
        result.GetPayload().GetProperty("totalCount").GetInt32().ShouldBe(0);
    }

    /// <summary>Rejects malformed canonical rows without falling back to a formerly empty cache.</summary>
    /// <param name="queryType">The list or search discriminator.</param>
    /// <param name="dictionary">The explicitly null dictionary.</param>
    [Theory]
    [InlineData("PartyIndex", "Entries")]
    [InlineData("PartySearch", "Entries")]
    [InlineData("PartyIndex", "LastIndexedEvents")]
    [InlineData("PartySearch", "LastIndexedEvents")]
    [InlineData("PartyIndex", "LastSequenceNumbers")]
    [InlineData("PartySearch", "LastSequenceNumbers")]
    [InlineData("PartyIndex", "ErasureSequenceNumbers")]
    [InlineData("PartySearch", "ErasureSequenceNumbers")]
    public async Task NullIndexDictionariesFailBoundedAndEvictCacheAsync(string queryType, string dictionary)
    {
        var store = new InMemoryReadModelStore();
        string key = PartySdkReadModelAddresses.Index("tenant-a");
        var reads = new PartySdkFaultingReadModelStore(store);
        var cache = new PartySdkLastKnownReadModelCache();
        PartySdkQueryService service = Service(reads, cache);
        await store.SaveAsync(StoreName, key, new PartyIndexSdkReadModel(), TestContext.Current.CancellationToken);
        (await ExecuteAsync(service, queryType)).Success.ShouldBeTrue();
        PartyIndexSdkReadModel corrupt = NullIndexDictionary(dictionary);
        await store.SaveAsync(StoreName, key, corrupt, TestContext.Current.CancellationToken);

        AssertBoundedFailure(await ExecuteAsync(service, queryType));
        cache.TryGetIndex("tenant-a", out _).ShouldBeFalse();
        JsonSerializer.Serialize((await store.GetAsync<PartyIndexSdkReadModel>(StoreName, key, TestContext.Current.CancellationToken)).Value, PartiesJsonOptions.Default)
            .ShouldBe(JsonSerializer.Serialize(corrupt, PartiesJsonOptions.Default));
        reads.UnavailableKeys.Add(key);

        AssertBoundedFailure(await ExecuteAsync(service, queryType));
        cache.TryGetIndex("tenant-a", out _).ShouldBeFalse();
        // An old host/cache API may still have retained an empty-looking malformed row.
        cache.StoreIndex("tenant-a", corrupt);
        AssertBoundedFailure(await ExecuteAsync(service, queryType));
        cache.TryGetIndex("tenant-a", out _).ShouldBeFalse();

        reads.UnavailableKeys.Clear();
        await store.SaveAsync(StoreName, key, new PartyIndexSdkReadModel(), TestContext.Current.CancellationToken);
        QueryResult recovered = await ExecuteAsync(service, queryType);
        recovered.Success.ShouldBeTrue();
        recovered.Metadata!.IsDegraded.ShouldBe(false);
    }

    /// <summary>Invalidates older read tokens when a query discovers canonical corruption.</summary>
    /// <param name="queryType">The list or search discriminator.</param>
    [Theory]
    [InlineData("PartyIndex")]
    [InlineData("PartySearch")]
    public async Task CorruptCanonicalReadInvalidatesEarlierEmptyCacheCandidateAsync(string queryType)
    {
        var store = new InMemoryReadModelStore();
        string key = PartySdkReadModelAddresses.Index("tenant-a");
        await store.SaveAsync(StoreName, key, new PartyIndexSdkReadModel(), TestContext.Current.CancellationToken);
        var captured = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reads = new PartySdkFaultingReadModelStore(store)
        {
            AfterReadAsync = async (_, token) =>
            {
                captured.TrySetResult();
                await release.Task.WaitAsync(token).ConfigureAwait(false);
            },
        };
        var cache = new PartySdkLastKnownReadModelCache();
        PartySdkQueryService service = Service(reads, cache);
        Task<QueryResult> pending = ExecuteAsync(service, queryType);
        try
        {
            await captured.Task.WaitAsync(TestContext.Current.CancellationToken);
            reads.AfterReadAsync = null;
            await store.SaveAsync(StoreName, key, NullIndexDictionary("Entries"), TestContext.Current.CancellationToken);
            AssertBoundedFailure(await ExecuteAsync(service, queryType));
        }
        finally
        {
            release.TrySetResult();
            _ = await pending.ConfigureAwait(true);
        }

        AssertBoundedFailure(await pending.ConfigureAwait(true));
        cache.TryGetIndex("tenant-a", out _).ShouldBeFalse();
        reads.UnavailableKeys.Add(key);
        AssertBoundedFailure(await ExecuteAsync(service, queryType));
    }

    /// <summary>Enforces the recovery plan's ETag through the SDK batch protocol.</summary>
    [Fact]
    public async Task StaleIndexRecoveryPlanCannotOverwriteConcurrentCanonicalDataAsync()
    {
        var store = new InMemoryReadModelStore();
        string key = PartySdkReadModelAddresses.Index("tenant-a");
        await store.SaveAsync(StoreName, key, NullIndexDictionary("Entries"), TestContext.Current.CancellationToken);
        var handler = new PartyIndexSdkProjectionHandler(store, s_options);
        var identity = new DomainSharedProjectionRebuildIdentity("tenant-a", "party", PartyProjectionNames.Index, "recovery", "inventory");
        DomainSharedProjectionRebuildCandidate candidate = await handler.CreateEmptyCandidateAsync(identity, TestContext.Current.CancellationToken);
        var history = new ProjectionRequest("tenant-a", "party", "party-1",
            [new ProjectionEventDto(nameof(PartyCreated), JsonSerializer.SerializeToUtf8Bytes(new PartyCreated { Type = PartyType.Person }, PartiesJsonOptions.Default), "json", 1, s_now, "correlation")]);
        candidate = await handler.AccumulateAsync(identity, candidate, history, TestContext.Current.CancellationToken);
        DomainProjectionRebuildPlan plan = await handler.FinalizeAsync(identity, candidate, TestContext.Current.CancellationToken);
        await SeedAsync(store, "tenant-a", "concurrent-party", "concurrent");
        ReadModelEntry<PartyIndexSdkReadModel> before = await store.GetAsync<PartyIndexSdkReadModel>(StoreName, key, TestContext.Current.CancellationToken);
        var scope = new ReadModelBatchScope(StoreName, "tenant-a", "party", PartySdkReadModelAddresses.SharedIndexAggregateId, PartyProjectionNames.Index, identity.OperationId);

        ReadModelBatchResult result = await store.ExecuteAsync(new ReadModelBatch(scope, plan.Operations), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ReadModelBatchStatus.Conflict);
        result.ConflictKind.ShouldBe(ReadModelBatchConflictKind.Optimistic);
        ReadModelEntry<PartyIndexSdkReadModel> after = await store.GetAsync<PartyIndexSdkReadModel>(StoreName, key, TestContext.Current.CancellationToken);
        after.ETag.ShouldBe(before.ETag);
        JsonSerializer.Serialize(after.Value, PartiesJsonOptions.Default).ShouldBe(JsonSerializer.Serialize(before.Value, PartiesJsonOptions.Default));
        after.Value!.Entries.Keys.ShouldBe(["concurrent-party"]);
    }

    private static PartyIndexSdkReadModel NullIndexDictionary(string dictionary)
        => dictionary switch
        {
            "Entries" => new PartyIndexSdkReadModel { Entries = null! },
            "LastIndexedEvents" => new PartyIndexSdkReadModel { LastIndexedEvents = null! },
            "LastSequenceNumbers" => new PartyIndexSdkReadModel { LastSequenceNumbers = null! },
            "ErasureSequenceNumbers" => new PartyIndexSdkReadModel { ErasureSequenceNumbers = null! },
            _ => throw new ArgumentOutOfRangeException(nameof(dictionary)),
        };

    /// <summary>Preserves an established absent-detail cache as not found.</summary>
    [Fact]
    public async Task AbsentDetailCacheRetainsNotFoundSemanticsAsync()
    {
        var store = new InMemoryReadModelStore();
        string key = PartySdkReadModelAddresses.Detail("tenant-a", "party-1");
        await store.SaveAsync(StoreName, key, new PartyDetailSdkReadModel(), TestContext.Current.CancellationToken);
        var reads = new PartySdkFaultingReadModelStore(store);
        var cache = new PartySdkLastKnownReadModelCache();
        PartySdkQueryService service = Service(reads, cache);
        (await ExecuteAsync(service, "GetParty")).ErrorMessage.ShouldBe(PartySdkQueryFailureReason.ReadModelUnavailable);
        reads.UnavailableKeys.Add(key);
        (await ExecuteAsync(service, "GetParty")).ErrorMessage.ShouldBe(PartySdkQueryFailureReason.ReadModelUnavailable);
        cache.TryGetDetail("tenant-a", "party-1", out _).ShouldBeTrue();
    }

    /// <summary>Rejects unsafe late candidates that complete after another instance erases shared state.</summary>
    /// <param name="queryType">The detail or index query discriminator.</param>
    [Theory]
    [InlineData("GetParty")]
    [InlineData("PartyIndex")]
    public async Task DelayedPreErasureReadCannotAuthorizePersonalFallbackAsync(string queryType)
    {
        var store = new InMemoryReadModelStore();
        await SeedAsync(store, "tenant-a", "party-1", "private-a");
        string key = IsIndex(queryType) ? PartySdkReadModelAddresses.Index("tenant-a") : PartySdkReadModelAddresses.Detail("tenant-a", "party-1");
        var captured = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reads = new PartySdkFaultingReadModelStore(store)
        {
            AfterReadAsync = async (_, cancellationToken) =>
            {
                captured.TrySetResult();
                await release.Task.WaitAsync(cancellationToken).ConfigureAwait(true);
            },
        };
        var cache = new PartySdkLastKnownReadModelCache();
        PartySdkQueryService service = Service(reads, cache);
        Task<QueryResult> pending = ExecuteAsync(service, queryType);
        try
        {
            await captured.Task.WaitAsync(TestContext.Current.CancellationToken);
            await new PartySdkReadModelEraser(store, store, s_options).EraseAsync("tenant-a", "party-1", TestContext.Current.CancellationToken);
            await AssertErasedStoreAsync(store);
        }
        finally
        {
            release.TrySetResult();
            _ = await pending.ConfigureAwait(true);
            reads.AfterReadAsync = null;
        }

        // Canonical consistency is outside DW-172; the old successful read becomes a cache candidate.
        (await pending.ConfigureAwait(true)).Success.ShouldBeTrue();
        reads.UnavailableKeys.Add(key);

        AssertBoundedFailure(await ExecuteAsync(service, queryType));
        (IsIndex(queryType) ? cache.TryGetIndex("tenant-a", out _) : cache.TryGetDetail("tenant-a", "party-1", out _)).ShouldBeFalse();
    }

    /// <summary>Retains Article 30 records after shared erasure when only processing is unavailable.</summary>
    /// <param name="queryType">The GDPR query discriminator.</param>
    /// <param name="warmProcessing">Whether this host has a last-known processing slot.</param>
    [Theory]
    [InlineData("ExportPartyData", true)]
    [InlineData("ExportPartyData", false)]
    [InlineData("GetProcessingRecords", true)]
    [InlineData("GetProcessingRecords", false)]
    public async Task ReadableErasedDetailPreservesProcessingFallbackAsync(string queryType, bool warmProcessing)
    {
        var store = new InMemoryReadModelStore();
        await SeedAsync(store, "tenant-a", "party-1", "private-a");
        var reads = new PartySdkFaultingReadModelStore(store);
        PartySdkQueryService service = Service(reads, new PartySdkLastKnownReadModelCache());
        if (warmProcessing)
        {
            (await ExecuteAsync(service, queryType)).Success.ShouldBeTrue();
        }

        await new PartySdkReadModelEraser(store, store, s_options).EraseAsync("tenant-a", "party-1", TestContext.Current.CancellationToken);
        await AssertErasedStoreAsync(store);
        reads.UnavailableKeys.Add(PartySdkReadModelAddresses.Processing("tenant-a", "party-1"));

        QueryResult result = await ExecuteAsync(service, queryType);
        result.Success.ShouldBeTrue();
        AssertDegraded(result);
        AssertNoPersonalData(result);
        JsonElement records = queryType == "ExportPartyData" ? result.GetPayload().GetProperty("processingRecords") : result.GetPayload();
        records.GetArrayLength().ShouldBe(warmProcessing ? 1 : 0);
    }

    /// <summary>Rejects unsafe cached content even when canonical-read tracking is saturated.</summary>
    /// <param name="queryType">The detail or index query discriminator.</param>
    [Theory]
    [InlineData("GetParty")]
    [InlineData("PartyIndex")]
    public async Task SaturatedTrackingCannotAuthorizePersonalFallbackAsync(string queryType)
    {
        var store = new InMemoryReadModelStore();
        await SeedAsync(store, "tenant-a", "party-1", "private-a");
        var cache = new PartySdkLastKnownReadModelCache(TimeProvider.System, 2, TimeSpan.FromMinutes(1));
        PartySdkQueryService service = Service(store, cache);
        (await ExecuteAsync(service, queryType)).Success.ShouldBeTrue();
        long first = cache.BeginRead("unrelated-first");
        long second = cache.BeginRead("unrelated-second");
        try
        {
            AssertBoundedFailure(await ExecuteAsync(service, queryType));
        }
        finally
        {
            cache.EndRead(first);
            cache.EndRead(second);
        }

        (await ExecuteAsync(service, queryType)).Success.ShouldBeTrue();
    }

    private static async Task SeedAsync(InMemoryReadModelStore store, string tenantId, string partyId, string canary)
    {
        var detail = new PartyDetail
        {
            Id = partyId,
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = canary + "-display",
            SortName = canary + "-sort",
            PersonDetails = new PersonDetails { FirstName = canary + "-first", LastName = canary + "-last" },
            OrganizationDetails = new OrganizationDetails { LegalName = canary + "-legal", TradingName = canary + "-trading" },
            ContactChannels = [new ContactChannel { Id = "email", Type = ContactChannelType.Email, Value = canary + "@example.test" }],
            Identifiers = [new PartyIdentifier { Id = "identifier", Type = IdentifierType.NationalId, Value = canary + "-identifier" }],
            ConsentRecords = [new ConsentRecord { ConsentId = "consent", ChannelId = "email", Purpose = canary + "-purpose", LawfulBasis = LawfulBasis.Consent, GrantedAt = s_now, GrantedBy = canary + "-actor" }],
            NameHistory = [new NameHistoryEntry { DisplayName = canary + "-historic", SortName = canary + "-historic-sort", ChangedAt = s_now }],
            CreatedAt = s_now.AddDays(-1),
            LastModifiedAt = s_now,
        };
        await store.SaveAsync(StoreName, PartySdkReadModelAddresses.Detail(tenantId, partyId), new PartyDetailSdkReadModel
        {
            Detail = detail, LastSequenceNumber = 7, ProjectedAt = s_now, ProjectionVersion = "7",
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await store.SaveAsync(StoreName, PartySdkReadModelAddresses.Index(tenantId), new PartyIndexSdkReadModel
        {
            Entries = new Dictionary<string, PartyIndexEntry>(StringComparer.Ordinal)
            {
                [partyId] = new PartyIndexEntry
                {
                    Id = partyId, Type = PartyType.Person, IsActive = true, DisplayName = detail.DisplayName, SortName = detail.SortName,
                    SearchableContactChannels = detail.ContactChannels, SearchableIdentifiers = detail.Identifiers,
                },
            },
            LastSequenceNumbers = new Dictionary<string, long>(StringComparer.Ordinal) { [partyId] = 7 },
            ProjectedAt = s_now, ProjectionVersion = "global:7",
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await store.SaveAsync(StoreName, PartySdkReadModelAddresses.Processing(tenantId, partyId), new PartyProcessingSdkReadModel
        {
            Records = [new ProcessingActivityRecord
            {
                SequenceNumber = 7, PartyId = partyId, TenantId = tenantId, ActorId = "retained-operator", CorrelationId = "retained-correlation",
                OperationCategory = "Consent", Outcome = "Succeeded", EventType = "ConsentRecorded", Timestamp = s_now, Summary = "Consent preference changed.",
            }],
            LastSequenceNumber = 7, ProjectedAt = s_now, ProjectionVersion = "7",
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    private static async Task AssertErasedStoreAsync(InMemoryReadModelStore store)
    {
        PartyDetailSdkReadModel detail = (await store.GetAsync<PartyDetailSdkReadModel>(StoreName, PartySdkReadModelAddresses.Detail("tenant-a", "party-1"), TestContext.Current.CancellationToken).ConfigureAwait(true)).Value!;
        PartyIndexSdkReadModel index = (await store.GetAsync<PartyIndexSdkReadModel>(StoreName, PartySdkReadModelAddresses.Index("tenant-a"), TestContext.Current.CancellationToken).ConfigureAwait(true)).Value!;
        PartyProcessingSdkReadModel processing = (await store.GetAsync<PartyProcessingSdkReadModel>(StoreName, PartySdkReadModelAddresses.Processing("tenant-a", "party-1"), TestContext.Current.CancellationToken).ConfigureAwait(true)).Value!;
        detail.Detail!.IsErased.ShouldBeTrue();
        detail.ErasureSequenceNumber.ShouldBe(7);
        detail.ErasedAt.ShouldNotBeNull();
        JsonSerializer.Serialize(detail, PartiesJsonOptions.Default).ShouldNotContain("private-a");
        index.Entries.ShouldBeEmpty();
        index.ErasureSequenceNumbers["party-1"].ShouldBe(7);
        processing.ErasureSequenceNumber.ShouldBe(7);
        processing.Records.ShouldHaveSingleItem().ActorId.ShouldBe("retained-operator");
    }

    private static PartySdkQueryService Service(IReadModelStore store, PartySdkLastKnownReadModelCache cache)
    {
        TimeProvider clock = Substitute.For<TimeProvider>();
        clock.GetUtcNow().Returns(s_now);
        return new PartySdkQueryService(store, s_options, clock, new BasicPartySearchProvider(), Substitute.For<IPartyErasureRecordStore>(),
            Substitute.For<IQueryCursorCodec>(), cache, NullLogger<PartySdkQueryService>.Instance);
    }

    private static Task<QueryResult> ExecuteAsync(PartySdkQueryService service, string queryType, string tenantId = "tenant-a", string detailPartyId = "party-1")
    {
        string partyId = IsIndex(queryType) ? PartyIndexQueryTypes.ListAggregateId : detailPartyId;
        var envelope = new QueryEnvelope(tenantId, "party", partyId, queryType,
            JsonSerializer.SerializeToUtf8Bytes(IsIndex(queryType) ? new { query = "private", page = 1, pageSize = 20 } : (object)new { }),
            "correlation-1", "user-1", entityId: partyId);
        // List has a stricter payload than search.
        if (queryType == "PartyIndex")
        {
            envelope = envelope with { Payload = JsonSerializer.SerializeToUtf8Bytes(new { page = 1, pageSize = 20 }) };
        }

        PartySdkQueryHandler handler = queryType switch
        {
            "GetParty" => new GetPartyQueryHandler(service),
            "PartyDetail" => new PartyDetailQueryHandler(service),
            "ExportPartyData" => new ExportPartyDataQueryHandler(service),
            "GetProcessingRecords" => new GetProcessingRecordsQueryHandler(service),
            "PartyIndex" => new PartyIndexQueryHandler(service),
            _ => new PartySearchQueryHandler(service),
        };
        return handler.ExecuteAsync(envelope, TestContext.Current.CancellationToken);
    }

    private static void AssertUnaffectedCache(PartySdkLastKnownReadModelCache cache, string queryType)
    {
        cache.TryGetDetail("tenant-a", "party-2", out PartyDetailSdkReadModel? sameTenant).ShouldBeTrue();
        sameTenant!.Detail!.Id.ShouldBe("party-2");
        sameTenant.Detail.DisplayName.ShouldBe("unaffected-a-display");
        sameTenant.Detail.IsErased.ShouldBeFalse();
        if (IsIndex(queryType))
        {
            cache.TryGetIndex("tenant-b", out PartyIndexSdkReadModel? index).ShouldBeTrue();
            index!.Entries.ShouldHaveSingleItem().Value.DisplayName.ShouldBe("private-b-display");
        }
        else
        {
            cache.TryGetDetail("tenant-b", "party-1", out PartyDetailSdkReadModel? detail).ShouldBeTrue();
            detail!.Detail!.DisplayName.ShouldBe("private-b-display");
            detail.Detail.IsErased.ShouldBeFalse();
            if (queryType is "ExportPartyData" or "GetProcessingRecords")
            {
                cache.TryGetProcessing("tenant-b", "party-1", out PartyProcessingSdkReadModel? processing).ShouldBeTrue();
                processing!.Records.ShouldHaveSingleItem().TenantId.ShouldBe("tenant-b");
            }
        }
    }

    private static void AssertUnaffectedPayload(QueryResult result, string queryType)
    {
        result.Success.ShouldBeTrue();
        result.Metadata!.IsDegraded.ShouldBe(false);
        JsonElement payload = result.GetPayload();
        if (IsIndex(queryType))
        {
            JsonElement item = payload.GetProperty("items").EnumerateArray().ShouldHaveSingleItem();
            if (queryType == "PartySearch")
            {
                item = item.GetProperty("party");
            }

            item.GetProperty("id").GetString().ShouldBe("party-1");
            item.GetProperty("displayName").GetString().ShouldBe("private-b-display");
        }
        else if (queryType == "GetProcessingRecords")
        {
            ProcessingActivityRecord record = payload.Deserialize<ProcessingActivityRecord[]>(PartiesJsonOptions.Default)!.ShouldHaveSingleItem();
            record.PartyId.ShouldBe("party-1");
            record.TenantId.ShouldBe("tenant-b");
            record.ActorId.ShouldBe("retained-operator");
        }
        else
        {
            PartyDetail detail = queryType == "ExportPartyData"
                ? payload.Deserialize<PartyDataPortabilityPackage>(PartiesJsonOptions.Default)!.Party!
                : payload.Deserialize<PartyDetail>(PartiesJsonOptions.Default)!;
            detail.Id.ShouldBe("party-1");
            detail.DisplayName.ShouldBe("private-b-display");
            detail.IsErased.ShouldBeFalse();
        }
    }

    private static bool IsIndex(string queryType) => queryType is "PartyIndex" or "PartySearch";

    private static void AssertBoundedFailure(QueryResult result)
    {
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(PartySdkQueryFailureReason.QueryExecutionFailure);
        result.Metadata.ShouldBeNull();
        result.PayloadBytes.ShouldBeNull();
        AssertNoPersonalData(result);
    }

    private static void AssertNoPersonalData(QueryResult result)
    {
        string serialized = JsonSerializer.Serialize(result, PartiesJsonOptions.Default);
        if (result.PayloadBytes is { Length: > 0 } payload)
        {
            serialized += Encoding.UTF8.GetString(payload);
        }

        serialized.ShouldNotContain("private-a");
    }

    private static void AssertDegraded(QueryResult result)
    {
        result.Metadata!.IsDegraded.ShouldBe(true);
        result.Metadata.IsStale.ShouldBe(true);
        result.Metadata.Lifecycle.ShouldBe(ProjectionLifecycleState.Stale);
        result.Metadata.WarningCodes!.ShouldContain(ProjectionFreshnessMetadata.WarningProjectionStateStoreUnavailable);
    }
}
