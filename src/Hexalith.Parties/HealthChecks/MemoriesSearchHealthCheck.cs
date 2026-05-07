using Hexalith.Memories.Client.Rest;
using Hexalith.Memories.Contracts.V1;
using Hexalith.Parties.Search;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Hexalith.Parties.HealthChecks;

/// <summary>
/// Health check for Hexalith.Memories search integration. Distinguishes local Parties
/// availability from Memories-backed rich search availability so /health can answer the
/// AC6 question: "is rich search up, and can erasure cleanup actually run?"
/// <para>
/// AC6 requires deployment validation to check Memories endpoint configuration, tenant /
/// case provisioning, auth, search health, <b>and cleanup capability</b>. The probes below
/// cover all five — search reachability via <see cref="MemoriesClient.HybridSearchAsync"/>,
/// cleanup-route reachability via <see cref="PartyMemoryCleanupService.ProbeCleanupRouteAsync"/>,
/// and mapping-store reachability via a sentinel record/get/clear cycle. Operators inspecting
/// <c>/health</c> see each probe's outcome in the result data, so a deployment that has the
/// search endpoint up but a misconfigured DELETE route or unreachable Dapr state store cannot
/// silently ship — the GDPR cleanup path AC5 depends on either works at deployment time, or
/// the readiness signal flags it.
/// </para>
/// </summary>
internal sealed class MemoriesSearchHealthCheck(
    IOptionsMonitor<PartyMemorySearchOptions> options,
    IServiceProvider serviceProvider)
    : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        PartyMemorySearchOptions current = options.CurrentValue;
        if (!current.Enabled)
        {
            return HealthCheckResult.Healthy(
                "Hexalith.Memories rich search is disabled. Local display-name search is the only configured search path.",
                new Dictionary<string, object>
                {
                    ["enabled"] = false,
                    ["mode"] = "local-only",
                });
        }

        if (current.Endpoint is null || string.IsNullOrWhiteSpace(current.TenantId) || string.IsNullOrWhiteSpace(current.CaseId))
        {
            return HealthCheckResult.Unhealthy(
                "Memories search is enabled but endpoint/tenant/case configuration is incomplete.",
                data: new Dictionary<string, object>
                {
                    ["enabled"] = true,
                    ["endpointConfigured"] = current.Endpoint is not null,
                    ["tenantConfigured"] = !string.IsNullOrWhiteSpace(current.TenantId),
                    ["caseConfigured"] = !string.IsNullOrWhiteSpace(current.CaseId),
                });
        }

        if (current.RequireApiToken && string.IsNullOrWhiteSpace(current.ApiToken))
        {
            return HealthCheckResult.Unhealthy(
                "Memories search requires an API token but none is configured.",
                data: new Dictionary<string, object>
                {
                    ["enabled"] = true,
                    ["requireApiToken"] = true,
                    ["apiTokenConfigured"] = false,
                });
        }

        if (serviceProvider.GetService(typeof(MemoriesClient)) is not MemoriesClient client)
        {
            return HealthCheckResult.Unhealthy("MemoriesClient is not registered in DI even though Memories search is enabled.");
        }

        // Run the three AC6 probes (search, cleanup route, mapping store) and aggregate results.
        // Each probe surfaces its own status in the result data so operators can pinpoint
        // exactly which capability is degraded — e.g., search reachable but cleanup route 405
        // means erasure cleanup will silently fail until the Memories server is fixed.
        SearchProbeOutcome searchOutcome = await ProbeSearchAsync(client, current, cancellationToken).ConfigureAwait(false);
        CleanupProbeOutcome cleanupOutcome = await ProbeCleanupAsync(current, cancellationToken).ConfigureAwait(false);
        MappingStoreProbeOutcome mappingStoreOutcome = await ProbeMappingStoreAsync(cancellationToken).ConfigureAwait(false);

        Dictionary<string, object> data = new()
        {
            ["enabled"] = true,
            ["endpoint"] = current.Endpoint!.ToString(),
            ["tenantId"] = current.TenantId!,
            ["caseId"] = current.CaseId!,
            ["searchReachable"] = searchOutcome.Reachable,
            ["cleanupRouteReachable"] = cleanupOutcome.Reachable,
            ["mappingStoreReachable"] = mappingStoreOutcome.Reachable,
        };

        if (searchOutcome.Reachable)
        {
            data["degradedReportedByMemories"] = searchOutcome.DegradedReportedByMemories;
            data["unavailableAxes"] = searchOutcome.UnavailableAxes ?? (IReadOnlyList<string>)Array.Empty<string>();
        }

        if (cleanupOutcome.StatusCode is { } cleanupStatus)
        {
            data["cleanupRouteStatus"] = cleanupStatus;
        }

        if (cleanupOutcome.Reason is not null)
        {
            data["cleanupRouteReason"] = cleanupOutcome.Reason;
        }

        if (mappingStoreOutcome.Reason is not null)
        {
            data["mappingStoreReason"] = mappingStoreOutcome.Reason;
        }

        bool allReachable = searchOutcome.Reachable && cleanupOutcome.Reachable && mappingStoreOutcome.Reachable;
        if (allReachable)
        {
            return HealthCheckResult.Healthy(
                "Memories rich search, cleanup route, and mapping store are all reachable.",
                data);
        }

        // Degraded (not unhealthy) so /ready does not flap when Memories is briefly unreachable;
        // local fallback search remains operational. Search outage degrades reads only;
        // cleanup-route outage blocks AC5 GDPR cleanup; mapping-store outage prevents new
        // indexings from recording mappings. Any of the three failing surfaces here as Degraded.
        string description = BuildDegradedDescription(searchOutcome, cleanupOutcome, mappingStoreOutcome);
        return HealthCheckResult.Degraded(description, searchOutcome.Exception, data);
    }

    private static async Task<SearchProbeOutcome> ProbeSearchAsync(
        MemoriesClient client,
        PartyMemorySearchOptions current,
        CancellationToken cancellationToken)
    {
        try
        {
            // Probe Memories with a low-cost search call so the check exercises endpoint
            // reachability, auth, and tenant/case provisioning together. We ask for one result
            // to keep the call cheap; any successful HTTP round-trip — even with empty results —
            // proves search is wired correctly.
            HybridSearchResult probe = await client
                .HybridSearchAsync(
                    new HybridSearchRequest(current.TenantId!, "healthcheck", current.CaseId!, 1, Explain: false),
                    cancellationToken)
                .ConfigureAwait(false);
            return new SearchProbeOutcome(true, probe.Degraded, probe.UnavailableAxes, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new SearchProbeOutcome(false, false, null, ex);
        }
    }

    private async Task<CleanupProbeOutcome> ProbeCleanupAsync(
        PartyMemorySearchOptions current,
        CancellationToken cancellationToken)
    {
        if (serviceProvider.GetService(typeof(PartyMemoryCleanupService)) is not PartyMemoryCleanupService cleanupService)
        {
            return new CleanupProbeOutcome(false, null, "PartyMemoryCleanupService is not registered in DI.");
        }

        PartyMemoryCleanupProbeResult probe = await cleanupService
            .ProbeCleanupRouteAsync(current.TenantId!, current.CaseId!, cancellationToken)
            .ConfigureAwait(false);
        return new CleanupProbeOutcome(probe.Reachable, probe.StatusCode, probe.Reason);
    }

    private async Task<MappingStoreProbeOutcome> ProbeMappingStoreAsync(CancellationToken cancellationToken)
    {
        if (serviceProvider.GetService(typeof(IPartyMemoryUnitMappingStore)) is not IPartyMemoryUnitMappingStore store)
        {
            return new MappingStoreProbeOutcome(false, "IPartyMemoryUnitMappingStore is not registered in DI.");
        }

        // Sentinel tenant + party id so the probe cannot collide with real data. The leading
        // underscore reserves it as an internal probe namespace.
        string probeTenant = "_health-probe";
        string probeParty = $"_health-probe-{Guid.NewGuid():N}";
        try
        {
            await store.RecordMappingAsync(probeTenant, probeParty, "_probe-unit", "urn:_probe", cancellationToken).ConfigureAwait(false);
            IReadOnlyList<PartyMemoryUnitMappingEntry> read = await store.GetMappingsAsync(probeTenant, probeParty, cancellationToken).ConfigureAwait(false);
            await store.ClearMappingsAsync(probeTenant, probeParty, cancellationToken).ConfigureAwait(false);
            return read.Count == 1
                ? new MappingStoreProbeOutcome(true, null)
                : new MappingStoreProbeOutcome(false, $"Mapping store round-trip returned {read.Count} entries (expected 1).");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new MappingStoreProbeOutcome(false, $"Mapping store round-trip failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static string BuildDegradedDescription(
        SearchProbeOutcome search,
        CleanupProbeOutcome cleanup,
        MappingStoreProbeOutcome mappingStore)
    {
        List<string> failing = [];
        if (!search.Reachable)
        {
            failing.Add($"search ({search.Exception?.GetType().Name ?? "unknown"})");
        }

        if (!cleanup.Reachable)
        {
            failing.Add($"cleanup route ({cleanup.Reason ?? "unknown"})");
        }

        if (!mappingStore.Reachable)
        {
            failing.Add($"mapping store ({mappingStore.Reason ?? "unknown"})");
        }

        return failing.Count == 0
            ? "Memories integration degraded."
            : $"Memories integration degraded: {string.Join("; ", failing)}.";
    }

    private sealed record SearchProbeOutcome(
        bool Reachable,
        bool DegradedReportedByMemories,
        IReadOnlyList<string>? UnavailableAxes,
        Exception? Exception);

    private sealed record CleanupProbeOutcome(bool Reachable, int? StatusCode, string? Reason);

    private sealed record MappingStoreProbeOutcome(bool Reachable, string? Reason);
}
