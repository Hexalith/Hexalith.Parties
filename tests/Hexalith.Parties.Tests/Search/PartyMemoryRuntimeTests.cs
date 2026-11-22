using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

using Dapr.Client;

using Hexalith.EventStore.Client.Projections;
using Hexalith.Memories.Client.Rest;
using Hexalith.Memories.Contracts.V1;
using Hexalith.Parties.Contracts.Authorization;
using Hexalith.Parties.Search;
using Hexalith.Parties.Testing;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

using Shouldly;

namespace Hexalith.Parties.Tests.Search;

/// <summary>Opt-in qualification against the selected local Memories workflow and real SDK state store.</summary>
public sealed class PartyMemoryRuntimeTests
{
    private bool _includeProducerControls = true;
    private bool _producerRestarts;
    /// <summary>Observes durable canonical identities and terminal remote absence across fresh clients.</summary>
    [Fact(Timeout = 420000)]
    public async Task SelectedMemoriesIngestionAndCleanupPersistAcrossRestart()
    {
        string? path = Environment.GetEnvironmentVariable("PARTIES_MEMORIES_RUNTIME_CONFIG");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(path), "Set PARTIES_MEMORIES_RUNTIME_CONFIG to an owned local runtime's private JSON configuration.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        // The full producer matrix ingests and erases two additional remote-only sources.
        deadline.CancelAfter(TimeSpan.FromMinutes(_producerRestarts ? 24 : _includeProducerControls ? 6 : 5));
        CancellationToken token = deadline.Token;
        PartyMemoryRuntimeConfiguration config = JsonSerializer.Deserialize<PartyMemoryRuntimeConfiguration>(
            await File.ReadAllTextAsync(path!, token), new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("Runtime configuration is empty.");
        string? provenance = Environment.GetEnvironmentVariable("PARTIES_MEMORIES_RUNTIME_PROVENANCE");
        if (!string.IsNullOrWhiteSpace(provenance))
        {
            Dictionary<string, string> files = JsonSerializer.Deserialize<Dictionary<string, string>>(await File.ReadAllTextAsync(provenance, token))!;
            files.ContainsKey(typeof(PartyMemoryRuntimeTests).Assembly.Location).ShouldBeTrue("The provenance must bind this executed child.");
            foreach ((string file, string expected) in files)
            {
                Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(await File.ReadAllBytesAsync(file, token))).ToLowerInvariant().ShouldBe(expected);
            }
        }
        config.OwnedSourceInventoryQualificationEnabled.ShouldBeTrue("Enable the explicit non-Production owned source-inventory protocol and both controlled delays.");
        await PartyMemoryUnitAuthorityRuntime.ValidateAsync(config, token);
        if (_producerRestarts)
        {
            config.ProducerRestartDirectory.ShouldNotBeNullOrWhiteSpace();
            config.ProducerRestartSecret.ShouldNotBeNullOrWhiteSpace();
            config.OwnershipGeneration.ShouldNotBeNullOrWhiteSpace();
        }
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
        string otherTenant = "parties-recovery-survivor-" + Guid.NewGuid().ToString("N");
        string? otherCaseId = null;
        using var otherHttp = new HttpClient { BaseAddress = http.BaseAddress, Timeout = http.Timeout };
        otherHttp.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Bearer(config, otherTenant));
        if (!string.IsNullOrWhiteSpace(config.AppApiToken)) { otherHttp.DefaultRequestHeaders.Add("dapr-api-token", config.AppApiToken); }
        var otherMemories = new MemoriesClient(otherHttp, Options.Create(new MemoriesClientOptions()), NullLogger<MemoriesClient>.Instance);
        List<string> authorityUnitIds = [];
        var canonicalOutputs = new List<object>();
        var unitAuthorities = new List<object>();
        var issuedAuthorities = new Dictionary<string, string?>();
        var producerWorkflows = new Dictionary<string, string>();
        string mappingKey = PartyMemoryUnitMappingStore.BuildKey(tenant, party);
        string intentKey = mappingKey.Replace("memory-mapping", "memory-ingestion", StringComparison.Ordinal);
        bool terminalUnitAbsenceObserved = false;
        bool survivorsVerified = false;
        bool qualificationCompleted = false;
        (string Tenant, string Case, string PartyA, string UnitA, string PartyB, string UnitB)? ownerComponent = null;
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
        string survivorParty = "same-tenant-survivor-" + Guid.NewGuid().ToString("N");
        string otherParty = "other-tenant-survivor-" + Guid.NewGuid().ToString("N");
        string survivorUnit = await PopulateSurvivorAsync(http, memories, tenant, survivorCaseId, survivorParty, "survivorgreen", token);
        string otherUnit = await PopulateSurvivorAsync(otherHttp, otherMemories, otherTenant, otherCaseId, otherParty, "survivorviolet", token);
        byte[] survivorUnitBefore = await http.GetByteArrayAsync(MemoriesRoutes.CaseMemoryUnitPath(tenant, survivorCaseId, survivorUnit), token);
        byte[] otherUnitBefore = await otherHttp.GetByteArrayAsync(MemoriesRoutes.CaseMemoryUnitPath(otherTenant, otherCaseId, otherUnit), token);
        byte[] survivorDerivedBefore = await ReadDerivedStateAsync(http, tenant, survivorParty, token);
        byte[] otherDerivedBefore = await ReadDerivedStateAsync(otherHttp, otherTenant, otherParty, token);
        byte[] survivorCaseBefore = await http.GetByteArrayAsync(MemoriesRoutes.CasePath(tenant, survivorCaseId), token);
        byte[] otherCaseBefore = await otherHttp.GetByteArrayAsync(MemoriesRoutes.CasePath(otherTenant, otherCaseId), token);
        var entry = PartyMemoryRecoveryFixture.Entry() with { Id = party, DisplayName = "oldamber synthetic recovery record" };
        var context = new PartyMemoryUnitMappingContext(tenant, caseId, "PartyCreated", Timestamp: DateTimeOffset.UnixEpoch, SequenceNumber: 1);
        await InitializeOwnedSourceAsync(http, tenant, party, token);
        var preparedSources = new Dictionary<string, string>(StringComparer.Ordinal);
        if (_producerRestarts)
        {
            // The owner endpoint requires a tenant born after this server startup.
            // Establish every exact synthetic scope before replacing that startup;
            // later phases consume persisted coverage rather than reattesting it.
            foreach (string control in new[] { "lost-index", "failed", "race", "bounded-legacy" }
                .Concat(PartyMemoryUnitAuthorityRuntime.Faults.Select(fault => "authority-cleanup:" + fault)))
            {
                string prepared = "prepared-" + Guid.NewGuid().ToString("N");
                await InitializeOwnedSourceAsync(http, tenant, prepared, token);
                preparedSources.Add(control, prepared);
            }
            ownerComponent = await PrepareOwnerComponentAsync(config, token);
        }
        IOptions<PartyMemoryUnitMappingStoreOptions> stateOptions = Options.Create(new PartyMemoryUnitMappingStoreOptions { StateStoreName = config.StateStoreName });
        var searchOptions = new PartyMemoryTestOptionsMonitor(new PartyMemorySearchOptions { Endpoint = http.BaseAddress, ApiToken = bearer, CaseId = caseId });
        if (_includeProducerControls)
        {
            await QualifyRemoteOnlyInventoryAsync(config, http, memories, tenant, caseId, stateOptions, coverageComplete: false, unitAuthorities, token);
            await QualifyRemoteOnlyInventoryAsync(config, http, memories, tenant, caseId, stateOptions, coverageComplete: true, unitAuthorities, token);
        }
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
        canonicalOutputs.Add(new { Revision = 1, MemoryUnitId = canonical });
        ingestionRequests.Observations.ShouldContain(observation => observation.RefreshExistingSource && observation.SourceRevisionNumber == 1);
        canonical.ShouldNotBeNullOrWhiteSpace();
        (await memories.GetMemoryUnitAsync(tenant, caseId, canonical!, token)).Id.ShouldBe(canonical);
        (await memories.GetMemoryUnitAsync(tenant, caseId, canonical!, token)).Content.ShouldContain("oldamber");

        string firstCanonical = canonical!;
        string firstSource = indexed.SourceUri;
        firstSource.ShouldNotBeNullOrWhiteSpace();
        await AssertDerivedStateAsync(http, tenant, party, populated: true, token);
        producerWorkflows[canonical!] = indexed.WorkflowInstanceId!;
        issuedAuthorities[canonical!] = await PartyMemoryUnitAuthorityRuntime.InspectAsync(config, http, tenant, caseId,
            firstSource, indexed.WorkflowInstanceId!, canonical!, deleted: false, unitAuthorities, token);
        await RestartProducerAsync(config, "cross-source-authority", tenant, firstSource, indexed.WorkflowInstanceId!, canonical!, token);
        if (_producerRestarts)
        {
            var component = ownerComponent!.Value;
            await PartyMemoryOwnerSourceRuntime.CrossSourceAsync(config, component.Tenant, component.Case,
                PartyMemoryUrn.Build(component.Tenant, component.PartyA), component.UnitA,
                PartyMemoryUrn.Build(component.Tenant, component.PartyB), component.UnitB, unitAuthorities, token);
        }
        await PartyMemoryUnitAuthorityRuntime.RefuseUnqualifiedRefreshAsync(config, http, tenant, caseId, firstSource, canonical!, async () =>
        {
            using DaprClient negativeClient = Dapr(config);
            var negativeSdk = new DaprReadModelStore(negativeClient);
            (await new PartyMemoryUnitMappingStore(negativeSdk, stateOptions, NullLogger<PartyMemoryUnitMappingStore>.Instance)
                .GetMappingsAsync(tenant, party, token).ConfigureAwait(true)).ShouldHaveSingleItem().MemoryUnitId.ShouldBe(firstCanonical);
            (await new PartyMemoryIngestionIntentStore(negativeSdk, stateOptions).GetAsync(tenant, party, token).ConfigureAwait(true))
                .ShouldHaveSingleItem().MemoryUnitId.ShouldBe(firstCanonical);
        }, unitAuthorities, token);
        await RestartProducerAsync(config, "same-uri-refresh", tenant, firstSource, indexed.WorkflowInstanceId!, canonical!, token);
        (await PartyMemoryUnitAuthorityRuntime.InspectAsync(config, http, tenant, caseId, firstSource,
            indexed.WorkflowInstanceId!, canonical!, deleted: false, unitAuthorities, token)).ShouldBe(issuedAuthorities[canonical!]);
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
        canonicalOutputs.Add(new { Revision = 2, MemoryUnitId = canonical });
        ingestionRequests.Observations.ShouldContain(observation => observation.RefreshExistingSource && observation.SourceRevisionNumber == 2);
        canonical.ShouldNotBeNullOrWhiteSpace();
        canonical.ShouldNotBe(firstCanonical);
        producerWorkflows[canonical!] = indexed.WorkflowInstanceId!;
        issuedAuthorities[canonical!] = await PartyMemoryUnitAuthorityRuntime.InspectAsync(config, http, tenant, caseId,
            firstSource, indexed.WorkflowInstanceId!, canonical!, deleted: false, unitAuthorities, token);
        indexed.SourceUri.ShouldBe(firstSource);
        ingestionRequests.Observations.Where(observation => observation.SourceRevisionNumber is 1 or 2)
            .Select(observation => observation.SourceUri).Distinct().ShouldBe([firstSource]);
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
        (await PartyMemoryUnitAuthorityRuntime.InspectAsync(config, http, tenant, caseId, firstSource,
            indexed.WorkflowInstanceId!, canonical!, deleted: false, unitAuthorities, token)).ShouldBe(issuedAuthorities[canonical!]);
        (ReadOnlyMemory<byte> before, string previousEtag) = await restarted.GetByteStateAndETagAsync(config.StateStoreName, mappingKey, cancellationToken: token);
        previousEtag.ShouldNotBeNullOrWhiteSpace();
        using (JsonDocument mappingJson = JsonDocument.Parse(before))
        {
            mappingJson.RootElement.ValueKind.ShouldBe(JsonValueKind.Array);
        }

        if (_includeProducerControls)
        {
            await QualifyCanonicalLostAcknowledgementAsync(config, http, tenant, caseId, stateOptions, searchOptions, preparedSources.GetValueOrDefault("lost-index"), token);
            await QualifyFailedProducerCleanupAsync(config, http, memories, tenant, caseId, stateOptions, preparedSources.GetValueOrDefault("failed"), token);
            await QualifyLostAcknowledgementAndDelayedCleanupAsync(config, http, tenant, caseId, stateOptions, searchOptions, preparedSources.GetValueOrDefault("race"), unitAuthorities, token);
        }
        await AssertDerivedStateAsync(http, tenant, party, populated: true, token);

        if (config.OwnershipGeneration is not null)
        {
            if (_producerRestarts)
            {
                string legacyParty = preparedSources["bounded-legacy"];
                string legacyUnit = await PopulateSurvivorAsync(http, memories, tenant, caseId, legacyParty, "boundedlegacy", token, alreadyQualified: true);
                await mappings.RecordMappingAsync(tenant, legacyParty, legacyUnit, PartyMemoryUrn.Build(tenant, legacyParty), caseId, token);
                await PartyMemoryUnitAuthorityRuntime.QualifyBoundedLegacyAsync(config, http, tenant, caseId,
                    PartyMemoryUrn.Build(tenant, legacyParty), legacyUnit,
                    async () => (await new PartyMemoryCleanupService(http, mappings, NullLogger<PartyMemoryCleanupService>.Instance, intents)
                        .DeleteByPartyAsync(tenant, caseId, legacyParty, token).ConfigureAwait(true)).Cleaned,
                    async () =>
                    {
                        (await mappings.GetMappingsAsync(tenant, legacyParty, token).ConfigureAwait(true))
                            .ShouldContain(item => item.MemoryUnitId == legacyUnit);
                        (await intents.GetAsync(tenant, legacyParty, token).ConfigureAwait(true))
                            .ShouldContain(item => item.MemoryUnitId == legacyUnit && !item.CleanupSettled);
                    }, unitAuthorities, token);
                (await mappings.GetMappingsAsync(tenant, legacyParty, token)).ShouldBeEmpty();
                (await intents.GetAsync(tenant, legacyParty, token)).ShouldBeEmpty();
            }
            // Dedicated sources keep these destructive cleanup faults after the
            // main refresh proof and independent of each other and its obligations.
            foreach (string control in PartyMemoryUnitAuthorityRuntime.Faults)
            {
                string negativeParty = preparedSources.GetValueOrDefault("authority-cleanup:" + control) ?? "authority-cleanup-" + Guid.NewGuid().ToString("N");
                string negativeUnit = await PopulateSurvivorAsync(http, memories, tenant, caseId, negativeParty, "retainedauthority", token, alreadyQualified: _producerRestarts);
                string negativeSource = PartyMemoryUrn.Build(tenant, negativeParty);
                await mappings.RecordMappingAsync(tenant, negativeParty, negativeUnit, negativeSource, caseId, token);
                async Task<bool> AttemptCleanupAsync() => (await new PartyMemoryCleanupService(http, mappings,
                    NullLogger<PartyMemoryCleanupService>.Instance, intents).DeleteByPartyAsync(tenant, caseId, negativeParty, token).ConfigureAwait(true)).Cleaned;
                await PartyMemoryUnitAuthorityRuntime.RefuseUnqualifiedCleanupAsync(config, http, tenant, caseId, negativeSource,
                    negativeUnit, control, AttemptCleanupAsync, async () =>
                    {
                        (await mappings.GetMappingsAsync(tenant, negativeParty, token).ConfigureAwait(true))
                            .ShouldContain(item => item.MemoryUnitId == negativeUnit);
                        (await intents.GetAsync(tenant, negativeParty, token).ConfigureAwait(true))
                            .ShouldContain(item => item.MemoryUnitId == negativeUnit && !item.CleanupSettled);
                    }, unitAuthorities, token);
                bool repaired = false;
                for (int attempt = 0; attempt < 120; attempt++)
                {
                    repaired = await AttemptCleanupAsync();
                    if (repaired) { break; }
                    await Task.Delay(250, token);
                }
                repaired.ShouldBeTrue("Restored exact authority must allow this dedicated scope to finish cleanup.");
            }
        }

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
        foreach (string output in authorityUnitIds.Distinct())
        {
            using HttpResponseMessage absence = await http.GetAsync(MemoriesRoutes.CaseMemoryUnitPath(tenant, caseId, output), token);
            (await PartyMemoryCleanupResponse.IsAbsentAsync(absence, token)).ShouldBeTrue("Every recorded canonical output requires a direct qualified absence read: " + output);
            (await PartyMemoryUnitAuthorityRuntime.InspectAsync(config, http, tenant, caseId, firstSource,
                producerWorkflows[output], output, deleted: true, unitAuthorities, token)).ShouldBe(issuedAuthorities[output]);
        }
        await AssertDerivedStateAsync(http, tenant, party, populated: false, token);
        terminalUnitAbsenceObserved = true;
        (await http.GetByteArrayAsync(MemoriesRoutes.CasePath(tenant, survivorCaseId!), token)).ShouldBe(survivorCaseBefore);
        (await otherHttp.GetByteArrayAsync(MemoriesRoutes.CasePath(otherTenant, otherCaseId!), token)).ShouldBe(otherCaseBefore);
        (await http.GetByteArrayAsync(MemoriesRoutes.CaseMemoryUnitPath(tenant, survivorCaseId!, survivorUnit), token)).ShouldBe(survivorUnitBefore);
        (await otherHttp.GetByteArrayAsync(MemoriesRoutes.CaseMemoryUnitPath(otherTenant, otherCaseId!, otherUnit), token)).ShouldBe(otherUnitBefore);
        (await ReadDerivedStateAsync(http, tenant, survivorParty, token)).ShouldBe(survivorDerivedBefore);
        (await ReadDerivedStateAsync(otherHttp, otherTenant, otherParty, token)).ShouldBe(otherDerivedBefore);
        await AssertDerivedStateAsync(http, tenant, survivorParty, populated: true, token);
        await AssertDerivedStateAsync(otherHttp, otherTenant, otherParty, populated: true, token);
        (await memories.GetCaseAsync(tenant, survivorCaseId!, token)).Id.ShouldBe(survivorCaseId);
        (await memories.GetTenantAsync(tenant, token))!.Status.ShouldBe(TenantStatus.Active);
        (await otherMemories.GetCaseAsync(otherTenant, otherCaseId!, token)).Id.ShouldBe(otherCaseId);
        (await otherMemories.GetTenantAsync(otherTenant, token))!.Status.ShouldBe(TenantStatus.Active);
        survivorsVerified = true;
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
        qualificationCompleted = true;
        }
        finally
        {
            // Qualification owns these synthetic resources even when provisioning, assertions or the authority receipt fail.
            string otherMapping = PartyMemoryUnitMappingStore.BuildKey(otherTenant, "survivor");
            await PersistAuthorityThenTeardownAsync(
                Path.ChangeExtension(path!, ".authority.json"),
                () => JsonSerializer.Serialize(new
                { TenantId = tenant, CaseId = caseId, UnitIds = authorityUnitIds.Distinct().ToArray(), CanonicalOutputs = canonicalOutputs,
                    UnitAuthorityObservations = unitAuthorities, SurvivorCaseId = survivorCaseId, OtherTenantId = otherTenant,
                    OtherCaseId = otherCaseId, SurvivorsVerified = survivorsVerified, Requests = ingestionRequests.Observations,
                    OwnerComponent = ownerComponent is { } component ? new { component.Tenant, component.Case, component.UnitA, component.UnitB,
                        Scope = "isolated direct owner-contract fixture; intentionally unknown historical obligation retained until verified owned backing disposal" } : null }),
                qualificationCompleted,
                () => TeardownAsync(config, http, tenant, party, caseId, mappingKey, intentKey,
                    terminalUnitAbsenceObserved, diagnosticPath),
                () => TeardownAsync(config, otherHttp, otherTenant, "survivor", otherCaseId, otherMapping,
                    otherMapping.Replace("memory-mapping", "memory-ingestion", StringComparison.Ordinal), true,
                    Path.ChangeExtension(diagnosticPath, ".survivor.json"))).ConfigureAwait(true);
        }
    }

    /// <summary>Runs the bounded unchanged-URI refresh and terminal erasure matrix without certifying the broader failed-ingestion gate.</summary>
    [Fact(Timeout = 360000)]
    public async Task SelectedMemoriesRefreshAndCleanupPersistAcrossClientRecreation()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        _includeProducerControls = false;
        await SelectedMemoriesIngestionAndCleanupPersistAcrossRestart();
    }

    /// <summary>Runs all five boundaries with actual owned producer replacement at each persisted interruption phase.</summary>
    [Fact(Timeout = 1500000)]
    public async Task SelectedMemoriesRecoverAcrossProducerWorkloadRestarts()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        _producerRestarts = true;
        await SelectedMemoriesIngestionAndCleanupPersistAcrossRestart();
    }

    private static Task RestartProducerAsync(PartyMemoryRuntimeConfiguration config, string phase,
        string tenant, string source, string workflow, string unit, CancellationToken token)
        => config.ProducerRestartDirectory is null ? Task.CompletedTask
            : PartyMemoryProducerRestartProtocol.RequestAsync(config.ProducerRestartDirectory, config.OwnershipSessionId!,
                config.ProducerRestartSecret!, phase, new { tenant, source, workflow, unit }, token);

    private static async Task QualifyCanonicalLostAcknowledgementAsync(PartyMemoryRuntimeConfiguration config, HttpClient owner,
        string tenant, string caseId, IOptions<PartyMemoryUnitMappingStoreOptions> stateOptions,
        IOptionsMonitor<PartyMemorySearchOptions> searchOptions, string? preparedParty, CancellationToken token)
    {
        string party = preparedParty ?? "lost-index-" + Guid.NewGuid().ToString("N");
        if (preparedParty is null) { await InitializeOwnedSourceAsync(owner, tenant, party, token).ConfigureAwait(true); }
        var lostHandler = new PartyMemoryLostAcknowledgementHandler(new HttpClientHandler());
        using var lost = new HttpClient(lostHandler) { BaseAddress = new Uri(config.Endpoint), Timeout = TimeSpan.FromSeconds(15) };
        lost.DefaultRequestHeaders.Authorization = owner.DefaultRequestHeaders.Authorization;
        if (!string.IsNullOrWhiteSpace(config.AppApiToken)) { lost.DefaultRequestHeaders.Add("dapr-api-token", config.AppApiToken); }
        var client = new MemoriesClient(lost, Options.Create(new MemoriesClientOptions()), NullLogger<MemoriesClient>.Instance);
        var context = new PartyMemoryUnitMappingContext(tenant, caseId, "PartyCreated", SequenceNumber: 1);
        var entry = PartyMemoryRecoveryFixture.Entry() with { Id = party, DisplayName = "synthetic normal lost acknowledgement" };
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
        cleanup.ShouldNotBeNull().Cleaned.ShouldBeTrue(cleanup?.BlockedReason);
        await AssertDerivedStateAsync(owner, tenant, party, populated: false, token).ConfigureAwait(true);
    }

    private static async Task QualifyFailedProducerCleanupAsync(PartyMemoryRuntimeConfiguration config, HttpClient owner, MemoriesClient client,
        string tenant, string caseId, IOptions<PartyMemoryUnitMappingStoreOptions> stateOptions, string? preparedParty, CancellationToken token)
    {
        string party = preparedParty ?? "failed-" + Guid.NewGuid().ToString("N");
        string source = PartyMemoryUrn.Build(tenant, party);
        if (preparedParty is null) { await InitializeOwnedSourceAsync(owner, tenant, party, token).ConfigureAwait(true); }
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
        if (cleanup?.Cleaned != true)
        {
            string? configuration = Environment.GetEnvironmentVariable("PARTIES_MEMORIES_RUNTIME_CONFIG");
            if (!string.IsNullOrWhiteSpace(configuration))
            {
                using HttpResponseMessage inventory = await owner.GetAsync("api/v1/tenants/" + Uri.EscapeDataString(tenant)
                    + "/source-inventory?sourceUri=" + Uri.EscapeDataString(source), token).ConfigureAwait(true);
                await File.WriteAllTextAsync(Path.ChangeExtension(configuration, ".failed-producer.json"), JsonSerializer.Serialize(new
                {
                    Cleanup = cleanup,
                    DerivedBefore = before,
                    DerivedAfter = await owner.GetFromJsonAsync<JsonElement>(route, token).ConfigureAwait(true),
                    InventoryStatus = (int)inventory.StatusCode,
                    InventoryBody = await inventory.Content.ReadAsStringAsync(token).ConfigureAwait(true),
                    Intents = await intents.GetAsync(tenant, party, token).ConfigureAwait(true),
                    Status = status,
                }), token).ConfigureAwait(true);
            }
        }
        cleanup.ShouldNotBeNull().Cleaned.ShouldBeTrue(cleanup?.BlockedReason);
        JsonElement after = await owner.GetFromJsonAsync<JsonElement>(route, token).ConfigureAwait(true);
        after.GetProperty("failedUnits").GetInt32().ShouldBe(0);
        after.GetProperty("sourcePayloads").GetInt32().ShouldBe(0);
        after.GetProperty("retainedPayloadReferences").GetInt64().ShouldBe(0);
        await AssertDerivedStateAsync(owner, tenant, party, populated: false, token).ConfigureAwait(true);
    }

    private static async Task QualifyRemoteOnlyInventoryAsync(PartyMemoryRuntimeConfiguration config, HttpClient owner, MemoriesClient client,
        string tenant, string caseId, IOptions<PartyMemoryUnitMappingStoreOptions> stateOptions, bool coverageComplete,
        List<object> observations, CancellationToken token)
    {
        string party = "inventory-only-" + Guid.NewGuid().ToString("N");
        string source = PartyMemoryUrn.Build(tenant, party);
        if (coverageComplete) { await InitializeOwnedSourceAsync(owner, tenant, party, token).ConfigureAwait(true); }
        (string workflow, string unit) = await IngestRemoteUnitAsync(owner, client, tenant, caseId, party, "remoteamber", token).ConfigureAwait(true);
        using DaprClient dapr = Dapr(config);
        var sdk = new DaprReadModelStore(dapr);
        var mappings = new PartyMemoryUnitMappingStore(sdk, stateOptions, NullLogger<PartyMemoryUnitMappingStore>.Instance);
        var intents = new PartyMemoryIngestionIntentStore(sdk, stateOptions);
        (await mappings.GetMappingsAsync(tenant, party, token).ConfigureAwait(true)).ShouldBeEmpty();
        (await intents.GetAsync(tenant, party, token).ConfigureAwait(true)).ShouldBeEmpty();
        using var firstNetwork = new HttpMessageInvoker(new HttpClientHandler());
        bool deletionInterrupted = false;
        using HttpClient first = ObserveCleanupClient(owner, firstNetwork, _ => { }, request =>
        {
            request.RequestUri.ShouldBe(new Uri(owner.BaseAddress!, MemoriesRoutes.CaseMemoryUnitPath(tenant, caseId, unit)));
            deletionInterrupted = true;
            throw new HttpRequestException("Interrupt the consumer before transmitting its first DELETE.");
        });
        var cleanup = new PartyMemoryCleanupService(first, mappings, NullLogger<PartyMemoryCleanupService>.Instance, intents);
        (await cleanup.DeleteByPartyAsync(tenant, caseId, party, token).ConfigureAwait(true)).Cleaned.ShouldBeFalse();
        deletionInterrupted.ShouldBeTrue();
        PartyMemoryIngestionIntent discovered = (await intents.GetAsync(tenant, party, token).ConfigureAwait(true)).ShouldHaveSingleItem();
        (discovered.SourceUri, discovered.CaseId, discovered.WorkflowInstanceId, discovered.MemoryUnitId)
            .ShouldBe((source, caseId, workflow, unit));
        discovered.CleanupSettled.ShouldBeFalse();

        // An individually deleted output can settle its obligation. Unknown source coverage must still block closure.
        using DaprClient restarted = Dapr(config);
        var restartedStore = new DaprReadModelStore(restarted);
        var recoveredIntents = new PartyMemoryIngestionIntentStore(restartedStore, stateOptions);
        (await recoveredIntents.GetAsync(tenant, party, token).ConfigureAwait(true)).ShouldHaveSingleItem().ShouldBe(discovered);
        JsonElement? lastInventory = null;
        using var restartedNetwork = new HttpMessageInvoker(new HttpClientHandler());
        using HttpClient consumer = ObserveCleanupClient(owner, restartedNetwork, page => lastInventory = page);
        var recovered = new PartyMemoryCleanupService(consumer,
            new PartyMemoryUnitMappingStore(restartedStore, stateOptions, NullLogger<PartyMemoryUnitMappingStore>.Instance),
            NullLogger<PartyMemoryCleanupService>.Instance, recoveredIntents);
        JsonElement inventory;
        for (int attempt = 0; ; attempt++)
        {
            attempt.ShouldBeLessThan(120, "Remote discovery and deletion must reach the expected bounded outcome.");
            lastInventory = null;
            var result = await recovered.DeleteByPartyAsync(tenant, caseId, party, token).ConfigureAwait(true);
            inventory = lastInventory.ShouldNotBeNull("Inspect the actual inventory response used by this cleanup call.");
            inventory.GetProperty("coverageComplete").GetBoolean().ShouldBe(coverageComplete);
            if (result.Cleaned)
            {
                coverageComplete.ShouldBeTrue("Unknown historical source coverage must block completion.");
                break;
            }
            if (!coverageComplete && inventory.GetProperty("entries")[0].GetProperty("state").GetString() == "Deleted"
                && (await recoveredIntents.GetAsync(tenant, party, token).ConfigureAwait(true)).Count == 0)
            {
                // Retry with only the settled unit receipt; it cannot discharge unknown historical coverage.
                lastInventory = null;
                (await recovered.DeleteByPartyAsync(tenant, caseId, party, token).ConfigureAwait(true)).Cleaned.ShouldBeFalse();
                inventory = lastInventory.ShouldNotBeNull();
                break;
            }
            await Task.Delay(250, token).ConfigureAwait(true);
        }
        await AssertDeletedOutputAsync(owner, restartedStore, config.StateStoreName, tenant, caseId, party, workflow, unit,
            inventory, coverageComplete, observations, token).ConfigureAwait(true);
    }

    private static async Task QualifyLostAcknowledgementAndDelayedCleanupAsync(PartyMemoryRuntimeConfiguration config, HttpClient owner,
        string tenant, string caseId, IOptions<PartyMemoryUnitMappingStoreOptions> stateOptions,
        IOptionsMonitor<PartyMemorySearchOptions> searchOptions, string? preparedParty, List<object> observations, CancellationToken token)
    {
        string party = preparedParty ?? "race-" + Guid.NewGuid().ToString("N");
        string source = PartyMemoryUrn.Build(tenant, party);
        if (preparedParty is null) { await InitializeOwnedSourceAsync(owner, tenant, party, token).ConfigureAwait(true); }
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
            (await indexer.IndexAsync(PartyMemoryRecoveryFixture.Entry() with { Id = party, DisplayName = "synthetic delayed producer" },
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
        (await mappings.GetMappingsAsync(tenant, party, token).ConfigureAwait(true)).ShouldBeEmpty();
        await RestartProducerAsync(config, "admitted-empty-mappings", tenant, source, admittedWorkflow!, admittedWorkflow!, token).ConfigureAwait(true);
        (await cleanup.DeleteByPartyAsync(tenant, caseId, party, token).ConfigureAwait(true)).Cleaned.ShouldBeFalse("A scoped 404 cannot retire an admitted producer that has not materialized its output.");
        (await intentsAfterRestart.GetAsync(tenant, party, token).ConfigureAwait(true)).ShouldHaveSingleItem().WorkflowInstanceId.ShouldBe(admittedWorkflow);
        (await mappings.GetMappingsAsync(tenant, party, token).ConfigureAwait(true)).ShouldBeEmpty();
        bool pausedOutputObserved = false;
        string? outputUnit = null;
        for (int attempt = 0; attempt < 120; attempt++)
        {
            JsonElement inventory = await owner.GetFromJsonAsync<JsonElement>(inventoryRoute, token).ConfigureAwait(true);
            JsonElement[] entries = inventory.GetProperty("entries").EnumerateArray().ToArray();
            pausedOutputObserved = entries.Any(entry => entry.TryGetProperty("memoryUnitId", out JsonElement unit) && unit.ValueKind == JsonValueKind.String
                && (!entry.TryGetProperty("terminalVerified", out JsonElement proof) || !proof.GetBoolean()));
            if (pausedOutputObserved)
            {
                outputUnit = entries.Single(entry => entry.GetProperty("workflowInstanceId").GetString() == admittedWorkflow).GetProperty("memoryUnitId").GetString();
                break;
            }
            await Task.Delay(100, token).ConfigureAwait(true);
        }
        pausedOutputObserved.ShouldBeTrue("The real producer must pause after output persistence and before terminal workflow acknowledgement.");
        await AssertDerivedStateAsync(owner, tenant, party, populated: true, token).ConfigureAwait(true);
        outputUnit.ShouldNotBeNullOrWhiteSpace();
        string? outputAuthority = await PartyMemoryUnitAuthorityRuntime.InspectAsync(config, owner, tenant, caseId, source,
            admittedWorkflow!, outputUnit!, deleted: false, observations, token).ConfigureAwait(true);
        await RestartProducerAsync(config, "output-known", tenant, source, admittedWorkflow!, outputUnit!, token).ConfigureAwait(true);
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
        JsonElement? cleanupInventory = null;
        using var cleanupNetwork = new HttpMessageInvoker(new HttpClientHandler());
        using HttpClient consumer = ObserveCleanupClient(owner, cleanupNetwork, page => cleanupInventory = page);
        PartyMemoryCleanupResult? final = null;
        bool delayedDeletionObserved = false;
        for (int attempt = 0; attempt < 120; attempt++)
        {
            cleanupInventory = null;
            final = await new PartyMemoryCleanupService(consumer, mappings, NullLogger<PartyMemoryCleanupService>.Instance, intentsAfterRestart)
                .DeleteByPartyAsync(tenant, caseId, party, token).ConfigureAwait(true);
            if (final.Cleaned)
            {
                await AssertDeletedOutputAsync(owner, restartedSdk, config.StateStoreName, tenant, caseId, party, admittedWorkflow!,
                    outputUnit!, cleanupInventory.ShouldNotBeNull("The first successful call must have read terminal inventory."),
                    coverageComplete: true, observations, token).ConfigureAwait(true);
                break;
            }
            bool acceptedDeletion = (await intentsAfterRestart.GetAsync(tenant, party, token).ConfigureAwait(true)).Any(intent => intent.MemoryUnitId is not null && !intent.RequiresTerminalEvidence);
            if (acceptedDeletion && !delayedDeletionObserved)
            {
                if (config.ProducerRestartDirectory is not null)
                {
                    using var deletionClient = new HttpClient { BaseAddress = new Uri(config.DaprHttpEndpoint), Timeout = TimeSpan.FromSeconds(5) };
                    deletionClient.DefaultRequestHeaders.Add("dapr-api-token", config.DaprApiToken);
                    using HttpResponseMessage observed = await deletionClient.GetAsync("v1.0/workflows/dapr/memory-unit-delete-" + Uri.EscapeDataString(outputUnit!), token).ConfigureAwait(true);
                    if (!observed.IsSuccessStatusCode) { await Task.Delay(50, token).ConfigureAwait(true); continue; }
                    JsonElement deletion = await observed.Content.ReadFromJsonAsync<JsonElement>(token).ConfigureAwait(true);
                    if (!string.Equals(deletion.GetProperty("runtimeStatus").GetString(), "Running", StringComparison.OrdinalIgnoreCase))
                    { await Task.Delay(50, token).ConfigureAwait(true); continue; }
                }
                using HttpResponseMessage readable = await owner.GetAsync(MemoriesRoutes.CaseMemoryUnitPath(tenant, caseId, outputUnit!), token).ConfigureAwait(true);
                readable.EnsureSuccessStatusCode();
                await RestartProducerAsync(config, "accepted-deletion", tenant, source, admittedWorkflow!, outputUnit!, token).ConfigureAwait(true);
                delayedDeletionObserved = true;
            }
            await Task.Delay(250, token).ConfigureAwait(true);
        }
        delayedDeletionObserved.ShouldBeTrue("DELETE acceptance must remain incomplete while the actual derived-store deletion is delayed.");
        final.ShouldNotBeNull().Cleaned.ShouldBeTrue();
        using (var deletionClient = new HttpClient { BaseAddress = new Uri(config.DaprHttpEndpoint), Timeout = TimeSpan.FromSeconds(5) })
        {
            deletionClient.DefaultRequestHeaders.Add("dapr-api-token", config.DaprApiToken);
            await WaitForDeletionTerminalAsync(deletionClient, outputUnit!, TimeSpan.FromSeconds(30), token).ConfigureAwait(true);
            observations.Add(new { Boundary = "delayed-output-cleanup", Unit = outputUnit,
                DeletionWorkflow = "memory-unit-delete-" + outputUnit, RuntimeStatus = "Completed" });
        }
        (await PartyMemoryUnitAuthorityRuntime.InspectAsync(config, owner, tenant, caseId, source,
            admittedWorkflow!, outputUnit!, deleted: true, observations, token).ConfigureAwait(true)).ShouldBe(outputAuthority);
        (await mappings.GetMappingsAsync(tenant, party, token).ConfigureAwait(true)).ShouldBeEmpty();
        (await intentsAfterRestart.GetAsync(tenant, party, token).ConfigureAwait(true)).ShouldBeEmpty();
    }

    // Mirror the producer responses the consumer actually receives. An extra inventory GET could reconcile workflow state.
    private static HttpClient ObserveCleanupClient(HttpClient owner, HttpMessageInvoker network, Action<JsonElement> observeInventory,
        Action<HttpRequestMessage>? beforeDelete = null)
    {
        var handler = new PartySearchErasureHttpHandler(async (request, token) =>
        {
            if (request.Method == HttpMethod.Delete) { beforeDelete?.Invoke(request); }
            HttpResponseMessage response = await network.SendAsync(request, token).ConfigureAwait(false);
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("/source-inventory", StringComparison.Ordinal)
                && response.IsSuccessStatusCode)
            {
                using JsonDocument inventory = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(token).ConfigureAwait(false));
                observeInventory(inventory.RootElement.Clone());
            }
            return response;
        }, simulateInventory: false);
        var client = new HttpClient(handler) { BaseAddress = owner.BaseAddress, Timeout = owner.Timeout };
        foreach (var header in owner.DefaultRequestHeaders) { client.DefaultRequestHeaders.Add(header.Key, header.Value); }
        return client;
    }

    private static async Task AssertDeletedOutputAsync(HttpClient owner, IReadModelStore sdk, string storeName, string tenant, string caseId,
        string party, string workflow, string unit, JsonElement inventory, bool coverageComplete, List<object> observations, CancellationToken token)
    {
        inventory.GetProperty("tenantId").GetString().ShouldBe(tenant);
        inventory.GetProperty("sourceUri").GetString().ShouldBe(PartyMemoryUrn.Build(tenant, party));
        inventory.GetProperty("coverageComplete").GetBoolean().ShouldBe(coverageComplete);
        inventory.GetProperty("admissionClosed").GetBoolean().ShouldBeTrue();
        inventory.GetProperty("permanent").GetBoolean().ShouldBeTrue();
        (!inventory.TryGetProperty("nextCursor", out JsonElement cursor) || cursor.ValueKind == JsonValueKind.Null).ShouldBeTrue();
        JsonElement entry = inventory.GetProperty("entries").EnumerateArray().ShouldHaveSingleItem();
        entry.GetProperty("caseId").GetString().ShouldBe(caseId);
        entry.GetProperty("workflowInstanceId").GetString().ShouldBe(workflow);
        entry.GetProperty("memoryUnitId").GetString().ShouldBe(unit);
        entry.GetProperty("state").GetString().ShouldBe("Deleted");
        entry.GetProperty("terminalVerified").GetBoolean().ShouldBeTrue();
        string intentKey = PartyMemoryUnitMappingStore.BuildKey(tenant, party).Replace("memory-mapping", "memory-ingestion", StringComparison.Ordinal);
        var durable = (await sdk.GetAsync<PartyMemoryIngestionInventory>(storeName, intentKey, token).ConfigureAwait(true)).Value.ShouldNotBeNull();
        durable.Intents.ShouldBeEmpty();
        durable.PermanentCleanup.ShouldBeTrue();
        string? authority = PartyMemoryCleanupService.BuildProducerAuthority(owner.BaseAddress);
        var receipt = durable.CleanupReceipts.ShouldNotBeNull().ShouldHaveSingleItem();
        (receipt.SourceUri, receipt.CaseId, receipt.WorkflowInstanceId, receipt.MemoryUnitId, receipt.CleanupSettled, receipt.AbsenceAuthority)
            .ShouldBe((PartyMemoryUrn.Build(tenant, party), caseId, workflow, unit, true, authority));
        if (coverageComplete)
        {
            durable.SourceClosureVersion.ShouldBe(inventory.GetProperty("version").GetString());
            durable.SourceClosureAuthority.ShouldBe(authority);
        }
        else { durable.SourceClosureVersion.ShouldBeNull(); }
        using HttpResponseMessage absent = await owner.GetAsync(MemoriesRoutes.CaseMemoryUnitPath(tenant, caseId, unit), token).ConfigureAwait(true);
        (await PartyMemoryCleanupResponse.IsAbsentAsync(absent, token).ConfigureAwait(true)).ShouldBeTrue("The exact unit must return MEMORY_UNIT_NOT_FOUND.");
        JsonElement derived = await AssertDerivedStateAsync(owner, tenant, party, populated: false, token).ConfigureAwait(true);
        observations.Add(new { Boundary = "consumer-cleanup-observation", Tenant = tenant, Case = caseId, Party = party,
            Workflow = workflow, Unit = unit, Cleaned = coverageComplete, Inventory = inventory, ScopedAbsence = true, Derived = derived,
            SettledReceipt = receipt, ClosureVersion = durable.SourceClosureVersion });
    }

    private static async Task WaitForDeletionTerminalAsync(HttpClient http, string unit, TimeSpan limit, CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(limit);
        try
        {
            while (true)
            {
                JsonElement state = await http.GetFromJsonAsync<JsonElement>("v1.0/workflows/dapr/memory-unit-delete-" + Uri.EscapeDataString(unit), deadline.Token).ConfigureAwait(true);
                string status = state.GetProperty("runtimeStatus").GetString()!;
                if (string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase)) { return; }
                if (status.ToUpperInvariant() is "FAILED" or "TERMINATED" or "CANCELED" or "CANCELLED")
                { throw new InvalidOperationException("Exact deletion workflow for " + unit + " ended with " + status + "."); }
                await Task.Delay(100, deadline.Token).ConfigureAwait(true);
            }
        }
        catch (OperationCanceledException failure) when (deadline.IsCancellationRequested && !token.IsCancellationRequested)
        { throw new TimeoutException("Exact deletion workflow for " + unit + " did not reach Completed within its deadline.", failure); }
    }

    private static async Task<string> PopulateSurvivorAsync(HttpClient http,
        MemoriesClient memories, string tenant, string caseId, string party, string content, CancellationToken token, bool alreadyQualified = false)
    {
        if (!alreadyQualified) { await InitializeOwnedSourceAsync(http, tenant, party, token).ConfigureAwait(true); }
        return (await IngestRemoteUnitAsync(http, memories, tenant, caseId, party, content, token).ConfigureAwait(true)).Unit;
    }

    private static async Task<(string Workflow, string Unit)> IngestRemoteUnitAsync(HttpClient http, MemoriesClient memories,
        string tenant, string caseId, string party, string content, CancellationToken token)
    {
        PartyMemoryUnit input = PartyMemoryUnitMapper.Map(
            PartyMemoryRecoveryFixture.Entry() with { Id = party, DisplayName = content + " synthetic survivor" },
            new PartyMemoryUnitMappingContext(tenant, caseId, "PartyCreated", Timestamp: DateTimeOffset.UnixEpoch, SequenceNumber: 1))!;
        string workflow = await memories.IngestAsync(tenant, caseId, input.SourceUri, Encoding.UTF8.GetBytes(input.Content),
            "text/plain", "synthetic-survivor", input.Metadata, "survivor-" + party, token).ConfigureAwait(true);
        string? unit = null;
        for (int attempt = 0; attempt < 120; attempt++)
        {
            IngestionWorkflowStatus terminal = await memories.GetIngestionWorkflowStatusAsync(workflow, token).ConfigureAwait(true);
            unit = PartyMemoryIngestionEvidence.Resolve(terminal, workflow, tenant, caseId);
            if (!string.IsNullOrWhiteSpace(unit)) { break; }
            await Task.Delay(500, token).ConfigureAwait(true);
        }
        unit.ShouldNotBeNullOrWhiteSpace("Survivor scopes must hold real persisted and derived content before main-party erasure.");
        (await memories.GetMemoryUnitAsync(tenant, caseId, unit!, token).ConfigureAwait(true)).Content.ShouldContain(content);
        await AssertDerivedStateAsync(http, tenant, party, populated: true, token).ConfigureAwait(true);
        return (workflow, unit!);
    }

    private static async Task<(string Tenant, string Case, string PartyA, string UnitA, string PartyB, string UnitB)> PrepareOwnerComponentAsync(
        PartyMemoryRuntimeConfiguration config, CancellationToken token)
    {
        string tenant = "parties-recovery-oc-" + Guid.NewGuid().ToString("N");
        using var http = new HttpClient { BaseAddress = new Uri(config.Endpoint), Timeout = TimeSpan.FromSeconds(15) };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Bearer(config, tenant));
        http.DefaultRequestHeaders.Add("dapr-api-token", config.AppApiToken);
        await File.WriteAllTextAsync(Path.ChangeExtension(Environment.GetEnvironmentVariable("PARTIES_MEMORIES_RUNTIME_CONFIG")!, ".owner-component.json"),
            JsonSerializer.Serialize(new { Tenant = tenant, Scope = "isolated direct owner-contract fixture; disposal owns all backing resources" }), token).ConfigureAwait(true);
        using (HttpResponseMessage provision = await http.PostAsJsonAsync(MemoriesRoutes.TenantsPath(),
            new { tenantId = tenant, displayName = "Owned source-contract component", vectorDimensions = 768 }, token).ConfigureAwait(true))
        { provision.EnsureSuccessStatusCode(); }
        var memories = new MemoriesClient(http, Options.Create(new MemoriesClientOptions()), NullLogger<MemoriesClient>.Instance);
        bool active = false;
        for (int attempt = 0; attempt < 120; attempt++)
        {
            if ((await memories.GetTenantAsync(tenant, token).ConfigureAwait(true))?.Status == TenantStatus.Active) { active = true; break; }
            await Task.Delay(500, token).ConfigureAwait(true);
        }
        active.ShouldBeTrue();
#pragma warning disable HXL001 // Exact disposable owner-component case; never production tenant reconciliation.
        string caseId = (await memories.CreateCaseAsync(tenant, "Owned source-contract component", null, token).ConfigureAwait(true)).Id;
#pragma warning restore HXL001
        string first = "component-source-a-" + Guid.NewGuid().ToString("N"), second = "component-source-b-" + Guid.NewGuid().ToString("N");
        string unitA = await PopulateSurvivorAsync(http, memories, tenant, caseId, first, "componentamber", token).ConfigureAwait(true);
        string unitB = await PopulateSurvivorAsync(http, memories, tenant, caseId, second, "componentcobalt", token).ConfigureAwait(true);
        await PartyMemoryOwnerSourceRuntime.CaptureBaselineAsync(config, http, tenant, caseId,
            PartyMemoryUrn.Build(tenant, first), unitA, PartyMemoryUrn.Build(tenant, second), unitB, token).ConfigureAwait(true);
        return (tenant, caseId, first, unitA, second, unitB);
    }

    private static Task<byte[]> ReadDerivedStateAsync(HttpClient http, string tenant, string party, CancellationToken token)
        => http.GetByteArrayAsync("api/v1/tenants/" + Uri.EscapeDataString(tenant) + "/source-inventory/owned-derived-state?sourceUri="
            + Uri.EscapeDataString(PartyMemoryUrn.Build(tenant, party)), token);

    private static async Task InitializeOwnedSourceAsync(HttpClient http, string tenant, string party, CancellationToken token)
    {
        string route = "api/v1/tenants/" + Uri.EscapeDataString(tenant) + "/source-inventory/qualify-owned-test-scope?sourceUri="
            + Uri.EscapeDataString(PartyMemoryUrn.Build(tenant, party));
        using HttpResponseMessage response = await http.PostAsync(route, null, token).ConfigureAwait(true);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<JsonElement> AssertDerivedStateAsync(HttpClient http, string tenant, string party, bool populated, CancellationToken token)
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
        if (!populated)
        {
            state.GetProperty("naturalLanguage").GetInt32().ShouldBe(0);
            foreach (string counter in new[] { "sourcePayloads", "retainedPayloadReferences", "failedUnits" })
                if (state.TryGetProperty(counter, out JsonElement count)) { count.GetInt64().ShouldBe(0, counter); }
        }
        return state;
    }

    [Fact]
    public async Task DeletionTerminalWaitObservesCompletedAfterRunning()
    {
        int requests = 0;
        using var transport = new PartySearchErasureHttpHandler((request, _) =>
        {
            request.RequestUri!.AbsolutePath.ShouldBe("/v1.0/workflows/dapr/memory-unit-delete-exact-output");
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                { Content = JsonContent.Create(new { runtimeStatus = ++requests == 1 ? "RUNNING" : "COMPLETED" }) });
        });
        using var http = new HttpClient(transport) { BaseAddress = new Uri("http://127.0.0.1:1/") };
        await WaitForDeletionTerminalAsync(http, "exact-output", TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        requests.ShouldBe(2);
    }

    [Theory]
    [InlineData("Failed")]
    [InlineData("Terminated")]
    [InlineData("Canceled")]
    [InlineData("Cancelled")]
    public async Task DeletionTerminalWaitRejectsFailedOrTerminatedWorkflows(string status)
    {
        int requests = 0;
        using var transport = new PartySearchErasureHttpHandler((_, _) =>
        {
            requests++;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = JsonContent.Create(new { runtimeStatus = status }) });
        });
        using var http = new HttpClient(transport) { BaseAddress = new Uri("http://127.0.0.1:1/") };
        (await Should.ThrowAsync<InvalidOperationException>(() => WaitForDeletionTerminalAsync(http, "exact-output", TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken)))
            .Message.ShouldContain(status);
        requests.ShouldBe(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeletionTerminalWaitIsBoundedAndHonorsCancellation(bool cancelCaller)
    {
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        int requests = 0;
        using var transport = new PartySearchErasureHttpHandler((_, _) =>
        {
            requests++;
            if (cancelCaller) { caller.CancelAfter(TimeSpan.FromMilliseconds(20)); }
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = JsonContent.Create(new { runtimeStatus = "Running" }) });
        });
        using var http = new HttpClient(transport) { BaseAddress = new Uri("http://127.0.0.1:1/") };
        Task waiting = WaitForDeletionTerminalAsync(http, "exact-output", cancelCaller ? TimeSpan.FromSeconds(2) : TimeSpan.FromMilliseconds(40), caller.Token);
        if (cancelCaller) { await Should.ThrowAsync<OperationCanceledException>(waiting); }
        else { await Should.ThrowAsync<TimeoutException>(waiting); }
        requests.ShouldBe(1);
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
        using var http = PartyMemoryRecoveryFixture.Http(transport);
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

    /// <summary>An unwritable authority receipt cannot skip either actual owned teardown receipt.</summary>
    /// <param name="qualificationCompleted">Whether the qualification body completed without a primary failure.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AuthorityReceiptFailureStillTearsDownPrimaryAndSurvivor(bool qualificationCompleted)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var state = new PartyMemoryTestReadModelStore();
        var config = new PartyMemoryRuntimeConfiguration { StateStoreName = "synthetic-store" };
        string tenant = "synthetic-" + Guid.NewGuid().ToString("N");
        string otherTenant = "synthetic-survivor-" + Guid.NewGuid().ToString("N");
        string mappingKey = PartyMemoryUnitMappingStore.BuildKey(tenant, "synthetic-party");
        string otherMapping = PartyMemoryUnitMappingStore.BuildKey(otherTenant, "survivor");
        var deletes = new List<string>();
        using var transport = new PartySearchErasureHttpHandler((request, _) =>
        {
            if (request.Method == HttpMethod.Delete)
            {
                lock (deletes)
                {
                    deletes.Add(request.RequestUri!.AbsolutePath);
                }

                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NoContent));
            }

            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound) { Content = JsonContent.Create(new { code = "TENANT_NOT_FOUND" }) });
        });
        using var http = PartyMemoryRecoveryFixture.Http(transport);
        string root = Path.Combine(Path.GetTempPath(), "parties-authority-" + Guid.NewGuid().ToString("N"));
        string diagnosticPath = Path.Combine(root, "runtime.provisioning.json");
        string authorityPath = Path.Combine(root, "runtime.authority.json");
        // A directory at the receipt path makes the actual file write fail.
        Directory.CreateDirectory(authorityPath);
        try
        {
            Task finalization = PersistAuthorityThenTeardownAsync(authorityPath, () => "{}", qualificationCompleted,
                () => TeardownAsync(config, http, tenant, "synthetic-party", "case", mappingKey,
                    mappingKey.Replace("memory-mapping", "memory-ingestion", StringComparison.Ordinal), true, diagnosticPath, state),
                () => TeardownAsync(config, http, otherTenant, "survivor", "other-case", otherMapping,
                    otherMapping.Replace("memory-mapping", "memory-ingestion", StringComparison.Ordinal), true,
                    Path.ChangeExtension(diagnosticPath, ".survivor.json"), state));
            if (qualificationCompleted)
            {
                Exception failure = await Should.ThrowAsync<Exception>(finalization);
                (failure is IOException or UnauthorizedAccessException).ShouldBeTrue(failure.GetType().FullName);
            }
            else
            {
                await finalization.ConfigureAwait(true);
            }

            using JsonDocument primary = JsonDocument.Parse(await File.ReadAllTextAsync(Path.ChangeExtension(diagnosticPath, ".teardown.json"), token));
            primary.RootElement.GetProperty("TenantId").GetString().ShouldBe(tenant);
            using JsonDocument survivor = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root, "runtime.provisioning.survivor.teardown.json"), token));
            survivor.RootElement.GetProperty("TenantId").GetString().ShouldBe(otherTenant);
            string Route(string path) => new Uri(http.BaseAddress!, path).AbsolutePath;
            deletes.ShouldBe([
                Route(MemoriesRoutes.CasePath(tenant, "case")), Route(MemoriesRoutes.TenantPath(tenant)),
                Route(MemoriesRoutes.CasePath(otherTenant, "other-case")), Route(MemoriesRoutes.TenantPath(otherTenant))]);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Every finalization step runs in order; failures surface only for a completed qualification and never replace its failure.</summary>
    /// <param name="failure">The injected finalization failure.</param>
    /// <param name="qualificationCompleted">Whether the qualification body completed without a primary failure.</param>
    [Theory]
    [InlineData("none", true)]
    [InlineData("serialization", true)]
    [InlineData("serialization", false)]
    [InlineData("primary-teardown", true)]
    [InlineData("primary-teardown", false)]
    [InlineData("survivor-teardown", true)]
    [InlineData("both-teardowns", true)]
    [InlineData("both-teardowns", false)]
    public async Task AuthorityFinalizationRunsBothTeardownsAndPreservesTheOwningFailure(string failure, bool qualificationCompleted)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        string authorityPath = Path.Combine(Path.GetTempPath(), "parties-authority-" + Guid.NewGuid().ToString("N") + ".json");
        var calls = new List<string>();
        var serializationFailure = new InvalidOperationException("synthetic-authority-serialization");
        var primaryFailure = new InvalidOperationException("synthetic-primary-teardown");
        var survivorFailure = new InvalidOperationException("synthetic-survivor-teardown");
        try
        {
            Task finalization = PersistAuthorityThenTeardownAsync(authorityPath,
                () => failure == "serialization" ? throw serializationFailure : "{\"Authority\":true}",
                qualificationCompleted,
                () =>
                {
                    calls.Add("primary");
                    return failure is "primary-teardown" or "both-teardowns" ? Task.FromException(primaryFailure) : Task.CompletedTask;
                },
                () =>
                {
                    calls.Add("survivor");
                    return failure is "survivor-teardown" or "both-teardowns" ? Task.FromException(survivorFailure) : Task.CompletedTask;
                });
            if (failure == "none" || !qualificationCompleted)
            {
                // The propagating qualification failure, if any, is never replaced by finalization failures.
                await finalization.ConfigureAwait(true);
            }
            else if (failure == "both-teardowns")
            {
                (await Should.ThrowAsync<AggregateException>(finalization)).InnerExceptions.ShouldBe([primaryFailure, survivorFailure]);
            }
            else
            {
                Exception expected = failure switch
                {
                    "serialization" => serializationFailure,
                    "primary-teardown" => primaryFailure,
                    _ => survivorFailure,
                };
                (await Should.ThrowAsync<InvalidOperationException>(finalization)).ShouldBeSameAs(expected);
            }

            calls.ShouldBe(["primary", "survivor"]);
            if (failure == "serialization")
            {
                File.Exists(authorityPath).ShouldBeFalse();
            }
            else
            {
                (await File.ReadAllTextAsync(authorityPath, token)).ShouldBe("{\"Authority\":true}");
            }
        }
        finally
        {
            File.Delete(authorityPath);
        }
    }

    /// <summary>Persists the final authority receipt, then always runs both owned teardowns.</summary>
    /// <param name="authorityPath">The private authority receipt path.</param>
    /// <param name="authority">Produces the serialized authority receipt.</param>
    /// <param name="qualificationCompleted">Whether the qualification body completed without a primary failure.</param>
    /// <param name="primaryTeardown">Tears down the primary synthetic tenant.</param>
    /// <param name="survivorTeardown">Tears down the foreign-tenant survivor.</param>
    /// <returns>A task that represents the finalization.</returns>
    /// <remarks>
    /// Every step runs even when an earlier one fails. Finalization failures surface only after both teardowns and only
    /// when the qualification completed; otherwise the propagating qualification failure is preserved and they are reported.
    /// </remarks>
    private static async Task PersistAuthorityThenTeardownAsync(string authorityPath, Func<string> authority,
        bool qualificationCompleted, Func<Task> primaryTeardown, Func<Task> survivorTeardown)
    {
        List<Exception> failures = [];
        async Task RunAsync(Func<Task> step)
        {
            try
            {
                await step().ConfigureAwait(true);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
            {
                failures.Add(ex);
            }
        }

        await RunAsync(() => File.WriteAllTextAsync(authorityPath, authority(), CancellationToken.None)).ConfigureAwait(true);
        await RunAsync(primaryTeardown).ConfigureAwait(true);
        await RunAsync(survivorTeardown).ConfigureAwait(true);
        if (failures.Count == 0)
        {
            return;
        }

        if (!qualificationCompleted)
        {
            foreach (Exception failure in failures)
            {
                TestContext.Current.TestOutputHelper?.WriteLine("Owned finalization failed after the qualification failure (" + failure.GetType().Name + "); operator cleanup may be required.");
            }

            return;
        }

        if (failures.Count == 1)
        {
            ExceptionDispatchInfo.Capture(failures[0]).Throw();
        }

        throw new AggregateException("Owned qualification finalization failed.", failures);
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
            DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(config.ProducerRestartDirectory is null ? 10 : 30), signing);
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }
}
