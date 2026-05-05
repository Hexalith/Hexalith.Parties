// ATDD red-phase scaffolds for Story 11.4 — MCP tool authorization-before-projection.
// MCP tools must reject denied access via the Tenants-backed access service before
// reading projections or routing commands.

using Hexalith.Parties.CommandApi.Authorization;
using Hexalith.Parties.CommandApi.Mcp;

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.CommandApi.Tests.Mcp;

/// <summary>
/// Story 11.4 — AC2: MCP tools must use the same Tenants-backed access decision
/// as REST endpoints. Denial messages must include the stable reason code so that
/// MCP clients can react deterministically.
/// </summary>
public sealed class McpToolTenantAuthorizationTests
{
    private const string SkipReason =
        "TDD red phase — Story 11.4 must wire MCP tools to ITenantAccessService " +
        "with denial messages that include stable reason codes (tenant-disabled, " +
        "insufficient-role, tenant-state-stale).";

    [Fact(Skip = SkipReason)]
    public async Task FindPartiesMcpTool_GivenDisabledTenant_ThrowsWithTenantDisabledCodeAsync()
    {
        // Arrange — controllable access service decides "DisabledTenant".
        TestTenantAccessService access = new((_, _, _, _) =>
            Task.FromResult(TenantAccessDecision.Denied(TenantAccessDenialReason.DisabledTenant)));

        using McpSessionScope session = McpSessionScope.For("tenant-a", "user-1");

        // Act / Assert — auth gate throws BEFORE any projection lookup.
        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => InvokeFindParties(access, query: "Ada"));

        ex.Message.ShouldContain("tenant-disabled");
    }

    [Fact(Skip = SkipReason)]
    public async Task CreatePartyMcpTool_GivenReader_ThrowsInsufficientRole_BeforeIssuingCommandAsync()
    {
        // Arrange — reader denied for write.
        TestTenantAccessService access = new((_, _, requirement, _) =>
            Task.FromResult(requirement == TenantAccessRequirement.Read
                ? TenantAccessDecision.Allowed
                : TenantAccessDecision.Denied(TenantAccessDenialReason.InsufficientRole)));

        ICommandRouterDouble router = Substitute.For<ICommandRouterDouble>();
        using McpSessionScope session = McpSessionScope.For("tenant-a", "user-reader");

        // Act / Assert
        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => InvokeCreateParty(access, router));

        ex.Message.ShouldContain("insufficient-role");

        // Critical: command router was never invoked because authorization gated the call.
        await router.DidNotReceive().RouteAsync(Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact(Skip = SkipReason)]
    public async Task GetPartyMcpTool_GivenStaleProjection_ThrowsTenantStateStaleAsync()
    {
        TestTenantAccessService access = new((_, _, _, _) =>
            Task.FromResult(TenantAccessDecision.Denied(
                TenantAccessDenialReason.TenantStateStale,
                diagnosticText: "Tenant access state is unavailable.")));

        using McpSessionScope session = McpSessionScope.For("tenant-a", "user-1");

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => InvokeGetParty(access, partyId: Guid.NewGuid().ToString()));

        ex.Message.ShouldContain("tenant-state-stale");
    }

    /// <summary>
    /// Story 11.4 must add a sidecar-free MCP test harness that wires
    /// <see cref="McpTenantAuthorization"/> with the supplied access service into the actual
    /// <c>FindPartiesMcpTool</c>, then invokes it under <see cref="McpSessionContext"/>.
    /// </summary>
    private static Task InvokeFindParties(ITenantAccessService access, string query)
        => throw new NotImplementedException(
            "Story 11.4: add sidecar-free MCP tool invocation harness driven by ITenantAccessService.");

    private static Task InvokeCreateParty(ITenantAccessService access, ICommandRouterDouble router)
        => throw new NotImplementedException(
            "Story 11.4: add sidecar-free MCP tool invocation harness with ICommandRouter tracking double.");

    private static Task InvokeGetParty(ITenantAccessService access, string partyId)
        => throw new NotImplementedException(
            "Story 11.4: add sidecar-free MCP tool invocation harness driven by ITenantAccessService.");

    /// <summary>
    /// Marker interface representing the command-routing seam used by MCP tools.
    /// Story 11.4 must expose a substitutable seam so authorization-before-routing
    /// can be asserted with NSubstitute.
    /// </summary>
    public interface ICommandRouterDouble
    {
        Task RouteAsync(object envelope, CancellationToken cancellationToken);
    }

    private readonly struct McpSessionScope : IDisposable
    {
        private readonly IDisposable _tenantScope;
        private readonly IDisposable _userScope;

        private McpSessionScope(IDisposable tenantScope, IDisposable userScope)
        {
            _tenantScope = tenantScope;
            _userScope = userScope;
        }

        public static McpSessionScope For(string tenantId, string userId)
        {
            McpSessionContext.Tenant.Value = tenantId;
            McpSessionContext.UserId.Value = userId;
            return new McpSessionScope(
                new ResetOnDispose(() => McpSessionContext.Tenant.Value = null),
                new ResetOnDispose(() => McpSessionContext.UserId.Value = null));
        }

        public void Dispose()
        {
            _tenantScope.Dispose();
            _userScope.Dispose();
        }

        private sealed class ResetOnDispose(Action action) : IDisposable
        {
            private readonly Action _action = action;
            public void Dispose() => _action();
        }
    }
}
