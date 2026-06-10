using Hexalith.Parties.AdminPortal.Services;
using Hexalith.Parties.UI.IdentityBinding;

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.UI.Tests;

public sealed class IdentityBindingProvisioningServiceTests
{
    [Fact]
    public async Task LinkAsync_AuthorizedOperator_CreatesAuditRecordAndSetsIdpPartyIdAsync()
    {
        IdentityBindingTestHost host = CreateHost();

        IdentityBindingOperationResult result = await host.Service.LinkAsync(CreateRequest());

        result.Succeeded.ShouldBeTrue();
        result.Code.ShouldBe("Linked");
        result.Binding.ShouldNotBeNull();
        result.Binding.Status.ShouldBe(IdentityBindingStatus.Active);
        result.Binding.PartyId.ShouldBe("party-bound-001");
        result.Binding.Version.ShouldBe(1);
        result.Binding.AuditTrail.ShouldHaveSingleItem().Action.ShouldBe("Linked");

        IReadOnlyList<string> idpPartyIds = await host.IdpClient.GetPartyIdsAsync(DefaultKey());
        idpPartyIds.ShouldBe(["party-bound-001"]);
    }

    [Fact]
    public async Task LinkAsync_ExistingActiveBinding_RejectsDuplicateAndLeavesIdpAttributeUnchangedAsync()
    {
        IdentityBindingTestHost host = CreateHost();
        await host.Service.LinkAsync(CreateRequest());

        IdentityBindingOperationResult result = await host.Service.LinkAsync(CreateRequest() with { PartyId = "party-other-001" });

        result.Succeeded.ShouldBeFalse();
        result.Code.ShouldBe("DuplicateActiveBinding");

        IReadOnlyList<string> idpPartyIds = await host.IdpClient.GetPartyIdsAsync(DefaultKey());
        idpPartyIds.ShouldBe(["party-bound-001"]);
    }

    [Fact]
    public async Task RotateAsync_ExpectedVersionMatches_SupersedesActivePartyIdAndIncrementsVersionAsync()
    {
        IdentityBindingTestHost host = CreateHost();
        await host.Service.LinkAsync(CreateRequest());

        IdentityBindingOperationResult result = await host.Service.RotateAsync(new RotateIdentityBindingRequest(
            "tenant-a",
            "https://idp.example.test/realms/hexalith",
            "idp-subject-001",
            "party-rotated-001",
            "operator-001",
            "verify-002",
            "manual-rotation",
            ExpectedVersion: 1));

        result.Succeeded.ShouldBeTrue();
        result.Code.ShouldBe("Rotated");
        result.Binding.ShouldNotBeNull();
        result.Binding.PartyId.ShouldBe("party-rotated-001");
        result.Binding.Version.ShouldBe(2);
        result.Binding.AuditTrail.Count.ShouldBe(2);
        result.Binding.AuditTrail[0].PartyId.ShouldBe("party-bound-001");
        result.Binding.AuditTrail[1].Action.ShouldBe("Rotated");

        IReadOnlyList<string> idpPartyIds = await host.IdpClient.GetPartyIdsAsync(DefaultKey());
        idpPartyIds.ShouldBe(["party-rotated-001"]);
    }

    [Fact]
    public async Task RotateAsync_StaleExpectedVersion_FailsClosedWithoutChangingIdpAttributeAsync()
    {
        IdentityBindingTestHost host = CreateHost();
        await host.Service.LinkAsync(CreateRequest());

        IdentityBindingOperationResult result = await host.Service.RotateAsync(new RotateIdentityBindingRequest(
            "tenant-a",
            "https://idp.example.test/realms/hexalith",
            "idp-subject-001",
            "party-rotated-001",
            "operator-001",
            "verify-002",
            "manual-rotation",
            ExpectedVersion: 0));

        result.Succeeded.ShouldBeFalse();
        result.Code.ShouldBe("VersionConflict");

        IReadOnlyList<string> idpPartyIds = await host.IdpClient.GetPartyIdsAsync(DefaultKey());
        idpPartyIds.ShouldBe(["party-bound-001"]);
    }

    [Fact]
    public async Task SuspendAsync_ActiveBinding_ClearsIdpAttributeAndRetainsAuditHistoryAsync()
    {
        IdentityBindingTestHost host = CreateHost();
        await host.Service.LinkAsync(CreateRequest());

        IdentityBindingOperationResult result = await host.Service.SuspendAsync(ChangeRequest(expectedVersion: 1));

        result.Succeeded.ShouldBeTrue();
        result.Code.ShouldBe("Suspended");
        result.Binding.ShouldNotBeNull();
        result.Binding.Status.ShouldBe(IdentityBindingStatus.Suspended);
        result.Binding.PartyId.ShouldBeNull();
        result.Binding.Version.ShouldBe(2);
        result.Binding.AuditTrail.Count.ShouldBe(2);
        result.Binding.AuditTrail[0].PartyId.ShouldBe("party-bound-001");
        result.Binding.AuditTrail[1].Action.ShouldBe("Suspended");

        IReadOnlyList<string> idpPartyIds = await host.IdpClient.GetPartyIdsAsync(DefaultKey());
        idpPartyIds.ShouldBeEmpty();
    }

    [Fact]
    public async Task RemoveAsync_ActiveBinding_ClearsIdpAttributeAndMarksRemovedAsync()
    {
        IdentityBindingTestHost host = CreateHost();
        await host.Service.LinkAsync(CreateRequest());

        IdentityBindingOperationResult result = await host.Service.RemoveAsync(ChangeRequest(expectedVersion: 1));

        result.Succeeded.ShouldBeTrue();
        result.Code.ShouldBe("Removed");
        result.Binding.ShouldNotBeNull();
        result.Binding.Status.ShouldBe(IdentityBindingStatus.Removed);
        result.Binding.PartyId.ShouldBeNull();

        IReadOnlyList<string> idpPartyIds = await host.IdpClient.GetPartyIdsAsync(DefaultKey());
        idpPartyIds.ShouldBeEmpty();
    }

    [Fact]
    public async Task LinkAsync_UnauthorizedOperator_DeniesWithoutStoreOrIdpMutationAsync()
    {
        IdentityBindingTestHost host = CreateHost(isAuthorized: false);

        IdentityBindingOperationResult result = await host.Service.LinkAsync(CreateRequest());

        result.Succeeded.ShouldBeFalse();
        result.Code.ShouldBe("UnauthorizedOperator");

        IdentityBindingRecord? stored = await host.Store.GetAsync(DefaultKey());
        stored.ShouldBeNull();

        IReadOnlyList<string> idpPartyIds = await host.IdpClient.GetPartyIdsAsync(DefaultKey());
        idpPartyIds.ShouldBeEmpty();
    }

    [Fact]
    public async Task ReconcileAsync_UnauthorizedOperator_DeniesWithoutReadingBindingStateAsync()
    {
        IdentityBindingTestHost host = CreateHost(isAuthorized: false);

        IdentityBindingOperationResult result = await host.Service.ReconcileAsync(new ReconcileIdentityBindingRequest(
            "tenant-a",
            "https://idp.example.test/realms/hexalith",
            "idp-subject-001"));

        result.Succeeded.ShouldBeFalse();
        result.Code.ShouldBe("UnauthorizedOperator");
        result.Drift.ShouldBeNull();
    }

    [Fact]
    public async Task ReconcileAsync_StoreAndIdpAttributeDiverge_ReportsDriftWithoutGrantingRuntimeAccessAsync()
    {
        IdentityBindingTestHost host = CreateHost();
        await host.Service.LinkAsync(CreateRequest());
        await host.IdpClient.ClearPartyIdAsync(DefaultKey());

        IdentityBindingOperationResult result = await host.Service.ReconcileAsync(new ReconcileIdentityBindingRequest(
            "tenant-a",
            "https://idp.example.test/realms/hexalith",
            "idp-subject-001"));

        result.Succeeded.ShouldBeTrue();
        result.Code.ShouldBe("DriftDetected");
        result.Drift.ShouldNotBeNull();
        result.Drift.HasDrift.ShouldBeTrue();
        result.Drift.StoreStatus.ShouldBe("Active");
        result.Drift.IdpAttributeShape.ShouldBe("Missing");
    }

    [Fact]
    public async Task ReconcileAsync_MissingStoreAndIdpAttribute_ReportsInSyncAsync()
    {
        IdentityBindingTestHost host = CreateHost();

        IdentityBindingOperationResult result = await host.Service.ReconcileAsync(new ReconcileIdentityBindingRequest(
            "tenant-a",
            "https://idp.example.test/realms/hexalith",
            "idp-subject-001"));

        result.Succeeded.ShouldBeTrue();
        result.Code.ShouldBe("InSync");
        result.Drift.ShouldNotBeNull();
        result.Drift.HasDrift.ShouldBeFalse();
        result.Drift.StoreStatus.ShouldBe("Missing");
        result.Drift.IdpAttributeShape.ShouldBe("Missing");
    }

    [Fact]
    public async Task LinkAsync_UnboundedAuditMetadata_FailsClosedAsync()
    {
        IdentityBindingTestHost host = CreateHost();
        string unboundedReason = new('x', 129);

        IdentityBindingOperationResult result = await host.Service.LinkAsync(CreateRequest() with { ReasonCode = unboundedReason });

        result.Succeeded.ShouldBeFalse();
        result.Code.ShouldBe("InvalidAuditMetadata");
    }

    [Fact]
    public async Task LinkAsync_IdpAttributeWriteFails_RollsBackNewAuditRecordAsync()
    {
        var store = new InMemoryIdentityBindingStore();
        IIdentityProviderPartyAttributeClient idpClient = new FailingIdentityProviderPartyAttributeClient(failSet: true);
        var service = new IdentityBindingProvisioningService(
            store,
            idpClient,
            AuthorizedOperator(),
            TimeProvider.System);

        IdentityBindingOperationResult result = await service.LinkAsync(CreateRequest());

        result.Succeeded.ShouldBeFalse();
        result.Code.ShouldBe("IdpAttributeUpdateFailed");

        IdentityBindingRecord? stored = await store.GetAsync(DefaultKey());
        stored.ShouldBeNull();
    }

    [Fact]
    public async Task SuspendAsync_IdpAttributeClearFails_RestoresActiveAuditRecordAsync()
    {
        IdentityBindingTestHost host = CreateHost();
        await host.Service.LinkAsync(CreateRequest());
        var service = new IdentityBindingProvisioningService(
            host.Store,
            new FailingIdentityProviderPartyAttributeClient(failClear: true),
            AuthorizedOperator(),
            TimeProvider.System);

        IdentityBindingOperationResult result = await service.SuspendAsync(ChangeRequest(expectedVersion: 1));

        result.Succeeded.ShouldBeFalse();
        result.Code.ShouldBe("IdpAttributeUpdateFailed");

        IdentityBindingRecord? stored = await host.Store.GetAsync(DefaultKey());
        stored.ShouldNotBeNull();
        stored.Status.ShouldBe(IdentityBindingStatus.Active);
        stored.PartyId.ShouldBe("party-bound-001");
        stored.Version.ShouldBe(1);
        stored.AuditTrail.ShouldHaveSingleItem().Action.ShouldBe("Linked");
    }

    [Fact]
    public async Task LinkAsync_PreviouslySuspendedBinding_RetainsHistoricalAuditTrailAsync()
    {
        IdentityBindingTestHost host = CreateHost();
        await host.Service.LinkAsync(CreateRequest());
        await host.Service.SuspendAsync(ChangeRequest(expectedVersion: 1));

        IdentityBindingOperationResult result = await host.Service.LinkAsync(CreateRequest() with
        {
            PartyId = "party-relinked-001",
            VerificationReference = "verify-004",
            ReasonCode = "relinked-after-review",
        });

        result.Succeeded.ShouldBeTrue();
        result.Binding.ShouldNotBeNull();
        result.Binding.Status.ShouldBe(IdentityBindingStatus.Active);
        result.Binding.PartyId.ShouldBe("party-relinked-001");
        result.Binding.Version.ShouldBe(3);
        result.Binding.AuditTrail.Select(static entry => entry.Action).ShouldBe(["Linked", "Suspended", "Linked"]);
        result.Binding.AuditTrail[0].PartyId.ShouldBe("party-bound-001");
        result.Binding.AuditTrail[1].PartyId.ShouldBeNull();
        result.Binding.AuditTrail[2].PartyId.ShouldBe("party-relinked-001");
    }

    private static IdentityBindingTestHost CreateHost(bool isAuthorized = true)
    {
        var store = new InMemoryIdentityBindingStore();
        var idpClient = new InMemoryIdentityProviderPartyAttributeClient();
        IAdminPortalAuthorizationService authorization = isAuthorized
            ? AuthorizedOperator()
            : UnauthorizedOperator();
        var service = new IdentityBindingProvisioningService(store, idpClient, authorization, TimeProvider.System);
        return new(service, store, idpClient);
    }

    private static IAdminPortalAuthorizationService AuthorizedOperator()
    {
        IAdminPortalAuthorizationService authorization = Substitute.For<IAdminPortalAuthorizationService>();
        authorization.GetAuthorizationStateAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new AdminPortalAuthorizationState(
                IsAuthenticated: true,
                HasTenantContext: true,
                IsAdmin: true,
                ContextSignature: "tenant:tenant-a:user:operator-001:admin")));
        return authorization;
    }

    private static IAdminPortalAuthorizationService UnauthorizedOperator()
    {
        IAdminPortalAuthorizationService authorization = Substitute.For<IAdminPortalAuthorizationService>();
        authorization.GetAuthorizationStateAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new AdminPortalAuthorizationState(
                IsAuthenticated: true,
                HasTenantContext: true,
                IsAdmin: false,
                ContextSignature: "tenant:tenant-a:user:operator-001:not-admin")));
        return authorization;
    }

    private static CreateIdentityBindingRequest CreateRequest()
        => new(
            "tenant-a",
            "https://idp.example.test/realms/hexalith",
            "idp-subject-001",
            "party-bound-001",
            "operator-001",
            "verify-001",
            "verified-admin-link");

    private static ChangeIdentityBindingStatusRequest ChangeRequest(long expectedVersion)
        => new(
            "tenant-a",
            "https://idp.example.test/realms/hexalith",
            "idp-subject-001",
            "operator-001",
            "verify-003",
            "operator-request",
            expectedVersion);

    private static IdentityBindingKey DefaultKey()
        => new("tenant-a", "https://idp.example.test/realms/hexalith", "idp-subject-001");

    private sealed record IdentityBindingTestHost(
        IIdentityBindingProvisioningService Service,
        IIdentityBindingStore Store,
        IIdentityProviderPartyAttributeClient IdpClient);

    private sealed class FailingIdentityProviderPartyAttributeClient(
        bool failSet = false,
        bool failClear = false) : IIdentityProviderPartyAttributeClient
    {
        public Task SetPartyIdAsync(
            IdentityBindingKey key,
            string partyId,
            CancellationToken cancellationToken = default)
            => failSet
                ? throw new InvalidOperationException("IdP attribute set failed.")
                : Task.CompletedTask;

        public Task ClearPartyIdAsync(
            IdentityBindingKey key,
            CancellationToken cancellationToken = default)
            => failClear
                ? throw new InvalidOperationException("IdP attribute clear failed.")
                : Task.CompletedTask;

        public Task<IReadOnlyList<string>> GetPartyIdsAsync(
            IdentityBindingKey key,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>([]);
    }
}
