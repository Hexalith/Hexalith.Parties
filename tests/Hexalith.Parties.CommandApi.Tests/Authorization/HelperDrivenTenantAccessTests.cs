using Hexalith.EventStore.Contracts.Results;
using Hexalith.Parties.CommandApi.Authorization;
using Hexalith.Tenants.Client.Projections;
using Hexalith.Tenants.Contracts.Commands;
using Hexalith.Tenants.Contracts.Enums;
using Hexalith.Tenants.Testing.Fakes;
using Hexalith.Tenants.Testing.Helpers;
using Hexalith.Tenants.Testing.Projections;

using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

namespace Hexalith.Parties.CommandApi.Tests.Authorization;

/// <summary>
/// Story 11.4 — AC1, AC2: Fast tenant-access decisions wired through
/// the public Hexalith.Tenants.Testing helpers, not by hand-rolling
/// <see cref="TenantLocalState"/> directly.
/// </summary>
public sealed class HelperDrivenTenantAccessTests
{
    [Fact]
    public async Task CheckAccessAsync_GivenTenantSeededViaHelpers_AllowsOwnerWriteAsync()
    {
        // Arrange — drive state through Tenants public helpers only.
        InMemoryTenantService tenants = new();
        TenantTestHelpers.CreateTenantWithOwner(tenants, tenantId: "tenant-a", ownerUserId: "user-1");

        ITenantProjectionStore store = await ProjectFromTenantsAsync(tenants);
        TenantAccessService service = new(store, NullLogger<TenantAccessService>.Instance);

        // Act
        TenantAccessDecision decision = await service.CheckAccessAsync("tenant-a", "user-1", TenantAccessRequirement.Write);

        // Assert
        decision.IsAllowed.ShouldBeTrue();
        decision.Reason.ShouldBe(TenantAccessDenialReason.None);
    }

    [Fact]
    public async Task CheckAccessAsync_GivenJwtTenantClaimButNoMembershipApplied_DeniesAsMissingMemberAsync()
    {
        // Arrange — tenant exists and is active, but the calling user has never been added.
        InMemoryTenantService tenants = new();
        TenantTestHelpers.BootstrapGlobalAdmin(tenants, userId: "global-admin");
        TenantTestHelpers.CreateTenant(tenants, tenantId: "tenant-a");

        ITenantProjectionStore store = await ProjectFromTenantsAsync(tenants);
        TenantAccessService service = new(store, NullLogger<TenantAccessService>.Instance);

        // Act — caller presents a valid JWT tenant claim ("tenant-a") but is not a member.
        TenantAccessDecision decision = await service.CheckAccessAsync("tenant-a", "user-without-membership", TenantAccessRequirement.Read);

        // Assert — JWT claim alone never authorizes; Tenants membership is the source of truth.
        decision.IsAllowed.ShouldBeFalse();
        decision.Reason.ShouldBe(TenantAccessDenialReason.MissingMember);
    }

    [Fact]
    public async Task CheckAccessAsync_AfterUserRemovedFromTenantEventApplied_DeniesAsMissingMemberAsync()
    {
        // Arrange — owner is added, then removed via Tenants command.
        InMemoryTenantService tenants = new();
        TenantTestHelpers.CreateTenantWithOwner(tenants, tenantId: "tenant-a", ownerUserId: "user-1");
        DomainResult removeResult = tenants.ProcessCommand(
            new RemoveUserFromTenant("tenant-a", "user-1"),
            userId: "global-admin",
            isGlobalAdmin: true);
        removeResult.IsSuccess.ShouldBeTrue();

        ITenantProjectionStore store = await ProjectFromTenantsAsync(tenants);
        TenantAccessService service = new(store, NullLogger<TenantAccessService>.Instance);

        // Act
        TenantAccessDecision decision = await service.CheckAccessAsync("tenant-a", "user-1", TenantAccessRequirement.Read);

        // Assert
        decision.IsAllowed.ShouldBeFalse();
        decision.Reason.ShouldBe(TenantAccessDenialReason.MissingMember);
    }

    [Fact]
    public async Task CheckAccessAsync_AfterTenantDisabledEventApplied_DeniesAsDisabledTenantAsync()
    {
        // Arrange
        InMemoryTenantService tenants = new();
        TenantTestHelpers.CreateTenantWithOwner(tenants, tenantId: "tenant-a", ownerUserId: "user-1");
        DomainResult disableResult = tenants.ProcessCommand(new DisableTenant("tenant-a"));
        disableResult.IsSuccess.ShouldBeTrue();

        ITenantProjectionStore store = await ProjectFromTenantsAsync(tenants);
        TenantAccessService service = new(store, NullLogger<TenantAccessService>.Instance);

        // Act
        TenantAccessDecision decision = await service.CheckAccessAsync("tenant-a", "user-1", TenantAccessRequirement.Read);

        // Assert
        decision.IsAllowed.ShouldBeFalse();
        decision.Reason.ShouldBe(TenantAccessDenialReason.DisabledTenant);
    }

    [Fact]
    public async Task CheckAccessAsync_AfterUserRoleChangedToReader_DeniesWriteAsInsufficientRoleAsync()
    {
        // Arrange — owner downgraded to reader.
        InMemoryTenantService tenants = new();
        TenantTestHelpers.CreateTenantWithOwner(tenants, tenantId: "tenant-a", ownerUserId: "user-1");
        DomainResult roleChange = tenants.ProcessCommand(
            new ChangeUserRole("tenant-a", "user-1", TenantRole.TenantReader),
            userId: "user-1",
            isGlobalAdmin: true);
        roleChange.IsSuccess.ShouldBeTrue();

        ITenantProjectionStore store = await ProjectFromTenantsAsync(tenants);
        TenantAccessService service = new(store, NullLogger<TenantAccessService>.Instance);

        // Act — write requirement against a reader role.
        TenantAccessDecision decision = await service.CheckAccessAsync("tenant-a", "user-1", TenantAccessRequirement.Write);

        // Assert
        decision.IsAllowed.ShouldBeFalse();
        decision.Reason.ShouldBe(TenantAccessDenialReason.InsufficientRole);
    }

    [Fact]
    public async Task CheckAccessAsync_GivenStaleProjectionStore_DeniesAsTenantStateStaleAsync()
    {
        // Arrange — a projection store wrapper that signals staleness for the requested tenant.
        ITenantProjectionStore staleStore = new StaleSignalingTenantProjectionStore("tenant-a");
        TenantAccessService service = new(staleStore, NullLogger<TenantAccessService>.Instance);

        // Act
        TenantAccessDecision decision = await service.CheckAccessAsync("tenant-a", "user-1", TenantAccessRequirement.Read);

        // Assert — local projection cannot honor the request without throwing tenant data away.
        decision.IsAllowed.ShouldBeFalse();
        decision.Reason.ShouldBe(TenantAccessDenialReason.TenantStateStale);
    }

    /// <summary>
    /// Replays public Tenants testing events through <see cref="InMemoryTenantProjection"/>
    /// and copies the resulting read model into the Parties access seam.
    /// </summary>
    private static async Task<ITenantProjectionStore> ProjectFromTenantsAsync(InMemoryTenantService tenants)
    {
        ArgumentNullException.ThrowIfNull(tenants);

        InMemoryTenantProjection projection = new();
        projection.ApplyEvents(tenants.EventHistory);

        InMemoryTenantProjectionStore store = new();
        foreach (var tenant in projection.GetAllTenants())
        {
            TenantLocalState state = new()
            {
                TenantId = tenant.TenantId,
                Name = tenant.Name,
                Description = tenant.Description,
                Status = tenant.Status,
            };
            foreach (KeyValuePair<string, TenantRole> member in tenant.Members)
            {
                state.Members[member.Key] = member.Value;
            }

            foreach (KeyValuePair<string, string> configuration in tenant.Configuration)
            {
                state.Configuration[configuration.Key] = configuration.Value;
            }

            await store.SaveAsync(state).ConfigureAwait(false);
        }

        return store;
    }

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
