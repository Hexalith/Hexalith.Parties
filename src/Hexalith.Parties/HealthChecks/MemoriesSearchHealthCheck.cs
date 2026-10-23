using Hexalith.Memories.Client.Rest;
using Hexalith.Memories.Contracts.V1;
using Hexalith.Parties.Search;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Hexalith.Parties.HealthChecks;

/// <summary>
/// Health check for Memories search, cleanup routing, ingestion-status routing and SDK mapping storage.
/// Synthetic probes report explicit authorization rejection but cannot verify access to an
/// existing tenant-scoped workflow or qualify physical erasure. Operators must retain that
/// separate capability evidence before deployment qualification.
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
            return await CheckDisabledHealthAsync(current, cancellationToken).ConfigureAwait(false);
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

        // Aggregate search, cleanup route, ingestion-status route and SDK mapping-store probes.
        // Each probe surfaces its own status in the result data so operators can pinpoint
        // exactly which capability is degraded — e.g., search reachable but cleanup route 405
        // means erasure cleanup will silently fail until the Memories server is fixed.
        SearchProbeOutcome searchOutcome = await ProbeSearchAsync(client, current, cancellationToken).ConfigureAwait(false);
        CleanupProbeOutcome cleanupOutcome = await ProbeCleanupAsync(current, cancellationToken).ConfigureAwait(false);
        CleanupProbeOutcome statusOutcome = await ProbeIngestionStatusAsync(cancellationToken).ConfigureAwait(false);
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
            ["ingestionStatusRouteReachable"] = statusOutcome.Reachable,
            ["ingestionStatusAuthorizationVerified"] = false,
            ["ingestionStatusAuthorizationEvidence"] = "Synthetic workflow misses cannot verify tenant-scoped status access; validate an existing owned workflow before deployment qualification.",
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

        if (statusOutcome.StatusCode is { } statusReadCode)
        {
            data["ingestionStatusRouteStatus"] = statusReadCode;
        }

        if (statusOutcome.Reason is not null)
        {
            data["ingestionStatusRouteReason"] = statusOutcome.Reason;
        }

        bool allReachable = searchOutcome.Reachable && cleanupOutcome.Reachable && mappingStoreOutcome.Reachable && statusOutcome.Reachable;
        if (allReachable && !searchOutcome.DegradedReportedByMemories)
        {
            return HealthCheckResult.Healthy(
                "Memories rich search, cleanup and ingestion-status routes, and mapping store are all reachable.",
                data);
        }

        if (allReachable)
        {
            return HealthCheckResult.Degraded(
                "Memories is reachable but reported degraded search capabilities.",
                data: data);
        }

        // Degraded (not unhealthy) so /ready does not flap when Memories is briefly unreachable;
        // local fallback search remains operational. Search outage degrades reads only;
        // cleanup-route outage blocks AC5 GDPR cleanup; mapping-store outage prevents new
        // indexings from recording mappings. A failed capability surfaces here as Degraded.
        string description = BuildDegradedDescription(searchOutcome, cleanupOutcome, mappingStoreOutcome);
        if (!statusOutcome.Reachable)
        {
            description += $" Ingestion status-read unavailable ({statusOutcome.Reason}).";
        }
        return HealthCheckResult.Degraded(description, searchOutcome.Exception, data);
    }

    private async Task<HealthCheckResult> CheckDisabledHealthAsync(
        PartyMemorySearchOptions current,
        CancellationToken cancellationToken)
    {
        // Disabling ingestion does not remove previously indexed units or their ledger.
        // A sentinel probes storage capability; it cannot inventory historical obligations.
        MappingStoreProbeOutcome mapping = await ProbeMappingStoreAsync(cancellationToken).ConfigureAwait(false);
        bool configurationPresent = current.Endpoint is not null
            || !string.IsNullOrWhiteSpace(current.TenantId)
            || !string.IsNullOrWhiteSpace(current.CaseId)
            || !string.IsNullOrWhiteSpace(current.ApiToken);
        bool configured = current.Endpoint is { IsAbsoluteUri: true } endpoint
            && (endpoint.Scheme == Uri.UriSchemeHttp || endpoint.Scheme == Uri.UriSchemeHttps)
            && endpoint.AbsolutePath.EndsWith('/')
            && string.IsNullOrEmpty(endpoint.Query)
            && string.IsNullOrEmpty(endpoint.Fragment)
            && !string.IsNullOrWhiteSpace(current.TenantId)
            && !string.IsNullOrWhiteSpace(current.CaseId)
            && (!current.RequireApiToken
                || (!string.IsNullOrWhiteSpace(current.ApiToken) && !current.ApiToken.Any(char.IsControl)));
        CleanupProbeOutcome cleanup = configured
            ? await ProbeCleanupAsync(current, cancellationToken).ConfigureAwait(false)
            : new CleanupProbeOutcome(false, null, configurationPresent
                ? "Memories cleanup configuration is incomplete or invalid; remote cleanup was not verified."
                : "Memories cleanup is not configured; remote cleanup was not verified.");
        CleanupProbeOutcome statusRead = configured
            ? await ProbeIngestionStatusAsync(cancellationToken).ConfigureAwait(false)
            : new(false, null, "Memories ingestion status-read was not configured or verified.");
        Dictionary<string, object> data = new()
        {
            ["ingestionStatusRouteReachable"] = statusRead.Reachable,
            ["ingestionStatusAuthorizationVerified"] = false,
            ["ingestionStatusRouteReason"] = statusRead.Reason ?? "Synthetic status miss; validate authorization using an existing owned workflow.",
            ["enabled"] = false,
            ["mode"] = "local-only",
            ["cleanupConfigured"] = configured,
            ["cleanupProbeSkipped"] = !configured,
            ["cleanupRouteReachable"] = cleanup.Reachable,
            ["mappingStoreReachable"] = mapping.Reachable,
        };
        if (statusRead.StatusCode is { } statusReadCode)
        {
            data["ingestionStatusRouteStatus"] = statusReadCode;
        }

        if (cleanup.StatusCode is { } statusCode)
        {
            data["cleanupRouteStatus"] = statusCode;
        }

        if (cleanup.Reason is not null)
        {
            data["cleanupRouteReason"] = cleanup.Reason;
        }

        if (mapping.Reason is not null)
        {
            data["mappingStoreReason"] = mapping.Reason;
        }

        if (!mapping.Reachable || (configurationPresent && (!cleanup.Reachable || !statusRead.Reachable)))
        {
            return HealthCheckResult.Degraded(
                BuildDegradedDescription(new SearchProbeOutcome(true, false, null, null), cleanup, mapping)
                    + (!statusRead.Reachable && configured ? $" Ingestion status-read unavailable ({statusRead.Reason})." : string.Empty),
                data: data);
        }

        return HealthCheckResult.Healthy(configured
            ? "Memories indexing is disabled; cleanup and ingestion-status routes and mapping store are reachable."
            : "Memories indexing is disabled; local search and mapping storage are available. Remote cleanup is not configured or verified.",
            data);
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

    private async Task<CleanupProbeOutcome> ProbeIngestionStatusAsync(CancellationToken cancellationToken)
    {
        if (serviceProvider.GetService(typeof(PartyMemoryCleanupService)) is not PartyMemoryCleanupService cleanupService)
        {
            return new(false, null, "PartyMemoryCleanupService is not registered in DI.");
        }

        PartyMemoryCleanupProbeResult probe = await cleanupService.ProbeIngestionStatusRouteAsync(cancellationToken).ConfigureAwait(false);
        return new(probe.Reachable, probe.StatusCode, probe.Reason);
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
        bool sentinelCreated = false;
        MappingStoreProbeOutcome? outcome = null;
        try
        {
            await store.RecordMappingAsync(
                probeTenant,
                probeParty,
                "_probe-unit",
                "urn:_probe",
                "_probe-case",
                cancellationToken).ConfigureAwait(false);
            sentinelCreated = true;
            IReadOnlyList<PartyMemoryUnitMappingEntry> read = await store.GetMappingsAsync(probeTenant, probeParty, cancellationToken).ConfigureAwait(false);
            outcome = read is [{ MemoryUnitId: "_probe-unit", SourceUri: "urn:_probe", CaseId: "_probe-case" }]
                ? new MappingStoreProbeOutcome(true, null)
                : new MappingStoreProbeOutcome(false, "Mapping store round-trip did not return the expected sentinel.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            outcome = new MappingStoreProbeOutcome(false, $"Mapping store round-trip failed: {ex.GetType().Name}.");
        }
        finally
        {
            if (sentinelCreated)
            {
                try
                {
                    // Health-request cancellation must not prevent disposal of a recorded
                    // sentinel. Keep cleanup bounded independently, then propagate cancellation.
                    using var cleanupBudget = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await store.ClearMappingsAsync(probeTenant, probeParty, cleanupBudget.Token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // A cleanup failure must affect a successful primary probe, but it must not
                    // replace a more useful read/round-trip failure that already explains why the
                    // mapping store is unavailable.
                    if (outcome is null || outcome.Reachable)
                    {
                        outcome = new MappingStoreProbeOutcome(
                            false,
                            $"Mapping store sentinel cleanup failed: {ex.GetType().Name}.");
                    }
                }
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        return outcome ?? new MappingStoreProbeOutcome(false, "Mapping store probe did not produce a result.");
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
