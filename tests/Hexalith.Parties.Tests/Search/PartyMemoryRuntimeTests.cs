using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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
    [Fact(Timeout = 240000)]
    public async Task SelectedMemoriesIngestionAndCleanupPersistAcrossRestart()
    {
        string? path = Environment.GetEnvironmentVariable("PARTIES_MEMORIES_RUNTIME_CONFIG");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(path), "Set PARTIES_MEMORIES_RUNTIME_CONFIG to an owned local runtime's private JSON configuration.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(3));
        CancellationToken token = deadline.Token;
        PartyMemoryRuntimeConfiguration config = JsonSerializer.Deserialize<PartyMemoryRuntimeConfiguration>(
            await File.ReadAllTextAsync(path!, token), new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("Runtime configuration is empty.");
        config.FakeEmbeddingEnabled.ShouldBeTrue("the owned runtime must explicitly enable its local fake embedding provider");
        string tenant = $"parties-recovery-{Guid.NewGuid():N}";
        string party = $"party-{Guid.NewGuid():N}";
        string bearer = Bearer(config, tenant);
        using var http = new HttpClient { BaseAddress = new Uri(config.Endpoint), Timeout = TimeSpan.FromSeconds(15) };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        if (!string.IsNullOrWhiteSpace(config.AppApiToken))
        {
            http.DefaultRequestHeaders.Add("dapr-api-token", config.AppApiToken);
        }

        var memories = new MemoriesClient(http, Options.Create(new MemoriesClientOptions()), NullLogger<MemoriesClient>.Instance);
        string provisioningWorkflow;
        string? caseId = null;
        string diagnosticPath = Path.ChangeExtension(Path.GetFullPath(path!), ".provisioning.json");
        try
        {
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
            Phase = "provisioning-accepted", TenantId = tenant, WorkflowInstanceId = provisioningWorkflow,
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
            string observation = await CaptureProvisioningDiagnosticsAsync(config, diagnosticPath, tenant,
                provisioningWorkflow, observedTenantStatus).ConfigureAwait(true);
            active.ShouldBeTrue($"the selected Memories implementation must finish provisioning; {observation}");
        }
#pragma warning disable HXL001 // Published opt-in case provisioning API, used only by this explicit runtime qualification.
        var created = await memories.CreateCaseAsync(tenant, "Synthetic recovery qualification", null, token);
#pragma warning restore HXL001
        caseId = created.Id;
        var entry = PartyMemoryRecoveryTests.Entry() with { Id = party, DisplayName = "Synthetic recovery record" };
        var context = new PartyMemoryUnitMappingContext(tenant, caseId, "PartyCreated", Timestamp: DateTimeOffset.UnixEpoch);
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
        canonical.ShouldNotBeNullOrWhiteSpace();
        (await memories.GetMemoryUnitAsync(tenant, caseId, canonical!, token)).Id.ShouldBe(canonical);

        // Recreate both the Dapr client and SDK adapter: no process-local dictionary supplies this proof.
        using DaprClient restarted = Dapr(config);
        var restartedSdk = new DaprReadModelStore(restarted);
        var mappings = new PartyMemoryUnitMappingStore(restartedSdk, stateOptions, NullLogger<PartyMemoryUnitMappingStore>.Instance);
        var intents = new PartyMemoryIngestionIntentStore(restartedSdk, stateOptions);
        PartyMemoryIngestionIntent persisted = (await intents.GetAsync(tenant, party, token)).ShouldHaveSingleItem();
        persisted.MemoryUnitId.ShouldBe(canonical);
        persisted.WorkflowInstanceId.ShouldBe(indexed.WorkflowInstanceId);
        (await mappings.GetMappingsAsync(tenant, party, token)).ShouldBe([new(canonical!, PartyMemoryUrn.Build(tenant, party), caseId)]);
        string mappingKey = PartyMemoryUnitMappingStore.BuildKey(tenant, party);
        (ReadOnlyMemory<byte> before, string previousEtag) = await restarted.GetByteStateAndETagAsync(config.StateStoreName, mappingKey, cancellationToken: token);
        previousEtag.ShouldNotBeNullOrWhiteSpace();
        using (JsonDocument mappingJson = JsonDocument.Parse(before))
        {
            mappingJson.RootElement.ValueKind.ShouldBe(JsonValueKind.Array);
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
        using HttpResponseMessage absence = await http.GetAsync(MemoriesRoutes.CaseMemoryUnitPath(tenant, caseId, canonical!), token);
        (await PartyMemoryCleanupResponse.IsAbsentAsync(absence, token)).ShouldBeTrue();
        (await intents.GetAsync(tenant, party, token)).ShouldBeEmpty();
        (await mappings.GetMappingsAsync(tenant, party, token)).ShouldBeEmpty();
        (ReadOnlyMemory<byte> empty, string finalEtag) = await restarted.GetByteStateAndETagAsync(config.StateStoreName, mappingKey, cancellationToken: token);
        finalEtag.ShouldNotBeNullOrWhiteSpace();
        finalEtag.ShouldNotBe(previousEtag);
        using JsonDocument emptyJson = JsonDocument.Parse(empty);
        emptyJson.RootElement.GetArrayLength().ShouldBe(0);
        string intentKey = mappingKey.Replace("memory-mapping", "memory-ingestion", StringComparison.Ordinal);
        PartyMemoryIngestionInventory? fenced = (await restartedSdk.GetAsync<PartyMemoryIngestionInventory>(config.StateStoreName, intentKey, token)).Value;
        fenced.ShouldNotBeNull().CleanupRequested.ShouldBeTrue();
        fenced.Intents.ShouldBeEmpty();
        }
        finally
        {
            // Qualification owns these synthetic resources even when provisioning or assertions fail.
            await TeardownAsync(http, tenant, caseId, diagnosticPath).ConfigureAwait(true);
        }
    }

    private static async Task TeardownAsync(HttpClient http, string tenant, string? caseId, string diagnosticPath)
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        List<object> evidence = [];
        bool tenantAbsent = false;
        try
        {
            if (caseId is not null)
            {
                using HttpResponseMessage removedCase = await http.DeleteAsync(MemoriesRoutes.CasePath(tenant, caseId), budget.Token).ConfigureAwait(false);
                evidence.Add(new { Phase = "case-delete", HttpStatus = (int)removedCase.StatusCode,
                    PrivateResponse = await removedCase.Content.ReadAsStringAsync(budget.Token).ConfigureAwait(false) });
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

                await Task.Delay(TimeSpan.FromMilliseconds(500), budget.Token).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            evidence.Add(new { Phase = "teardown-incomplete", ExceptionType = ex.GetType().Name });
        }

        // Preserve handles privately for operator teardown; never replace the primary test failure.
        try
        {
            using var receiptBudget = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await File.WriteAllTextAsync(Path.ChangeExtension(diagnosticPath, ".teardown.json"), JsonSerializer.Serialize(new
            {
                TenantId = tenant, CaseId = caseId, TenantAbsenceObserved = tenantAbsent,
                OperatorCleanupRequired = !tenantAbsent, Observations = evidence,
            }), receiptBudget.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            // The earlier private provisioning receipt still retains the synthetic tenant handle.
        }
    }

    private static DaprClient Dapr(PartyMemoryRuntimeConfiguration config)
        => new DaprClientBuilder().UseHttpEndpoint(config.DaprHttpEndpoint).UseGrpcEndpoint(config.DaprGrpcEndpoint)
            .UseDaprApiToken(config.DaprApiToken).Build();

    private static async Task<string> CaptureProvisioningDiagnosticsAsync(PartyMemoryRuntimeConfiguration config,
        string diagnosticPath, string tenant, string workflow, TenantStatus? tenantStatus)
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
                Phase = "provisioning-not-active", TenantId = tenant, WorkflowInstanceId = workflow,
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
            [new Claim(PartiesClaimTypes.Subject, "synthetic-recovery-qualification"), new Claim(config.TenantClaimName, tenant)],
            DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(10), signing);
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }
}
