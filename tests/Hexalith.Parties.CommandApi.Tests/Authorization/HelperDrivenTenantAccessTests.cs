// ATDD red-phase scaffolds for Story 11.4 — drive tenant access state through
// public Hexalith.Tenants.Testing helpers (InMemoryTenantService + InMemoryTenantProjection)
// instead of mutating ITenantProjectionStore directly. Tests are skipped until the
// fixture bridge that maps Tenants events into the Parties tenant-access projection
// is implemented (Story 11.4, AC1).

using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.Parties.CommandApi.Authorization;
using Hexalith.Tenants.Client.Projections;
using Hexalith.Tenants.Contracts.Commands;
using Hexalith.Tenants.Contracts.Enums;
using Hexalith.Tenants.Testing.Fakes;
using Hexalith.Tenants.Testing.Helpers;
using Hexalith.Tenants.Testing.Projections;

using Shouldly;

namespace Hexalith.Parties.CommandApi.Tests.Authorization;

/// <summary>
/// Story 11.4 — AC1, AC2: Fast tenant-access decisions wired through
/// the public Hexalith.Tenants.Testing helpers, not by hand-rolling
/// <see cref="TenantLocalState"/> directly. Each test below documents the
/// expected contract; they remain skipped until the implementation lands a
/// bridge that translates Tenants events (via <see cref="InMemoryTenantProjection"/>
/// or an equivalent helper) into the Parties tenant-access projection store consumed by
/// <see cref="TenantAccessService"/>.
/// </summary>
public sealed class HelperDrivenTenantAccessTests
{
    private const string SkipReason =
        "TDD red phase — Story 11.4 must add the test bridge that projects Tenants events " +
        "into ITenantProjectionStore via Hexalith.Tenants.Testing helpers.";

    [Fact(Skip = SkipReason)]
    public async Task CheckAccessAsync_GivenTenantSeededViaHelpers_AllowsOwnerWriteAsync()
    {
        // Arrange — drive state through Tenants public helpers only.
        InMemoryTenantService tenants = new();
        TenantTestHelpers.CreateTenantWithOwner(tenants, tenantId: "tenant-a", ownerUserId: "user-1");

        ITenantProjectionStore store = ProjectFromTenants(tenants);
        TenantAccessService service = new(store);

        // Act
        TenantAccessDecision decision = await service.CheckAccessAsync("tenant-a", "user-1", TenantAccessRequirement.Write);

        // Assert
        decision.IsAllowed.ShouldBeTrue();
        decision.Reason.ShouldBe(TenantAccessDenialReason.None);
    }

    [Fact(Skip = SkipReason)]
    public async Task CheckAccessAsync_GivenJwtTenantClaimButNoMembershipApplied_DeniesAsMissingMemberAsync()
    {
        // Arrange — tenant exists and is active, but the calling user has never been added.
        InMemoryTenantService tenants = new();
        TenantTestHelpers.BootstrapGlobalAdmin(tenants, userId: "global-admin");
        TenantTestHelpers.CreateTenant(tenants, tenantId: "tenant-a");

        ITenantProjectionStore store = ProjectFromTenants(tenants);
        TenantAccessService service = new(store);

        // Act — caller presents a valid JWT tenant claim ("tenant-a") but is not a member.
        TenantAccessDecision decision = await service.CheckAccessAsync("tenant-a", "user-without-membership", TenantAccessRequirement.Read);

        // Assert — JWT claim alone never authorizes; Tenants membership is the source of truth.
        decision.IsAllowed.ShouldBeFalse();
        decision.Reason.ShouldBe(TenantAccessDenialReason.MissingMember);
    }

    [Fact(Skip = SkipReason)]
    public async Task CheckAccessAsync_AfterUserRemovedFromTenantEventApplied_DeniesAsMissingMemberAsync()
    {
        // Arrange — owner is added, then removed via Tenants command.
        InMemoryTenantService tenants = new();
        TenantTestHelpers.CreateTenantWithOwner(tenants, tenantId: "tenant-a", ownerUserId: "user-1");
        DomainResult removeResult = tenants.ProcessCommand(
            new RemoveUserFromTenant("tenant-a", "user-1"),
            userId: "global-admin",
            isGlobalAdmin: true);
        removeResult.IsSuccessful.ShouldBeTrue();

        ITenantProjectionStore store = ProjectFromTenants(tenants);
        TenantAccessService service = new(store);

        // Act
        TenantAccessDecision decision = await service.CheckAccessAsync("tenant-a", "user-1", TenantAccessRequirement.Read);

        // Assert
        decision.IsAllowed.ShouldBeFalse();
        decision.Reason.ShouldBe(TenantAccessDenialReason.MissingMember);
    }

    [Fact(Skip = SkipReason)]
    public async Task CheckAccessAsync_AfterTenantDisabledEventApplied_DeniesAsDisabledTenantAsync()
    {
        // Arrange
        InMemoryTenantService tenants = new();
        TenantTestHelpers.CreateTenantWithOwner(tenants, tenantId: "tenant-a", ownerUserId: "user-1");
        DomainResult disableResult = tenants.ProcessCommand(
            new DisableTenant("tenant-a"),
            userId: "user-1",
            isGlobalAdmin: true);
        disableResult.IsSuccessful.ShouldBeTrue();

        ITenantProjectionStore store = ProjectFromTenants(tenants);
        TenantAccessService service = new(store);

        // Act
        TenantAccessDecision decision = await service.CheckAccessAsync("tenant-a", "user-1", TenantAccessRequirement.Read);

        // Assert
        decision.IsAllowed.ShouldBeFalse();
        decision.Reason.ShouldBe(TenantAccessDenialReason.DisabledTenant);
    }

    [Fact(Skip = SkipReason)]
    public async Task CheckAccessAsync_AfterUserRoleChangedToReader_DeniesWriteAsInsufficientRoleAsync()
    {
        // Arrange — owner downgraded to reader.
        InMemoryTenantService tenants = new();
        TenantTestHelpers.CreateTenantWithOwner(tenants, tenantId: "tenant-a", ownerUserId: "user-1");
        DomainResult roleChange = tenants.ProcessCommand(
            new ChangeUserRole("tenant-a", "user-1", TenantRole.TenantReader),
            userId: "user-1",
            isGlobalAdmin: true);
        roleChange.IsSuccessful.ShouldBeTrue();

        ITenantProjectionStore store = ProjectFromTenants(tenants);
        TenantAccessService service = new(store);

        // Act — write requirement against a reader role.
        TenantAccessDecision decision = await service.CheckAccessAsync("tenant-a", "user-1", TenantAccessRequirement.Write);

        // Assert
        decision.IsAllowed.ShouldBeFalse();
        decision.Reason.ShouldBe(TenantAccessDenialReason.InsufficientRole);
    }

    [Fact(Skip = SkipReason)]
    public async Task CheckAccessAsync_GivenStaleProjectionStore_DeniesAsTenantStateStaleAsync()
    {
        // Arrange — a projection store wrapper that signals staleness for the requested tenant.
        ITenantProjectionStore staleStore = new StaleSignalingTenantProjectionStore("tenant-a");
        TenantAccessService service = new(staleStore);

        // Act
        TenantAccessDecision decision = await service.CheckAccessAsync("tenant-a", "user-1", TenantAccessRequirement.Read);

        // Assert — local projection cannot honor the request without throwing tenant data away.
        decision.IsAllowed.ShouldBeFalse();
        decision.Reason.ShouldBe(TenantAccessDenialReason.TenantStateStale);
    }

    /// <summary>
    /// Helper bridge expected to be added by Story 11.4 implementation. It must:
    ///   1. Drain events from <paramref name="tenants"/> (or replay through <see cref="InMemoryTenantProjection"/>);
    ///   2. Translate the resulting Tenants read-model state into a populated <see cref="ITenantProjectionStore"/>
    ///      consumed by <see cref="TenantAccessService"/>;
    ///   3. Stay sidecar-free and synchronous so unit tests run sub-100ms.
    ///
    /// Until that bridge exists, this method intentionally throws to keep the tests in the red phase.
    /// </summary>
    private static ITenantProjectionStore ProjectFromTenants(InMemoryTenantService tenants)
        => throw new NotImplementedException(
            "Story 11.4: implement the Tenants→Parties projection bridge using public Hexalith.Tenants.Testing helpers.");

    private sealed class StaleSignalingTenantProjectionStore(string staleTenantId) : ITenantProjectionStore
    {
        private readonly string _staleTenantId = staleTenantId;

        public Task<TenantLocalState?> GetAsync(string tenantId, CancellationToken cancellationToken = default)
            => string.Equals(tenantId, _staleTenantId, StringComparison.Ordinal)
                ? throw new InvalidOperationException("Local Tenants projection is stale for this tenant.")
                : Task.FromResult<TenantLocalState?>(null);

        public Task SaveAsync(TenantLocalState state, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
