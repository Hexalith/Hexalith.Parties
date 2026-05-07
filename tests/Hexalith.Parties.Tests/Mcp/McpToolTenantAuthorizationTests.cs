// ATDD red-phase scaffolds for Story 11.4 — MCP tool authorization-before-projection.
// MCP tools must reject denied access via the Tenants-backed access service before
// reading projections or routing commands.

using Hexalith.Parties.Authorization;
using Hexalith.Parties.Mcp;
using Hexalith.Parties.Tests.Authorization;

using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline.Commands;

using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.Tests.Mcp;

/// <summary>
/// Story 11.4 — AC2: MCP tools must use the same Tenants-backed access decision
/// as REST endpoints. Denial messages must include the stable reason code so that
/// MCP clients can react deterministically.
/// </summary>
public sealed class McpToolTenantAuthorizationTests
{
    [Fact]
    public async Task FindPartiesMcpTool_GivenDisabledTenant_ThrowsWithTenantDisabledCodeAsync()
    {
        // Arrange — controllable access service decides "DisabledTenant".
        TestTenantAccessService access = new((_, _, _, _) =>
            Task.FromResult(TenantAccessDecision.Denied(TenantAccessDenialReason.DisabledTenant)));

        using McpSessionScope session = McpSessionScope.For("tenant-a", "user-1");

        // Act / Assert — auth gate throws BEFORE any projection lookup.
        McpTenantAuthorizationException ex = await Should.ThrowAsync<McpTenantAuthorizationException>(
            () => InvokeFindParties(access, query: "Ada"));

        ex.ReasonCode.ShouldBe("tenant-disabled");
    }

    [Fact]
    public async Task CreatePartyMcpTool_GivenReader_ThrowsInsufficientRole_BeforeIssuingCommandAsync()
    {
        // Arrange — reader denied for write.
        TestTenantAccessService access = new((_, _, requirement, _) =>
            Task.FromResult(requirement == TenantAccessRequirement.Read
                ? TenantAccessDecision.Allowed
                : TenantAccessDecision.Denied(TenantAccessDenialReason.InsufficientRole)));

        ICommandRouter router = Substitute.For<ICommandRouter>();
        using McpSessionScope session = McpSessionScope.For("tenant-a", "user-reader");

        // Act / Assert
        McpTenantAuthorizationException ex = await Should.ThrowAsync<McpTenantAuthorizationException>(
            () => InvokeCreateParty(access, router));

        ex.ReasonCode.ShouldBe("insufficient-role");

        // Critical: command router was never invoked because authorization gated the call.
        await router.DidNotReceive().RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetPartyMcpTool_GivenStaleProjection_ThrowsTenantStateStaleAsync()
    {
        TestTenantAccessService access = new((_, _, _, _) =>
            Task.FromResult(TenantAccessDecision.Denied(
                TenantAccessDenialReason.TenantStateStale,
                diagnosticText: "Tenant access state is unavailable.")));

        using McpSessionScope session = McpSessionScope.For("tenant-a", "user-1");

        McpTenantAuthorizationException ex = await Should.ThrowAsync<McpTenantAuthorizationException>(
            () => InvokeGetParty(access, partyId: Guid.NewGuid().ToString()));

        ex.ReasonCode.ShouldBe("tenant-state-stale");
    }

    /// <summary>
    /// Sidecar-free MCP harness that wires <see cref="McpTenantAuthorization"/>
    /// with the supplied access service into the actual tool.
    /// </summary>
    private static async Task InvokeFindParties(ITenantAccessService access, string query)
    {
        ServiceProvider services = McpToolTestServices.BuildForFind(access);
        try
        {
            await FindPartiesMcpTool.FindPartiesAsync(services, query).ConfigureAwait(false);
        }
        finally
        {
            await services.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static async Task InvokeCreateParty(ITenantAccessService access, ICommandRouter router)
    {
        ServiceProvider services = McpToolTestServices.BuildForCreate(access, router);
        try
        {
            await CreatePartyMcpTool.CreatePartyAsync(
                "Person",
                services,
                firstName: "Ada",
                lastName: "Lovelace").ConfigureAwait(false);
        }
        finally
        {
            await services.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static async Task InvokeGetParty(ITenantAccessService access, string partyId)
    {
        ServiceProvider services = McpToolTestServices.BuildForGet(access);
        try
        {
            await GetPartyMcpTool.GetPartyAsync(partyId, services).ConfigureAwait(false);
        }
        finally
        {
            await services.DisposeAsync().ConfigureAwait(false);
        }
    }
}
