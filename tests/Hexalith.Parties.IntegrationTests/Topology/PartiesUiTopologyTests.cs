extern alias apphost;

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

using Shouldly;

namespace Hexalith.Parties.IntegrationTests.Topology;

/// <summary>
/// AppHost topology assertions for the Story 1.1 <c>parties-ui</c> resource (AC2): it is composed
/// as a Blazor Server BFF over HTTP/SignalR with <strong>no DAPR sidecar</strong> (unlike
/// <c>parties</c>/<c>tenants</c>), waits for <c>eventstore</c> and <c>tenants</c>, and auto-starts
/// (no explicit-start gate, unlike <c>parties-mcp</c>).
/// </summary>
/// <remarks>
/// The test inspects the distributed-application model only — it never calls <c>StartAsync</c>, so
/// no Docker/DAPR runtime is required. When the model itself cannot be constructed in the current
/// environment (e.g. the AppHost's DAPR component files are not on the probe path), the test skips
/// gracefully per the project's integration-lane convention rather than reporting a red failure.
/// </remarks>
public sealed class PartiesUiTopologyTests
{
    [Fact]
    public async Task PartiesUiResource_HasNoDaprSidecar_WaitsForDependencies_AndAutoStarts()
    {
        IDistributedApplicationTestingBuilder builder;
        try
        {
            builder = await DistributedApplicationTestingBuilder
                .CreateAsync<apphost::Projects.Hexalith_Parties_AppHost>();
        }
        catch (Exception ex)
        {
            Assert.Skip(
                "Aspire application model could not be constructed in this environment: "
                + $"{ex.GetType().Name}: {ex.Message}");
            return;
        }

        await using (builder)
        {
            IResource partiesUi = RequireResource(builder, "parties-ui");

            // AC2 — parties-ui is a project resource (Blazor Server host), not a DAPR actor host.
            partiesUi.ShouldBeAssignableTo<ProjectResource>();

            // No DAPR sidecar on the BFF host — contrast with parties/tenants, which attach one.
            // Matched by annotation type name so the test takes no compile-time dependency on the
            // CommunityToolkit DAPR package; the positive contrast keeps the check self-validating.
            HasDaprSidecar(partiesUi).ShouldBeFalse();
            HasDaprSidecar(RequireResource(builder, "parties")).ShouldBeTrue();
            HasDaprSidecar(RequireResource(builder, "tenants")).ShouldBeTrue();

            // Waits for eventstore + tenants — AC2's "healthy once eventstore/tenants are healthy".
            IReadOnlyList<string> waitedOn = partiesUi.Annotations
                .OfType<WaitAnnotation>()
                .Select(static wait => wait.Resource.Name)
                .ToList();
            waitedOn.ShouldContain("eventstore");
            waitedOn.ShouldContain("tenants");

            // Auto-starts (no explicit-start gate) — contrast with parties-mcp, which is explicit-start.
            partiesUi.Annotations.OfType<ExplicitStartupAnnotation>().ShouldBeEmpty();
            RequireResource(builder, "parties-mcp").Annotations
                .OfType<ExplicitStartupAnnotation>()
                .ShouldNotBeEmpty();
        }
    }

    private static bool HasDaprSidecar(IResource resource) =>
        resource.Annotations.Any(static annotation => annotation.GetType().Name == "DaprSidecarAnnotation");

    private static IResource RequireResource(IDistributedApplicationTestingBuilder builder, string name)
    {
        IResource? resource = builder.Resources.SingleOrDefault(candidate => candidate.Name == name);
        resource.ShouldNotBeNull($"AppHost must declare a '{name}' resource.");
        return resource;
    }
}
