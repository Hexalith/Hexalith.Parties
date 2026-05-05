// ATDD red-phase authorization-state scaffolds for Story 10.1 — Admin Portal.
// AC4 + AC7: missing token, missing tenant claim, missing admin role, and tenant
// switches must all clear visible browse/search/detail state and discard in-flight
// responses from the previous tenant. These contract checks verify the AdminPortal
// exposes a state coordinator with the matching distinguishable states; they are
// skipped until the implementation lands the corresponding seam in green phase.

using System.Linq;
using System.Reflection;

using Shouldly;

namespace Hexalith.Parties.Contracts.Tests.AdminPortal;

/// <summary>
/// Story 10.1 — AC4 + AC7. Reflective fitness checks ensuring the admin portal exposes
/// distinguishable state names and a tenant-coordinated reset hook the FrontComposer
/// shell can dispatch on tenant switch.
/// </summary>
public sealed class AdminPortalAuthorizationStateTests
{
    private const string SkipReason =
        "TDD red phase — Hexalith.Parties.AdminPortal authorization state coordinator not yet wired.";

    private const string AdminPortalAssemblyName = "Hexalith.Parties.AdminPortal";

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

    [Fact(Skip = SkipReason)]
    public void AdminPortal_DefinesDistinguishableAuthorizationStates()
    {
        // AC7: missing token / missing tenant / missing admin role must each be a
        // distinguishable UI copy state, not collapsed into a single "unauthorized" bucket.
        Assembly portal = LoadPortalAssembly();

        Type stateEnum = portal.GetTypes()
            .FirstOrDefault(t => t.Name == "AdminPortalListState" && t.IsEnum)
            ?? throw new InvalidOperationException(
                "AdminPortal must expose an enum AdminPortalListState describing each browse state.");

        IEnumerable<string> declared = Enum.GetNames(stateEnum);
        foreach (string required in _requiredStateNames)
        {
            declared.ShouldContain(required,
                $"AdminPortalListState is missing required state '{required}' (AC4/AC7).");
        }
    }

    [Fact(Skip = SkipReason)]
    public void AdminPortal_DefinesTenantSwitchResetHook()
    {
        // AC7: tenant context changes must clear list/search/detail state and ignore
        // in-flight responses from the previous tenant. The portal must expose a
        // ResetForTenantSwitch (or equivalent) method invoked by the FrontComposer scope
        // observer the moment a tenant flip is announced.
        Assembly portal = LoadPortalAssembly();

        Type coordinator = portal.GetTypes()
            .FirstOrDefault(t => t.Name == "PartiesAdminListCoordinator")
            ?? throw new InvalidOperationException(
                "AdminPortal must expose PartiesAdminListCoordinator for tenant-switch reset.");

        MethodInfo? resetMethod = coordinator
            .GetMethod("ResetForTenantSwitch", BindingFlags.Public | BindingFlags.Instance);
        resetMethod.ShouldNotBeNull(
            "PartiesAdminListCoordinator must define ResetForTenantSwitch (AC7).");
    }

    [Fact(Skip = SkipReason)]
    public void AdminPortal_DefinesScopedQueryServiceFailingClosed()
    {
        // AC7 + Implementation Guardrails: cached rows must not survive 401, 403, missing
        // tenant, or tenant-switch failures. The query service exposed to portal pages must
        // be tenant-scoped (not singleton) so that scope disposal removes cached state.
        Assembly portal = LoadPortalAssembly();

        Type queryService = portal.GetTypes()
            .FirstOrDefault(t => t.Name == "AdminPortalPartyQueryService")
            ?? throw new InvalidOperationException(
                "AdminPortal must expose AdminPortalPartyQueryService.");

        // The service must implement IDisposable or IAsyncDisposable so scoped disposal
        // can drop in-flight responses (CTS cancellation + cached results).
        bool disposable = queryService
            .GetInterfaces()
            .Any(i => i == typeof(IDisposable) || i == typeof(IAsyncDisposable));

        disposable.ShouldBeTrue(
            "AdminPortalPartyQueryService must implement IDisposable/IAsyncDisposable to drop cached state on tenant switch.");
    }

    [Fact(Skip = SkipReason)]
    public void AdminPortal_DoesNotInferAuthorizationFromJwtTenantClaim()
    {
        // Party-Mode Clarification + Epic 11: tenant authority must come from
        // Hexalith.Tenants, not from JWT tenant-claim parsing in the admin portal.
        Assembly portal = LoadPortalAssembly();

        IEnumerable<string> classNames = portal.GetTypes()
            .Where(t => t.IsClass)
            .Select(t => t.Name);

        // Forbid type names that imply local JWT-claim parsing of tenant authority.
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

    private static Assembly LoadPortalAssembly()
    {
        Assembly? loaded = AppDomain.CurrentDomain
            .GetAssemblies()
            .FirstOrDefault(a => string.Equals(a.GetName().Name, AdminPortalAssemblyName, StringComparison.Ordinal));

        return loaded ?? Assembly.Load(new AssemblyName(AdminPortalAssemblyName));
    }
}
