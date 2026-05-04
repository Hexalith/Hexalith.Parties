using Hexalith.Parties.CommandApi.Authorization;
using Hexalith.Tenants.Client.Projections;
using Hexalith.Tenants.Contracts.Enums;

using Shouldly;

namespace Hexalith.Parties.CommandApi.Tests.Authorization;

public class TenantAccessServiceTests {
    [Theory]
    [InlineData(TenantRole.TenantReader, TenantAccessRequirement.Read, true)]
    [InlineData(TenantRole.TenantReader, TenantAccessRequirement.Write, false)]
    [InlineData(TenantRole.TenantReader, TenantAccessRequirement.Admin, false)]
    [InlineData(TenantRole.TenantContributor, TenantAccessRequirement.Read, true)]
    [InlineData(TenantRole.TenantContributor, TenantAccessRequirement.Write, true)]
    [InlineData(TenantRole.TenantContributor, TenantAccessRequirement.Admin, false)]
    [InlineData(TenantRole.TenantOwner, TenantAccessRequirement.Read, true)]
    [InlineData(TenantRole.TenantOwner, TenantAccessRequirement.Write, true)]
    [InlineData(TenantRole.TenantOwner, TenantAccessRequirement.Admin, true)]
    public async Task CheckAccessAsyncMapsTenantRolesToPartiesRequirements(
        TenantRole role,
        TenantAccessRequirement requirement,
        bool expectedAllowed) {
        InMemoryTenantProjectionStore store = new();
        await store.SaveAsync(new TenantLocalState
        {
            TenantId = "tenant-1",
            Status = TenantStatus.Active,
            Members = { ["user-1"] = role },
        });
        TenantAccessService service = new(store);

        TenantAccessDecision decision = await service.CheckAccessAsync("tenant-1", "user-1", requirement);

        decision.IsAllowed.ShouldBe(expectedAllowed);
        decision.Reason.ShouldBe(expectedAllowed
            ? TenantAccessDenialReason.None
            : TenantAccessDenialReason.InsufficientRole);
    }

    [Theory]
    [InlineData(null, "user-1", TenantAccessDenialReason.MissingTenantId)]
    [InlineData("", "user-1", TenantAccessDenialReason.MissingTenantId)]
    [InlineData("tenant-1", null, TenantAccessDenialReason.MissingUserId)]
    [InlineData("tenant-1", "", TenantAccessDenialReason.MissingUserId)]
    public async Task CheckAccessAsyncFailsClosedWhenInputIdentityIsMissing(
        string? tenantId,
        string? userId,
        TenantAccessDenialReason expectedReason) {
        TenantAccessService service = new(new InMemoryTenantProjectionStore());

        TenantAccessDecision decision = await service.CheckAccessAsync(tenantId, userId, TenantAccessRequirement.Read);

        decision.IsAllowed.ShouldBeFalse();
        decision.Reason.ShouldBe(expectedReason);
    }

    [Fact]
    public async Task CheckAccessAsyncFailsClosedWhenTenantProjectionIsMissing() {
        TenantAccessService service = new(new InMemoryTenantProjectionStore());

        TenantAccessDecision decision = await service.CheckAccessAsync("tenant-1", "user-1", TenantAccessRequirement.Read);

        decision.IsAllowed.ShouldBeFalse();
        decision.Reason.ShouldBe(TenantAccessDenialReason.UnknownTenant);
    }

    [Fact]
    public async Task CheckAccessAsyncFailsClosedWhenTenantIsDisabled() {
        InMemoryTenantProjectionStore store = new();
        await store.SaveAsync(new TenantLocalState
        {
            TenantId = "tenant-1",
            Status = TenantStatus.Disabled,
            Members = { ["user-1"] = TenantRole.TenantOwner },
        });
        TenantAccessService service = new(store);

        TenantAccessDecision decision = await service.CheckAccessAsync("tenant-1", "user-1", TenantAccessRequirement.Read);

        decision.IsAllowed.ShouldBeFalse();
        decision.Reason.ShouldBe(TenantAccessDenialReason.DisabledTenant);
    }

    [Fact]
    public async Task CheckAccessAsyncFailsClosedWhenUserIsNotTenantMember() {
        InMemoryTenantProjectionStore store = new();
        await store.SaveAsync(new TenantLocalState
        {
            TenantId = "tenant-1",
            Status = TenantStatus.Active,
            Members = { ["other-user"] = TenantRole.TenantOwner },
        });
        TenantAccessService service = new(store);

        TenantAccessDecision decision = await service.CheckAccessAsync("tenant-1", "user-1", TenantAccessRequirement.Read);

        decision.IsAllowed.ShouldBeFalse();
        decision.Reason.ShouldBe(TenantAccessDenialReason.MissingMember);
    }

    [Fact]
    public async Task CheckAccessAsyncFailsClosedForUnmappedTenantRoleValue() {
        InMemoryTenantProjectionStore store = new();
        await store.SaveAsync(new TenantLocalState
        {
            TenantId = "tenant-1",
            Status = TenantStatus.Active,
            Members = { ["user-1"] = (TenantRole)999 },
        });
        TenantAccessService service = new(store);

        TenantAccessDecision decision = await service.CheckAccessAsync("tenant-1", "user-1", TenantAccessRequirement.Read);

        decision.IsAllowed.ShouldBeFalse();
        decision.Reason.ShouldBe(TenantAccessDenialReason.InsufficientRole);
    }

    [Fact]
    public async Task CheckAccessAsyncFailsClosedWhenProjectionStoreThrows() {
        TenantAccessService service = new(new ThrowingTenantProjectionStore());

        TenantAccessDecision decision = await service.CheckAccessAsync("tenant-1", "user-1", TenantAccessRequirement.Read);

        decision.IsAllowed.ShouldBeFalse();
        decision.Reason.ShouldBe(TenantAccessDenialReason.TenantStateStale);
        decision.DiagnosticText.ShouldBe("Tenant access state is unavailable.");
    }

    private sealed class ThrowingTenantProjectionStore : ITenantProjectionStore {
        public Task<TenantLocalState?> GetAsync(string tenantId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Projection store unavailable.");

        public Task SaveAsync(TenantLocalState state, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
