using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Testing.Fakes;
using Hexalith.Memories.Client.Rest;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Projections.Configuration;
using Hexalith.Parties.Projections.Handlers;
using Hexalith.Parties.Projections.Models;
using Hexalith.Parties.Projections.Search;
using Hexalith.Parties.Projections.Services;
using Hexalith.Parties.Search;
using Hexalith.Parties.Tests.Gateway;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.Tests.Search;

/// <summary>Verifies canonical erasure wins over independent search notification dispatches.</summary>
public sealed class PartySearchErasureFenceTests
{
    private const string StoreName = "statestore";
    // This fixture owns the complete synthetic store, including any private SDK recovery state.
    private static readonly IOptions<PartySdkReadModelOptions> s_options = Options.Create(
        new PartySdkReadModelOptions { ReadModelStateStoreName = StoreName, HistoricalPrivateRecoveryReconciliationAttested = true });

    /// <summary>Covers live persistence and rebuild removal windows before a stale handoff.</summary>
    /// <param name="rebuild">Whether to resume shared-rebuild completion.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ErasureBeforeHandoffDoesNotRepublishSnapshotAsync(bool rebuild)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        CancellationToken token = deadline.Token;
        var store = new InMemoryReadModelStore();
        var remote = new ConcurrentDictionary<(string Tenant, string Party), PartyIndexEntry>();
        IPartyIndexSearchIndexer search = Search(remote);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<DomainProjectionHandlerResult> pending;
        if (rebuild)
        {
            await SeedAsync(store, "tenant-a", "stale-party");
            var handler = new PartyIndexSdkProjectionHandler(store, s_options, search);
            var identity = Identity();
            DomainSharedProjectionRebuildCandidate candidate = await CandidateAsync(handler, identity);
            DomainProjectionRebuildPlan plan = await CommitAsync(store, handler, identity, candidate);
            search.NotifyRemovedAsync("tenant-a", "stale-party", Arg.Any<CancellationToken>())
                .Returns(async call =>
                {
                    entered.TrySetResult();
                    await release.Task.WaitAsync(call.Arg<CancellationToken>()).ConfigureAwait(false);
                    return true;
                });
            pending = handler.CompleteRebuildAsync(identity, candidate, plan.CompletionState, token);
        }
        else
        {
            IReadModelStore writer = Substitute.For<IReadModelStore>();
            writer.GetAsync<PartyIndexSdkReadModel>(StoreName, Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(call => store.GetAsync<PartyIndexSdkReadModel>(StoreName, call.ArgAt<string>(1), call.Arg<CancellationToken>()));
            writer.TrySaveAsync(StoreName, Arg.Any<string>(), Arg.Any<PartyIndexSdkReadModel>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(async call =>
                {
                    bool saved = await store.TrySaveAsync(StoreName, call.ArgAt<string>(1), call.Arg<PartyIndexSdkReadModel>(),
                        call.ArgAt<string>(3), call.Arg<CancellationToken>()).ConfigureAwait(false);
                    entered.TrySetResult();
                    await release.Task.WaitAsync(call.Arg<CancellationToken>()).ConfigureAwait(false);
                    return saved;
                });
            pending = new PartyIndexSdkProjectionHandler(writer, s_options, search)
                .ProjectAsync(Request(), "live", token);
        }

        try
        {
            await Task.WhenAny(entered.Task, pending).WaitAsync(token).ConfigureAwait(true);
            entered.Task.IsCompletedSuccessfully.ShouldBeTrue("the notification must reach the controlled barrier");
            await new PartySdkReadModelEraser(store, store, s_options, search)
                .EraseAsync("tenant-a", "party-1", token);
        }
        finally
        {
            release.TrySetResult();
            _ = await pending.ConfigureAwait(true);
        }

        (await pending.ConfigureAwait(true)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        remote.ContainsKey(("tenant-a", "party-1")).ShouldBeFalse();
        await search.DidNotReceive().NotifyIndexedAsync("tenant-a", Arg.Is<PartyIndexEntry>(e => e.Id == "party-1"),
            Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<long?>(), Arg.Any<CancellationToken>());
        await AssertTombstoneAsync(store);
    }

    /// <summary>Compensates accepted indexing after erasure has already finished its cleanup.</summary>
    /// <param name="rebuild">Whether to resume shared-rebuild completion.</param>
    /// <param name="cleanupFails">Whether the first post-index cleanup must be retried.</param>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task ErasureDuringIndexingCleansLateResultAndRetriesFailureAsync(bool rebuild, bool cleanupFails)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        CancellationToken token = deadline.Token;
        var store = new InMemoryReadModelStore();
        var remote = new ConcurrentDictionary<(string Tenant, string Party), PartyIndexEntry>();
        IPartyIndexSearchIndexer search = Search(remote);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool rejectCleanup = false;
        search.NotifyIndexedAsync("tenant-a", Arg.Any<PartyIndexEntry>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<long?>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                entered.TrySetResult();
                await release.Task.WaitAsync(call.Arg<CancellationToken>()).ConfigureAwait(false);
                remote[("tenant-a", call.Arg<PartyIndexEntry>().Id)] = call.Arg<PartyIndexEntry>();
                return true;
            });
        search.NotifyRemovedAsync("tenant-a", "party-1", Arg.Any<CancellationToken>()).Returns(call =>
        {
            if (rejectCleanup)
            {
                return false;
            }

            remote.TryRemove(("tenant-a", "party-1"), out _);
            return true;
        });
        var handler = new PartyIndexSdkProjectionHandler(store, s_options, search);
        var identity = Identity();
        DomainSharedProjectionRebuildCandidate candidate = await CandidateAsync(handler, identity);
        DomainProjectionRebuildPlan? plan = rebuild ? await CommitAsync(store, handler, identity, candidate) : null;
        Task<DomainProjectionHandlerResult> pending = rebuild
            ? handler.CompleteRebuildAsync(identity, candidate, plan!.CompletionState, token)
            : handler.ProjectAsync(Request(), "live", token);
        try
        {
            await Task.WhenAny(entered.Task, pending).WaitAsync(token).ConfigureAwait(true);
            entered.Task.IsCompletedSuccessfully.ShouldBeTrue("the notification must reach the controlled barrier");
            await new PartySdkReadModelEraser(store, store, s_options, search)
                .EraseAsync("tenant-a", "party-1", token);
            rejectCleanup = cleanupFails;
        }
        finally
        {
            release.TrySetResult();
            _ = await pending.ConfigureAwait(true);
        }

        DomainProjectionHandlerResult result = await pending.ConfigureAwait(true);
        if (cleanupFails)
        {
            result.Status.ShouldBe(ProjectionDispatchStatus.Retryable);
            result.ReasonCode.ShouldBe("search-reconciliation-required");
            remote.ContainsKey(("tenant-a", "party-1")).ShouldBeTrue();
            rejectCleanup = false;
            handler = new PartyIndexSdkProjectionHandler(store, s_options, search);
            result = rebuild
                ? await handler.CompleteRebuildAsync(identity, candidate, plan!.CompletionState, token)
                : await handler.ProjectAsync(Request(), "retry", token);
        }

        result.Status.ShouldBe(rebuild || !cleanupFails ? ProjectionDispatchStatus.Completed : ProjectionDispatchStatus.AlreadyCompleted);
        remote.ContainsKey(("tenant-a", "party-1")).ShouldBeFalse();
        await search.Received(1).NotifyIndexedAsync("tenant-a", Arg.Any<PartyIndexEntry>(), Arg.Any<string>(),
            Arg.Any<DateTimeOffset>(), Arg.Any<long?>(), Arg.Any<CancellationToken>());
        await AssertTombstoneAsync(store);
    }

    /// <summary>Unknown post-index canonical state never reports successful convergence.</summary>
    [Fact]
    public async Task PostIndexCanonicalOutageReturnsRetryableWithoutDeletingLivePartyAsync()
    {
        var store = new InMemoryReadModelStore();
        var reads = new PartySdkFaultingReadModelStore(store);
        var remote = new ConcurrentDictionary<(string Tenant, string Party), PartyIndexEntry>();
        IPartyIndexSearchIndexer search = Search(remote);
        search.NotifyIndexedAsync(Arg.Any<string>(), Arg.Any<PartyIndexEntry>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<long?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                remote[(call.ArgAt<string>(0), call.Arg<PartyIndexEntry>().Id)] = call.Arg<PartyIndexEntry>();
                reads.UnavailableKeys.Add(PartySdkReadModelAddresses.Index("tenant-a"));
                return true;
            });

        DomainProjectionHandlerResult result = await new PartyIndexSdkProjectionHandler(reads, s_options, search)
            .ProjectAsync(Request(), "live", TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        result.ReasonCode.ShouldBe("search-reconciliation-required");
        remote.ContainsKey(("tenant-a", "party-1")).ShouldBeTrue();
        await search.DidNotReceiveWithAnyArgs().NotifyRemovedAsync(default!, default!, default);
    }

    /// <summary>Verifies late mapping persistence is cleaned through the real Memories adapter.</summary>
    /// <param name="rebuild">Whether to resume shared-rebuild completion.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealAdapterCleansAcceptedProducerWithoutPublishingAfterIndependentErasureAsync(bool rebuild)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        CancellationToken token = deadline.Token;
        var store = new InMemoryReadModelStore();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var remote = new Dictionary<string, string>(StringComparer.Ordinal);
        var mappingStore = new PartySearchErasureMappingStore();
        IPartyMemoryIngestionIntentStore intentStore = PartyMemoryTestServices.CreateIntentStore();
        using var httpHandler = new PartySearchErasureHttpHandler(async (request, requestToken) =>
        {
            if (request.Method == HttpMethod.Post)
            {
                using JsonDocument input = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(requestToken).ConfigureAwait(false));
                input.RootElement.GetProperty("tenantId").GetString().ShouldBe("tenant-a");
                input.RootElement.GetProperty("caseId").GetString().ShouldBe("case-a");
                string content = Encoding.UTF8.GetString(input.RootElement.GetProperty("contentBytes").GetBytesFromBase64());
                content.ShouldContain("SyntheticPrivate");
                entered.TrySetResult();
                await release.Task.WaitAsync(requestToken).ConfigureAwait(false);
                remote["unit-1"] = content;
                return new HttpResponseMessage(HttpStatusCode.Accepted) { Content = JsonContent.Create(new { instanceId = "unit-1" }) };
            }

            if (request.RequestUri!.AbsolutePath.Contains("/ingest/", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                { Content = JsonContent.Create(PartyMemoryRecoveryHandler.Completed("unit-1", "unit-1", "tenant-a", "case-a")) };
            }

            if (request.Method == HttpMethod.Get)
            {
                remote.ShouldBeEmpty();
                return new HttpResponseMessage(HttpStatusCode.NotFound)
                { Content = JsonContent.Create(new { code = "MEMORY_UNIT_NOT_FOUND" }) };
            }

            request.Method.ShouldBe(HttpMethod.Delete);
            request.RequestUri!.AbsolutePath.ShouldBe("/api/v1/tenants/tenant-a/cases/case-a/memory-units/unit-1");
            remote.Remove("unit-1").ShouldBeTrue();
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        using var http = new HttpClient(httpHandler) { BaseAddress = new Uri("https://memories.example/") };
        var cache = new OptionsCache<PartyMemorySearchOptions>();
        cache.TryAdd(Options.DefaultName, new PartyMemorySearchOptions
        {
            Enabled = true, CaseId = "case-a", Endpoint = http.BaseAddress, RequireApiToken = false,
        }).ShouldBeTrue();
        using var options = new OptionsMonitor<PartyMemorySearchOptions>(new OptionsFactory<PartyMemorySearchOptions>([], []), [], cache);
        PartyMemoryIndexEntrySearchIndexer Adapter() => new(
            new PartyMemoryIndexingService(new MemoriesClient(http, Options.Create(new MemoriesClientOptions()), NullLogger<MemoriesClient>.Instance),
                mappingStore, options, NullLogger<PartyMemoryIndexingService>.Instance, intentStore),
            new PartyMemoryCleanupService(http, mappingStore, NullLogger<PartyMemoryCleanupService>.Instance, intentStore),
            options, NullLogger<PartyMemoryIndexEntrySearchIndexer>.Instance);
        var handler = new PartyIndexSdkProjectionHandler(store, s_options, Adapter());
        var identity = Identity();
        DomainSharedProjectionRebuildCandidate candidate = await CandidateAsync(handler, identity);
        DomainProjectionRebuildPlan? plan = rebuild ? await CommitAsync(store, handler, identity, candidate) : null;
        Task<DomainProjectionHandlerResult> pending = rebuild
            ? handler.CompleteRebuildAsync(identity, candidate, plan!.CompletionState, token)
            : handler.ProjectAsync(Request(), "live", token);
        try
        {
            await Task.WhenAny(entered.Task, pending).WaitAsync(token).ConfigureAwait(true);
            entered.Task.IsCompletedSuccessfully.ShouldBeTrue("the notification must reach the controlled barrier");
            await new PartySdkReadModelEraser(store, store, s_options, Adapter())
                .EraseAsync("tenant-a", "party-1", token);
            (await mappingStore.GetMappingsAsync("tenant-a", "party-1", token)).ShouldBeEmpty();
            remote.ShouldBeEmpty();
        }
        finally
        {
            release.TrySetResult();
            _ = await pending.ConfigureAwait(true);
        }

        (await pending.ConfigureAwait(true)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        remote.ShouldBeEmpty();
        (await mappingStore.GetMappingsAsync("tenant-a", "party-1", token)).ShouldBeEmpty();
        // Admission closed during the awaited acceptance; canonical provenance in the
        // durable intent allows cleanup without publishing a stale mapping.
        mappingStore.RecordCount.ShouldBe(0);
        mappingStore.RemovalCount.ShouldBe(0);
        await AssertTombstoneAsync(store);
    }

    /// <summary>A re-add racing removal stays retryable and is restored by a fresh handler.</summary>
    [Fact]
    public async Task RebuildRemovalRacingLiveReAddRestoresCurrentEntryOnRetryAsync()
    {
        var store = new InMemoryReadModelStore();
        await SeedAsync(store, "tenant-a", "stale-party");
        var remote = new ConcurrentDictionary<(string Tenant, string Party), PartyIndexEntry>();
        IPartyIndexSearchIndexer search = Search(remote);
        var handler = new PartyIndexSdkProjectionHandler(store, s_options, search);
        var identity = Identity();
        DomainSharedProjectionRebuildCandidate candidate = await CandidateAsync(handler, identity);
        DomainProjectionRebuildPlan plan = await CommitAsync(store, handler, identity, candidate);
        search.NotifyRemovedAsync("tenant-a", "stale-party", Arg.Any<CancellationToken>()).Returns(async call =>
        {
            await SeedAsync(store, "tenant-a", "stale-party").ConfigureAwait(false);
            remote.TryRemove(("tenant-a", "stale-party"), out _);
            return true;
        });

        DomainProjectionHandlerResult first = await handler.CompleteRebuildAsync(identity, candidate, plan.CompletionState, TestContext.Current.CancellationToken);
        first.Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        first.ReasonCode.ShouldBe("search-reconciliation-required");
        handler = new PartyIndexSdkProjectionHandler(store, s_options, search);
        (await handler.CompleteRebuildAsync(identity, candidate, plan.CompletionState, TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        remote.Keys.ShouldContain(("tenant-a", "stale-party"));
        remote.Keys.ShouldContain(("tenant-a", "party-1"));
        (await store.GetAsync<PartyIndexSdkReadModel>(StoreName, PartySdkReadModelAddresses.Index("tenant-a"), TestContext.Current.CancellationToken))
            .Value!.Entries.Keys.Order().ShouldBe(["party-1", "stale-party"]);
        await search.Received(1).NotifyRemovedAsync("tenant-a", "stale-party", Arg.Any<CancellationToken>());
    }

    /// <summary>Canonical changes during indexing require retry without affecting unrelated scopes.</summary>
    [Fact]
    public async Task ChangedTargetRetriesAndPreservesOtherPartiesAndTenantsAsync()
    {
        var store = new InMemoryReadModelStore();
        var remote = new ConcurrentDictionary<(string Tenant, string Party), PartyIndexEntry>();
        IPartyIndexSearchIndexer search = Search(remote);
        var handler = new PartyIndexSdkProjectionHandler(store, s_options, search);
        await handler.ProjectAsync(Request("tenant-b"), "seed-b", TestContext.Current.CancellationToken);
        await handler.ProjectAsync(Request(party: "party-2"), "seed-a", TestContext.Current.CancellationToken);
        ProjectionRequest currentReplay = Request() with
        {
            Events = [.. Request().Events,
                new ProjectionEventDto(nameof(PartyDeactivated), JsonSerializer.SerializeToUtf8Bytes(new PartyDeactivated(), PartiesJsonOptions.Default),
                    "json", 2, DateTimeOffset.UnixEpoch.AddMinutes(1), "correlation")],
        };
        string tenantBKey = PartySdkReadModelAddresses.Index("tenant-b");
        string otherTenantBefore = JsonSerializer.Serialize(
            (await store.GetAsync<PartyIndexSdkReadModel>(StoreName, tenantBKey, TestContext.Current.CancellationToken)).Value,
            PartiesJsonOptions.Default);
        PartyIndexEntry otherPartyBefore = remote[("tenant-a", "party-2")];
        PartyIndexEntry otherTenantBeforeEntry = remote[("tenant-b", "party-1")];
        search.NotifyIndexedAsync("tenant-a", Arg.Is<PartyIndexEntry>(e => e.Id == "party-1"), Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<long?>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                PartyIndexEntry entry = call.Arg<PartyIndexEntry>();
                remote[("tenant-a", entry.Id)] = entry;
                if (entry.IsActive)
                {
                    ProjectionRequest update = currentReplay with { Events = [currentReplay.Events[^1]] };
                    await new PartyIndexSdkProjectionHandler(store, s_options).ProjectAsync(update, "newer", call.Arg<CancellationToken>()).ConfigureAwait(false);
                }

                return true;
            });
        DomainProjectionHandlerResult first = await handler.ProjectAsync(Request(), "live", TestContext.Current.CancellationToken);
        first.Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        first.ReasonCode.ShouldBe("search-reconciliation-required");
        string tenantAKey = PartySdkReadModelAddresses.Index("tenant-a");
        string committed = JsonSerializer.Serialize(
            (await store.GetAsync<PartyIndexSdkReadModel>(StoreName, tenantAKey, TestContext.Current.CancellationToken)).Value,
            PartiesJsonOptions.Default);
        handler = new PartyIndexSdkProjectionHandler(store, s_options, search);
        DomainProjectionHandlerResult stale = await handler.ProjectAsync(Request(), "retry-older", TestContext.Current.CancellationToken);
        stale.Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        stale.ReasonCode.ShouldBe("search-reconciliation-required");
        remote[("tenant-a", "party-1")].IsActive.ShouldBeTrue();
        await search.Received(1).NotifyIndexedAsync("tenant-a", Arg.Is<PartyIndexEntry>(entry => entry.Id == "party-1"),
            Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<long?>(), Arg.Any<CancellationToken>());
        JsonSerializer.Serialize((await store.GetAsync<PartyIndexSdkReadModel>(StoreName, tenantAKey, TestContext.Current.CancellationToken)).Value,
            PartiesJsonOptions.Default).ShouldBe(committed);

        handler = new PartyIndexSdkProjectionHandler(store, s_options, search);
        (await handler.ProjectAsync(currentReplay, "retry-current-complete", TestContext.Current.CancellationToken))
            .Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        await search.Received(1).NotifyIndexedAsync("tenant-a", Arg.Is<PartyIndexEntry>(entry => entry.Id == "party-1" && !entry.IsActive),
            nameof(PartyDeactivated), currentReplay.Events[^1].Timestamp, 2, Arg.Any<CancellationToken>());
        JsonSerializer.Serialize((await store.GetAsync<PartyIndexSdkReadModel>(StoreName, tenantAKey, TestContext.Current.CancellationToken)).Value,
            PartiesJsonOptions.Default).ShouldBe(committed);
        JsonSerializer.Serialize((await store.GetAsync<PartyIndexSdkReadModel>(StoreName, tenantBKey, TestContext.Current.CancellationToken)).Value,
            PartiesJsonOptions.Default).ShouldBe(otherTenantBefore);
        remote[("tenant-a", "party-1")].IsActive.ShouldBeFalse();
        remote[("tenant-a", "party-2")].ShouldBe(otherPartyBefore);
        remote[("tenant-b", "party-1")].ShouldBe(otherTenantBeforeEntry);
        remote[("tenant-a", "party-2")].IsActive.ShouldBeTrue();
        remote[("tenant-b", "party-1")].IsActive.ShouldBeTrue();
        await search.DidNotReceiveWithAnyArgs().NotifyRemovedAsync(default!, default!, default);
    }

    /// <summary>Canonical fencing preserves fields intentionally excluded from index JSON.</summary>
    [Fact]
    public async Task LiveNotificationRetainsTransientSearchFieldsForMatchingCanonicalTargetAsync()
    {
        var store = new InMemoryReadModelStore();
        var remote = new ConcurrentDictionary<(string Tenant, string Party), PartyIndexEntry>();
        IPartyIndexSearchIndexer search = Search(remote);
        ProjectionRequest request = Request();
        var contact = new ContactChannelAdded { ContactChannelId = "contact-1", Type = ContactChannelType.Email, Value = "synthetic-private@example.test" };
        var identifier = new IdentifierAdded { IdentifierId = "identifier-1", Type = IdentifierType.Other, Value = "synthetic-private-identifier" };
        request = request with
        {
            Events = [.. request.Events,
                new ProjectionEventDto(nameof(ContactChannelAdded), JsonSerializer.SerializeToUtf8Bytes(contact, PartiesJsonOptions.Default),
                    "json", 2, DateTimeOffset.UnixEpoch.AddMinutes(1), "correlation"),
                new ProjectionEventDto(nameof(IdentifierAdded), JsonSerializer.SerializeToUtf8Bytes(identifier, PartiesJsonOptions.Default),
                    "json", 3, DateTimeOffset.UnixEpoch.AddMinutes(2), "correlation")],
        };

        (await new PartyIndexSdkProjectionHandler(store, s_options, search)
            .ProjectAsync(request, "live", TestContext.Current.CancellationToken)).Status.ShouldBe(ProjectionDispatchStatus.Completed);

        remote[("tenant-a", "party-1")].SearchableContactChannels.ShouldHaveSingleItem().Value.ShouldBe(contact.Value);
        remote[("tenant-a", "party-1")].SearchableIdentifiers.ShouldHaveSingleItem().Value.ShouldBe(identifier.Value);
        PartyIndexSdkReadModel persisted = (await store.GetAsync<PartyIndexSdkReadModel>(StoreName,
            PartySdkReadModelAddresses.Index("tenant-a"), TestContext.Current.CancellationToken)).Value!;
        persisted.Entries["party-1"].SearchableContactChannels.ShouldBeEmpty();
        persisted.Entries["party-1"].SearchableIdentifiers.ShouldBeEmpty();
    }

    /// <summary>Unrelated writes during indexing do not cause a false retry.</summary>
    [Fact]
    public async Task UnrelatedCanonicalWriteDuringIndexingStillConvergesAsync()
    {
        var store = new InMemoryReadModelStore();
        var remote = new ConcurrentDictionary<(string Tenant, string Party), PartyIndexEntry>();
        IPartyIndexSearchIndexer search = Search(remote);
        search.NotifyIndexedAsync("tenant-a", Arg.Any<PartyIndexEntry>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<long?>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                remote[("tenant-a", call.Arg<PartyIndexEntry>().Id)] = call.Arg<PartyIndexEntry>();
                await SeedAsync(store, "tenant-a", "party-2").ConfigureAwait(false);
                return true;
            });

        (await new PartyIndexSdkProjectionHandler(store, s_options, search)
            .ProjectAsync(Request(), "live", TestContext.Current.CancellationToken)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await store.GetAsync<PartyIndexSdkReadModel>(StoreName, PartySdkReadModelAddresses.Index("tenant-a"), TestContext.Current.CancellationToken))
            .Value!.Entries.Keys.Order().ShouldBe(["party-1", "party-2"]);
    }

    /// <summary>A failed canonical check immediately before notification authorizes no external work.</summary>
    [Fact]
    public async Task PreNotificationCanonicalOutageReturnsRetryableWithoutExternalWorkAsync()
    {
        var store = new InMemoryReadModelStore();
        await SeedAsync(store, "tenant-a", "party-1");
        var reads = new PartySdkFaultingReadModelStore(store);
        reads.AfterReadAsync = (key, _) =>
        {
            reads.UnavailableKeys.Add(key);
            return Task.CompletedTask;
        };
        IPartyIndexSearchIndexer search = Substitute.For<IPartyIndexSearchIndexer>();

        (await new PartyIndexSdkProjectionHandler(reads, s_options, search)
            .ProjectAsync(Request(), "retry", TestContext.Current.CancellationToken)).Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        search.ReceivedCalls().ShouldBeEmpty();
    }

    /// <summary>Malformed or missing canonical state cannot authorize indexing or removal.</summary>
    /// <param name="corruption">The reachable serialized-state defect injected after preflight.</param>
    [Theory]
    [InlineData("entries")]
    [InlineData("provenance")]
    [InlineData("sequences")]
    [InlineData("erasure")]
    [InlineData("null-target")]
    [InlineData("wrong-id")]
    [InlineData("missing")]
    public async Task InvalidCanonicalTargetReturnsRetryableWithoutExternalWorkAsync(string corruption)
    {
        var store = new InMemoryReadModelStore();
        await SeedAsync(store, "tenant-a", "party-1");
        var reads = new PartySdkFaultingReadModelStore(store);
        reads.AfterReadAsync = async (key, token) =>
        {
            reads.AfterReadAsync = null;
            ReadModelEntry<PartyIndexSdkReadModel> current = await store.GetAsync<PartyIndexSdkReadModel>(StoreName, key, token).ConfigureAwait(false);
            if (corruption == "missing")
            {
                (await store.TryEraseAsync(StoreName, key, current.ETag!, token).ConfigureAwait(false)).ShouldBeTrue();
                return;
            }

            PartyIndexSdkReadModel model = current.Value!;
            model = corruption switch
            {
                "entries" => model with { Entries = null! },
                "provenance" => model with { LastIndexedEvents = null! },
                "sequences" => model with { LastSequenceNumbers = null! },
                "erasure" => model with { ErasureSequenceNumbers = null! },
                "null-target" => model with { Entries = new Dictionary<string, PartyIndexEntry> { ["party-1"] = null! } },
                _ => model with { Entries = new Dictionary<string, PartyIndexEntry> { ["party-1"] = model.Entries["party-1"] with { Id = "party-2" } } },
            };
            await store.SaveAsync(StoreName, key, model, token).ConfigureAwait(false);
        };
        IPartyIndexSearchIndexer search = Substitute.For<IPartyIndexSearchIndexer>();

        DomainProjectionHandlerResult result = await new PartyIndexSdkProjectionHandler(reads, s_options, search)
            .ProjectAsync(Request(), "retry", TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        result.ReasonCode.ShouldBe("search-reconciliation-required");
        search.ReceivedCalls().ShouldBeEmpty();
    }

    /// <summary>A failed post-removal check cannot claim convergence, and a fresh dispatch recovers.</summary>
    [Fact]
    public async Task PostRemovalCanonicalOutageRequiresFreshDispatchAsync()
    {
        var store = new InMemoryReadModelStore();
        await SeedAsync(store, "tenant-a", "party-1");
        await new PartySdkReadModelEraser(store, store, s_options).EraseAsync("tenant-a", "party-1", TestContext.Current.CancellationToken);
        var reads = new PartySdkFaultingReadModelStore(store);
        IPartyIndexSearchIndexer search = Substitute.For<IPartyIndexSearchIndexer>();
        search.NotifyRemovedAsync("tenant-a", "party-1", Arg.Any<CancellationToken>()).Returns(_ =>
        {
            reads.UnavailableKeys.Add(PartySdkReadModelAddresses.Index("tenant-a"));
            return true;
        });
        (await new PartyIndexSdkProjectionHandler(reads, s_options, search)
            .ProjectAsync(Request(), "retry", TestContext.Current.CancellationToken)).Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        (await new PartyIndexSdkProjectionHandler(store, s_options, search)
            .ProjectAsync(Request(), "fresh-retry", TestContext.Current.CancellationToken)).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        await search.Received(2).NotifyRemovedAsync("tenant-a", "party-1", Arg.Any<CancellationToken>());
        await AssertTombstoneAsync(store);
    }

    /// <summary>Cancellation during removal or compensation propagates and leaves retryable inventory.</summary>
    /// <param name="rebuild">Whether to resume shared-rebuild completion.</param>
    /// <param name="compensating">Whether erasure occurs within indexing.</param>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task RemovalCancellationPropagatesAndFreshDispatchRecoversAsync(bool rebuild, bool compensating)
    {
        var store = new InMemoryReadModelStore();
        await SeedAsync(store, "tenant-a", "party-1");
        var remote = new ConcurrentDictionary<(string Tenant, string Party), PartyIndexEntry>();
        IPartyIndexSearchIndexer search = Search(remote);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var handler = new PartyIndexSdkProjectionHandler(store, s_options, search);
        var identity = Identity();
        DomainSharedProjectionRebuildCandidate candidate = await CandidateAsync(handler, identity);
        DomainProjectionRebuildPlan? plan = rebuild ? await CommitAsync(store, handler, identity, candidate) : null;
        search.NotifyIndexedAsync(Arg.Any<string>(), Arg.Any<PartyIndexEntry>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<long?>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                remote[("tenant-a", "party-1")] = call.Arg<PartyIndexEntry>();
                await new PartySdkReadModelEraser(store, store, s_options)
                    .EraseAsync("tenant-a", "party-1", call.Arg<CancellationToken>()).ConfigureAwait(false);
                return true;
            });
        search.NotifyRemovedAsync("tenant-a", "party-1", Arg.Any<CancellationToken>()).Returns(call =>
        {
            cancellation.Cancel();
            return Task.FromCanceled<bool>(cancellation.Token);
        });
        if (!compensating)
        {
            await new PartySdkReadModelEraser(store, store, s_options).EraseAsync("tenant-a", "party-1", TestContext.Current.CancellationToken);
        }

        await Should.ThrowAsync<OperationCanceledException>(() => rebuild
            ? handler.CompleteRebuildAsync(identity, candidate, plan!.CompletionState, cancellation.Token)
            : handler.ProjectAsync(Request(), "retry", cancellation.Token));

        search.NotifyRemovedAsync("tenant-a", "party-1", Arg.Any<CancellationToken>()).Returns(call =>
        {
            remote.TryRemove(("tenant-a", "party-1"), out _);
            return Task.FromResult(true);
        });
        handler = new PartyIndexSdkProjectionHandler(store, s_options, search);
        DomainProjectionHandlerResult result = rebuild
            ? await handler.CompleteRebuildAsync(identity, candidate, plan!.CompletionState, TestContext.Current.CancellationToken)
            : await handler.ProjectAsync(Request(), "fresh-retry", TestContext.Current.CancellationToken);
        result.Status.ShouldBe(rebuild ? ProjectionDispatchStatus.Completed : ProjectionDispatchStatus.AlreadyCompleted);
        remote.ShouldBeEmpty();
        await AssertTombstoneAsync(store);
    }

    /// <summary>Forwards cancellation even when an indexer finishes after canceling its caller.</summary>
    [Fact]
    public async Task CancellationAfterIndexingPropagatesAsync()
    {
        var store = new InMemoryReadModelStore();
        IPartyIndexSearchIndexer search = Substitute.For<IPartyIndexSearchIndexer>();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        search.NotifyIndexedAsync(Arg.Any<string>(), Arg.Any<PartyIndexEntry>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<long?>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                cancellation.Cancel();
                return true;
            });

        await Should.ThrowAsync<OperationCanceledException>(() => new PartyIndexSdkProjectionHandler(store, s_options, search)
            .ProjectAsync(Request(), "live", cancellation.Token));
        (await store.GetAsync<PartyIndexSdkReadModel>(StoreName, PartySdkReadModelAddresses.Index("tenant-a"), TestContext.Current.CancellationToken))
            .Value!.Entries.ContainsKey("party-1").ShouldBeTrue();
    }

    private static IPartyIndexSearchIndexer Search(ConcurrentDictionary<(string Tenant, string Party), PartyIndexEntry> remote)
    {
        IPartyIndexSearchIndexer search = Substitute.For<IPartyIndexSearchIndexer>();
        search.NotifyIndexedAsync(Arg.Any<string>(), Arg.Any<PartyIndexEntry>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<long?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                remote[(call.ArgAt<string>(0), call.Arg<PartyIndexEntry>().Id)] = call.Arg<PartyIndexEntry>();
                return true;
            });
        search.NotifyRemovedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                remote.TryRemove((call.ArgAt<string>(0), call.ArgAt<string>(1)), out _);
                return true;
            });
        return search;
    }

    private static ProjectionRequest Request(string tenant = "tenant-a", string party = "party-1")
    {
        var payload = new PartyCreated
        {
            Type = PartyType.Person,
            PersonDetails = new PersonDetails { FirstName = "SyntheticPrivate", LastName = "Party" },
        };
        return new ProjectionRequest(tenant, "party", party,
        [new ProjectionEventDto(nameof(PartyCreated), JsonSerializer.SerializeToUtf8Bytes(payload, PartiesJsonOptions.Default),
            "json", 1, DateTimeOffset.UnixEpoch, "correlation")]);
    }

    private static async Task SeedAsync(InMemoryReadModelStore store, string tenant, string party)
        => (await new PartyIndexSdkProjectionHandler(store, s_options)
            .ProjectAsync(Request(tenant, party), "seed", TestContext.Current.CancellationToken).ConfigureAwait(false))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);

    private static DomainSharedProjectionRebuildIdentity Identity()
        => new("tenant-a", "party", PartyProjectionNames.Index, "rebuild", "catalog");

    private static async Task<DomainSharedProjectionRebuildCandidate> CandidateAsync(
        PartyIndexSdkProjectionHandler handler, DomainSharedProjectionRebuildIdentity identity)
    {
        DomainSharedProjectionRebuildCandidate candidate = await handler.CreateEmptyCandidateAsync(identity, TestContext.Current.CancellationToken).ConfigureAwait(false);
        return await handler.AccumulateAsync(identity, candidate, Request(), TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    private static async Task<DomainProjectionRebuildPlan> CommitAsync(InMemoryReadModelStore store,
        PartyIndexSdkProjectionHandler handler, DomainSharedProjectionRebuildIdentity identity, DomainSharedProjectionRebuildCandidate candidate)
    {
        DomainProjectionRebuildPlan plan = await handler.FinalizeAsync(identity, candidate, TestContext.Current.CancellationToken).ConfigureAwait(false);
        var scope = new ReadModelBatchScope(StoreName, identity.TenantId, "party", PartySdkReadModelAddresses.SharedIndexAggregateId,
            PartyProjectionNames.Index, identity.OperationId);
        (await store.ExecuteAsync(new ReadModelBatch(scope, plan.Operations), TestContext.Current.CancellationToken).ConfigureAwait(false))
            .Status.ShouldBe(ReadModelBatchStatus.Completed);
        return plan;
    }

    private static async Task AssertTombstoneAsync(InMemoryReadModelStore store)
    {
        PartyIndexSdkReadModel index = (await store.GetAsync<PartyIndexSdkReadModel>(StoreName,
            PartySdkReadModelAddresses.Index("tenant-a"), TestContext.Current.CancellationToken).ConfigureAwait(false)).Value!;
        index.Entries.ContainsKey("party-1").ShouldBeFalse();
        index.ErasureSequenceNumbers.ContainsKey("party-1").ShouldBeTrue();
        JsonSerializer.Serialize(index, PartiesJsonOptions.Default).ShouldNotContain("SyntheticPrivate");
        PartyDetailSdkReadModel detail = (await store.GetAsync<PartyDetailSdkReadModel>(StoreName,
            PartySdkReadModelAddresses.Detail("tenant-a", "party-1"), TestContext.Current.CancellationToken).ConfigureAwait(false)).Value!;
        detail.ErasedAt.ShouldNotBeNull();
        JsonSerializer.Serialize(detail, PartiesJsonOptions.Default).ShouldNotContain("SyntheticPrivate");
    }
}
