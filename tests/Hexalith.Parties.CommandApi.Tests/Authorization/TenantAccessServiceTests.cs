using Hexalith.Parties.CommandApi.Authorization;
using Hexalith.Tenants.Client.Projections;
using Hexalith.Tenants.Client.Registration;
using Hexalith.Tenants.Client.Subscription;
using Hexalith.Tenants.Contracts.Enums;
using Hexalith.Tenants.Contracts.Events;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using System.Text.Json;

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
        TenantAccessService service = new(store, NullLogger<TenantAccessService>.Instance);

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
        TenantAccessService service = new(new InMemoryTenantProjectionStore(), NullLogger<TenantAccessService>.Instance);

        TenantAccessDecision decision = await service.CheckAccessAsync(tenantId, userId, TenantAccessRequirement.Read);

        decision.IsAllowed.ShouldBeFalse();
        decision.Reason.ShouldBe(expectedReason);
    }

    [Fact]
    public async Task CheckAccessAsyncFailsClosedWhenTenantProjectionIsMissing() {
        TenantAccessService service = new(new InMemoryTenantProjectionStore(), NullLogger<TenantAccessService>.Instance);

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
        TenantAccessService service = new(store, NullLogger<TenantAccessService>.Instance);

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
        TenantAccessService service = new(store, NullLogger<TenantAccessService>.Instance);

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
        TenantAccessService service = new(store, NullLogger<TenantAccessService>.Instance);

        TenantAccessDecision decision = await service.CheckAccessAsync("tenant-1", "user-1", TenantAccessRequirement.Read);

        decision.IsAllowed.ShouldBeFalse();
        decision.Reason.ShouldBe(TenantAccessDenialReason.InsufficientRole);
    }

    [Fact]
    public async Task CheckAccessAsyncFailsClosedWhenProjectionStoreThrows() {
        TenantAccessService service = new(new ThrowingTenantProjectionStore(), NullLogger<TenantAccessService>.Instance);

        TenantAccessDecision decision = await service.CheckAccessAsync("tenant-1", "user-1", TenantAccessRequirement.Read);

        decision.IsAllowed.ShouldBeFalse();
        decision.Reason.ShouldBe(TenantAccessDenialReason.TenantStateStale);
        decision.DiagnosticText.ShouldBe("Tenant access state is unavailable.");
    }

    [Fact]
    public async Task CheckAccessAsyncPropagatesOperationCanceledExceptionFromProjectionStore() {
        // The projection-store catch filter intentionally lets OperationCanceledException
        // propagate so callers honor request cancellation. A future change to that filter
        // would silently turn cancellations into TenantStateStale denials — this test
        // pins that contract.
        TenantAccessService service = new(new CancellingTenantProjectionStore(), NullLogger<TenantAccessService>.Instance);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(() =>
            service.CheckAccessAsync("tenant-1", "user-1", TenantAccessRequirement.Read, cts.Token));
    }

    [Fact]
    public async Task CheckAccessAsyncDeniesAfterTenantDisabledEventIsProcessed() {
        // AC3 round-trip: tenant active and member allowed,
        // then TenantDisabled event flows through the Tenants client pipeline,
        // then the same access check fails closed with DisabledTenant.
        (TenantEventProcessor processor, ITenantProjectionStore store, ServiceProvider provider) = BuildTenantsPipeline();
        using (provider) {
            await processor.ProcessAsync(Envelope("m-1", new TenantCreated("tenant-1", "Tenant One", null, DateTimeOffset.UtcNow)));
            await processor.ProcessAsync(Envelope("m-2", new UserAddedToTenant("tenant-1", "user-1", TenantRole.TenantContributor)));
            TenantAccessService service = new(store, NullLogger<TenantAccessService>.Instance);

            (await service.CheckAccessAsync("tenant-1", "user-1", TenantAccessRequirement.Read))
                .IsAllowed.ShouldBeTrue();

            await processor.ProcessAsync(Envelope("m-3", new TenantDisabled("tenant-1", DateTimeOffset.UtcNow)));

            TenantAccessDecision decision = await service.CheckAccessAsync("tenant-1", "user-1", TenantAccessRequirement.Read);
            decision.IsAllowed.ShouldBeFalse();
            decision.Reason.ShouldBe(TenantAccessDenialReason.DisabledTenant);
        }
    }

    [Fact]
    public async Task CheckAccessAsyncDeniesAfterUserRemovedFromTenantEventIsProcessed() {
        // AC3 round-trip: user added then removed via Tenants events,
        // access check after removal fails closed with MissingMember.
        (TenantEventProcessor processor, ITenantProjectionStore store, ServiceProvider provider) = BuildTenantsPipeline();
        using (provider) {
            await processor.ProcessAsync(Envelope("m-1", new TenantCreated("tenant-1", "Tenant One", null, DateTimeOffset.UtcNow)));
            await processor.ProcessAsync(Envelope("m-2", new UserAddedToTenant("tenant-1", "user-1", TenantRole.TenantContributor)));
            TenantAccessService service = new(store, NullLogger<TenantAccessService>.Instance);

            (await service.CheckAccessAsync("tenant-1", "user-1", TenantAccessRequirement.Read))
                .IsAllowed.ShouldBeTrue();

            await processor.ProcessAsync(Envelope("m-3", new UserRemovedFromTenant("tenant-1", "user-1")));

            TenantAccessDecision decision = await service.CheckAccessAsync("tenant-1", "user-1", TenantAccessRequirement.Read);
            decision.IsAllowed.ShouldBeFalse();
            decision.Reason.ShouldBe(TenantAccessDenialReason.MissingMember);
        }
    }

    private static (TenantEventProcessor Processor, ITenantProjectionStore Store, ServiceProvider Provider) BuildTenantsPipeline() {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddHexalithTenants();
        ServiceProvider provider = services.BuildServiceProvider();
        return (
            provider.GetRequiredService<TenantEventProcessor>(),
            provider.GetRequiredService<ITenantProjectionStore>(),
            provider);
    }

    private static TenantEventEnvelope Envelope<TEvent>(string messageId, TEvent @event)
        => new(
            messageId,
            "tenant-1",
            "system",
            typeof(TEvent).FullName!,
            1,
            DateTimeOffset.UtcNow,
            "correlation-1",
            "json",
            JsonSerializer.SerializeToUtf8Bytes(@event));

    private sealed class ThrowingTenantProjectionStore : ITenantProjectionStore {
        public Task<TenantLocalState?> GetAsync(string tenantId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Projection store unavailable.");

        public Task SaveAsync(TenantLocalState state, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class CancellingTenantProjectionStore : ITenantProjectionStore {
        public Task<TenantLocalState?> GetAsync(string tenantId, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<TenantLocalState?>(null);
        }

        public Task SaveAsync(TenantLocalState state, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
