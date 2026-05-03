using Hexalith.Memories.Client.Rest;
using Hexalith.Memories.Contracts.V1;
using Hexalith.Parties.CommandApi.Search;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Hexalith.Parties.CommandApi.HealthChecks;

/// <summary>
/// Health check for Hexalith.Memories search integration. Distinguishes local Parties
/// availability from Memories-backed rich search availability so /health can answer the
/// AC6 question: "is rich search up, or are we serving local fallback?"
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

        // Probe Memories with a low-cost search call so the check exercises endpoint reachability,
        // auth, and tenant/case provisioning together. We ask for zero results to keep the call
        // cheap; any successful HTTP round-trip — even with empty results — proves the integration
        // is wired correctly.
        if (serviceProvider.GetService(typeof(MemoriesClient)) is not MemoriesClient client)
        {
            return HealthCheckResult.Unhealthy("MemoriesClient is not registered in DI even though Memories search is enabled.");
        }

        try
        {
            HybridSearchResult probe = await client
                .HybridSearchAsync(
                    new HybridSearchRequest(current.TenantId!, "healthcheck", current.CaseId!, 1, Explain: false),
                    cancellationToken)
                .ConfigureAwait(false);

            return HealthCheckResult.Healthy(
                "Memories rich search is reachable.",
                new Dictionary<string, object>
                {
                    ["enabled"] = true,
                    ["endpoint"] = current.Endpoint!.ToString(),
                    ["tenantId"] = current.TenantId!,
                    ["caseId"] = current.CaseId!,
                    ["degradedReportedByMemories"] = probe.Degraded,
                    ["unavailableAxes"] = probe.UnavailableAxes ?? (IReadOnlyList<string>)Array.Empty<string>(),
                });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Degraded (not unhealthy) so /ready does not flap when Memories is briefly unreachable;
            // local fallback search remains operational.
            return HealthCheckResult.Degraded(
                "Memories rich search is unreachable. Local fallback is still operational.",
                ex,
                new Dictionary<string, object>
                {
                    ["enabled"] = true,
                    ["endpoint"] = current.Endpoint!.ToString(),
                    ["error"] = ex.GetType().Name,
                });
        }
    }
}
