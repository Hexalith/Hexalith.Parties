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

    [Fact]
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

    [Fact]
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

    [Fact]
    public void AdminPortal_DefinesScopedQueryServiceFailingClosed()
    {
        // AC7 + Implementation Guardrails: cached rows must not survive 401, 403, missing
        // tenant, or tenant-switch failures. The query service exposed to portal pages must
        // be tenant-scoped (not singleton) so that scope disposal removes cached state. We
        // verify both disposability AND the DI lifetime by reflectively reading the
        // ServiceDescriptor that the AddHexalithPartiesAdminPortal extension registers.
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

        // Verify the DI registration lifetime via the public extension, using reflection
        // so this test project stays framework-free. Loads Microsoft.Extensions.DependencyInjection
        // at runtime, invokes AddHexalithPartiesAdminPortal on a fresh ServiceCollection,
        // and reads the resulting ServiceDescriptor.Lifetime for AdminPortalPartyQueryService.
        VerifyScopedLifetime(portal, queryService);
    }

    private static void VerifyScopedLifetime(Assembly portal, Type queryService)
    {
        Type extensionsType = portal.GetTypes()
            .FirstOrDefault(t => t.Name == "PartiesAdminPortalServiceCollectionExtensions")
            ?? throw new InvalidOperationException(
                "AdminPortal must expose PartiesAdminPortalServiceCollectionExtensions.");

        MethodInfo addMethod = extensionsType
            .GetMethod("AddHexalithPartiesAdminPortal", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException(
                "AddHexalithPartiesAdminPortal extension method must exist.");

        Type serviceCollectionType = Type.GetType(
            "Microsoft.Extensions.DependencyInjection.ServiceCollection, Microsoft.Extensions.DependencyInjection",
            throwOnError: false)
            ?? throw new InvalidOperationException(
                "Microsoft.Extensions.DependencyInjection.ServiceCollection must be loadable to verify lifetimes.");

        object services = Activator.CreateInstance(serviceCollectionType)
            ?? throw new InvalidOperationException("Failed to create ServiceCollection.");

        addMethod.Invoke(null, [services]);

        Type descriptorType = Type.GetType(
            "Microsoft.Extensions.DependencyInjection.ServiceDescriptor, Microsoft.Extensions.DependencyInjection.Abstractions",
            throwOnError: false)
            ?? throw new InvalidOperationException(
                "Microsoft.Extensions.DependencyInjection.ServiceDescriptor must be loadable.");

        PropertyInfo serviceTypeProp = descriptorType.GetProperty("ServiceType")
            ?? throw new InvalidOperationException("ServiceDescriptor.ServiceType must exist.");
        PropertyInfo lifetimeProp = descriptorType.GetProperty("Lifetime")
            ?? throw new InvalidOperationException("ServiceDescriptor.Lifetime must exist.");

        object? descriptor = ((System.Collections.IEnumerable)services)
            .Cast<object>()
            .FirstOrDefault(d => (Type)serviceTypeProp.GetValue(d)! == queryService);

        descriptor.ShouldNotBeNull(
            "AdminPortalPartyQueryService must be registered by AddHexalithPartiesAdminPortal.");

        object lifetime = lifetimeProp.GetValue(descriptor)!;
        lifetime.ToString().ShouldBe(
            "Scoped",
            "AdminPortalPartyQueryService must be registered as Scoped (per-circuit) so tenant switches drop cached state.");
    }

    [Fact]
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
