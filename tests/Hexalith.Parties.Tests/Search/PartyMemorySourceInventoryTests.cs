using System.Net;
using System.Net.Http.Json;

using Hexalith.Parties.Search;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.Parties.Tests.Search;

/// <summary>Consumer source-inventory reconciliation with serialized SDK state and explicit remote negative controls.</summary>
public sealed class PartyMemorySourceInventoryTests
{
    [Fact]
    public async Task VariableSizeProgressPagesRetainAllCapacityObligationsAcrossSerializedRestart()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var state = new PartyMemoryTestReadModelStore();
        var remote = new PartyMemoryRecoveryHandler();
        remote.InventoryRequest = (request, _) =>
        {
            if (request.Method == HttpMethod.Post) { return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent)); }
            string? cursor = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(request.RequestUri!.Query).TryGetValue("cursor", out var position) ? position.ToString() : null;
            int offset = cursor is null ? 0 : int.Parse(cursor, System.Globalization.CultureInfo.InvariantCulture);
            int count = Math.Min(32, PartyMemoryIngestionIntentStore.Capacity - offset);
            object[] entries = Enumerable.Range(offset, count).Select(index => (object)new
            { caseId = new string('漢', 512), workflowInstanceId = new string('漢', 500) + index, state = "Admitted" }).ToArray();
            return Task.FromResult(Page(entries, offset + count < PartyMemoryIngestionIntentStore.Capacity
                ? (offset + count).ToString(System.Globalization.CultureInfo.InvariantCulture) : null));
        };
        using var http = PartyMemoryRecoveryTests.Http(remote);
        (await PartyMemoryRecoveryTests.Cleanup(state, http).DeleteByPartyAsync("tenant", "case", "party", token)).Cleaned.ShouldBeFalse();
        var retained = await PartyMemoryRecoveryTests.Intents(state).GetAsync("tenant", "party", token);
        retained.Count.ShouldBe(PartyMemoryIngestionIntentStore.Capacity);
        retained.Select(item => item.WorkflowInstanceId).Distinct().Count().ShouldBe(PartyMemoryIngestionIntentStore.Capacity);
        remote.InventoryRequests.Count(item => item.Method == HttpMethod.Get).ShouldBeGreaterThan(11);
        retained.ShouldAllBe(item => item.CaseId.Length == 512 && item.WorkflowInstanceId!.StartsWith(new string('漢', 500), StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StalledInventoryBodyHonorsTotalClientTimeoutAndCallerCancellation(bool callerCancels)
    {
        var state = new PartyMemoryTestReadModelStore();
        var remote = new PartyMemoryRecoveryHandler { InventoryRequest = (request, _) => Task.FromResult(request.Method == HttpMethod.Post
            ? new HttpResponseMessage(HttpStatusCode.NoContent)
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new PartyMemoryStalledInventoryStream()) }) };
        using var http = PartyMemoryRecoveryTests.Http(remote);
        http.Timeout = callerCancels ? Timeout.InfiniteTimeSpan : TimeSpan.FromMilliseconds(100);
        using var caller = new CancellationTokenSource();
        if (callerCancels) { caller.CancelAfter(TimeSpan.FromMilliseconds(100)); }
        await Should.ThrowAsync<OperationCanceledException>(() => PartyMemoryRecoveryTests.Cleanup(state, http)
            .DeleteByPartyAsync("tenant", "case", "party", caller.Token).WaitAsync(TimeSpan.FromSeconds(2)));
        caller.IsCancellationRequested.ShouldBe(callerCancels);
        (await PartyMemoryRecoveryTests.Intents(state).GetSourceClosureVersionAsync("tenant", "party", TestContext.Current.CancellationToken,
            PartyMemoryCleanupService.BuildProducerAuthority(http.BaseAddress))).ShouldBeNull();
    }

    [Fact]
    public async Task CanonicalMappingCannotBypassRunningProducerTerminalEvidence()
    {
        var state = new PartyMemoryTestReadModelStore();
        var remote = new PartyMemoryRecoveryHandler();
        remote.Units.Add("canonical");
        remote.Workflows["workflow"] = PartyMemoryRecoveryHandler.Completed() with { RuntimeStatus = "Running" };
        remote.Workflows["canonical"] = PartyMemoryRecoveryHandler.Completed("canonical", "canonical");
        bool producerCompleted = false;
        remote.InventoryRequest = (request, _) => Task.FromResult(request.Method == HttpMethod.Post
            ? new HttpResponseMessage(HttpStatusCode.NoContent)
            : Page([new { caseId = "case", workflowInstanceId = "workflow", memoryUnitId = "canonical",
                state = remote.Units.Contains("canonical") ? "Completed" : "Deleted", terminalVerified = producerCompleted }], null));
        using var http = PartyMemoryRecoveryTests.Http(remote);
        await PartyMemoryRecoveryTests.Mappings(state).RecordMappingAsync("tenant", "party", "canonical", PartyMemoryUrn.Build("tenant", "party"), "case", TestContext.Current.CancellationToken);
        (await PartyMemoryRecoveryTests.Cleanup(state, http).DeleteByPartyAsync("tenant", "case", "party", TestContext.Current.CancellationToken)).Cleaned.ShouldBeFalse();
        remote.Requests.ShouldNotContain(item => item.Method == HttpMethod.Delete);
        remote.Units.ShouldContain("canonical");
        (await PartyMemoryRecoveryTests.Intents(state).GetAsync("tenant", "party", TestContext.Current.CancellationToken)).ShouldHaveSingleItem().RequiresTerminalEvidence.ShouldBeTrue();
        (await PartyMemoryRecoveryTests.Mappings(state).GetMappingsAsync("tenant", "party", TestContext.Current.CancellationToken))
            .ShouldHaveSingleItem().MemoryUnitId.ShouldBe("canonical");

        remote.Workflows["workflow"] = PartyMemoryRecoveryHandler.Completed();
        producerCompleted = true;
        (await PartyMemoryRecoveryTests.Cleanup(state, http).DeleteByPartyAsync("tenant", "case", "party", TestContext.Current.CancellationToken)).Cleaned.ShouldBeTrue();
        remote.Requests.ShouldContain(item => item.Method == HttpMethod.Delete);
        remote.Units.ShouldNotContain("canonical");
        (await PartyMemoryRecoveryTests.Mappings(state).GetMappingsAsync("tenant", "party", TestContext.Current.CancellationToken)).ShouldBeEmpty();
        (await PartyMemoryRecoveryTests.Intents(state).GetAsync("tenant", "party", TestContext.Current.CancellationToken)).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EmptyLocalStateRequiresExplicitCompleteRemoteCoverage(bool complete)
    {
        var state = new PartyMemoryTestReadModelStore();
        var remote = new PartyMemoryRecoveryHandler { InventoryComplete = complete };
        remote.Units.Add("unrelated-source-unit");
        using var http = PartyMemoryRecoveryTests.Http(remote);
        PartyMemoryCleanupResult result = await PartyMemoryRecoveryTests.Cleanup(state, http)
            .DeleteByPartyAsync("tenant", "case", "party", TestContext.Current.CancellationToken);
        result.Cleaned.ShouldBe(complete);
        remote.InventoryRequests.ShouldContain(item => item.Method == HttpMethod.Post && item.Path.Contains("/source-inventory/fence", StringComparison.Ordinal));
        remote.Units.ShouldBe(["unrelated-source-unit"]);
        (await PartyMemoryRecoveryTests.Intents(state).GetSourceClosureVersionAsync("tenant", "party", TestContext.Current.CancellationToken, PartyMemoryCleanupService.BuildProducerAuthority(http.BaseAddress)))
            .ShouldBe(complete ? "synthetic-complete-inventory" : null);
    }

    [Fact]
    public async Task PartialHistoricalPagesAndPendingProducersSurviveRestartUntilTerminalAbsence()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var state = new PartyMemoryTestReadModelStore();
        var remote = new PartyMemoryRecoveryHandler();
        remote.Units.UnionWith(["old-unit", "pending-unit", "unrelated-source-unit"]);
        remote.Workflows["pending"] = PartyMemoryRecoveryHandler.Completed("pending", "pending-unit", caseId: "historical-case-2") with
        { RuntimeStatus = "Running", MemoryUnitId = null, MemoryUnitStatus = null };
        bool failSecondPage = true;
        bool pending = true;
        remote.InventoryRequest = (request, _) =>
        {
            if (request.Method == HttpMethod.Post) { return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent)); }
            bool second = request.RequestUri!.Query.Contains("cursor=more", StringComparison.Ordinal);
            if (second && failSecondPage) { return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)); }
            object entry = second
                ? new { caseId = "historical-case-2", workflowInstanceId = "pending", memoryUnitId = pending ? null : "pending-unit",
                    state = pending ? "Admitted" : remote.Units.Contains("pending-unit") ? "Completed" : "Deleted", terminalVerified = !pending }
                : new { caseId = "historical-case-1", workflowInstanceId = "old", memoryUnitId = (string?)"old-unit",
                    state = remote.Units.Contains("old-unit") ? "Completed" : "Deleted", terminalVerified = true };
            return Task.FromResult(Page([entry], second ? null : "more"));
        };
        using var http = PartyMemoryRecoveryTests.Http(remote);
        (await PartyMemoryRecoveryTests.Cleanup(state, http).DeleteByPartyAsync("tenant", "current-case", "party", token)).Cleaned.ShouldBeFalse();
        (await PartyMemoryRecoveryTests.Intents(state).BeginCleanupAsync("tenant", "party", token)).ShouldHaveSingleItem().CaseId.ShouldBe("historical-case-1");
        remote.Requests.ShouldContain(item => item.Method == HttpMethod.Delete && item.Path.EndsWith("/old-unit", StringComparison.Ordinal));
        failSecondPage = false;
        (await PartyMemoryRecoveryTests.Cleanup(state, http).DeleteByPartyAsync("tenant", "changed-current-case", "party", token)).Cleaned.ShouldBeFalse();
        remote.Units.ShouldBe(["pending-unit", "unrelated-source-unit"], ignoreOrder: true);
        (await PartyMemoryRecoveryTests.Intents(state).GetAsync("tenant", "party", token)).ShouldHaveSingleItem().WorkflowInstanceId.ShouldBe("pending");
        pending = false;
        remote.Workflows["pending"] = PartyMemoryRecoveryHandler.Completed("pending", "pending-unit", caseId: "historical-case-2");
        (await PartyMemoryRecoveryTests.Cleanup(state, http).DeleteByPartyAsync("tenant", "unrelated-current-case", "party", token)).Cleaned.ShouldBeTrue();
        remote.Units.ShouldBe(["unrelated-source-unit"]);
        (await PartyMemoryRecoveryTests.Intents(state).GetAsync("tenant", "party", token)).ShouldBeEmpty();
        state.SerializedValues.ShouldContain(json => json.Contains("historical-case-1", StringComparison.Ordinal)
            && json.Contains("historical-case-2", StringComparison.Ordinal) && json.Contains("SourceClosureVersion", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(503)]
    public async Task UnavailableInventoryNeverSettlesEmptyLocalState(int status)
    {
        var state = new PartyMemoryTestReadModelStore();
        var remote = new PartyMemoryRecoveryHandler
        {
            InventoryRequest = (_, _) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status)),
        };
        using var http = PartyMemoryRecoveryTests.Http(remote);
        (await PartyMemoryRecoveryTests.Cleanup(state, http).DeleteByPartyAsync("tenant", "case", "party", TestContext.Current.CancellationToken)).Cleaned.ShouldBeFalse();
        (await PartyMemoryRecoveryTests.Intents(state).GetSourceClosureVersionAsync("tenant", "party", TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task WrongTenantInventoryCannotAuthorizeDeletion()
    {
        var state = new PartyMemoryTestReadModelStore();
        var remote = new PartyMemoryRecoveryHandler
        {
            InventoryRequest = (request, _) => Task.FromResult(request.Method == HttpMethod.Post
                ? new HttpResponseMessage(HttpStatusCode.NoContent)
                : Page([new { caseId = "case", workflowInstanceId = "foreign-workflow", memoryUnitId = "foreign-unit", state = "Completed" }], tenant: "other-tenant")),
        };
        remote.Units.Add("foreign-unit");
        using var http = PartyMemoryRecoveryTests.Http(remote);
        (await PartyMemoryRecoveryTests.Cleanup(state, http).DeleteByPartyAsync("tenant", "case", "party", TestContext.Current.CancellationToken)).Cleaned.ShouldBeFalse();
        remote.Requests.ShouldBeEmpty();
        remote.Units.ShouldBe(["foreign-unit"]);
    }

    [Theory]
    [InlineData("Completed")]
    [InlineData("Deleted")]
    public async Task OutputKnownOrDeletedStillRequiresIndependentWorkflowTerminalEvidence(string state)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var persisted = new PartyMemoryTestReadModelStore();
        var remote = new PartyMemoryRecoveryHandler();
        bool terminal = false;
        remote.Workflows["producer"] = PartyMemoryRecoveryHandler.Completed("producer", "canonical") with
        { RuntimeStatus = "Running", MemoryUnitId = null, MemoryUnitStatus = null };
        remote.Units.Add("canonical");
        remote.InventoryRequest = (request, _) => Task.FromResult(request.Method == HttpMethod.Post
            ? new HttpResponseMessage(HttpStatusCode.NoContent)
            : Page([new { caseId = "case", workflowInstanceId = "producer", memoryUnitId = "canonical", state = terminal ? remote.Units.Contains("canonical") ? "Completed" : "Deleted" : state, terminalVerified = terminal }]));
        using var http = PartyMemoryRecoveryTests.Http(remote);
        (await PartyMemoryRecoveryTests.Cleanup(persisted, http).DeleteByPartyAsync("tenant", "case", "party", token)).Cleaned.ShouldBeFalse();
        remote.Units.ShouldContain("canonical");
        remote.Requests.ShouldNotContain(item => item.Method == HttpMethod.Delete);
        (await PartyMemoryRecoveryTests.Intents(persisted).GetAsync("tenant", "party", token)).ShouldHaveSingleItem().RequiresTerminalEvidence.ShouldBeTrue();
        terminal = true;
        remote.Workflows["producer"] = PartyMemoryRecoveryHandler.Completed("producer", "canonical");
        (await PartyMemoryRecoveryTests.Cleanup(persisted, http).DeleteByPartyAsync("tenant", "case", "party", token)).Cleaned.ShouldBeTrue();
        remote.Units.ShouldBeEmpty();
    }

    [Fact]
    public async Task CompensatedPublicationConflictRetainsAStableSuccessorAcrossRestartAndCleanupFence()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var persisted = new PartyMemoryTestReadModelStore();
        PartyMemoryIngestionIntentStore store = PartyMemoryRecoveryTests.Intents(persisted);
        var requested = new PartyMemoryIngestionIntent("logical-revision", "case", PartyMemoryUrn.Build("tenant", "party"));
        PartyMemoryIngestionRegistration admitted = await store.RegisterAsync("tenant", "party", requested, token);
        admitted.Created.ShouldBeTrue();
        PartyMemoryIngestionIntent prior = admitted.Intent with { WorkflowInstanceId = "superseded-workflow" };
        await store.UpdateAsync("tenant", "party", prior, token);
        await store.RegisterSupersededSuccessorAsync("tenant", "party", prior, "compensated-output", "new-predecessor", token);
        PartyMemoryIngestionRegistration successor = await PartyMemoryRecoveryTests.Intents(persisted).RegisterAsync("tenant", "party", requested, token);
        successor.Created.ShouldBeTrue("Only the reserved successor gains a new submission authority.");
        successor.Intent.AttemptId.ShouldNotBe(requested.AttemptId);
        PartyMemoryIngestionIntent active = successor.Intent with { WorkflowInstanceId = "successor-workflow" };
        await store.UpdateAsync("tenant", "party", active, token);
        PartyMemoryIngestionRegistration replay = await PartyMemoryRecoveryTests.Intents(persisted).RegisterAsync("tenant", "party", requested, token);
        replay.Created.ShouldBeFalse();
        replay.Intent.ShouldBe(active);
        (await store.GetAsync("tenant", "party", token)).ShouldContain(item => item.AttemptId == prior.AttemptId
            && item.MemoryUnitId == "compensated-output" && item.SuccessorAttemptId == active.AttemptId);
        await store.BeginCleanupAsync("tenant", "party", token);
        await Should.ThrowAsync<InvalidOperationException>(() => store.RegisterSupersededSuccessorAsync("tenant", "party", active,
            "another-output", "another-predecessor", token));
    }

    [Fact]
    public async Task PermanentClosureFromOneProducerDoesNotCertifyAnotherEndpointOrLegacyUnknownAuthority()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var persisted = new PartyMemoryTestReadModelStore();
        static PartyMemoryRecoveryHandler Producer()
        {
            var producer = new PartyMemoryRecoveryHandler();
            producer.Units.UnionWith(["same-unit", "unrelated-survivor"]);
            producer.InventoryRequest = (request, _) => Task.FromResult(request.Method == HttpMethod.Post
                ? new HttpResponseMessage(HttpStatusCode.NoContent)
                : Page([new { caseId = "same-case", workflowInstanceId = "same-workflow", memoryUnitId = "same-unit",
                    state = producer.Units.Contains("same-unit") ? "Completed" : "Deleted", terminalVerified = true }]));
            return producer;
        }
        var producerA = Producer();
        using var first = PartyMemoryRecoveryTests.Http(producerA);
        first.BaseAddress = new Uri("https://producer-a.example/prefix/");
        (await PartyMemoryRecoveryTests.Cleanup(persisted, first).DeleteByPartyAsync("tenant", "same-case", "party", token)).Cleaned.ShouldBeTrue();
        producerA.Units.ShouldBe(["unrelated-survivor"]);
        PartyMemoryIngestionIntentStore store = PartyMemoryRecoveryTests.Intents(persisted);
        (await store.GetSourceClosureVersionAsync("tenant", "party", token)).ShouldBeNull("Unbound legacy authority cannot qualify permanent closure.");
        (await store.GetSourceClosureVersionAsync("tenant", "party", token, PartyMemoryCleanupService.BuildProducerAuthority(first.BaseAddress))).ShouldNotBeNull();
        var other = Producer();
        using var second = PartyMemoryRecoveryTests.Http(other);
        second.BaseAddress = new Uri("https://producer-b.example/prefix/");
        other.Units.ShouldContain("same-unit", "B has independent live content under identical case/workflow/unit identifiers.");
        (await PartyMemoryRecoveryTests.Cleanup(persisted, second).DeleteByPartyAsync("tenant", "same-case", "party", token)).Cleaned.ShouldBeTrue();
        other.InventoryRequests.ShouldNotBeEmpty("An authority transition must revalidate the actual producer.");
        other.Requests.ShouldContain(item => item.Method == HttpMethod.Delete && item.Path.EndsWith("/same-unit", StringComparison.Ordinal));
        other.Units.ShouldBe(["unrelated-survivor"]);
        (await store.BeginCleanupAsync("tenant", "party", token)).Where(item => item.MemoryUnitId == "same-unit")
            .Select(item => item.AbsenceAuthority).Distinct().Count().ShouldBe(2);
        using var differentPrefix = PartyMemoryRecoveryTests.Http(new PartyMemoryRecoveryHandler { InventoryComplete = false });
        differentPrefix.BaseAddress = new Uri("https://producer-a.example/different-prefix/");
        (await PartyMemoryRecoveryTests.Cleanup(persisted, differentPrefix).DeleteByPartyAsync("tenant", "case", "party", token)).Cleaned.ShouldBeFalse();
    }

    [Fact]
    public async Task DirectUnitCleanupDoesNotReuseAnotherProducerAbsenceForIdenticalIdentifiers()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var persisted = new PartyMemoryTestReadModelStore();
        static PartyMemoryRecoveryHandler Producer()
        {
            var remote = new PartyMemoryRecoveryHandler();
            remote.Units.UnionWith(["same-unit", "unrelated-survivor"]);
            remote.Workflows["same-workflow"] = PartyMemoryRecoveryHandler.Completed("same-workflow", "same-unit", caseId: "same-case");
            return remote;
        }
        var producerA = Producer();
        using var first = PartyMemoryRecoveryTests.Http(producerA);
        first.BaseAddress = new Uri("https://producer-a.example/prefix/");
        (await PartyMemoryRecoveryTests.Cleanup(persisted, first).DeleteMemoryUnitAsync("tenant", "same-case", "party", "same-workflow", token)).Cleaned.ShouldBeTrue();
        producerA.Units.ShouldBe(["unrelated-survivor"]);
        var producerB = Producer();
        using var second = PartyMemoryRecoveryTests.Http(producerB);
        second.BaseAddress = new Uri("https://producer-b.example/prefix/");
        producerB.Workflows["same-workflow"] = producerB.Workflows["same-workflow"] with { RuntimeStatus = "Running", MemoryUnitId = null, MemoryUnitStatus = null };
        (await PartyMemoryRecoveryTests.Cleanup(persisted, second).DeleteMemoryUnitAsync("tenant", "same-case", "party", "same-workflow", token)).Cleaned.ShouldBeFalse();
        producerB.Requests.ShouldNotContain(item => item.Method == HttpMethod.Delete);
        producerB.Units.ShouldContain("same-unit");
        producerB.Workflows["same-workflow"] = PartyMemoryRecoveryHandler.Completed("same-workflow", "same-unit", caseId: "same-case");
        (await PartyMemoryRecoveryTests.Cleanup(persisted, second).DeleteMemoryUnitAsync("tenant", "same-case", "party", "same-workflow", token)).Cleaned.ShouldBeTrue();
        producerB.Requests.ShouldContain(item => item.Method == HttpMethod.Delete && item.Path.EndsWith("/same-unit", StringComparison.Ordinal));
        producerB.Units.ShouldBe(["unrelated-survivor"]);
        (await PartyMemoryRecoveryTests.Intents(persisted).BeginCleanupAsync("tenant", "party", token))
            .Where(item => item.MemoryUnitId == "same-unit").Select(item => item.AbsenceAuthority).Distinct().Count().ShouldBe(2);
    }

#if HEXALITH_MEMORIES_SOURCE_REFRESH
    [Fact]
    public async Task AdmissionAuthorityUsesActualTypedClientAfterConfiguredEndpointChanges()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var persisted = new PartyMemoryTestReadModelStore();
        var remote = new PartyMemoryRecoveryHandler();
        remote.Workflows["workflow"] = PartyMemoryRecoveryHandler.Completed("workflow", "canonical");
        using var http = PartyMemoryRecoveryTests.Http(remote);
        http.BaseAddress = new Uri("https://producer-a.example/");
        var client = new PartyMemoryRecoveryClient(http);
        IOptionsMonitor<PartyMemorySearchOptions> monitor = new PartyMemoryTestOptionsMonitor(new PartyMemorySearchOptions
        { Enabled = true, Endpoint = new Uri("https://producer-b.example/"), TenantId = "tenant", CaseId = "case" });
        var indexer = new PartyMemoryIndexingService(client, PartyMemoryRecoveryTests.Mappings(persisted), monitor,
            NullLogger<PartyMemoryIndexingService>.Instance, PartyMemoryRecoveryTests.Intents(persisted));
        (await indexer.IndexAsync(PartyMemoryRecoveryTests.Entry(), PartyMemoryRecoveryTests.Context(), token)).ShouldNotBeNull().Indexed.ShouldBeTrue();
        PartyMemoryIngestionIntent captured = (await PartyMemoryRecoveryTests.Intents(persisted).GetAsync("tenant", "party", token)).Single();
        captured.ProducerAuthority.ShouldBe(PartyMemoryCleanupService.BuildProducerAuthority(http.BaseAddress));
        captured.ProducerAuthority.ShouldNotBe(PartyMemoryCleanupService.BuildProducerAuthority(monitor.CurrentValue.Endpoint));
    }
#endif

    internal static HttpResponseMessage Page(object[] entries, string? next = null, string tenant = "tenant") => new(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(new
        {
            tenantId = tenant, sourceUri = PartyMemoryUrn.Build("tenant", "party"), version = "controlled-snapshot",
            coverageComplete = true, admissionClosed = true, permanent = true, entries, nextCursor = next,
        }),
    };
}
