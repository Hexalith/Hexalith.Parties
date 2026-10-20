using System.Diagnostics;
using System.Net;

using Hexalith.Parties.Search;
using Hexalith.Parties.Testing;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.Parties.Tests.Search;

public class PartyMemoryCleanupCancellationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationAfterInventoryReadNeverReportsSuccessOrStartsDeletion(bool empty)
    {
        var original = new PartyMemoryUnitMappingEntry("memory-original", "urn:original", "case-original");
        var state = new PartyMemoryMappingState(empty ? [] : [original]);
        var store = CreateStore(state);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var observed = new InterceptedPartyMemoryMappingStore(store, store.RemoveMappingsAsync, cancellation.Cancel);
        int requests = 0;
        using var client = CreateClient((_, _) =>
        {
            requests++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        });
        var service = new PartyMemoryCleanupService(client, observed, NullLogger<PartyMemoryCleanupService>.Instance);

        OperationCanceledException error = await Should.ThrowAsync<OperationCanceledException>(() =>
            service.DeleteByPartyAsync("tenant", "case-fallback", "party", cancellation.Token));

        error.CancellationToken.ShouldBe(cancellation.Token);
        state.Entries.ShouldBe(empty ? [] : [original]);
        requests.ShouldBe(0);
        (state.SaveAttempts + state.DeleteAttempts).ShouldBe(0);
    }

    [Fact]
    public async Task CancellationBeforeAnyDeletionPreservesInventoryWithoutWritingProgress()
    {
        var original = new PartyMemoryUnitMappingEntry("memory-original", "urn:original", "case-original");
        var state = new PartyMemoryMappingState(original);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        int requests = 0;
        using var client = CreateClient((_, _) =>
        {
            requests++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        });
        var service = new PartyMemoryCleanupService(client, CreateStore(state), NullLogger<PartyMemoryCleanupService>.Instance);
        cancellation.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(() =>
            service.DeleteByPartyAsync("tenant", "case-fallback", "party", cancellation.Token));

        state.Entries.ShouldBe([original]);
        requests.ShouldBe(0);
        (state.SaveAttempts + state.DeleteAttempts).ShouldBe(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationAfterConfirmedDeletionPersistsRemainderAndRetryUsesCapturedCase(bool interruptBodyRead)
    {
        var cleaned = new PartyMemoryUnitMappingEntry("memory-cleaned", "urn:cleaned", "case-cleaned");
        var pending = new PartyMemoryUnitMappingEntry("memory-pending", "urn:pending", "case-pending");
        var state = new PartyMemoryMappingState(cleaned, pending);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using var body = new BlockingCleanupResponseStream();
        var secondRequest = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        List<string> paths = [];
        using var client = CreateClient((request, _) =>
        {
            paths.Add(request.RequestUri!.AbsolutePath);
            if (paths.Count == 1)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
            }

            secondRequest.SetResult();
            if (interruptBodyRead)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StreamContent(body),
                });
            }

            cancellation.Cancel();
            return Task.FromCanceled<HttpResponseMessage>(cancellation.Token);
        });
        var service = new PartyMemoryCleanupService(client, CreateStore(state), NullLogger<PartyMemoryCleanupService>.Instance);
        try
        {
            Task<PartyMemoryCleanupResult> cleanup = service.DeleteByPartyAsync("tenant", "case-fallback", "party", cancellation.Token);
            Task boundary = interruptBodyRead ? body.ReadStarted : secondRequest.Task;
            await boundary.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            cancellation.Cancel();

            OperationCanceledException error = await Should.ThrowAsync<OperationCanceledException>(() => cleanup);

            error.CancellationToken.ShouldBe(cancellation.Token);
            state.KeyExists.ShouldBeTrue();
            state.Entries.ShouldBe([pending]);
            state.SaveAttempts.ShouldBe(1);
            state.DeleteAttempts.ShouldBe(0);

            // A fresh service/store reads the persisted remainder, not the original snapshot.
            using var retryClient = CreateClient((request, _) =>
            {
                paths.Add(request.RequestUri!.AbsolutePath);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
            });
            var retryService = new PartyMemoryCleanupService(retryClient, CreateStore(state), NullLogger<PartyMemoryCleanupService>.Instance);
            PartyMemoryCleanupResult retry = await retryService.DeleteByPartyAsync(
                "tenant", "case-new-fallback", "party", TestContext.Current.CancellationToken);

            retry.Cleaned.ShouldBeTrue();
            state.Entries.ShouldBeEmpty();
            paths.ShouldBe([
                "/api/v1/tenants/tenant/cases/case-cleaned/memory-units/memory-cleaned",
                "/api/v1/tenants/tenant/cases/case-pending/memory-units/memory-pending",
                "/api/v1/tenants/tenant/cases/case-pending/memory-units/memory-pending",
            ]);
        }
        finally
        {
            cancellation.Cancel();
        }
    }

    [Theory]
    [InlineData(HttpStatusCode.NoContent)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task CancellationBeforeLastResponseIsObservedRetainsItsMappingRegardlessOfStatus(HttpStatusCode lastStatus)
    {
        var cleaned = new PartyMemoryUnitMappingEntry("memory-cleaned", "urn:cleaned", "case-cleaned");
        var last = new PartyMemoryUnitMappingEntry("memory-last", "urn:last", "case-last");
        var state = new PartyMemoryMappingState(cleaned, last);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        int requests = 0;
        using var client = CreateClient((_, _) =>
        {
            requests++;
            if (requests == 1)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
            }

            cancellation.Cancel();
            return Task.FromResult(new HttpResponseMessage(lastStatus));
        });
        var service = new PartyMemoryCleanupService(client, CreateStore(state), NullLogger<PartyMemoryCleanupService>.Instance);

        OperationCanceledException error = await Should.ThrowAsync<OperationCanceledException>(() =>
            service.DeleteByPartyAsync("tenant", "case-fallback", "party", cancellation.Token));

        error.CancellationToken.ShouldBe(cancellation.Token);
        // HttpClient observes cancellation before delivering even a 204 response. That
        // deletion is ambiguous, so only the earlier confirmed target can be removed.
        state.Entries.ShouldBe([last]);
        state.KeyExists.ShouldBeTrue();
        state.DeleteAttempts.ShouldBe(0);
        requests.ShouldBe(2);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationDuringProgressPersistenceWaitsForTheIndependentWriteBeforePropagating(bool partialFailure)
    {
        var cleaned = new PartyMemoryUnitMappingEntry("memory-cleaned", "urn:cleaned", "case-cleaned");
        var failed = new PartyMemoryUnitMappingEntry("memory-failed", "urn:failed", "case-failed");
        var state = new PartyMemoryMappingState(partialFailure ? [cleaned, failed] : [cleaned]);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var writeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finishWrite = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        state.BeforeSave = async () =>
        {
            writeStarted.SetResult();
            await finishWrite.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken).ConfigureAwait(false);
        };
        var store = CreateStore(state);
        IPartyMemoryUnitMappingStore observed = ObserveRemoval(store, async (tenant, party, targets, token) =>
        {
            token.CanBeCanceled.ShouldBeTrue();
            token.ShouldNotBe(cancellation.Token);
            int remaining = await store.RemoveMappingsAsync(tenant, party, targets, token).ConfigureAwait(false);
            token.IsCancellationRequested.ShouldBeFalse();
            return remaining;
        });
        int requests = 0;
        using var client = CreateClient((_, _) => Task.FromResult(new HttpResponseMessage(
            ++requests == 1 ? HttpStatusCode.NoContent : HttpStatusCode.ServiceUnavailable)));
        var service = new PartyMemoryCleanupService(client, observed, NullLogger<PartyMemoryCleanupService>.Instance);
        Task<PartyMemoryCleanupResult> cleanup = service.DeleteByPartyAsync("tenant", "case-fallback", "party", cancellation.Token);
        try
        {
            await writeStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            cancellation.Cancel();
            cleanup.IsCompleted.ShouldBeFalse();
        }
        finally
        {
            finishWrite.TrySetResult();
        }

        OperationCanceledException error = await Should.ThrowAsync<OperationCanceledException>(() => cleanup);

        error.CancellationToken.ShouldBe(cancellation.Token);
        state.Entries.ShouldBe(partialFailure ? [failed] : []);
        state.KeyExists.ShouldBeTrue();
        state.SaveAttempts.ShouldBe(1);
    }

    [Theory]
    [InlineData("append")]
    [InlineData("case")]
    [InlineData("source")]
    [InlineData("unit")]
    public async Task CancelledProgressReconciliationRetriesConflictsAndPreservesChangedOrAddedMappings(string change)
    {
        var cleaned = new PartyMemoryUnitMappingEntry("memory-cleaned", "urn:cleaned", "case-cleaned");
        var pending = new PartyMemoryUnitMappingEntry("memory-pending", "urn:pending", "case-pending");
        var added = new PartyMemoryUnitMappingEntry("memory-added", "urn:added", "case-added");
        PartyMemoryUnitMappingEntry changed = change switch
        {
            "case" => cleaned with { CaseId = "case-new" },
            "source" => cleaned with { SourceUri = "urn:new" },
            "unit" => cleaned with { MemoryUnitId = "memory-new" },
            _ => added,
        };
        var state = new PartyMemoryMappingState(cleaned, pending);
        var store = CreateStore(state);
        state.BeforeMutation = async () =>
        {
            if (change == "append")
            {
                await store.RecordMappingAsync("tenant", "party", added.MemoryUnitId, added.SourceUri,
                    added.CaseId!, TestContext.Current.CancellationToken).ConfigureAwait(false);
            }
            else
            {
                state.SetEntries([changed, pending]);
            }
        };
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        int requests = 0;
        using var client = CreateClient((_, _) =>
        {
            if (++requests == 1)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
            }

            cancellation.Cancel();
            return Task.FromCanceled<HttpResponseMessage>(cancellation.Token);
        });
        var service = new PartyMemoryCleanupService(client, store, NullLogger<PartyMemoryCleanupService>.Instance);

        await Should.ThrowAsync<OperationCanceledException>(() =>
            service.DeleteByPartyAsync("tenant", "case-fallback", "party", cancellation.Token));

        IReadOnlyList<PartyMemoryUnitMappingEntry> persisted = await CreateStore(state)
            .GetMappingsAsync("tenant", "party", TestContext.Current.CancellationToken);
        persisted.ShouldBe(change == "append" ? [pending, added] : [changed, pending]);
        state.SaveAttempts.ShouldBe(change == "append" ? 3 : 1);
        state.DeleteAttempts.ShouldBe(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RecoveryFailureRetainsInventoryAndLogsOnlyItsTypeWithoutMaskingCallerCancellation(bool cancelDuringReconciliation)
    {
        const string privateMessage = "tenant-private party-private urn:private case-private Ada Lovelace";
        var cleaned = new PartyMemoryUnitMappingEntry("memory-cleaned", "urn:cleaned", "case-cleaned");
        var pending = new PartyMemoryUnitMappingEntry("memory-pending", "urn:pending", "case-pending");
        var state = new PartyMemoryMappingState(cleaned, pending);
        var store = CreateStore(state);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var logger = new RecordingLogger<PartyMemoryCleanupService>();
        IPartyMemoryUnitMappingStore observed = ObserveRemoval(store, (_, _, _, token) =>
        {
            token.CanBeCanceled.ShouldBeTrue();
            token.IsCancellationRequested.ShouldBeFalse();
            if (cancelDuringReconciliation)
            {
                cancellation.Cancel();
            }

            return Task.FromException<int>(new InvalidOperationException(privateMessage));
        });
        int requests = 0;
        using var client = CreateClient((_, _) =>
        {
            if (++requests == 1)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
            }

            if (!cancelDuringReconciliation)
            {
                cancellation.Cancel();
                return Task.FromCanceled<HttpResponseMessage>(cancellation.Token);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        });
        var service = new PartyMemoryCleanupService(client, observed, logger);

        OperationCanceledException error = await Should.ThrowAsync<OperationCanceledException>(() =>
            service.DeleteByPartyAsync("tenant", "case-fallback", "party", cancellation.Token));

        error.CancellationToken.ShouldBe(cancellation.Token);
        error.ToString().ShouldNotContain(privateMessage);
        state.Entries.ShouldBe([cleaned, pending]);
        (state.SaveAttempts + state.DeleteAttempts).ShouldBe(0);
        logger.Records.ShouldHaveSingleItem();
        logger.Records[0].Level.ShouldBe(LogLevel.Warning);
        logger.Records[0].Exception.ShouldBeNull();
        logger.Records[0].Message.ShouldBe(
            "Memories cleanup progress persistence failed after caller cancellation with InvalidOperationException; cleanup must be retried.");
    }

    [Fact]
    public async Task CommittedProgressSurvivesALostAcknowledgementAndRetryOnlyDeletesThePersistedRemainder()
    {
        var cleaned = new PartyMemoryUnitMappingEntry("memory-cleaned", "urn:cleaned", "case-cleaned");
        var pending = new PartyMemoryUnitMappingEntry("memory-pending", "urn:pending", "case-pending");
        var state = new PartyMemoryMappingState(cleaned, pending);
        var store = CreateStore(state);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var logger = new RecordingLogger<PartyMemoryCleanupService>();
        IPartyMemoryUnitMappingStore observed = ObserveRemoval(store, async (tenant, party, targets, token) =>
        {
            _ = await store.RemoveMappingsAsync(tenant, party, targets, token).ConfigureAwait(false);
            cancellation.Cancel();
            throw new InvalidOperationException("acknowledgement-private");
        });
        List<string> paths = [];
        using var client = CreateClient((request, _) =>
        {
            paths.Add(request.RequestUri!.AbsolutePath);
            return Task.FromResult(new HttpResponseMessage(
                paths.Count == 1 ? HttpStatusCode.NoContent : HttpStatusCode.ServiceUnavailable));
        });
        var service = new PartyMemoryCleanupService(client, observed, logger);

        OperationCanceledException error = await Should.ThrowAsync<OperationCanceledException>(() =>
            service.DeleteByPartyAsync("tenant", "case-fallback", "party", cancellation.Token));

        error.CancellationToken.ShouldBe(cancellation.Token);
        var freshStore = CreateStore(state);
        IReadOnlyList<PartyMemoryUnitMappingEntry> persisted = await freshStore.GetMappingsAsync(
            "tenant", "party", TestContext.Current.CancellationToken);
        persisted.ShouldBe([pending]);
        logger.Records.ShouldHaveSingleItem().Exception.ShouldBeNull();
        logger.Records[0].Message.ShouldNotContain("acknowledgement-private");

        using var retryClient = CreateClient((request, _) =>
        {
            paths.Add(request.RequestUri!.AbsolutePath);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        });
        var retryService = new PartyMemoryCleanupService(retryClient, freshStore, NullLogger<PartyMemoryCleanupService>.Instance);
        PartyMemoryCleanupResult retry = await retryService.DeleteByPartyAsync(
            "tenant", "case-new-fallback", "party", TestContext.Current.CancellationToken);

        retry.Cleaned.ShouldBeTrue();
        state.Entries.ShouldBeEmpty();
        state.KeyExists.ShouldBeTrue();
        paths.ShouldBe([
            "/api/v1/tenants/tenant/cases/case-cleaned/memory-units/memory-cleaned",
            "/api/v1/tenants/tenant/cases/case-pending/memory-units/memory-pending",
            "/api/v1/tenants/tenant/cases/case-pending/memory-units/memory-pending",
        ]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CooperativeRecoveryTimeoutRetainsInventoryAndPreservesTheCorrectCancellation(bool cancelCaller)
    {
        var cleaned = new PartyMemoryUnitMappingEntry("memory-cleaned", "urn:cleaned", "case-cleaned");
        var state = new PartyMemoryMappingState(cleaned);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        CancellationToken recoveryToken = default;
        var logger = new RecordingLogger<PartyMemoryCleanupService>();
        IPartyMemoryUnitMappingStore observed = ObserveRemoval(CreateStore(state), async (_, _, _, token) =>
        {
            recoveryToken = token;
            token.CanBeCanceled.ShouldBeTrue();
            token.IsCancellationRequested.ShouldBeFalse();
            if (cancelCaller)
            {
                cancellation.Cancel();
            }

            // The service's own recovery deadline must cancel this cooperative provider.
            await Task.Delay(Timeout.InfiniteTimeSpan, token).ConfigureAwait(false);
            return 0;
        });
        using var client = CreateClient((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent)));
        var service = new PartyMemoryCleanupService(client, observed, logger);

        var elapsed = Stopwatch.StartNew();
        OperationCanceledException error = await Should.ThrowAsync<OperationCanceledException>(() =>
            service.DeleteByPartyAsync("tenant", "case-fallback", "party", cancellation.Token)
                .WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken));

        elapsed.Elapsed.ShouldBeInRange(TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(8));
        recoveryToken.IsCancellationRequested.ShouldBeTrue();
        error.CancellationToken.ShouldBe(cancelCaller ? cancellation.Token : recoveryToken);
        state.Entries.ShouldBe([cleaned]);
        (state.SaveAttempts + state.DeleteAttempts).ShouldBe(0);
        logger.Records.Count.ShouldBe(cancelCaller ? 1 : 0);
        logger.Records.ShouldAllBe(record => record.Exception == null);
    }

    private static PartyMemoryUnitMappingStore CreateStore(PartyMemoryMappingState state) => new(
        state.Client,
        Options.Create(new PartyMemoryUnitMappingStoreOptions { StateStoreName = "statestore" }),
        NullLogger<PartyMemoryUnitMappingStore>.Instance);

    private static HttpClient CreateClient(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) =>
        new(new PartySearchErasureHttpHandler(send)) { BaseAddress = new Uri("https://memories.example/") };

    private static IPartyMemoryUnitMappingStore ObserveRemoval(
        IPartyMemoryUnitMappingStore store,
        Func<string, string, IReadOnlyList<PartyMemoryUnitMappingEntry>, CancellationToken, Task<int>> remove) =>
        new InterceptedPartyMemoryMappingStore(store, remove);
}
