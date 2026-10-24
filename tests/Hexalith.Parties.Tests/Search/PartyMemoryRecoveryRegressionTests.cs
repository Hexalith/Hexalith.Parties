using System.Diagnostics;
using System.Net;
using System.Text;

using Hexalith.Parties.Search;
using Hexalith.Parties.Testing;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.Parties.Tests.Search;

/// <summary>Preserves prior persistence, privacy, routing and cancellation boundary regressions under terminal cleanup.</summary>
public sealed class PartyMemoryRecoveryRegressionTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IndependentCleanupProgressWriteCompletesBeforeCallerCancellationPropagates(bool loseAcknowledgement)
    {
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var state = new PartyMemoryTestReadModelStore();
        PartyMemoryUnitMappingStore real = PartyMemoryRecoveryTests.Mappings(state);
        await real.RecordMappingAsync("tenant", "party", "workflow", "urn:original", "case", Token);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observed = new InterceptedPartyMemoryMappingStore(real, async (tenant, party, targets, token) =>
        {
            token.ShouldNotBe(caller.Token);
            token.IsCancellationRequested.ShouldBeFalse();
            entered.TrySetResult();
            await release.Task.WaitAsync(token).ConfigureAwait(false);
            int retained = await real.RemoveMappingsAsync(tenant, party, targets, token).ConfigureAwait(false);
            if (loseAcknowledgement)
            {
                throw new InvalidOperationException("private-lost-acknowledgement");
            }

            return retained;
        });
        var handler = Ready();
        using var http = PartyMemoryRecoveryTests.Http(handler);
        var logger = new RecordingLogger<PartyMemoryCleanupService>();
        var service = new PartyMemoryCleanupService(http, observed, logger, PartyMemoryRecoveryTests.Intents(state));
        Task<PartyMemoryCleanupResult> pending = service.DeleteByPartyAsync("tenant", "case", "party", caller.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), Token);
        caller.Cancel();
        pending.IsCompleted.ShouldBeFalse();
        release.TrySetResult();

        (await Should.ThrowAsync<OperationCanceledException>(() => pending)).CancellationToken.ShouldBe(caller.Token);
        (await PartyMemoryRecoveryTests.Mappings(state).GetMappingsAsync("tenant", "party", Token)).ShouldBeEmpty();
        logger.Records.ShouldAllBe(record => record.Exception == null && !record.Message.Contains("private-lost-acknowledgement", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CooperativeProgressDeadlineRetainsInventoryAndCorrectCancellation(bool cancelCaller)
    {
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var state = new PartyMemoryTestReadModelStore();
        PartyMemoryUnitMappingStore real = PartyMemoryRecoveryTests.Mappings(state);
        await real.RecordMappingAsync("tenant", "party", "workflow", "urn:original", "case", Token);
        CancellationToken recoveryToken = default;
        var observed = new InterceptedPartyMemoryMappingStore(real, async (_, _, _, token) =>
        {
            recoveryToken = token;
            if (cancelCaller)
            {
                caller.Cancel();
            }

            await Task.Delay(Timeout.InfiniteTimeSpan, token).ConfigureAwait(false);
            return 0;
        });
        using var http = PartyMemoryRecoveryTests.Http(Ready());
        var service = new PartyMemoryCleanupService(http, observed, NullLogger<PartyMemoryCleanupService>.Instance, PartyMemoryRecoveryTests.Intents(state));
        Stopwatch elapsed = Stopwatch.StartNew();

        OperationCanceledException error = await Should.ThrowAsync<OperationCanceledException>(() =>
            service.DeleteByPartyAsync("tenant", "case", "party", caller.Token).WaitAsync(TimeSpan.FromSeconds(15), Token));

        elapsed.Elapsed.ShouldBeInRange(TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(10));
        recoveryToken.IsCancellationRequested.ShouldBeTrue();
        error.CancellationToken.ShouldBe(cancelCaller ? caller.Token : recoveryToken);
        (await PartyMemoryRecoveryTests.Mappings(state).GetMappingsAsync("tenant", "party", Token)).Count.ShouldBe(1);
    }

    [Fact]
    public async Task CancelledRecoveryFailureRetainsInventoryWithoutLeakingProviderDetails()
    {
        const string secret = "private-personal-name private-source private-case";
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var state = new PartyMemoryTestReadModelStore();
        PartyMemoryUnitMappingStore real = PartyMemoryRecoveryTests.Mappings(state);
        await real.RecordMappingAsync("tenant", "party", "workflow", "urn:original", "case", Token);
        var observed = new InterceptedPartyMemoryMappingStore(real, (_, _, _, _) =>
        {
            caller.Cancel();
            return Task.FromException<int>(new InvalidOperationException(secret));
        });
        var logger = new RecordingLogger<PartyMemoryCleanupService>();
        using var http = PartyMemoryRecoveryTests.Http(Ready());

        OperationCanceledException error = await Should.ThrowAsync<OperationCanceledException>(() =>
            new PartyMemoryCleanupService(http, observed, logger, PartyMemoryRecoveryTests.Intents(state))
                .DeleteByPartyAsync("tenant", "case", "party", caller.Token));

        error.CancellationToken.ShouldBe(caller.Token);
        error.ToString().ShouldNotContain(secret);
        (await real.GetMappingsAsync("tenant", "party", Token)).Count.ShouldBe(1);
        logger.Records.ShouldHaveSingleItem().Message.ShouldContain(nameof(InvalidOperationException));
        logger.Records.ShouldAllBe(record => record.Exception == null && !record.Message.Contains(secret, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationImmediatelyAfterInventoryReadNeverStartsRemoteCleanup(bool empty)
    {
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var state = new PartyMemoryTestReadModelStore();
        PartyMemoryUnitMappingStore real = PartyMemoryRecoveryTests.Mappings(state);
        if (!empty)
        {
            await real.RecordMappingAsync("tenant", "party", "workflow", "urn:original", "case", Token);
        }

        var observed = new InterceptedPartyMemoryMappingStore(real,
            (tenant, party, entries, token) => real.RemoveMappingsAsync(tenant, party, entries, token), caller.Cancel);
        var handler = Ready();
        using var http = PartyMemoryRecoveryTests.Http(handler);

        await Should.ThrowAsync<OperationCanceledException>(() => new PartyMemoryCleanupService(http, observed,
            NullLogger<PartyMemoryCleanupService>.Instance, PartyMemoryRecoveryTests.Intents(state))
            .DeleteByPartyAsync("tenant", "case", "party", caller.Token));

        handler.Requests.ShouldBeEmpty();
        (await real.GetMappingsAsync("tenant", "party", Token)).Count.ShouldBe(empty ? 0 : 1);
    }

    [Fact]
    public async Task ConditionalMappingRemovalFailureCannotReportCleanupSuccess()
    {
        var mappingState = new PartyMemoryMappingState(new PartyMemoryUnitMappingEntry("workflow", "urn:original", "case")) { RejectWrites = true };
        var mapping = new PartyMemoryUnitMappingStore(mappingState.Client, Options.Create(new PartyMemoryUnitMappingStoreOptions()), NullLogger<PartyMemoryUnitMappingStore>.Instance);
        using var http = PartyMemoryRecoveryTests.Http(Ready());

        InvalidOperationException error = await Should.ThrowAsync<InvalidOperationException>(() => new PartyMemoryCleanupService(http,
            mapping, NullLogger<PartyMemoryCleanupService>.Instance, PartyMemoryTestServices.CreateIntentStore())
            .DeleteByPartyAsync("tenant", "case", "party", Token));

        error.Message.ShouldBe("memory-unit-mapping-remove-conflict");
        mappingState.Entries.Count.ShouldBe(1);
        mappingState.SaveAttempts.ShouldBe(5);
    }

    [Fact]
    public async Task MappingReadFailureRemainsSanitizedAndBlocksCleanup()
    {
        var mappingState = new PartyMemoryMappingState(new PartyMemoryUnitMappingEntry("workflow", "urn:original", "case"))
        { ReadFailure = new InvalidOperationException("private-personal-name") };
        var logger = new RecordingLogger<PartyMemoryUnitMappingStore>();
        var mapping = new PartyMemoryUnitMappingStore(mappingState.Client, Options.Create(new PartyMemoryUnitMappingStoreOptions()), logger);
        using var http = PartyMemoryRecoveryTests.Http(Ready());

        PartyMemoryCleanupResult result = await new PartyMemoryCleanupService(http, mapping,
            NullLogger<PartyMemoryCleanupService>.Instance, PartyMemoryTestServices.CreateIntentStore())
            .DeleteByPartyAsync("tenant", "case", "party", Token);

        result.Cleaned.ShouldBeFalse();
        result.BlockedReason.ShouldNotBeNull().ShouldNotContain("private-personal-name");
        logger.Records.ShouldHaveSingleItem().Exception.ShouldBeNull();
        mappingState.Entries.Count.ShouldBe(1);
    }

    [Fact]
    public async Task DirectCleanupEscapesCanonicalRouteSegmentsAndPreservesEndpointPrefix()
    {
        var handler = new PartyMemoryRecoveryHandler();
        handler.Workflows["workflow"] = PartyMemoryRecoveryHandler.Completed("workflow", "memory?1", "tenant/a", "case a");
        using var http = PartyMemoryRecoveryTests.Http(handler);

        PartyMemoryCleanupResult result = await PartyMemoryRecoveryTests.Cleanup(new PartyMemoryTestReadModelStore(), http)
            .DeleteMemoryUnitAsync("tenant/a", "case a", "party", "workflow", Token);

        result.Cleaned.ShouldBeTrue();
        handler.Requests.Where(request => request.Method == HttpMethod.Delete).ShouldHaveSingleItem().Path.ShouldBe(
            "/gateway/api/v1/tenants/tenant%2Fa/cases/case%20a/memory-units/memory%3F1");
    }

    [Fact]
    public async Task CleanupPreservesOtherCaseWithSharedUnitIdentity()
    {
        var state = new PartyMemoryTestReadModelStore();
        await PartyMemoryRecoveryTests.Mappings(state).RecordMappingAsync("tenant", "party", "workflow", "urn:source", "case", Token);
        await PartyMemoryRecoveryTests.Mappings(state).RecordMappingAsync("tenant", "party", "workflow", "urn:source", "other-case", Token);
        using var http = PartyMemoryRecoveryTests.Http(Ready());

        (await PartyMemoryRecoveryTests.Cleanup(state, http).DeleteByPartyAsync("tenant", "case", "party", Token)).Cleaned.ShouldBeFalse();

        (await PartyMemoryRecoveryTests.Mappings(state).GetMappingsAsync("tenant", "party", Token)).ShouldBe([new("workflow", "urn:source", "other-case")]);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task EquivalentTerminalOutputsReuseScopedAbsenceBeforeTransportDenial(bool previouslySettled, bool directSecond)
    {
        var state = new PartyMemoryTestReadModelStore();
        var first = new PartyMemoryIngestionIntent("first", "case", "urn:first", "workflow-first", "canonical")
        { ProducerAuthority = PartyMemoryCleanupService.BuildProducerAuthority(new Uri("http://127.0.0.1:1/gateway/")) };
        var second = new PartyMemoryIngestionIntent("second", "case", "urn:second", "workflow-second", "canonical")
        { ProducerAuthority = first.ProducerAuthority };
        foreach (PartyMemoryIngestionIntent intent in new[] { first, second })
        {
            await PartyMemoryRecoveryTests.Intents(state).RegisterAsync("tenant", "party", intent, Token);
        }

        await PartyMemoryRecoveryTests.Mappings(state).RecordMappingAsync("tenant", "party", "canonical", "urn:first", "case", Token);
        await PartyMemoryRecoveryTests.Mappings(state).RecordMappingAsync("tenant", "party", "workflow-second", "urn:second", "case", Token);
        List<HttpMethod> requests = [];
        using var transport = new PartySearchErasureHttpHandler((request, _) =>
        {
            requests.Add(request.Method);
            return Task.FromResult(requests.Count switch
            {
                1 => new HttpResponseMessage(HttpStatusCode.NoContent),
                2 => new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent("{\"code\":\"MEMORY_UNIT_NOT_FOUND\"}", Encoding.UTF8, "application/json"),
                },
                _ => new HttpResponseMessage(HttpStatusCode.Unauthorized),
            });
        });
        using var http = PartyMemoryRecoveryTests.Http(transport);
        if (previouslySettled)
        {
            (await PartyMemoryRecoveryTests.Cleanup(state, http).DeleteMemoryUnitAsync("tenant", "case", "party", "workflow-first", Token)).Cleaned.ShouldBeTrue();
            (await PartyMemoryRecoveryTests.Intents(state).GetAsync("tenant", "party", Token)).ShouldHaveSingleItem().ShouldBe(second);
        }

        if (directSecond)
        {
            (await PartyMemoryRecoveryTests.Cleanup(state, http).DeleteMemoryUnitAsync("tenant", "case", "party", "workflow-second", Token)).Cleaned.ShouldBeTrue();
        }

        (await PartyMemoryRecoveryTests.Cleanup(state, http).DeleteByPartyAsync("tenant", "case", "party", Token)).Cleaned.ShouldBeTrue();
        requests.ShouldBe([HttpMethod.Delete, HttpMethod.Get]);
        (await PartyMemoryRecoveryTests.Intents(state).GetAsync("tenant", "party", Token)).ShouldBeEmpty();
        (await PartyMemoryRecoveryTests.Mappings(state).GetMappingsAsync("tenant", "party", Token)).ShouldBeEmpty();
        IReadOnlyList<PartyMemoryIngestionIntent> receipts = await PartyMemoryRecoveryTests.Intents(state).BeginCleanupAsync("tenant", "party", Token);
        receipts.Count.ShouldBe(2);
        receipts.ShouldAllBe(item => item.CleanupSettled && item.MemoryUnitId == "canonical");
        receipts.Select(item => item.WorkflowInstanceId).Order().ShouldBe(["workflow-first", "workflow-second"]);
        var denied = new ProbeHandler(401, "{}");
        using var deniedHttp = PartyMemoryRecoveryTests.Http(denied);
        (await PartyMemoryRecoveryTests.Cleanup(state, deniedHttp).DeleteByPartyAsync("tenant", "case", "party", Token)).Cleaned.ShouldBeTrue();
        denied.Requests.ShouldBe(0);
        (await PartyMemoryRecoveryTests.Intents(state).BeginCleanupAsync("tenant", "party", Token)).ShouldBe(receipts);
    }

    private static PartyMemoryRecoveryHandler Ready()
    {
        var handler = new PartyMemoryRecoveryHandler();
        handler.Workflows["workflow"] = PartyMemoryRecoveryHandler.Completed();
        return handler;
    }
}
