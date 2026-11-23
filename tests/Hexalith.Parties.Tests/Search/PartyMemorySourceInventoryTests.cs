using System.Net;
using System.Net.Http.Json;

using Hexalith.Memories.Contracts.V1;
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CanonicalMappingCannotBypassRunningProducerTerminalEvidence(bool legacyTerminalRow)
    {
        var state = new PartyMemoryTestReadModelStore();
        if (legacyTerminalRow)
        {
            await PartyMemoryRecoveryTests.Intents(state).RegisterAsync("tenant", "party",
                new("legacy", "case", PartyMemoryUrn.Build("tenant", "party"), "workflow", "canonical"), TestContext.Current.CancellationToken);
        }
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
    [InlineData(false, "unknown")]
    [InlineData(true, "unknown")]
    [InlineData(false, "cleared")]
    [InlineData(true, "cleared")]
    [InlineData(false, "pending")]
    [InlineData(true, "pending")]
    public async Task ContradictoryAdmittedRowRetainsItsObligationAndProtectsTheCase(bool admittedFirst, string existing)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var state = new PartyMemoryTestReadModelStore();
        var remote = new PartyMemoryRecoveryHandler();
        remote.Units.UnionWith(["canonical", "sibling-unit"]);
        var completed = PartyMemoryRecoveryHandler.Completed();
        remote.Workflows["workflow"] = completed with { RuntimeStatus = "Running" };
        bool producerCompleted = false;
        remote.InventoryRequest = (request, _) =>
        {
            if (request.Method == HttpMethod.Post) { return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent)); }
            object admitted = new { caseId = "case", workflowInstanceId = "workflow", memoryUnitId = "canonical", terminalVerified = true,
                state = !producerCompleted ? "Admitted" : remote.Units.Contains("canonical") ? "Completed" : "Deleted" };
            object sibling = new { caseId = "case", workflowInstanceId = "sibling", memoryUnitId = "sibling-unit", terminalVerified = true,
                state = remote.Units.Contains("sibling-unit") ? "Completed" : "Deleted" };
            return Task.FromResult(Page(admittedFirst ? [admitted, sibling] : [sibling, admitted]));
        };
        using var http = PartyMemoryRecoveryTests.Http(remote);
        var intents = PartyMemoryRecoveryTests.Intents(state);
        var mappings = PartyMemoryRecoveryTests.Mappings(state);
        string source = PartyMemoryUrn.Build("tenant", "party");
        string? authority = PartyMemoryCleanupService.BuildProducerAuthority(http.BaseAddress);
        if (existing != "unknown")
        {
            await intents.RegisterAsync("tenant", "party", new("attempt", "case", source, "workflow", "canonical",
                RequiresTerminalEvidence: existing == "pending", ProducerAuthority: authority), token);
        }
        await mappings.RecordMappingAsync("tenant", "party", "canonical", source, "case", token);

        (await PartyMemoryRecoveryTests.Cleanup(state, http).DeleteByPartyAsync("tenant", "case", "party", token)).Cleaned.ShouldBeFalse();

        remote.Requests.ShouldContain(item => item.Path.EndsWith("/ingest/workflow", StringComparison.Ordinal));
        remote.Requests.ShouldNotContain(item => item.Method == HttpMethod.Delete);
        remote.Units.ShouldBe(["canonical", "sibling-unit"], ignoreOrder: true);
        var retained = await intents.GetAsync("tenant", "party", token);
        retained.Select(item => item.WorkflowInstanceId).ShouldBe(["workflow", "sibling"], ignoreOrder: true);
        var pending = retained.Single(item => item.WorkflowInstanceId == "workflow");
        pending.RequiresTerminalEvidence.ShouldBeTrue();
        pending.MemoryUnitId.ShouldBe(existing == "unknown" ? null : "canonical");
        (await mappings.GetMappingsAsync("tenant", "party", token)).ShouldHaveSingleItem().MemoryUnitId.ShouldBe("canonical");
        (await intents.GetSourceClosureVersionAsync("tenant", "party", token, authority)).ShouldBeNull();

        producerCompleted = true;
        remote.Workflows.Remove("workflow");
        (await PartyMemoryRecoveryTests.Cleanup(state, http).DeleteByPartyAsync("tenant", "case", "party", token)).Cleaned.ShouldBeTrue();

        remote.Units.ShouldBeEmpty();
        (await mappings.GetMappingsAsync("tenant", "party", token)).ShouldBeEmpty();
        (await intents.GetAsync("tenant", "party", token)).ShouldBeEmpty();
        (await intents.BeginCleanupAsync("tenant", "party", token)).Single(item => item.WorkflowInstanceId == "workflow")
            .ShouldBe(pending with { MemoryUnitId = "canonical", CleanupSettled = true, RequiresTerminalEvidence = false, AbsenceAuthority = authority });
    }

    [Theory]
    [InlineData(false, "running")]
    [InlineData(true, "running")]
    [InlineData(false, "status-error")]
    [InlineData(true, "status-error")]
    [InlineData(false, "persistence-error")]
    [InlineData(true, "persistence-error")]
    [InlineData(false, "workflow-only-persistence-error")]
    [InlineData(true, "workflow-only-persistence-error")]
    public async Task SharedUnitAndMappingAwaitEveryKnownWorkflowRegardlessOfInventoryOrder(bool pendingFirst, string pending)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var state = new PartyMemoryTestReadModelStore();
        var remote = new PartyMemoryRecoveryHandler();
        remote.Units.UnionWith(["shared", "unrelated", "survivor"]);
        bool workflowOnly = pending == "workflow-only-persistence-error";
        var siblingStatus = PartyMemoryRecoveryHandler.Completed("sibling", "shared");
        remote.Workflows["sibling"] = pending == "running" ? siblingStatus with { RuntimeStatus = "Running" } : siblingStatus;
        if (pending == "status-error")
        {
            remote.WorkflowErrors["sibling"] = (HttpStatusCode.OK, "invalid status body");
        }
        if (pending == "persistence-error" || workflowOnly)
        {
            remote.BeforeRequest = (request, _) =>
            {
                if (request.RequestUri!.AbsolutePath.EndsWith("/ingest/sibling", StringComparison.Ordinal))
                {
                    state.BeforeSave = () => Task.FromException(new IOException("terminal evidence write failed"));
                }
                return Task.CompletedTask;
            };
        }

        bool recovered = false;
        bool unavailableRetry = false;
        remote.InventoryRequest = (request, _) =>
        {
            if (request.Method == HttpMethod.Post) { return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent)); }
            object Entry(string workflow, string unit, bool terminal) => new
            {
                caseId = "case", workflowInstanceId = workflow, memoryUnitId = unit,
                state = remote.Units.Contains(unit) ? "Completed" : "Deleted", terminalVerified = terminal,
            };
            object terminal = Entry("terminal", "shared", true);
            bool siblingAbsent = recovered && !remote.Units.Contains("shared");
            object sibling = workflowOnly && !siblingAbsent
                ? new { caseId = "case", workflowInstanceId = "sibling", state = "Admitted" }
                : Entry("sibling", "shared", siblingAbsent);
            object unrelated = Entry("other-workflow", "unrelated", true);
            List<object> entries = pendingFirst ? [sibling, terminal, unrelated] : [terminal, sibling, unrelated];
            if (unavailableRetry)
            {
                entries.Add(new { caseId = "different-case", workflowInstanceId = "different-terminal", memoryUnitId = "different-terminal-unit",
                    state = remote.Units.Contains("different-terminal-unit") ? "Completed" : "Deleted", terminalVerified = true });
            }
            return Task.FromResult(Page(entries.ToArray()));
        };
        using var http = PartyMemoryRecoveryTests.Http(remote);
        await PartyMemoryRecoveryTests.Mappings(state).RecordMappingAsync("tenant", "party", "shared", PartyMemoryUrn.Build("tenant", "party"), "case", token);

        (await PartyMemoryRecoveryTests.Cleanup(state, http).DeleteByPartyAsync("tenant", "case", "party", token)).Cleaned.ShouldBeFalse();

        remote.Requests.Where(item => item.Method == HttpMethod.Delete).ShouldHaveSingleItem().Path.ShouldEndWith("/unrelated");
        remote.Units.ShouldBe(["shared", "survivor"], ignoreOrder: true);
        var intents = PartyMemoryRecoveryTests.Intents(state);
        var pendingIntents = await intents.GetAsync("tenant", "party", token);
        pendingIntents.Select(item => item.WorkflowInstanceId).ShouldBe(["terminal", "sibling"], ignoreOrder: true);
        pendingIntents.ShouldAllBe(item => !item.CleanupSettled);
        pendingIntents.Single(item => item.WorkflowInstanceId == "terminal").MemoryUnitId.ShouldBe("shared");
        var pendingSibling = pendingIntents.Single(item => item.WorkflowInstanceId == "sibling");
        pendingSibling.RequiresTerminalEvidence.ShouldBeTrue();
        pendingSibling.MemoryUnitId.ShouldBe(workflowOnly ? null : "shared");
        (await PartyMemoryRecoveryTests.Mappings(state).GetMappingsAsync("tenant", "party", token)).ShouldHaveSingleItem().MemoryUnitId.ShouldBe("shared");

        remote.Requests.Clear();
        remote.WorkflowErrors.Clear();
        remote.BeforeRequest = null;
        if (workflowOnly)
        {
            var mappings = PartyMemoryRecoveryTests.Mappings(state);
            string source = PartyMemoryUrn.Build("tenant", "party");
            unavailableRetry = true;
            remote.Workflows.Remove("sibling");
            remote.Units.UnionWith(["case-candidate", "different-unit", "different-terminal-unit"]);
            remote.Workflows["candidate-workflow"] = PartyMemoryRecoveryHandler.Completed("candidate-workflow", "case-candidate");
            remote.Workflows["different-workflow"] = PartyMemoryRecoveryHandler.Completed("different-workflow", "different-unit", caseId: "different-case");
            await mappings.RecordMappingAsync("tenant", "party", "candidate-workflow", source, "case", token);
            await mappings.RecordMappingAsync("tenant", "party", "different-workflow", source, "different-case", token);

            (await PartyMemoryRecoveryTests.Cleanup(state, http).DeleteByPartyAsync("tenant", "case", "party", token)).Cleaned.ShouldBeFalse();

            remote.Requests.ShouldContain(item => item.Path.EndsWith("/ingest/sibling", StringComparison.Ordinal));
            remote.Requests.Where(item => item.Method == HttpMethod.Delete).Select(item => item.Path.Split('/')[^1])
                .ShouldBe(["different-unit", "different-terminal-unit"], ignoreOrder: true);
            remote.Units.ShouldBe(["shared", "case-candidate", "survivor"], ignoreOrder: true);
            (await intents.GetAsync("tenant", "party", token)).ShouldBe(pendingIntents, ignoreOrder: true);
            (await mappings.GetMappingsAsync("tenant", "party", token)).Select(item => item.MemoryUnitId)
                .ShouldBe(["shared", "candidate-workflow"], ignoreOrder: true);
            remote.Requests.Clear();
        }

        remote.Workflows["sibling"] = siblingStatus;
        recovered = true;
        (await PartyMemoryRecoveryTests.Cleanup(state, http).DeleteByPartyAsync("tenant", "case", "party", token)).Cleaned.ShouldBeTrue();

        remote.Requests.Where(item => item.Path.EndsWith("/shared", StringComparison.Ordinal)).Select(item => item.Method)
            .ShouldBe([HttpMethod.Delete, HttpMethod.Get]);
        if (workflowOnly)
        {
            remote.Requests.Where(item => item.Path.EndsWith("/case-candidate", StringComparison.Ordinal)).Select(item => item.Method)
                .ShouldBe([HttpMethod.Delete, HttpMethod.Get]);
        }

        remote.Units.ShouldBe(["survivor"]);
        (await PartyMemoryRecoveryTests.Mappings(state).GetMappingsAsync("tenant", "party", token)).ShouldBeEmpty();
        (await intents.GetAsync("tenant", "party", token)).ShouldBeEmpty();
        var receipts = (await intents.BeginCleanupAsync("tenant", "party", token)).Where(item => item.MemoryUnitId == "shared").ToArray();
        receipts.Select(item => item.WorkflowInstanceId).ShouldBe(["terminal", "sibling"], ignoreOrder: true);
        receipts.ShouldAllBe(item => item.CleanupSettled && !item.RequiresTerminalEvidence && item.AbsenceAuthority == item.ProducerAuthority);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnknownOutputFromLegacyOrForeignInventoryProtectsSameCaseCandidates(bool foreignInventory)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var state = new PartyMemoryTestReadModelStore();
        var intents = PartyMemoryRecoveryTests.Intents(state);
        var mappings = PartyMemoryRecoveryTests.Mappings(state);
        var remote = new PartyMemoryRecoveryHandler();
        remote.Units.UnionWith(["terminal-unit", "mapped-unit"]);
        remote.Workflows["mapped-workflow"] = PartyMemoryRecoveryHandler.Completed("mapped-workflow", "mapped-unit");
        remote.InventoryRequest = (request, _) => Task.FromResult(request.Method == HttpMethod.Post
            ? new HttpResponseMessage(HttpStatusCode.NoContent)
            : Page([new { caseId = "case", workflowInstanceId = "terminal", memoryUnitId = "terminal-unit", state = "Completed", terminalVerified = true }]));
        using var http = PartyMemoryRecoveryTests.Http(remote);
        string source = PartyMemoryUrn.Build("tenant", "party");
        if (foreignInventory)
        {
            await intents.BeginCleanupAsync("tenant", "party", token);
            await intents.RetainDiscoveredAsync("tenant", "party", "case", source, "unknown", null, null,
                terminalVerified: false, token, PartyMemoryCleanupService.BuildProducerAuthority(new Uri("https://foreign.example/")));
        }
        else
        {
            await intents.RegisterAsync("tenant", "party", new("legacy", "case", source, WorkflowInstanceId: "unknown"), token);
        }
        await mappings.RecordMappingAsync("tenant", "party", "mapped-workflow", source, "case", token);

        (await PartyMemoryRecoveryTests.Cleanup(state, http).DeleteByPartyAsync("tenant", "case", "party", token)).Cleaned.ShouldBeFalse();

        remote.Requests.ShouldNotContain(item => item.Method == HttpMethod.Delete);
        remote.Requests.Any(item => item.Path.EndsWith("/ingest/unknown", StringComparison.Ordinal)).ShouldBe(!foreignInventory);
        remote.Units.ShouldBe(["terminal-unit", "mapped-unit"], ignoreOrder: true);
        (await intents.GetAsync("tenant", "party", token)).Select(item => item.WorkflowInstanceId).ShouldBe(["unknown", "terminal"], ignoreOrder: true);
        (await mappings.GetMappingsAsync("tenant", "party", token)).ShouldHaveSingleItem().MemoryUnitId.ShouldBe("mapped-workflow");
    }

    [Fact]
    public async Task UnacknowledgedLegacyReservationHoldsItsCaseUntilInventoryBindsTheAttempt()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        const string tenant = "tenant";
        const string party = "party";
        string source = PartyMemoryUrn.Build(tenant, party);
        var state = new PartyMemoryTestReadModelStore();
        var remote = new PartyMemoryRecoveryHandler();
        remote.Units.UnionWith(["terminal-unit", "mapped-unit", "other-unit", "reserved-unit", "unrelated-survivor"]);
        remote.Workflows["mapped-workflow"] = PartyMemoryRecoveryHandler.Completed("mapped-workflow", "mapped-unit");
        using var http = PartyMemoryRecoveryTests.Http(remote);
        string? producer = PartyMemoryCleanupService.BuildProducerAuthority(http.BaseAddress);
        var reservation = new PartyMemoryIngestionIntent("unacknowledged", "case", source, ProducerAuthority: producer);
        await PartyMemoryRecoveryTests.Intents(state).RegisterAsync(tenant, party, reservation, token);
        await PartyMemoryRecoveryTests.Mappings(state).RecordMappingAsync(tenant, party, "mapped-workflow", source, "case", token);
        await PartyMemoryRecoveryTests.Mappings(state).RecordMappingAsync(tenant, party, "other-unit", source, "other-case", token);

        string retryHash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(reservation.AttemptId)));
        bool published = false;
        remote.InventoryRequest = (request, _) =>
        {
            if (request.Method == HttpMethod.Post)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
            }

            object Entry(string caseId, string workflow, string unit, string? retry = null) => new
            {
                caseId, workflowInstanceId = workflow, memoryUnitId = unit,
                state = remote.Units.Contains(unit) ? "Completed" : "Deleted",
                terminalVerified = true, idempotencyTokenHash = retry,
            };
            List<object> entries = [Entry("case", "terminal", "terminal-unit"), Entry("other-case", "other", "other-unit")];
            if (published)
            {
                entries.Add(Entry("case", "reserved-workflow", "reserved-unit", retryHash));
            }

            return Task.FromResult(Page(entries.ToArray(), complete: false));
        };

        (await PartyMemoryRecoveryTests.Cleanup(state, http).DeleteByPartyAsync(tenant, "case", party, token)).Cleaned.ShouldBeFalse();

        remote.Requests.Where(item => item.Method == HttpMethod.Delete).ShouldHaveSingleItem().Path.ShouldEndWith("/other-unit");
        remote.Units.ShouldBe(["terminal-unit", "mapped-unit", "reserved-unit", "unrelated-survivor"], ignoreOrder: true);
        var active = await PartyMemoryRecoveryTests.Intents(state).GetAsync(tenant, party, token);
        active.Single(item => item.AttemptId == reservation.AttemptId).ShouldBe(reservation);
        active.Single(item => item.WorkflowInstanceId == "terminal").MemoryUnitId.ShouldBe("terminal-unit");
        active.ShouldAllBe(item => !item.CleanupSettled);
        (await PartyMemoryRecoveryTests.Mappings(state).GetMappingsAsync(tenant, party, token))
            .ShouldHaveSingleItem().MemoryUnitId.ShouldBe("mapped-workflow");

        published = true;
        remote.Requests.Clear();
        (await PartyMemoryRecoveryTests.Cleanup(state, http).DeleteByPartyAsync(tenant, "case", party, token)).Cleaned.ShouldBeFalse();

        remote.Requests.Where(item => item.Method == HttpMethod.Delete).Select(item => item.Path.Split('/')[^1])
            .ShouldBe(["terminal-unit", "mapped-unit", "reserved-unit"], ignoreOrder: true);
        remote.Units.ShouldBe(["unrelated-survivor"]);
        (await PartyMemoryRecoveryTests.Intents(state).GetAsync(tenant, party, token)).ShouldBeEmpty();
        (await PartyMemoryRecoveryTests.Mappings(state).GetMappingsAsync(tenant, party, token)).ShouldBeEmpty();
        var receipts = await PartyMemoryRecoveryTests.Intents(state).BeginCleanupAsync(tenant, party, token);
        receipts.Single(item => item.AttemptId == reservation.AttemptId).ShouldBe(reservation with
        {
            WorkflowInstanceId = "reserved-workflow", MemoryUnitId = "reserved-unit", CleanupSettled = true, AbsenceAuthority = producer,
        });
        (await PartyMemoryRecoveryTests.Intents(state).GetSourceClosureVersionAsync(tenant, party, token, producer)).ShouldBeNull();
    }

    [Theory]
    [InlineData("missing-protocol")]
    [InlineData("missing-revision")]
    [InlineData("missing-digest")]
    [InlineData("different-protocol")]
    [InlineData("different-revision")]
    [InlineData("different-digest")]
    [InlineData("different-case")]
    [InlineData("duplicate-on-later-page")]
    [InlineData("uppercase-duplicate-on-later-page")]
    [InlineData("incomplete-later-page")]
    [InlineData("canceled-on-later-page")]
    public async Task TypedRetryInventoryRequiresExactAdmissionBeforeDeletingItsCase(string defect)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        const string tenant = "tenant";
        const string party = "party";
        const string caseId = "case";
        string source = PartyMemoryUrn.Build(tenant, party);
        var state = new PartyMemoryTestReadModelStore();
        var remote = new PartyMemoryRecoveryHandler();
        remote.Units.UnionWith(["reserved-unit", "mapped-unit", "other-unit", "unrelated-survivor"]);
        remote.Workflows["mapped-workflow"] = PartyMemoryRecoveryHandler.Completed("mapped-workflow", "mapped-unit");
        using HttpClient http = PartyMemoryRecoveryTests.Http(remote);
        string? producer = PartyMemoryCleanupService.BuildProducerAuthority(http.BaseAddress);
        string digest = new('a', 64);
        var reservation = new PartyMemoryIngestionIntent("typed-retry", caseId, source,
            MemoryUnitId: defect == "incomplete-later-page" ? "reserved-unit" : null, ProducerAuthority: producer,
            RefreshProtocolVersion: SourceRefreshProtocol.Version, SourceRevisionNumber: 29, RefreshIdentityDigest: digest);
        await PartyMemoryRecoveryTests.Intents(state).RegisterAsync(tenant, party, reservation, token);
        await PartyMemoryRecoveryTests.Mappings(state).RecordMappingAsync(tenant, party, "mapped-workflow", source, caseId, token);
        string retryHash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(reservation.AttemptId)));
        bool exact = false;
        bool canceled = defect == "canceled-on-later-page";
        using var interruption = CancellationTokenSource.CreateLinkedTokenSource(token);
        remote.InventoryRequest = (request, _) =>
        {
            if (request.Method == HttpMethod.Post)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
            }

            var observed = new Dictionary<string, object?>
            {
                ["caseId"] = caseId,
                ["workflowInstanceId"] = "reserved-workflow",
                ["memoryUnitId"] = "reserved-unit",
                ["state"] = remote.Units.Contains("reserved-unit") ? "Completed" : "Deleted",
                ["terminalVerified"] = true,
                ["idempotencyTokenHash"] = retryHash,
                ["refreshProtocolVersion"] = SourceRefreshProtocol.Version,
                ["sourceRevisionNumber"] = 29L,
                ["refreshIdentityDigest"] = digest,
            };
            string? next = null;
            if (!exact)
            {
                switch (defect)
                {
                    case "missing-protocol": observed.Remove("refreshProtocolVersion"); break;
                    case "missing-revision": observed.Remove("sourceRevisionNumber"); break;
                    case "missing-digest": observed.Remove("refreshIdentityDigest"); break;
                    case "different-protocol": observed["refreshProtocolVersion"] = "unknown"; break;
                    case "different-revision": observed["sourceRevisionNumber"] = 30L; break;
                    case "different-digest": observed["refreshIdentityDigest"] = new string('b', 64); break;
                    case "different-case": observed["caseId"] = "foreign-case"; break;
                    case "duplicate-on-later-page":
                    case "uppercase-duplicate-on-later-page":
                    case "incomplete-later-page":
                    case "canceled-on-later-page":
                        if (request.RequestUri!.Query.Contains("cursor=", StringComparison.Ordinal))
                        {
                            if (canceled)
                            {
                                interruption.Cancel();
                                return Task.FromCanceled<HttpResponseMessage>(interruption.Token);
                            }

                            if (defect == "incomplete-later-page")
                            {
                                return Task.FromResult(Page([], complete: false));
                            }

                            observed["workflowInstanceId"] = "competing-workflow";
                            if (defect == "uppercase-duplicate-on-later-page")
                            {
                                observed["idempotencyTokenHash"] = retryHash.ToUpperInvariant();
                            }

                            return Task.FromResult(Page([observed]));
                        }
                        next = "next";
                        break;
                }
            }

            return Task.FromResult(Page([observed, new
            {
                caseId = "other-case", workflowInstanceId = "other-workflow", memoryUnitId = "other-unit",
                state = remote.Units.Contains("other-unit") ? "Completed" : "Deleted", terminalVerified = true,
            }], next));
        };

        if (canceled)
        {
            await Should.ThrowAsync<OperationCanceledException>(() => PartyMemoryRecoveryTests.Cleanup(state, http)
                .DeleteByPartyAsync(tenant, caseId, party, interruption.Token));
            remote.Requests.ShouldNotContain(item => item.Method == HttpMethod.Delete);
            remote.Units.ShouldContain("other-unit");
        }
        else
        {
            (await PartyMemoryRecoveryTests.Cleanup(state, http).DeleteByPartyAsync(tenant, caseId, party, token)).Cleaned.ShouldBeFalse();
            remote.Requests.Where(item => item.Method == HttpMethod.Delete).ShouldHaveSingleItem().Path.ShouldEndWith("/other-unit");
            remote.Units.ShouldBe(["reserved-unit", "mapped-unit", "unrelated-survivor"], ignoreOrder: true);
        }

        (await PartyMemoryRecoveryTests.Intents(state).GetAsync(tenant, party, token))
            .Single(item => item.CaseId == caseId).ShouldBe(reservation);
        (await PartyMemoryRecoveryTests.Mappings(state).GetMappingsAsync(tenant, party, token))
            .ShouldHaveSingleItem().MemoryUnitId.ShouldBe("mapped-workflow");
        state.SerializedValues.ShouldNotContain(value => value.Contains("reserved-workflow", StringComparison.Ordinal)
            || value.Contains("competing-workflow", StringComparison.Ordinal));

        exact = true;
        remote.Requests.Clear();

        (await PartyMemoryRecoveryTests.Cleanup(state, http).DeleteByPartyAsync(tenant, caseId, party, token)).Cleaned.ShouldBeTrue();

        remote.Requests.Where(item => item.Method == HttpMethod.Delete).Select(item => item.Path.Split('/')[^1])
            .ShouldBe(canceled ? ["reserved-unit", "mapped-unit", "other-unit"] : ["reserved-unit", "mapped-unit"], ignoreOrder: true);
        remote.Units.ShouldBe(["unrelated-survivor"]);
        var intents = PartyMemoryRecoveryTests.Intents(state);
        (await intents.GetAsync(tenant, party, token)).ShouldBeEmpty();
        (await PartyMemoryRecoveryTests.Mappings(state).GetMappingsAsync(tenant, party, token)).ShouldBeEmpty();
        var receipt = (await intents.BeginCleanupAsync(tenant, party, token)).Single(item => item.AttemptId == reservation.AttemptId);
        receipt.ShouldBe(reservation with
        {
            WorkflowInstanceId = "reserved-workflow", MemoryUnitId = "reserved-unit", CleanupSettled = true, AbsenceAuthority = producer,
        });
    }

    [Fact]
    public async Task SharedUnitRetainsValidatedLegacyAliasForRetryAfterWorkflowExpiry()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var state = new PartyMemoryTestReadModelStore();
        var remote = new PartyMemoryRecoveryHandler();
        remote.Units.Add("shared");
        remote.Workflows["alias"] = PartyMemoryRecoveryHandler.Completed("alias", "shared");
        remote.Workflows["sibling"] = PartyMemoryRecoveryHandler.Completed("sibling", "shared") with { RuntimeStatus = "Running" };
        remote.InventoryRequest = (request, _) => Task.FromResult(request.Method == HttpMethod.Post
            ? new HttpResponseMessage(HttpStatusCode.NoContent)
            : Page([new { caseId = "case", workflowInstanceId = "sibling", memoryUnitId = "shared",
                state = remote.Units.Contains("shared") ? "Completed" : "Deleted", terminalVerified = !remote.Units.Contains("shared") }]));
        using var http = PartyMemoryRecoveryTests.Http(remote);
        string? authority = PartyMemoryCleanupService.BuildProducerAuthority(http.BaseAddress);
        string source = PartyMemoryUrn.Build("tenant", "party");
        var intents = PartyMemoryRecoveryTests.Intents(state);
        var old = new PartyMemoryIngestionIntent("old", "case", source, "old-workflow", "shared", ProducerAuthority: authority);
        await intents.RegisterAsync("tenant", "party", old, token);
        await intents.BeginCleanupAsync("tenant", "party", token);
        (await intents.RemoveAsync("tenant", "party", [old], token, authority)).ShouldBe(0);
        var mappings = PartyMemoryRecoveryTests.Mappings(state);
        await mappings.RecordMappingAsync("tenant", "party", "alias", source, "case", token);

        (await PartyMemoryRecoveryTests.Cleanup(state, http).DeleteByPartyAsync("tenant", "case", "party", token)).Cleaned.ShouldBeFalse();
        remote.Requests.ShouldNotContain(item => item.Method == HttpMethod.Delete);
        remote.Requests.ShouldContain(item => item.Path.EndsWith("/ingest/alias", StringComparison.Ordinal));
        var retained = (await intents.GetAsync("tenant", "party", token)).Single(item => item.WorkflowInstanceId == "alias");
        retained.MemoryUnitId.ShouldBe("shared");
        retained.ProducerAuthority.ShouldBe(authority);
        retained.CleanupSettled.ShouldBeFalse();
        (await mappings.GetMappingsAsync("tenant", "party", token)).ShouldHaveSingleItem().MemoryUnitId.ShouldBe("alias");

        remote.Workflows.Remove("alias");
        remote.Workflows["sibling"] = PartyMemoryRecoveryHandler.Completed("sibling", "shared");
        remote.Requests.Clear();
        (await PartyMemoryRecoveryTests.Cleanup(state, http).DeleteByPartyAsync("tenant", "case", "party", token)).Cleaned.ShouldBeTrue();
        remote.Requests.ShouldNotContain(item => item.Path.EndsWith("/ingest/alias", StringComparison.Ordinal));
        remote.Requests.Where(item => item.Path.EndsWith("/shared", StringComparison.Ordinal)).Select(item => item.Method)
            .ShouldBe([HttpMethod.Delete, HttpMethod.Get]);
        (await intents.GetAsync("tenant", "party", token)).ShouldBeEmpty();
        (await intents.BeginCleanupAsync("tenant", "party", token)).ShouldContain(item => item.AttemptId == retained.AttemptId && item.CleanupSettled);
        (await mappings.GetMappingsAsync("tenant", "party", token)).ShouldBeEmpty();
    }

    [Fact]
    public async Task RetainingPendingAliasCannotDuplicateConcurrentExactReceipt()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var state = new PartyMemoryTestReadModelStore();
        var intents = PartyMemoryRecoveryTests.Intents(state);
        var mapping = new PartyMemoryUnitMappingEntry("alias", PartyMemoryUrn.Build("tenant", "party"), "case");
        string? authority = PartyMemoryCleanupService.BuildProducerAuthority(new Uri("https://producer.example/"));
        await intents.BeginCleanupAsync("tenant", "party", token);
        state.BeforeSave = async () =>
        {
            var concurrent = await intents.RetainLegacyAsync("tenant", "party", mapping, "case", "shared", token, authority).ConfigureAwait(false);
            (await intents.RemoveAsync("tenant", "party", [concurrent], token, authority).ConfigureAwait(false)).ShouldBe(0);
        };

        var failure = await Should.ThrowAsync<InvalidOperationException>(() => intents.RetainLegacyAsync("tenant", "party", mapping,
            "case", "shared", token, authority, requirePending: true));

        failure.Message.ShouldBe("memory-ingestion-legacy-snapshot-changed");
        (await intents.GetAsync("tenant", "party", token)).ShouldBeEmpty();
        var receipt = (await intents.BeginCleanupAsync("tenant", "party", token)).ShouldHaveSingleItem();
        receipt.WorkflowInstanceId.ShouldBe("alias");
        receipt.CleanupSettled.ShouldBeTrue();
    }

    [Fact]
    public async Task FreshWorkflowsSharingOldReceiptRetryDelayedDeletionOncePerPass()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var state = new PartyMemoryTestReadModelStore();
        var remote = new PartyMemoryRecoveryHandler { DelayDeletion = true };
        remote.Units.Add("shared");
        remote.InventoryRequest = (request, _) => Task.FromResult(request.Method == HttpMethod.Post
            ? new HttpResponseMessage(HttpStatusCode.NoContent)
            : Page([.. new[] { "first", "second" }.Select(workflow => (object)new
            {
                caseId = "case", workflowInstanceId = workflow, memoryUnitId = "shared",
                state = remote.Units.Contains("shared") ? "Completed" : "Deleted", terminalVerified = true,
            })]));
        using var http = PartyMemoryRecoveryTests.Http(remote);
        string? authority = PartyMemoryCleanupService.BuildProducerAuthority(http.BaseAddress);
        string source = PartyMemoryUrn.Build("tenant", "party");
        var intents = PartyMemoryRecoveryTests.Intents(state);
        var old = new PartyMemoryIngestionIntent("old", "case", source, "old-workflow", "shared", ProducerAuthority: authority);
        await intents.RegisterAsync("tenant", "party", old, token);
        await intents.BeginCleanupAsync("tenant", "party", token);
        (await intents.RemoveAsync("tenant", "party", [old], token, authority)).ShouldBe(0);
        var mappings = PartyMemoryRecoveryTests.Mappings(state);
        await mappings.RecordMappingAsync("tenant", "party", "old-workflow", source, "case", token);
        await mappings.RecordMappingAsync("tenant", "party", "shared", source, "case", token);

        (await PartyMemoryRecoveryTests.Cleanup(state, http).DeleteByPartyAsync("tenant", "case", "party", token)).Cleaned.ShouldBeFalse();
        remote.Requests.Select(item => item.Method).ShouldBe([HttpMethod.Delete, HttpMethod.Get]);
        remote.Units.ShouldBe(["shared"]);
        (await intents.GetAsync("tenant", "party", token)).Select(item => item.WorkflowInstanceId).ShouldBe(["first", "second"], ignoreOrder: true);
        (await mappings.GetMappingsAsync("tenant", "party", token)).Count.ShouldBe(2);

        remote.DelayDeletion = false;
        remote.Requests.Clear();
        (await PartyMemoryRecoveryTests.Cleanup(state, http).DeleteByPartyAsync("tenant", "case", "party", token)).Cleaned.ShouldBeTrue();
        remote.Requests.Select(item => item.Method).ShouldBe([HttpMethod.Delete, HttpMethod.Get]);
        remote.Units.ShouldBeEmpty();
        (await intents.GetAsync("tenant", "party", token)).ShouldBeEmpty();
        (await intents.BeginCleanupAsync("tenant", "party", token)).Count.ShouldBe(3);
        (await mappings.GetMappingsAsync("tenant", "party", token)).ShouldBeEmpty();
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
    [InlineData("Completed", true)]
    [InlineData("Completed", false)]
    [InlineData("Deleted", true)]
    [InlineData("Deleted", false)]
    public async Task OutputKnownOrDeletedStillRequiresIndependentWorkflowTerminalEvidence(string state, bool unitFirst)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var persisted = new PartyMemoryTestReadModelStore();
        var remote = new PartyMemoryRecoveryHandler();
        bool terminal = false;
        remote.Workflows["producer"] = PartyMemoryRecoveryHandler.Completed("producer", "canonical") with
        { RuntimeStatus = "Running", MemoryUnitId = null, MemoryUnitStatus = null };
        remote.Units.Add("canonical");
        remote.InventoryRequest = (request, _) =>
        {
            if (request.Method == HttpMethod.Post) { return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent)); }
            string observed = terminal ? remote.Units.Contains("canonical") ? "Completed" : "Deleted" : state;
            object unit = new { caseId = "case", memoryUnitId = "canonical", state = observed, terminalVerified = true };
            object workflow = new { caseId = "case", workflowInstanceId = "producer", memoryUnitId = "canonical", state = observed, terminalVerified = terminal };
            return Task.FromResult(Page(unitFirst ? [unit, workflow] : [workflow, unit]));
        };
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
    public async Task LegacyIntentRetainsFirstInventoryProducerAcrossRestartUntilItsUnitIsAbsent()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var state = new PartyMemoryTestReadModelStore();
        string source = PartyMemoryUrn.Build("tenant", "party");
        await PartyMemoryRecoveryTests.Intents(state).RegisterAsync("tenant", "party",
            new("legacy", "case", source, WorkflowInstanceId: "workflow"), token);
        var producerA = new PartyMemoryRecoveryHandler { DelayDeletion = true };
        producerA.Units.UnionWith(["unit", "survivor"]);
        producerA.InventoryRequest = (request, _) => Task.FromResult(request.Method == HttpMethod.Post
            ? new HttpResponseMessage(HttpStatusCode.NoContent)
            : Page([new { caseId = "case", workflowInstanceId = "workflow", memoryUnitId = "unit",
                state = producerA.Units.Contains("unit") ? "Completed" : "Deleted", terminalVerified = true }]));
        using var first = PartyMemoryRecoveryTests.Http(producerA);
        first.BaseAddress = new Uri("https://producer-a.example/");
        (await PartyMemoryRecoveryTests.Cleanup(state, first).DeleteByPartyAsync("tenant", "case", "party", token)).Cleaned.ShouldBeFalse();

        var captured = (await PartyMemoryRecoveryTests.Intents(state).GetAsync("tenant", "party", token)).ShouldHaveSingleItem();
        captured.AttemptId.ShouldBe("legacy");
        captured.MemoryUnitId.ShouldBe("unit");
        captured.ProducerAuthority.ShouldBe(PartyMemoryCleanupService.BuildProducerAuthority(first.BaseAddress));
        var producerB = new PartyMemoryRecoveryHandler();
        using var second = PartyMemoryRecoveryTests.Http(producerB);
        second.BaseAddress = new Uri("https://producer-b.example/");
        (await PartyMemoryRecoveryTests.Cleanup(state, second).DeleteByPartyAsync("tenant", "case", "party", token)).Cleaned.ShouldBeFalse();
        producerB.Requests.ShouldBeEmpty();
        (await PartyMemoryRecoveryTests.Intents(state).GetAsync("tenant", "party", token)).ShouldHaveSingleItem().ShouldBe(captured);
        producerA.Units.ShouldContain("unit");

        producerA.DelayDeletion = false;
        (await PartyMemoryRecoveryTests.Cleanup(state, first).DeleteByPartyAsync("tenant", "case", "party", token)).Cleaned.ShouldBeTrue();
        producerA.Units.ShouldBe(["survivor"]);
        (await PartyMemoryRecoveryTests.Intents(state).GetAsync("tenant", "party", token)).ShouldBeEmpty();
    }

    [Fact]
    public async Task StatusOnlyLegacyRecoveryPersistsProducerBeforeDeleteAndPreservesItAcrossRestart()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var state = new PartyMemoryTestReadModelStore();
        string source = PartyMemoryUrn.Build("tenant", "party");
        var intents = PartyMemoryRecoveryTests.Intents(state);
        await intents.RegisterAsync("tenant", "party", new("legacy", "case", source, WorkflowInstanceId: "workflow"), token);
        await PartyMemoryRecoveryTests.Mappings(state).RecordMappingAsync("tenant", "party", "workflow", source, "case", token);
        var producerA = new PartyMemoryRecoveryHandler { DelayDeletion = true };
        producerA.Units.UnionWith(["canonical", "survivor"]);
        producerA.Workflows["workflow"] = PartyMemoryRecoveryHandler.Completed();
        using var first = PartyMemoryRecoveryTests.Http(producerA);
        first.BaseAddress = new Uri("https://producer-a.example/");
        string? authorityA = PartyMemoryCleanupService.BuildProducerAuthority(first.BaseAddress);
        producerA.BeforeRequest = async (request, cancellation) =>
        {
            if (request.Method == HttpMethod.Delete)
            {
                var terminal = (await intents.GetAsync("tenant", "party", cancellation).ConfigureAwait(false)).ShouldHaveSingleItem();
                terminal.AttemptId.ShouldBe("legacy");
                terminal.MemoryUnitId.ShouldBe("canonical");
                terminal.ProducerAuthority.ShouldBe(authorityA);
            }
        };

        (await PartyMemoryRecoveryTests.Cleanup(state, first).DeleteByPartyAsync("tenant", "case", "party", token)).Cleaned.ShouldBeFalse();
        producerA.Requests.ShouldContain(item => item.Path.EndsWith("/ingest/workflow", StringComparison.Ordinal));
        producerA.Requests.ShouldContain(item => item.Method == HttpMethod.Delete);
        var pending = (await intents.GetAsync("tenant", "party", token)).ShouldHaveSingleItem();
        pending.ProducerAuthority.ShouldBe(authorityA);

        var producerB = new PartyMemoryRecoveryHandler();
        producerB.Units.Add("canonical");
        producerB.Workflows["workflow"] = PartyMemoryRecoveryHandler.Completed();
        using var second = PartyMemoryRecoveryTests.Http(producerB);
        second.BaseAddress = new Uri("https://producer-b.example/");
        (await PartyMemoryRecoveryTests.Cleanup(state, second).DeleteByPartyAsync("tenant", "case", "party", token)).Cleaned.ShouldBeFalse();
        producerB.Requests.ShouldBeEmpty();
        producerB.Units.ShouldBe(["canonical"]);
        (await intents.GetAsync("tenant", "party", token)).ShouldHaveSingleItem().ShouldBe(pending);
        (await PartyMemoryRecoveryTests.Mappings(state).GetMappingsAsync("tenant", "party", token)).ShouldHaveSingleItem().MemoryUnitId.ShouldBe("workflow");

        producerA.DelayDeletion = false;
        producerA.Workflows.Clear();
        (await PartyMemoryRecoveryTests.Cleanup(state, first).DeleteByPartyAsync("tenant", "case", "party", token)).Cleaned.ShouldBeTrue();
        producerA.Units.ShouldBe(["survivor"]);
        (await intents.GetAsync("tenant", "party", token)).ShouldBeEmpty();
        (await PartyMemoryRecoveryTests.Mappings(state).GetMappingsAsync("tenant", "party", token)).ShouldBeEmpty();
    }

    [Fact]
    public async Task StatusOnlyLegacyRecoveryCannotOverwriteAConcurrentProducerBinding()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var state = new PartyMemoryTestReadModelStore();
        var intents = PartyMemoryRecoveryTests.Intents(state);
        string source = PartyMemoryUrn.Build("tenant", "party");
        await intents.RegisterAsync("tenant", "party", new("legacy", "case", source, WorkflowInstanceId: "workflow"), token);
        await PartyMemoryRecoveryTests.Mappings(state).RecordMappingAsync("tenant", "party", "workflow", source, "case", token);
        var remote = new PartyMemoryRecoveryHandler();
        remote.Workflows["workflow"] = PartyMemoryRecoveryHandler.Completed();
        remote.Units.Add("canonical");
        using var http = PartyMemoryRecoveryTests.Http(remote);
        http.BaseAddress = new Uri("https://producer-a.example/");
        string? authorityB = PartyMemoryCleanupService.BuildProducerAuthority(new Uri("https://producer-b.example/"));
        remote.BeforeRequest = (request, cancellation) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/ingest/workflow", StringComparison.Ordinal))
            {
                state.BeforeSave = () => intents.RetainDiscoveredAsync("tenant", "party", "case", source,
                    "workflow", "canonical", null, terminalVerified: true, cancellation, authorityB);
            }
            return Task.CompletedTask;
        };

        (await PartyMemoryRecoveryTests.Cleanup(state, http).DeleteByPartyAsync("tenant", "case", "party", token)).Cleaned.ShouldBeFalse();
        remote.Requests.ShouldHaveSingleItem().Path.ShouldEndWith("/ingest/workflow");
        remote.Units.ShouldBe(["canonical"]);
        var retained = (await intents.GetAsync("tenant", "party", token)).ShouldHaveSingleItem();
        retained.AttemptId.ShouldBe("legacy");
        retained.ProducerAuthority.ShouldBe(authorityB);
        retained.MemoryUnitId.ShouldBe("canonical");
        (await PartyMemoryRecoveryTests.Mappings(state).GetMappingsAsync("tenant", "party", token)).ShouldHaveSingleItem().MemoryUnitId.ShouldBe("workflow");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SerializedUnboundTerminalIntentRequiresFreshProducerEvidence(bool useInventory)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var state = new PartyMemoryTestReadModelStore();
        string source = PartyMemoryUrn.Build("tenant", "party");
        var legacy = new PartyMemoryIngestionIntent("legacy-terminal", "case", source, "workflow", "canonical");
        await PartyMemoryRecoveryTests.Intents(state).RegisterAsync("tenant", "party", legacy, token);
        var mappings = PartyMemoryRecoveryTests.Mappings(state);
        await mappings.RecordMappingAsync("tenant", "party", "canonical", source, "case", token);
        var other = new PartyMemoryRecoveryHandler();
        other.Units.Add("canonical");
        other.Workflows["workflow"] = PartyMemoryRecoveryHandler.Completed("workflow", "different-unit");
        other.InventoryRequest = (request, _) => Task.FromResult(request.Method == HttpMethod.Post
            ? new HttpResponseMessage(HttpStatusCode.NoContent)
            : Page([new { caseId = "case", memoryUnitId = "canonical", state = "Completed", terminalVerified = true }]));
        using var second = PartyMemoryRecoveryTests.Http(other);
        second.BaseAddress = new Uri("https://producer-b.example/");

        (await PartyMemoryRecoveryTests.Cleanup(state, second).DeleteByPartyAsync("tenant", "case", "party", token)).Cleaned.ShouldBeFalse();
        other.Requests.ShouldHaveSingleItem().Path.ShouldEndWith("/ingest/workflow");
        other.Units.ShouldBe(["canonical"]);
        (await PartyMemoryRecoveryTests.Intents(state).GetAsync("tenant", "party", token)).ShouldHaveSingleItem()
            .ShouldBe(legacy with { RequiresTerminalEvidence = true });
        (await mappings.GetMappingsAsync("tenant", "party", token)).ShouldHaveSingleItem().MemoryUnitId.ShouldBe("canonical");

        var owner = new PartyMemoryRecoveryHandler();
        owner.Units.Add("canonical");
        if (useInventory)
        {
            owner.InventoryRequest = (request, _) => Task.FromResult(request.Method == HttpMethod.Post
                ? new HttpResponseMessage(HttpStatusCode.NoContent)
                : Page([new { caseId = "case", workflowInstanceId = "workflow", memoryUnitId = "canonical",
                    state = owner.Units.Contains("canonical") ? "Completed" : "Deleted", terminalVerified = true }]));
        }
        else
        {
            owner.Workflows["workflow"] = PartyMemoryRecoveryHandler.Completed();
        }
        using var first = PartyMemoryRecoveryTests.Http(owner);
        first.BaseAddress = new Uri("https://producer-a.example/");

        (await PartyMemoryRecoveryTests.Cleanup(state, first).DeleteByPartyAsync("tenant", "case", "party", token)).Cleaned.ShouldBeTrue();
        owner.Requests.Count(item => item.Path.EndsWith("/ingest/workflow", StringComparison.Ordinal)).ShouldBe(useInventory ? 0 : 1);
        owner.Units.ShouldBeEmpty();
        var receipt = (await PartyMemoryRecoveryTests.Intents(state).BeginCleanupAsync("tenant", "party", token)).ShouldHaveSingleItem();
        receipt.AttemptId.ShouldBe(legacy.AttemptId);
        receipt.ProducerAuthority.ShouldBe(PartyMemoryCleanupService.BuildProducerAuthority(first.BaseAddress));
        receipt.CleanupSettled.ShouldBeTrue();
        (await mappings.GetMappingsAsync("tenant", "party", token)).ShouldBeEmpty();
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

    [Theory]
    [InlineData("unknown-output")]
    [InlineData("known-output")]
    [InlineData("unacknowledged")]
    public async Task DirectUnitCleanupRetainsAliasUntilSiblingCanBeReconciled(string siblingEvidence)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        const string tenant = "tenant";
        const string party = "party";
        const string caseId = "case";
        var state = new PartyMemoryTestReadModelStore();
        var remote = new PartyMemoryRecoveryHandler();
        remote.Units.UnionWith(["shared", "unrelated-survivor"]);
        remote.Workflows["selected"] = PartyMemoryRecoveryHandler.Completed("selected", "shared");
        var completedSibling = PartyMemoryRecoveryHandler.Completed("sibling", "shared");
        remote.Workflows["sibling"] = completedSibling with { RuntimeStatus = "Running" };
        using var http = PartyMemoryRecoveryTests.Http(remote);
        string? producer = PartyMemoryCleanupService.BuildProducerAuthority(http.BaseAddress);
        bool acknowledged = siblingEvidence != "unacknowledged";
        var sibling = new PartyMemoryIngestionIntent("pending-sibling", caseId, PartyMemoryUrn.Build(tenant, party),
            acknowledged ? "sibling" : null, siblingEvidence == "known-output" ? "shared" : null,
            RequiresTerminalEvidence: acknowledged, ProducerAuthority: producer);
        await PartyMemoryRecoveryTests.Intents(state).RegisterAsync(tenant, party, sibling, token);

        (await PartyMemoryRecoveryTests.Cleanup(state, http).DeleteMemoryUnitAsync(tenant, caseId, party, "selected", token)).Cleaned.ShouldBeFalse();

        remote.Requests.ShouldNotContain(item => item.Method == HttpMethod.Delete);
        remote.Units.ShouldContain("shared");
        var retained = await PartyMemoryRecoveryTests.Intents(state).GetAsync(tenant, party, token);
        retained.Count.ShouldBe(2);
        retained.ShouldContain(sibling);
        var selected = retained.Single(item => item.WorkflowInstanceId == "selected");
        selected.MemoryUnitId.ShouldBe("shared");
        selected.ProducerAuthority.ShouldBe(producer);
        retained.ShouldAllBe(item => !item.CleanupSettled);
        (await PartyMemoryRecoveryTests.Intents(state).BeginCleanupAsync(tenant, party, token)).ShouldBe(retained);

        if (!acknowledged)
        {
            remote.Requests.ShouldHaveSingleItem().Path.ShouldEndWith("/ingest/selected");
            remote.Requests.Clear();
            remote.Workflows.Remove("selected");
            (await PartyMemoryRecoveryTests.Cleanup(state, http).DeleteMemoryUnitAsync(tenant, caseId, party, "selected", token)).Cleaned.ShouldBeFalse();
            remote.Requests.ShouldBeEmpty();
            (await PartyMemoryRecoveryTests.Intents(state).BeginCleanupAsync(tenant, party, token)).ShouldBe(retained);
            return;
        }

        remote.Requests.Clear();
        remote.Workflows.Remove("selected");
        remote.Workflows["sibling"] = completedSibling;
        (await PartyMemoryRecoveryTests.Cleanup(state, http).DeleteMemoryUnitAsync(tenant, caseId, party, "selected", token)).Cleaned.ShouldBeTrue();

        remote.Requests.ShouldNotContain(item => item.Path.EndsWith("/ingest/selected", StringComparison.Ordinal));
        remote.Requests.Where(item => item.Path.EndsWith("/shared", StringComparison.Ordinal)).Select(item => item.Method)
            .ShouldBe([HttpMethod.Delete, HttpMethod.Get]);
        remote.Units.ShouldBe(["unrelated-survivor"]);
        var intents = PartyMemoryRecoveryTests.Intents(state);
        (await intents.GetAsync(tenant, party, token)).ShouldBeEmpty();
        (await intents.BeginCleanupAsync(tenant, party, token)).Single(item => item.WorkflowInstanceId == "sibling")
            .ShouldBe(sibling with
            {
                RequiresTerminalEvidence = false, MemoryUnitId = "shared", CleanupSettled = true, AbsenceAuthority = producer,
            });
        (await intents.BeginCleanupAsync(tenant, party, token)).Single(item => item.WorkflowInstanceId == "selected")
            .ShouldBe(selected with { CleanupSettled = true, AbsenceAuthority = producer });

        remote.Requests.Clear();
        remote.Workflows.Remove("sibling");
        (await PartyMemoryRecoveryTests.Cleanup(state, http).DeleteMemoryUnitAsync(tenant, caseId, party, "sibling", token)).Cleaned.ShouldBeTrue();
        remote.Requests.ShouldBeEmpty();
        (await intents.GetAsync(tenant, party, token)).ShouldBeEmpty();
        (await intents.BeginCleanupAsync(tenant, party, token)).ShouldAllBe(item => item.CleanupSettled && item.AbsenceAuthority == producer);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task DirectUnitCleanupRequiresFreshAbsenceAfterNewSharedProducerBecomesTerminal(bool selectNewProducer, bool alreadyTerminal)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        const string tenant = "tenant";
        const string party = "party";
        const string caseId = "case";
        var state = new PartyMemoryTestReadModelStore();
        var remote = new PartyMemoryRecoveryHandler();
        remote.Units.Add("shared");
        remote.Workflows["historical"] = PartyMemoryRecoveryHandler.Completed("historical", "shared");
        using var http = PartyMemoryRecoveryTests.Http(remote);
        string? producer = PartyMemoryCleanupService.BuildProducerAuthority(http.BaseAddress);
        (await PartyMemoryRecoveryTests.Cleanup(state, http).DeleteMemoryUnitAsync(tenant, caseId, party, "historical", token)).Cleaned.ShouldBeTrue();

        string source = PartyMemoryUrn.Build(tenant, party);
        await PartyMemoryRecoveryTests.Intents(state).RetainDiscoveredAsync(tenant, party, caseId, source, "new-producer",
            "shared", retryIdentityHash: null, terminalVerified: alreadyTerminal, token, producer);
        remote.Units.Add("shared");
        if (!alreadyTerminal)
        {
            remote.Workflows["new-producer"] = PartyMemoryRecoveryHandler.Completed("new-producer", "shared");
        }
        string selected = selectNewProducer ? "new-producer" : "historical";
        remote.Requests.Clear();
        remote.DeleteStatus = (int)HttpStatusCode.ServiceUnavailable;

        (await PartyMemoryRecoveryTests.Cleanup(state, http).DeleteMemoryUnitAsync(tenant, caseId, party, selected, token)).Cleaned.ShouldBeFalse();

        remote.Requests.Where(item => item.Method == HttpMethod.Delete).ShouldHaveSingleItem().Path.ShouldEndWith("/shared");
        var beforeRetry = await PartyMemoryRecoveryTests.Intents(state).BeginCleanupAsync(tenant, party, token);
        beforeRetry.Single(item => item.CleanupSettled).WorkflowInstanceId.ShouldBe("historical");
        beforeRetry.Single(item => !item.CleanupSettled).RequiresTerminalEvidence.ShouldBe(!alreadyTerminal);

        remote.Requests.Clear();
        remote.DeleteStatus = (int)HttpStatusCode.NoContent;
        remote.DelayDeletion = true;
        (await PartyMemoryRecoveryTests.Cleanup(state, http).DeleteMemoryUnitAsync(tenant, caseId, party, selected, token)).Cleaned.ShouldBeFalse();
        remote.Requests.Where(item => item.Path.EndsWith("/shared", StringComparison.Ordinal)).Select(item => item.Method)
            .ShouldBe([HttpMethod.Delete, HttpMethod.Get]);
        remote.Units.ShouldContain("shared");
        (await PartyMemoryRecoveryTests.Intents(state).BeginCleanupAsync(tenant, party, token)).ShouldBe(beforeRetry);

        remote.Requests.Clear();
        remote.DelayDeletion = false;
        (await PartyMemoryRecoveryTests.Cleanup(state, http).DeleteMemoryUnitAsync(tenant, caseId, party, selected, token)).Cleaned.ShouldBeTrue();
        remote.Requests.Where(item => item.Path.EndsWith("/shared", StringComparison.Ordinal)).Select(item => item.Method)
            .ShouldBe([HttpMethod.Delete, HttpMethod.Get]);
        remote.Units.ShouldBeEmpty();
        var recovered = (await PartyMemoryRecoveryTests.Intents(state).BeginCleanupAsync(tenant, party, token))
            .Single(item => item.WorkflowInstanceId == "new-producer");
        recovered.RequiresTerminalEvidence.ShouldBeFalse();
        recovered.CleanupSettled.ShouldBeTrue();
        recovered.AbsenceAuthority.ShouldBe(producer);
        (await PartyMemoryRecoveryTests.Intents(state).GetAsync(tenant, party, token)).ShouldBeEmpty();

        remote.Requests.Clear();
        (await PartyMemoryRecoveryTests.Cleanup(state, http).DeleteMemoryUnitAsync(tenant, caseId, party, selected, token)).Cleaned.ShouldBeTrue();
        remote.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DirectCleanupRefusesUnacknowledgedTypedReservationWithStoredCanonicalUnit(bool selectReceipt)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        const string tenant = "tenant";
        const string party = "party";
        const string caseId = "case";
        const string unitId = "shared";
        const string historicalWorkflow = "historical";
        var state = new PartyMemoryTestReadModelStore();
        var remote = new PartyMemoryRecoveryHandler();
        remote.Units.Add(unitId);
        using var http = PartyMemoryRecoveryTests.Http(remote);
        string? producer = PartyMemoryCleanupService.BuildProducerAuthority(http.BaseAddress);
        string source = PartyMemoryUrn.Build(tenant, party);
        var historical = new PartyMemoryIngestionIntent("historical-attempt", caseId, source, historicalWorkflow, unitId, ProducerAuthority: producer);
        var reservation = new PartyMemoryIngestionIntent("older-typed-attempt", caseId, source, MemoryUnitId: unitId, ProducerAuthority: producer,
            RefreshProtocolVersion: SourceRefreshProtocol.Version, SourceRevisionNumber: 29, RefreshIdentityDigest: new string('a', 64));
        var intents = PartyMemoryRecoveryTests.Intents(state);
        await intents.RegisterAsync(tenant, party, historical, token);
        await intents.RegisterAsync(tenant, party, reservation, token);
        await intents.BeginCleanupAsync(tenant, party, token);
        await intents.RemoveAsync(tenant, party, [historical], token, producer);

        (await PartyMemoryRecoveryTests.Cleanup(state, http)
            .DeleteMemoryUnitAsync(tenant, caseId, party, selectReceipt ? historicalWorkflow : unitId, token)).Cleaned.ShouldBeFalse();

        remote.Requests.ShouldNotContain(item => item.Method == HttpMethod.Delete);
        remote.Units.ShouldBe([unitId]);
        (await intents.GetAsync(tenant, party, token)).ShouldHaveSingleItem().ShouldBe(reservation);
        (await intents.BeginCleanupAsync(tenant, party, token)).Single(item => item.CleanupSettled)
            .ShouldBe(historical with { CleanupSettled = true, AbsenceAuthority = producer });
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task DirectUnitCleanupReusesExactAbsenceWhenSiblingPublishesToADifferentUnit(bool differentHasReceipt, bool mismatchedOutput)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        const string tenant = "tenant";
        const string party = "party";
        const string caseId = "case";
        var state = new PartyMemoryTestReadModelStore();
        var remote = new PartyMemoryRecoveryHandler();
        remote.Units.UnionWith(["historical-unit", "different-unit"]);
        remote.Workflows["historical"] = PartyMemoryRecoveryHandler.Completed("historical", "historical-unit");
        using var http = PartyMemoryRecoveryTests.Http(remote);
        string? producer = PartyMemoryCleanupService.BuildProducerAuthority(http.BaseAddress);
        (await PartyMemoryRecoveryTests.Cleanup(state, http).DeleteMemoryUnitAsync(tenant, caseId, party, "historical", token)).Cleaned.ShouldBeTrue();

        if (differentHasReceipt)
        {
            remote.Workflows["different-history"] = PartyMemoryRecoveryHandler.Completed("different-history", "different-unit");
            (await PartyMemoryRecoveryTests.Cleanup(state, http).DeleteMemoryUnitAsync(tenant, caseId, party, "different-history", token)).Cleaned.ShouldBeTrue();
            remote.Units.Add("different-unit");
        }

        string source = PartyMemoryUrn.Build(tenant, party);
        string? knownSiblingUnit = mismatchedOutput ? "historical-unit" : null;
        await PartyMemoryRecoveryTests.Intents(state).RetainDiscoveredAsync(tenant, party, caseId, source, "sibling",
            knownSiblingUnit, retryIdentityHash: null, terminalVerified: false, token, producer);
        remote.Workflows["sibling"] = PartyMemoryRecoveryHandler.Completed("sibling", "different-unit");
        remote.Workflows["alias"] = PartyMemoryRecoveryHandler.Completed("alias", "historical-unit");
        remote.Requests.Clear();
        remote.DeleteStatus = (int)HttpStatusCode.ServiceUnavailable;

        (await PartyMemoryRecoveryTests.Cleanup(state, http).DeleteMemoryUnitAsync(tenant, caseId, party, "alias", token)).Cleaned.ShouldBe(!mismatchedOutput);

        remote.Requests.ShouldAllBe(item => item.Method == HttpMethod.Get && item.Path.Contains("/ingest/", StringComparison.Ordinal));
        remote.Units.ShouldBe(["different-unit"]);
        var retained = await PartyMemoryRecoveryTests.Intents(state).BeginCleanupAsync(tenant, party, token);
        var different = retained.Single(item => item.WorkflowInstanceId == "sibling");
        different.MemoryUnitId.ShouldBe(differentHasReceipt ? knownSiblingUnit : "different-unit");
        different.RequiresTerminalEvidence.ShouldBe(differentHasReceipt);
        different.CleanupSettled.ShouldBeFalse();
        different.AbsenceAuthority.ShouldBeNull();
        retained.Single(item => item.WorkflowInstanceId == "alias").AbsenceAuthority.ShouldBe(mismatchedOutput ? null : producer);

        if (differentHasReceipt && !mismatchedOutput)
        {
            remote.Requests.Clear();
            (await PartyMemoryRecoveryTests.Cleanup(state, http).DeleteMemoryUnitAsync(tenant, caseId, party, "sibling", token)).Cleaned.ShouldBeFalse();
            remote.Requests.Where(item => item.Method == HttpMethod.Delete).ShouldHaveSingleItem().Path.ShouldEndWith("/different-unit");
            remote.Units.ShouldBe(["different-unit"]);
            (await PartyMemoryRecoveryTests.Intents(state).GetAsync(tenant, party, token)).ShouldHaveSingleItem().ShouldBe(different);
        }
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

    internal static HttpResponseMessage Page(object[] entries, string? next = null, string tenant = "tenant", bool complete = true) => new(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(new
        {
            tenantId = tenant, sourceUri = PartyMemoryUrn.Build("tenant", "party"), version = "controlled-snapshot",
            coverageComplete = complete, admissionClosed = true, permanent = true, entries, nextCursor = next,
        }),
    };
}
