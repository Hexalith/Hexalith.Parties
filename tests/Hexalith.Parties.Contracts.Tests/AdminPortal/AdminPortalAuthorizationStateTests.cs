// Story 10.1 + 10.1.1 — Admin Portal AC4/AC7 fitness tests.
//
// AC4 + AC7: missing token, missing tenant claim, missing admin role, and tenant switches
// must all clear visible browse/search/detail state and discard in-flight responses from
// the previous tenant. These checks now assert OBSERVABLE BEHAVIOR (state transitions,
// scope cancellation) rather than mere type/method presence so a future refactor cannot
// regress to dead-code scaffolding while still passing the suite.

using System.Linq;
using System.Reflection;

using Hexalith.Parties.AdminPortal.Extensions;
using Hexalith.Parties.AdminPortal.Services;

using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.Contracts.Tests.AdminPortal;

/// <summary>
/// Story 10.1 + 10.1.1 — AC4 + AC7. Behavioral fitness checks for the admin portal state
/// coordinator and per-circuit query service that drive the live component lifecycle.
/// </summary>
public sealed class AdminPortalAuthorizationStateTests
{
    private static readonly string[] _requiredStateNames =
    [
        "Loading",
        "ReadyEmpty",
        "ReadyHasResults",
        "MissingToken",
        "MissingTenant",
        "Forbidden",
        "NotFound",
        "Gone",
        "DegradedSearch",
        "TransientFailure",
    ];

    [Fact]
    public void AdminPortal_DefinesDistinguishableAuthorizationStates()
    {
        // AC7: missing token / missing tenant / missing admin role must each be a
        // distinguishable UI copy state, not collapsed into a single "unauthorized" bucket.
        IEnumerable<string> declared = Enum.GetNames(typeof(AdminPortalListState));
        foreach (string required in _requiredStateNames)
        {
            declared.ShouldContain(required,
                $"AdminPortalListState is missing required state '{required}' (AC4/AC7).");
        }
    }

    [Fact]
    public void AdminPortal_PartiesAdminListCoordinator_ExposesObservableStateTransitions()
    {
        // AC4: the coordinator must support real state transitions so the live component can
        // drive it (no longer dead-code scaffolding). Behavior assertion: instantiate the
        // coordinator, transition to ReadyHasResults, and assert the public State surface
        // reflects it; ResetForTenantSwitch must return State to Loading and bump Version
        // so subscribers can discard stale in-flight work.
        var coordinator = new PartiesAdminListCoordinator();

        coordinator.State.ShouldBe(AdminPortalListState.Loading,
            "Coordinator must start in Loading.");

        coordinator.Transition(AdminPortalListState.ReadyHasResults);
        coordinator.State.ShouldBe(AdminPortalListState.ReadyHasResults,
            "Transition(ReadyHasResults) must update the public State surface.");

        long versionBeforeReset = coordinator.Version;
        coordinator.ResetForTenantSwitch();

        coordinator.State.ShouldBe(AdminPortalListState.Loading,
            "ResetForTenantSwitch must return State to Loading.");
        coordinator.Version.ShouldBeGreaterThan(versionBeforeReset,
            "ResetForTenantSwitch must bump Version so observers can discard in-flight stale work.");
    }

    [Fact]
    public void AdminPortal_AdminPortalPartyQueryService_CancelsScopeTokenOnTenantSwitch()
    {
        // AC4 + AC7: cached rows must not survive 401, 403, missing tenant, or tenant-switch
        // failures. Behavior assertion: ResetForTenantSwitch must observably cancel any
        // CancellationToken handed out before the call, so an in-flight HTTP request races
        // its way to OperationCanceledException rather than completing against the new
        // tenant. Disposable + scoped lifetime are still required so per-circuit disposal
        // participates.
        IPartiesAdminPortalApiClient apiStub = Substitute.For<IPartiesAdminPortalApiClient>();
        using var queryService = new AdminPortalPartyQueryService(apiStub);

        CancellationToken capturedToken = queryService.ScopeCancellationToken;
        capturedToken.IsCancellationRequested.ShouldBeFalse(
            "Scope token must start uncancelled.");

        queryService.ResetForTenantSwitch();

        capturedToken.IsCancellationRequested.ShouldBeTrue(
            "ResetForTenantSwitch must cancel the previously handed-out scope token so in-flight requests fail closed.");

        // Disposing the service must also cancel the current scope token so circuit
        // teardown drops any remaining in-flight work.
        CancellationToken postResetToken = queryService.ScopeCancellationToken;
        queryService.Dispose();
        postResetToken.IsCancellationRequested.ShouldBeTrue(
            "Disposing AdminPortalPartyQueryService must cancel the current scope token.");

        VerifyScopedLifetime();
    }

    [Fact]
    public void AdminPortal_DoesNotInferAuthorizationFromJwtTenantClaim()
    {
        // Party-Mode Clarification + Epic 11: tenant authority must come from
        // Hexalith.Tenants, not from JWT tenant-claim parsing in the admin portal.
        Assembly portal = typeof(AdminPortalListState).Assembly;

        IEnumerable<string> classNames = portal.GetTypes()
            .Where(t => t.IsClass)
            .Select(t => t.Name);

        string[] forbidden =
        [
            "JwtTenantClaimParser",
            "TenantClaimAuthorityResolver",
            "LocalTenantRoleResolver",
        ];

        foreach (string forbiddenName in forbidden)
        {
            classNames.ShouldNotContain(forbiddenName,
                $"AdminPortal must not introduce '{forbiddenName}' — tenant authority comes from Hexalith.Tenants.");
        }
    }

    private static void VerifyScopedLifetime()
    {
        // The query service and the list coordinator both hold per-circuit state, so
        // AddHexalithPartiesAdminPortal must register them as Scoped — not Singleton, which
        // would bleed cached state across tenants/users.
        ServiceCollection services = new();
        services.AddHexalithPartiesAdminPortal();

        ServiceDescriptor? queryDescriptor = services
            .FirstOrDefault(d => d.ServiceType == typeof(AdminPortalPartyQueryService));
        queryDescriptor.ShouldNotBeNull(
            "AdminPortalPartyQueryService must be registered by AddHexalithPartiesAdminPortal.");
        queryDescriptor.Lifetime.ShouldBe(
            ServiceLifetime.Scoped,
            "AdminPortalPartyQueryService must be Scoped (per-circuit) so tenant switches drop cached state.");

        ServiceDescriptor? coordinatorDescriptor = services
            .FirstOrDefault(d => d.ServiceType == typeof(PartiesAdminListCoordinator));
        coordinatorDescriptor.ShouldNotBeNull(
            "PartiesAdminListCoordinator must be registered by AddHexalithPartiesAdminPortal.");
        coordinatorDescriptor.Lifetime.ShouldBe(
            ServiceLifetime.Scoped,
            "PartiesAdminListCoordinator must be Scoped so each circuit has its own list state.");
    }
}
