using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

using Dapr.Client;

using Hexalith.EventStore.Client.Projections;
using Hexalith.Memories.Client.Rest;
using Hexalith.Memories.Contracts.V1;
using Hexalith.Parties.Contracts.Authorization;
using Hexalith.Parties.Search;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

using Shouldly;

namespace Hexalith.Parties.Tests.Search;

/// <summary>Opt-in qualification against the selected local Memories workflow and real SDK state store.</summary>
public sealed class PartyMemoryRuntimeTests
{
    /// <summary>Observes durable canonical identities and terminal remote absence across fresh clients.</summary>
    [Fact(Timeout = 360000)]
    public async Task SelectedMemoriesIngestionAndCleanupPersistAcrossRestart()
    {
        string? path = Environment.GetEnvironmentVariable("PARTIES_MEMORIES_RUNTIME_CONFIG");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(path), "Set PARTIES_MEMORIES_RUNTIME_CONFIG to an owned local runtime's private JSON configuration.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(5));
        CancellationToken token = deadline.Token;
        PartyMemoryRuntimeConfiguration config = JsonSerializer.Deserialize<PartyMemoryRuntimeConfiguration>(
            await File.ReadAllTextAsync(path!, token), new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("Runtime configuration is empty.");
        config.OwnedSourceInventoryQualificationEnabled.ShouldBeTrue("Enable the explicit non-Production owned source-inventory protocol and both controlled delays.");
        PartyMemoryIndexingService.SupportsExplicitSourceRefresh.ShouldBeTrue("The actual loaded Parties assembly must support revision-aware source refresh.");
        typeof(PartyMemoryIndexingService).Assembly.GetCustomAttributes<System.Reflection.AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == "HexalithMemoriesSourceRefresh").Value.ShouldBe("true");
        new Uri(config.Endpoint).IsLoopback.ShouldBeTrue("This qualification owns only the explicitly selected local runtime.");
        config.FakeEmbeddingEnabled.ShouldBeTrue("the owned runtime must explicitly enable its local fake embedding provider");
        string tenant = $"parties-recovery-{Guid.NewGuid():N}";
        string party = $"party-{Guid.NewGuid():N}";
        string bearer = Bearer(config, tenant);
        using var ingestionRequests = new PartyMemoryRuntimeIngestionObserver(new HttpClientHandler());
        using var http = new HttpClient(ingestionRequests) { BaseAddress = new Uri(config.Endpoint), Timeout = TimeSpan.FromSeconds(15) };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        if (!string.IsNullOrWhiteSpace(config.AppApiToken))
        {
            http.DefaultRequestHeaders.Add("dapr-api-token", config.AppApiToken);
        }

        var memories = new MemoriesClient(http, Options.Create(new MemoriesClientOptions()), NullLogger<MemoriesClient>.Instance);
        string provisioningWorkflow;
        string? caseId = null;
        string? survivorCaseId = null;
        string otherTenant = "survivor-" + Guid.NewGuid().ToString("N");
        string? otherCaseId = null;
        using var otherHttp = new HttpClient { BaseAddress = http.BaseAddress, Timeout = http.Timeout };
        otherHttp.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Bearer(config, otherTenant));
        if (!string.IsNullOrWhiteSpace(config.AppApiToken)) { otherHttp.DefaultRequestHeaders.Add("dapr-api-token", config.AppApiToken); }
        var otherMemories = new MemoriesClient(otherHttp, Options.Create(new MemoriesClientOptions()), NullLogger<MemoriesClient>.Instance);
        List<string> authorityUnitIds = [];
        string mappingKey = PartyMemoryUnitMappingStore.BuildKey(tenant, party);
        string intentKey = mappingKey.Replace("memory-mapping", "memory-ingestion", StringComparison.Ordinal);
        bool terminalUnitAbsenceObserved = false;
        string diagnosticPath = Path.ChangeExtension(Path.GetFullPath(path!), ".provisioning.json");
        try
        {
        // Record exact synthetic state handles before the first remote side effect.
        await File.WriteAllTextAsync(diagnosticPath, JsonSerializer.Serialize(new
        {
            Phase = "qualification-started", TenantId = tenant, PartyId = party,
            config.StateStoreName, MappingKey = mappingKey, IngestionKey = intentKey,
        }), token);
        using (HttpResponseMessage provision = await http.PostAsJsonAsync(MemoriesRoutes.TenantsPath(),
            new { tenantId = tenant, displayName = "Synthetic Parties recovery qualification", vectorDimensions = 768 }, token))
        {
            provision.EnsureSuccessStatusCode();
            JsonElement accepted = await provision.Content.ReadFromJsonAsync<JsonElement>(token);
            provisioningWorkflow = accepted.TryGetProperty("workflowInstanceId", out JsonElement instance)
                ? instance.GetString() ?? string.Empty : string.Empty;
            if (string.IsNullOrWhiteSpace(provisioningWorkflow))
            {
                throw new InvalidOperationException("Memories accepted provisioning without a workflow identity.");
            }
        }

        // Workflow handles stay beside the private configuration, outside repository receipts.
        await File.WriteAllTextAsync(diagnosticPath, JsonSerializer.Serialize(new
        {
            Phase = "provisioning-accepted", TenantId = tenant, PartyId = party, WorkflowInstanceId = provisioningWorkflow,
            config.StateStoreName, MappingKey = mappingKey, IngestionKey = intentKey,
        }), token);
        bool active = false;
        TenantStatus? observedTenantStatus = null;
        for (int attempt = 0; attempt < 120; attempt++)
        {
            observedTenantStatus = (await memories.GetTenantAsync(tenant, token))?.Status;
            if (observedTenantStatus == TenantStatus.Active)
            {
                active = true;
                break;
            }

            if (observedTenantStatus is TenantStatus.Failed or TenantStatus.CompensationFailed)
            {
                break;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), token);
        }

        if (!active)
        {
            string observation = await CaptureProvisioningDiagnosticsAsync(config, diagnosticPath, tenant, party, mappingKey, intentKey,
                provisioningWorkflow, observedTenantStatus).ConfigureAwait(true);
            active.ShouldBeTrue($"the selected Memories implementation must finish provisioning; {observation}");
        }
#pragma warning disable HXL001 // Published opt-in case provisioning API, used only by this explicit runtime qualification.
        var created = await memories.CreateCaseAsync(tenant, "Synthetic recovery qualification", null, token);
#pragma warning restore HXL001
        caseId = created.Id;
#pragma warning disable HXL001 // Explicitly owned survivor cases use the published provisioning API.
        survivorCaseId = (await memories.CreateCaseAsync(tenant, "Unrelated surviving case", null, token)).Id;
        using (HttpResponseMessage otherProvision = await otherHttp.PostAsJsonAsync(MemoriesRoutes.TenantsPath(),
            new { tenantId = otherTenant, displayName = "Unrelated surviving tenant", vectorDimensions = 768 }, token))
        { otherProvision.EnsureSuccessStatusCode(); }
        bool otherActive = false;
        for (int attempt = 0; attempt < 120; attempt++)
        {
            if ((await otherMemories.GetTenantAsync(otherTenant, token))?.Status == TenantStatus.Active) { otherActive = true; break; }
            await Task.Delay(500, token);
        }
        otherActive.ShouldBeTrue();
        otherCaseId = (await otherMemories.CreateCaseAsync(otherTenant, "Unrelated tenant case", null, token)).Id;
#pragma warning restore HXL001
        var entry = PartyMemoryRecoveryTests.Entry() with { Id = party, DisplayName = "oldamber synthetic recovery record" };
        var context = new PartyMemoryUnitMappingContext(tenant, caseId, "PartyCreated", Timestamp: DateTimeOffset.UnixEpoch, SequenceNumber: 1);
        await InitializeOwnedSourceAsync(http, tenant, party, token);
        IOptions<PartyMemoryUnitMappingStoreOptions> stateOptions = Options.Create(new PartyMemoryUnitMappingStoreOptions { StateStoreName = config.StateStoreName });
        var searchOptions = new PartyMemoryTestOptionsMonitor(new PartyMemorySearchOptions { Endpoint = http.BaseAddress, ApiToken = bearer, CaseId = caseId });
        PartyMemoryIndexingResult? indexed = null;
        using (DaprClient dapr = Dapr(config))
        {
            var sdk = new DaprReadModelStore(dapr);
            for (int attempt = 0; attempt < 120; attempt++)
            {
                var indexer = new PartyMemoryIndexingService(memories,
                    new PartyMemoryUnitMappingStore(sdk, stateOptions, NullLogger<PartyMemoryUnitMappingStore>.Instance),
                    searchOptions, NullLogger<PartyMemoryIndexingService>.Instance, new PartyMemoryIngestionIntentStore(sdk, stateOptions));
                indexed = await indexer.IndexAsync(entry, context, token);
                if (indexed?.Indexed == true)
                {
                    break;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(500), token);
            }
        }

        indexed.ShouldNotBeNull().Indexed.ShouldBeTrue("the real ingestion workflow must return validated terminal output");
        IngestionWorkflowStatus terminal = await memories.GetIngestionWorkflowStatusAsync(indexed.WorkflowInstanceId!, token);
        string? canonical = PartyMemoryIngestionEvidence.Resolve(terminal, indexed.WorkflowInstanceId!, tenant, caseId);
        authorityUnitIds.Add(canonical!);
        ingestionRequests.Observations.ShouldContain(observation => observation.RefreshExistingSource && observation.SourceRevisionNumber == 1);
        canonical.ShouldNotBeNullOrWhiteSpace();
        (await memories.GetMemoryUnitAsync(tenant, caseId, canonical!, token)).Id.ShouldBe(canonical);

        string firstCanonical = canonical!;
        await AssertDerivedStateAsync(http, tenant, party, populated: true, token);
        // A new canonical projection revision must refresh the same source URI.
        using (DaprClient refreshDapr = Dapr(config))
        {
            var refreshSdk = new DaprReadModelStore(refreshDapr);
            for (int attempt = 0; attempt < 120; attempt++)
            {
                var refresh = new PartyMemoryIndexingService(memories,
                    new PartyMemoryUnitMappingStore(refreshSdk, stateOptions, NullLogger<PartyMemoryUnitMappingStore>.Instance),
                    searchOptions, NullLogger<PartyMemoryIndexingService>.Instance, new PartyMemoryIngestionIntentStore(refreshSdk, stateOptions));
                indexed = await refresh.IndexAsync(entry with { DisplayName = "newcobalt synthetic recovery record" }, context with { SequenceNumber = 2 }, token);
                if (indexed?.Indexed == true) { break; }
                await Task.Delay(500, token);
            }
        }
        indexed.ShouldNotBeNull().Indexed.ShouldBeTrue();
        terminal = await memories.GetIngestionWorkflowStatusAsync(indexed.WorkflowInstanceId!, token);
        canonical = PartyMemoryIngestionEvidence.Resolve(terminal, indexed.WorkflowInstanceId!, tenant, caseId);
        authorityUnitIds.Add(canonical!);
        ingestionRequests.Observations.ShouldContain(observation => observation.RefreshExistingSource && observation.SourceRevisionNumber == 2);
        canonical.ShouldNotBeNullOrWhiteSpace();
        canonical.ShouldNotBe(firstCanonical);
        (await memories.GetMemoryUnitAsync(tenant, caseId, canonical!, token)).Content.ShouldContain("newcobalt");
        SearchResult currentSearch = await http.GetFromJsonAsync<SearchResult>("api/v1/search?tenantId=" + Uri.EscapeDataString(tenant)
            + "&caseId=" + Uri.EscapeDataString(caseId) + "&query=newcobalt&axis=syntactic&maxResults=1", token)
            ?? throw new InvalidOperationException("Missing current-content search response.");
        currentSearch.Results.ShouldHaveSingleItem().MemoryUnitId.ShouldBe(canonical);

        // Recreate both the Dapr client and SDK adapter: no process-local dictionary supplies this proof.
        using DaprClient restarted = Dapr(config);
        var restartedSdk = new DaprReadModelStore(restarted);
        var mappings = new PartyMemoryUnitMappingStore(restartedSdk, stateOptions, NullLogger<PartyMemoryUnitMappingStore>.Instance);
        var intents = new PartyMemoryIngestionIntentStore(restartedSdk, stateOptions);
        PartyMemoryIngestionIntent persisted = (await intents.GetAsync(tenant, party, token)).Single(item => item.WorkflowInstanceId == indexed.WorkflowInstanceId);
        persisted.MemoryUnitId.ShouldBe(canonical);
        persisted.WorkflowInstanceId.ShouldBe(indexed.WorkflowInstanceId);
        (await mappings.GetMappingsAsync(tenant, party, token)).Select(item => item.MemoryUnitId).ShouldBe([firstCanonical, canonical!], ignoreOrder: true);
        (ReadOnlyMemory<byte> before, string previousEtag) = await restarted.GetByteStateAndETagAsync(config.StateStoreName, mappingKey, cancellationToken: token);
        previousEtag.ShouldNotBeNullOrWhiteSpace();
        using (JsonDocument mappingJson = JsonDocument.Parse(before))
        {
            mappingJson.RootElement.ValueKind.ShouldBe(JsonValueKind.Array);
        }

        await QualifyCanonicalLostAcknowledgementAsync(config, http, tenant, caseId, stateOptions, searchOptions, token);
        await QualifyFailedProducerCleanupAsync(config, http, memories, tenant, caseId, stateOptions, token);
        await QualifyLostAcknowledgementAndDelayedCleanupAsync(config, http, tenant, caseId, stateOptions, searchOptions, token);
        await AssertDerivedStateAsync(http, tenant, party, populated: true, token);

        PartyMemoryCleanupResult? cleanup = null;
        for (int attempt = 0; attempt < 120; attempt++)
        {
            // Fresh service instances reconcile the live workflow/deletion state on every retry.
            cleanup = await new PartyMemoryCleanupService(http, mappings, NullLogger<PartyMemoryCleanupService>.Instance, intents)
                .DeleteByPartyAsync(tenant, caseId, party, token);
            if (cleanup.Cleaned)
            {
                break;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), token);
        }

        cleanup.ShouldNotBeNull().Cleaned.ShouldBeTrue("terminal ingestion and qualified remote unit absence are required");
        using HttpResponseMessage absence = await http.GetAsync(MemoriesRoutes.CaseMemoryUnitPath(tenant, caseId, canonical!), token);
        (await PartyMemoryCleanupResponse.IsAbsentAsync(absence, token)).ShouldBeTrue();
        await AssertDerivedStateAsync(http, tenant, party, populated: false, token);
        terminalUnitAbsenceObserved = true;
        (await memories.GetCaseAsync(tenant, survivorCaseId!, token)).Id.ShouldBe(survivorCaseId);
        (await memories.GetTenantAsync(tenant, token))!.Status.ShouldBe(TenantStatus.Active);
        (await otherMemories.GetCaseAsync(otherTenant, otherCaseId!, token)).Id.ShouldBe(otherCaseId);
        (await otherMemories.GetTenantAsync(otherTenant, token))!.Status.ShouldBe(TenantStatus.Active);
        (await intents.GetAsync(tenant, party, token)).ShouldBeEmpty();
        (await mappings.GetMappingsAsync(tenant, party, token)).ShouldBeEmpty();
        (ReadOnlyMemory<byte> empty, string finalEtag) = await restarted.GetByteStateAndETagAsync(config.StateStoreName, mappingKey, cancellationToken: token);
        finalEtag.ShouldNotBeNullOrWhiteSpace();
        finalEtag.ShouldNotBe(previousEtag);
        using JsonDocument emptyJson = JsonDocument.Parse(empty);
        emptyJson.RootElement.GetArrayLength().ShouldBe(0);
        PartyMemoryIngestionInventory? fenced = (await restartedSdk.GetAsync<PartyMemoryIngestionInventory>(config.StateStoreName, intentKey, token)).Value;
        fenced.ShouldNotBeNull().CleanupRequested.ShouldBeTrue();
        fenced.Intents.ShouldBeEmpty();
        }
        finally
        {
            await File.WriteAllTextAsync(Path.ChangeExtension(path!, ".authority.json"), JsonSerializer.Serialize(new
            { TenantId = tenant, CaseId = caseId, UnitIds = authorityUnitIds.Distinct().ToArray(), SurvivorCaseId = survivorCaseId, OtherTenantId = otherTenant, OtherCaseId = otherCaseId, SurvivorsVerified = terminalUnitAbsenceObserved, Requests = ingestionRequests.Observations }), CancellationToken.None).ConfigureAwait(true);
            // Qualification owns these synthetic resources even when provisioning or assertions fail.
            await TeardownAsync(config, http, tenant, party, caseId, mappingKey, intentKey,
                terminalUnitAbsenceObserved, diagnosticPath).ConfigureAwait(true);
            string otherMapping = PartyMemoryUnitMappingStore.BuildKey(otherTenant, "survivor");
            await TeardownAsync(config, otherHttp, otherTenant, "survivor", otherCaseId, otherMapping,
                otherMapping.Replace("memory-mapping", "memory-ingestion", StringComparison.Ordinal), true,
                Path.ChangeExtension(diagnosticPath, ".survivor.json")).ConfigureAwait(true);
        }
    }

    private static async Task QualifyCanonicalLostAcknowledgementAsync(PartyMemoryRuntimeConfiguration config, HttpClient owner,
        string tenant, string caseId, IOptions<PartyMemoryUnitMappingStoreOptions> stateOptions,
        IOptionsMonitor<PartyMemorySearchOptions> searchOptions, CancellationToken token)
    {
        string party = "lost-index-" + Guid.NewGuid().ToString("N");
        await InitializeOwnedSourceAsync(owner, tenant, party, token).ConfigureAwait(true);
        var lostHandler = new PartyMemoryLostAcknowledgementHandler(new HttpClientHandler());
        using var lost = new HttpClient(lostHandler) { BaseAddress = new Uri(config.Endpoint), Timeout = TimeSpan.FromSeconds(15) };
        lost.DefaultRequestHeaders.Authorization = owner.DefaultRequestHeaders.Authorization;
        if (!string.IsNullOrWhiteSpace(config.AppApiToken)) { lost.DefaultRequestHeaders.Add("dapr-api-token", config.AppApiToken); }
        var client = new MemoriesClient(lost, Options.Create(new MemoriesClientOptions()), NullLogger<MemoriesClient>.Instance);
        var context = new PartyMemoryUnitMappingContext(tenant, caseId, "PartyCreated", SequenceNumber: 1);
        var entry = PartyMemoryRecoveryTests.Entry() with { Id = party, DisplayName = "synthetic normal lost acknowledgement" };
        PartyMemoryIndexingResult? result = null;
        for (int attempt = 0; attempt < 120; attempt++)
        {
            using DaprClient dapr = Dapr(config);
            var sdk = new DaprReadModelStore(dapr);
            result = await new PartyMemoryIndexingService(client, new PartyMemoryUnitMappingStore(sdk, stateOptions, NullLogger<PartyMemoryUnitMappingStore>.Instance),
                searchOptions, NullLogger<PartyMemoryIndexingService>.Instance, new PartyMemoryIngestionIntentStore(sdk, stateOptions)).IndexAsync(entry, context, token).ConfigureAwait(true);
            if (result?.Indexed == true) { break; }
            await Task.Delay(250, token).ConfigureAwait(true);
        }
        result.ShouldNotBeNull().Indexed.ShouldBeTrue("Canonical redelivery must recover the exact persisted admission through source inventory.");
        lostHandler.Submissions.ShouldBe(1);
        using DaprClient restarted = Dapr(config);
        var restartedSdk = new DaprReadModelStore(restarted);
        var mappings = new PartyMemoryUnitMappingStore(restartedSdk, stateOptions, NullLogger<PartyMemoryUnitMappingStore>.Instance);
        var intents = new PartyMemoryIngestionIntentStore(restartedSdk, stateOptions);
        PartyMemoryCleanupResult? cleanup = null;
        for (int attempt = 0; attempt < 120; attempt++)
        {
            cleanup = await new PartyMemoryCleanupService(owner, mappings, NullLogger<PartyMemoryCleanupService>.Instance, intents)
                .DeleteByPartyAsync(tenant, caseId, party, token).ConfigureAwait(true);
            if (cleanup.Cleaned) { break; }
            await Task.Delay(250, token).ConfigureAwait(true);
        }
        cleanup.ShouldNotBeNull().Cleaned.ShouldBeTrue();
        await AssertDerivedStateAsync(owner, tenant, party, populated: false, token).ConfigureAwait(true);
    }

    private static async Task QualifyFailedProducerCleanupAsync(PartyMemoryRuntimeConfiguration config, HttpClient owner, MemoriesClient client,
        string tenant, string caseId, IOptions<PartyMemoryUnitMappingStoreOptions> stateOptions, CancellationToken token)
    {
        string party = "failed-" + Guid.NewGuid().ToString("N");
        string source = PartyMemoryUrn.Build(tenant, party);
        await InitializeOwnedSourceAsync(owner, tenant, party, token).ConfigureAwait(true);
        string workflow = await client.IngestAsync(tenant, caseId, source, Encoding.UTF8.GetBytes("this is not a PDF document"),
            "application/pdf", "synthetic-failure-control", metadata: null, idempotencyToken: "failed-extraction-" + party, ct: token).ConfigureAwait(true);
        IngestionWorkflowStatus? status = null;
        for (int attempt = 0; attempt < 240; attempt++)
        {
            status = await client.GetIngestionWorkflowStatusAsync(workflow, token).ConfigureAwait(true);
            if (status.RuntimeStatus == "Failed") { break; }
            await Task.Delay(250, token).ConfigureAwait(true);
        }
        status.ShouldNotBeNull().RuntimeStatus.ShouldBe("Failed");
        string route = "api/v1/tenants/" + Uri.EscapeDataString(tenant) + "/source-inventory/owned-derived-state?sourceUri=" + Uri.EscapeDataString(source);
        JsonElement before = await owner.GetFromJsonAsync<JsonElement>(route, token).ConfigureAwait(true);
        before.GetProperty("failedUnits").GetInt32().ShouldBe(1);
        before.GetProperty("sourcePayloads").GetInt32().ShouldBe(1);
        before.GetProperty("retainedPayloadReferences").GetInt64().ShouldBeGreaterThan(0);
        using DaprClient restarted = Dapr(config);
        var sdk = new DaprReadModelStore(restarted);
        var mappings = new PartyMemoryUnitMappingStore(sdk, stateOptions, NullLogger<PartyMemoryUnitMappingStore>.Instance);
        var intents = new PartyMemoryIngestionIntentStore(sdk, stateOptions);
        PartyMemoryCleanupResult? cleanup = null;
        for (int attempt = 0; attempt < 120; attempt++)
        {
            cleanup = await new PartyMemoryCleanupService(owner, mappings, NullLogger<PartyMemoryCleanupService>.Instance, intents)
                .DeleteByPartyAsync(tenant, caseId, party, token).ConfigureAwait(true);
            if (cleanup.Cleaned) { break; }
            await Task.Delay(250, token).ConfigureAwait(true);
        }
        cleanup.ShouldNotBeNull().Cleaned.ShouldBeTrue();
        JsonElement after = await owner.GetFromJsonAsync<JsonElement>(route, token).ConfigureAwait(true);
        after.GetProperty("failedUnits").GetInt32().ShouldBe(0);
        after.GetProperty("sourcePayloads").GetInt32().ShouldBe(0);
        after.GetProperty("retainedPayloadReferences").GetInt64().ShouldBe(0);
        await AssertDerivedStateAsync(owner, tenant, party, populated: false, token).ConfigureAwait(true);
    }

    private static async Task QualifyLostAcknowledgementAndDelayedCleanupAsync(PartyMemoryRuntimeConfiguration config, HttpClient owner,
        string tenant, string caseId, IOptions<PartyMemoryUnitMappingStoreOptions> stateOptions,
        IOptionsMonitor<PartyMemorySearchOptions> searchOptions, CancellationToken token)
    {
        string party = "race-" + Guid.NewGuid().ToString("N");
        string source = PartyMemoryUrn.Build(tenant, party);
        await InitializeOwnedSourceAsync(owner, tenant, party, token).ConfigureAwait(true);
        using var lost = new HttpClient(new PartyMemoryLostAcknowledgementHandler(new HttpClientHandler()))
        { BaseAddress = new Uri(config.Endpoint), Timeout = TimeSpan.FromSeconds(15) };
        lost.DefaultRequestHeaders.Authorization = owner.DefaultRequestHeaders.Authorization;
        if (!string.IsNullOrWhiteSpace(config.AppApiToken)) { lost.DefaultRequestHeaders.Add("dapr-api-token", config.AppApiToken); }
        var client = new MemoriesClient(lost, Options.Create(new MemoriesClientOptions()), NullLogger<MemoriesClient>.Instance);
        using (DaprClient first = Dapr(config))
        {
            var sdk = new DaprReadModelStore(first);
            var intents = new PartyMemoryIngestionIntentStore(sdk, stateOptions);
            var indexer = new PartyMemoryIndexingService(client,
                new PartyMemoryUnitMappingStore(sdk, stateOptions, NullLogger<PartyMemoryUnitMappingStore>.Instance),
                searchOptions, NullLogger<PartyMemoryIndexingService>.Instance, intents);
            (await indexer.IndexAsync(PartyMemoryRecoveryTests.Entry() with { Id = party, DisplayName = "synthetic delayed producer" },
                new(tenant, caseId, "PartyCreated", Timestamp: DateTimeOffset.UnixEpoch, SequenceNumber: 1), token).ConfigureAwait(true)).ShouldNotBeNull().Indexed.ShouldBeFalse();
            (await intents.GetAsync(tenant, party, token).ConfigureAwait(true)).ShouldHaveSingleItem().WorkflowInstanceId.ShouldBeNull("The actual accepted HTTP acknowledgement was deliberately lost.");
        }
        string inventoryRoute = "api/v1/tenants/" + Uri.EscapeDataString(tenant) + "/source-inventory?sourceUri=" + Uri.EscapeDataString(source);
        using DaprClient restarted = Dapr(config);
        var restartedSdk = new DaprReadModelStore(restarted);
        var mappings = new PartyMemoryUnitMappingStore(restartedSdk, stateOptions, NullLogger<PartyMemoryUnitMappingStore>.Instance);
        var intentsAfterRestart = new PartyMemoryIngestionIntentStore(restartedSdk, stateOptions);
        var cleanup = new PartyMemoryCleanupService(owner, mappings, NullLogger<PartyMemoryCleanupService>.Instance, intentsAfterRestart);
        string? admittedWorkflow = null;
        for (int attempt = 0; attempt < 50; attempt++)
        {
            JsonElement admitted = await owner.GetFromJsonAsync<JsonElement>(inventoryRoute, token).ConfigureAwait(true);
            JsonElement[] entries = admitted.GetProperty("entries").EnumerateArray().ToArray();
            JsonElement? pending = entries.Cast<JsonElement?>().FirstOrDefault(entry => entry!.Value.GetProperty("state").GetString() == "Admitted"
                && (!entry.Value.TryGetProperty("memoryUnitId", out JsonElement output) || output.ValueKind == JsonValueKind.Null));
            if (pending is not null) { admittedWorkflow = pending.Value.GetProperty("workflowInstanceId").GetString(); break; }
            await Task.Delay(100, token).ConfigureAwait(true);
        }
        admittedWorkflow.ShouldNotBeNullOrWhiteSpace("Observe actual admitted work before any unit materializes.");
        using (HttpResponseMessage absent = await owner.GetAsync(MemoriesRoutes.CaseMemoryUnitPath(tenant, caseId, admittedWorkflow!), token).ConfigureAwait(true))
        {
            absent.StatusCode.ShouldBe(System.Net.HttpStatusCode.NotFound);
            (await absent.Content.ReadFromJsonAsync<JsonElement>(token).ConfigureAwait(true)).GetProperty("code").GetString().ShouldBe("MEMORY_UNIT_NOT_FOUND");
        }
        (await cleanup.DeleteByPartyAsync(tenant, caseId, party, token).ConfigureAwait(true)).Cleaned.ShouldBeFalse("A scoped 404 cannot retire an admitted producer that has not materialized its output.");
        (await intentsAfterRestart.GetAsync(tenant, party, token).ConfigureAwait(true)).ShouldHaveSingleItem().WorkflowInstanceId.ShouldBe(admittedWorkflow);
        (await mappings.GetMappingsAsync(tenant, party, token).ConfigureAwait(true)).ShouldBeEmpty();
        bool pausedOutputObserved = false;
        for (int attempt = 0; attempt < 120; attempt++)
        {
            JsonElement inventory = await owner.GetFromJsonAsync<JsonElement>(inventoryRoute, token).ConfigureAwait(true);
            JsonElement[] entries = inventory.GetProperty("entries").EnumerateArray().ToArray();
            pausedOutputObserved = entries.Any(entry => entry.TryGetProperty("memoryUnitId", out JsonElement unit) && unit.ValueKind == JsonValueKind.String
                && (!entry.TryGetProperty("terminalVerified", out JsonElement proof) || !proof.GetBoolean()));
            if (pausedOutputObserved) { break; }
            await Task.Delay(100, token).ConfigureAwait(true);
        }
        pausedOutputObserved.ShouldBeTrue("The real producer must pause after output persistence and before terminal workflow acknowledgement.");
        await AssertDerivedStateAsync(owner, tenant, party, populated: true, token).ConfigureAwait(true);
        (await cleanup.DeleteByPartyAsync(tenant, caseId, party, token).ConfigureAwait(true)).Cleaned.ShouldBeFalse("Output-known evidence cannot retire a still-running producer.");
        (await mappings.GetMappingsAsync(tenant, party, token).ConfigureAwait(true)).ShouldBeEmpty();
        (await intentsAfterRestart.GetAsync(tenant, party, token).ConfigureAwait(true)).ShouldHaveSingleItem().WorkflowInstanceId.ShouldNotBeNullOrWhiteSpace();
        using (HttpResponseMessage rejected = await owner.PostAsJsonAsync("api/v1/ingest", new
        {
            tenantId = tenant, caseId, sourceUri = source, contentBytes = Convert.ToBase64String(Encoding.UTF8.GetBytes("must remain rejected")),
            contentType = "text/plain", sourceType = "file", ingestedBy = "synthetic-negative-control", idempotencyToken = Guid.NewGuid().ToString("N"),
        }, token).ConfigureAwait(true))
        {
            rejected.StatusCode.ShouldBe(System.Net.HttpStatusCode.Conflict);
        }
        PartyMemoryCleanupResult? final = null;
        bool delayedDeletionObserved = false;
        for (int attempt = 0; attempt < 120; attempt++)
        {
            final = await new PartyMemoryCleanupService(owner, mappings, NullLogger<PartyMemoryCleanupService>.Instance, intentsAfterRestart)
                .DeleteByPartyAsync(tenant, caseId, party, token).ConfigureAwait(true);
            if (final.Cleaned) { break; }
            delayedDeletionObserved |= (await intentsAfterRestart.GetAsync(tenant, party, token).ConfigureAwait(true)).Any(intent => intent.MemoryUnitId is not null && !intent.RequiresTerminalEvidence);
            await Task.Delay(250, token).ConfigureAwait(true);
        }
        delayedDeletionObserved.ShouldBeTrue("DELETE acceptance must remain incomplete while the actual derived-store deletion is delayed.");
        final.ShouldNotBeNull().Cleaned.ShouldBeTrue();
        await AssertDerivedStateAsync(owner, tenant, party, populated: false, token).ConfigureAwait(true);
        (await intentsAfterRestart.GetAsync(tenant, party, token).ConfigureAwait(true)).ShouldBeEmpty();
    }

    private static async Task InitializeOwnedSourceAsync(HttpClient http, string tenant, string party, CancellationToken token)
    {
        string route = "api/v1/tenants/" + Uri.EscapeDataString(tenant) + "/source-inventory/qualify-owned-test-scope?sourceUri="
            + Uri.EscapeDataString(PartyMemoryUrn.Build(tenant, party));
        using HttpResponseMessage response = await http.PostAsync(route, null, token).ConfigureAwait(true);
        response.EnsureSuccessStatusCode();
    }

    private static async Task AssertDerivedStateAsync(HttpClient http, string tenant, string party, bool populated, CancellationToken token)
    {
        string route = "api/v1/tenants/" + Uri.EscapeDataString(tenant) + "/source-inventory/owned-derived-state?sourceUri="
            + Uri.EscapeDataString(PartyMemoryUrn.Build(tenant, party));
        JsonElement state = await http.GetFromJsonAsync<JsonElement>(route, token).ConfigureAwait(true);
        state.GetProperty("admissionDelayMilliseconds").GetInt32().ShouldBeGreaterThan(0);
        state.GetProperty("outputDelayMilliseconds").GetInt32().ShouldBeGreaterThan(0);
        state.GetProperty("deletionDelayMilliseconds").GetInt32().ShouldBeGreaterThan(0);
        state.GetProperty("unitCount").GetInt32().ShouldBeGreaterThan(0);
        foreach (string backend in new[] { "syntactic", "semantic", "sourceArtifacts", "graph" })
        {
            long count = state.GetProperty(backend).GetInt64();
            if (populated) { count.ShouldBeGreaterThan(0, backend + " must have positive source data before absence can qualify"); }
            else { count.ShouldBe(0, backend + " must have persisted/derived end-state absence"); }
        }
        if (!populated) { state.GetProperty("naturalLanguage").GetInt32().ShouldBe(0); }
    }

    /// <summary>Private handles survive incomplete teardown; only proven safe synthetic state is erased.</summary>
    /// <param name="terminalAbsence">Whether qualified terminal unit absence was observed.</param>
    /// <param name="remoteAbsent">Whether remote teardown safely finishes.</param>
    /// <param name="unresolvedProducer">Whether the durable inventory still has an active obligation.</param>
    [Theory]
    [InlineData(true, true, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    [InlineData(false, false, true)]
    public async Task PrivateTeardownRetainsExactKeysAndErasesOnlyAfterSafeTerminalCleanup(bool terminalAbsence, bool remoteAbsent, bool unresolvedProducer)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var state = new PartyMemoryTestReadModelStore();
        var config = new PartyMemoryRuntimeConfiguration { StateStoreName = "synthetic-store" };
        string tenant = "synthetic-" + Guid.NewGuid().ToString("N");
        string party = "synthetic-party";
        string mappingKey = PartyMemoryUnitMappingStore.BuildKey(tenant, party);
        string intentKey = mappingKey.Replace("memory-mapping", "memory-ingestion", StringComparison.Ordinal);
        var inventory = new PartyMemoryIngestionInventory(true, unresolvedProducer
            ? [new("unresolved", "case", "urn:synthetic", "pending-workflow")] : [], PermanentCleanup: true);
        (await state.TrySaveAsync(config.StateStoreName, mappingKey, new List<PartyMemoryUnitMappingEntry>(), string.Empty, token)).ShouldBeTrue();
        (await state.TrySaveAsync(config.StateStoreName, intentKey, inventory, string.Empty, token)).ShouldBeTrue();
        using var transport = new PartySearchErasureHttpHandler((request, _) => Task.FromResult(request.Method == HttpMethod.Delete
            ? new HttpResponseMessage(System.Net.HttpStatusCode.NoContent)
            : remoteAbsent
                ? new HttpResponseMessage(System.Net.HttpStatusCode.NotFound) { Content = JsonContent.Create(new { code = "TENANT_NOT_FOUND" }) }
                : new HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized)));
        using var http = PartyMemoryRecoveryTests.Http(transport);
        string diagnosticPath = Path.Combine(Path.GetTempPath(), "parties-teardown-" + Guid.NewGuid().ToString("N") + ".json");
        string receiptPath = Path.ChangeExtension(diagnosticPath, ".teardown.json");
        try
        {
            await TeardownAsync(config, http, tenant, party, "case", mappingKey, intentKey, terminalAbsence, diagnosticPath, state);
            using JsonDocument receipt = JsonDocument.Parse(await File.ReadAllTextAsync(receiptPath, token));
            receipt.RootElement.GetProperty("MappingKey").GetString().ShouldBe(mappingKey);
            receipt.RootElement.GetProperty("IngestionKey").GetString().ShouldBe(intentKey);
            receipt.RootElement.GetProperty("StateStoreName").GetString().ShouldBe(config.StateStoreName);
            receipt.RootElement.GetProperty("TenantId").GetString().ShouldBe(tenant);
            receipt.RootElement.GetProperty("PartyId").GetString().ShouldBe(party);
            receipt.RootElement.GetProperty("CaseId").GetString().ShouldBe("case");
            bool expectedRemoval = terminalAbsence && remoteAbsent && !unresolvedProducer;
            receipt.RootElement.GetProperty("StateKeysRemoved").GetBoolean().ShouldBe(expectedRemoval);
            receipt.RootElement.GetProperty("OperatorCleanupRequired").GetBoolean().ShouldBe(!expectedRemoval);
            var retainedMapping = await state.GetAsync<List<PartyMemoryUnitMappingEntry>>(config.StateStoreName, mappingKey, token);
            var retainedInventory = await state.GetAsync<PartyMemoryIngestionInventory>(config.StateStoreName, intentKey, token);
            if (expectedRemoval)
            {
                retainedMapping.Value.ShouldBeNull();
                retainedInventory.Value.ShouldBeNull();
            }
            else
            {
                retainedMapping.Value.ShouldNotBeNull().ShouldBeEmpty();
                retainedInventory.Value.ShouldNotBeNull().Intents.ShouldBe(inventory.Intents);
            }
        }
        finally
        {
            File.Delete(receiptPath);
        }
    }

    private static async Task TeardownAsync(PartyMemoryRuntimeConfiguration config, HttpClient http,
        string tenant, string party, string? caseId, string mappingKey, string intentKey,
        bool terminalUnitAbsenceObserved, string diagnosticPath, IReadModelStore? testState = null)
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        List<object> evidence = [];
        bool tenantAbsent = false;
        bool caseTeardownAccepted = caseId is null;
        bool stateKeysRemoved = false;
        try
        {
            if (caseId is not null)
            {
                using HttpResponseMessage removedCase = await http.DeleteAsync(MemoriesRoutes.CasePath(tenant, caseId), budget.Token).ConfigureAwait(false);
                string body = await removedCase.Content.ReadAsStringAsync(budget.Token).ConfigureAwait(false);
                caseTeardownAccepted = removedCase.IsSuccessStatusCode;
                if (removedCase.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    ErrorResponse? error = JsonSerializer.Deserialize<ErrorResponse>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                    caseTeardownAccepted = error?.Code is "CASE_NOT_FOUND" or "TENANT_NOT_FOUND";
                }

                evidence.Add(new { Phase = "case-delete", HttpStatus = (int)removedCase.StatusCode, PrivateResponse = body });
            }

            using HttpResponseMessage removedTenant = await http.DeleteAsync(MemoriesRoutes.TenantPath(tenant), budget.Token).ConfigureAwait(false);
            evidence.Add(new { Phase = "tenant-delete", HttpStatus = (int)removedTenant.StatusCode,
                PrivateResponse = await removedTenant.Content.ReadAsStringAsync(budget.Token).ConfigureAwait(false) });
            for (int attempt = 0; attempt < 8; attempt++)
            {
                using HttpResponseMessage observed = await http.GetAsync(MemoriesRoutes.TenantPath(tenant), budget.Token).ConfigureAwait(false);
                if (observed.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    ErrorResponse? error = await observed.Content.ReadFromJsonAsync<ErrorResponse>(budget.Token).ConfigureAwait(false);
                    tenantAbsent = string.Equals(error?.Code, "TENANT_NOT_FOUND", StringComparison.Ordinal);
                    if (tenantAbsent)
                    {
                        break;
                    }
                }

                if (observed.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
                {
                    break;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(500), budget.Token).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            evidence.Add(new { Phase = "teardown-incomplete", ExceptionType = ex.GetType().Name });
        }

        bool remoteTeardownSafe = tenantAbsent && caseTeardownAccepted;
        if (terminalUnitAbsenceObserved && remoteTeardownSafe)
        {
            try
            {
                using var stateBudget = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                using DaprClient? dapr = testState is null ? Dapr(config) : null;
                IReadModelStore state = testState ?? new DaprReadModelStore(dapr!);
                PartyMemoryIngestionInventory? inventory = (await state.GetAsync<PartyMemoryIngestionInventory>(
                    config.StateStoreName, intentKey, stateBudget.Token).ConfigureAwait(false)).Value;
                bool noActiveProducers = inventory is null || (inventory.CleanupRequested && inventory.PermanentCleanup
                    && inventory.Intents is { Count: 0 });
                if (noActiveProducers && state is IReadModelConditionalEraser eraser)
                {
                    // Fresh conditional snapshots prevent deleting new or unresolved obligations.
                    bool mappingsRemoved = await EraseSyntheticKeyAsync<List<PartyMemoryUnitMappingEntry>>(state, eraser,
                        config.StateStoreName, mappingKey, value => value is null || value.Count == 0, stateBudget.Token).ConfigureAwait(false);
                    if (mappingsRemoved)
                    {
                        stateKeysRemoved = await EraseSyntheticKeyAsync<PartyMemoryIngestionInventory>(state, eraser,
                            config.StateStoreName, intentKey, value => value is null || (value.CleanupRequested && value.PermanentCleanup
                                && value.Intents is { Count: 0 }), stateBudget.Token).ConfigureAwait(false);
                    }
                }

                evidence.Add(new { Phase = "synthetic-state-cleanup", StateKeysRemoved = stateKeysRemoved });
            }
            catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
            {
                evidence.Add(new { Phase = "synthetic-state-cleanup-incomplete", ExceptionType = ex.GetType().Name });
            }
        }

        // Preserve exact handles privately on every outcome; teardown must not replace the primary failure.
        bool operatorCleanupRequired = !remoteTeardownSafe || !stateKeysRemoved;
        try
        {
            using var receiptBudget = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await File.WriteAllTextAsync(Path.ChangeExtension(diagnosticPath, ".teardown.json"), JsonSerializer.Serialize(new
            {
                TenantId = tenant, PartyId = party, CaseId = caseId, config.StateStoreName,
                MappingKey = mappingKey, IngestionKey = intentKey, TenantAbsenceObserved = tenantAbsent,
                TerminalUnitAbsenceObserved = terminalUnitAbsenceObserved, RemoteTeardownSafe = remoteTeardownSafe,
                StateKeysRemoved = stateKeysRemoved, OperatorCleanupRequired = operatorCleanupRequired, Observations = evidence,
            }), receiptBudget.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            operatorCleanupRequired = true;
        }

        if (operatorCleanupRequired)
        {
            TestContext.Current.TestOutputHelper?.WriteLine("Synthetic runtime cleanup is incomplete; operator cleanup is required using the private provisioning/teardown handles.");
        }
    }

    private static async Task<bool> EraseSyntheticKeyAsync<TValue>(IReadModelStore state, IReadModelConditionalEraser eraser,
        string storeName, string key, Func<TValue?, bool> safeToErase, CancellationToken cancellationToken) where TValue : class
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            ReadModelEntry<TValue> current = await state.GetAsync<TValue>(storeName, key, cancellationToken).ConfigureAwait(false);
            if (!safeToErase(current.Value))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(current.ETag))
            {
                return current.Value is null;
            }

            if (await eraser.TryEraseAsync(storeName, key, current.ETag, cancellationToken).ConfigureAwait(false))
            {
                return true;
            }
        }

        return false;
    }

    private static DaprClient Dapr(PartyMemoryRuntimeConfiguration config)
        => new DaprClientBuilder().UseHttpEndpoint(config.DaprHttpEndpoint).UseGrpcEndpoint(config.DaprGrpcEndpoint)
            .UseDaprApiToken(config.DaprApiToken).Build();

    private static async Task<string> CaptureProvisioningDiagnosticsAsync(PartyMemoryRuntimeConfiguration config,
        string diagnosticPath, string tenant, string party, string mappingKey, string intentKey, string workflow, TenantStatus? tenantStatus)
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            using var statusClient = new HttpClient { BaseAddress = new Uri(config.DaprHttpEndpoint) };
            statusClient.DefaultRequestHeaders.Add("dapr-api-token", config.DaprApiToken);
            using HttpResponseMessage status = await statusClient.GetAsync(
                $"v1.0/workflows/dapr/{Uri.EscapeDataString(workflow)}", budget.Token).ConfigureAwait(false);
            string body = await status.Content.ReadAsStringAsync(budget.Token).ConfigureAwait(false);
            string runtimeStatus = "unavailable";
            if (status.IsSuccessStatusCode)
            {
                using JsonDocument document = JsonDocument.Parse(body);
                if (document.RootElement.TryGetProperty("runtimeStatus", out JsonElement observed)
                    && observed.ValueKind == JsonValueKind.String)
                {
                    runtimeStatus = observed.GetString()?.ToUpperInvariant() switch
                    {
                        "PENDING" => "pending",
                        "RUNNING" => "running",
                        "COMPLETED" => "completed",
                        "FAILED" => "failed",
                        "TERMINATED" => "terminated",
                        "SUSPENDED" => "suspended",
                        _ => "unrecognized",
                    };
                }
            }

            await File.WriteAllTextAsync(diagnosticPath, JsonSerializer.Serialize(new
            {
                Phase = "provisioning-not-active", TenantId = tenant, PartyId = party, WorkflowInstanceId = workflow,
                config.StateStoreName, MappingKey = mappingKey, IngestionKey = intentKey,
                TenantStatus = tenantStatus?.ToString() ?? "missing", HttpStatus = (int)status.StatusCode,
                RuntimeStatus = runtimeStatus, PrivateWorkflowResponse = body,
            }), budget.Token).ConfigureAwait(false);
            return $"tenant status={tenantStatus?.ToString() ?? "missing"}; workflow={runtimeStatus}; "
                + $"workflow HTTP={(int)status.StatusCode}; details retained beside the private runtime configuration";
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            return $"tenant status={tenantStatus?.ToString() ?? "missing"}; bounded workflow diagnostics failed ({ex.GetType().Name})";
        }
    }

    private static string Bearer(PartyMemoryRuntimeConfiguration config, string tenant)
    {
        if (string.IsNullOrWhiteSpace(config.JwtSigningKey))
        {
            throw new InvalidOperationException("Private runtime JWT signing configuration is required.");
        }

        var signing = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config.JwtSigningKey)), SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(config.JwtIssuer, config.JwtAudience,
            [new Claim(PartiesClaimTypes.Subject, "synthetic-recovery-qualification"), new Claim(config.TenantClaimName, tenant), new Claim("owned_source_inventory_test", "true")],
            DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(10), signing);
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }
}
