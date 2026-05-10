// ATDD red-phase authorization/state scaffolds for Story 10.2 — Admin Portal GDPR Operations.
// GDPR state may contain sensitive party data, consent values, processing summaries,
// and export metadata. These tests pin the cleanup and bounded-state seams.

using System.Linq;
using System.Reflection;

using Shouldly;

namespace Hexalith.Parties.Contracts.Tests.AdminPortal;

/// <summary>
/// Story 10.2 — AC3, AC7, and AC8. The portal must clear operation state on
/// auth/tenant/party changes and must not infer authorization from cached UI state.
/// </summary>
public sealed class AdminPortalGdprAuthorizationStateTests
{
    private const string AdminPortalAssemblyName = "Hexalith.Parties.AdminPortal";

    [Fact]
    public void GdprOperationState_DefinesBoundedPrivacyAndAuthorizationStates()
    {
        Type state = LoadPortalAssembly().GetTypes()
            .FirstOrDefault(t => t.Name == "AdminPortalGdprOperationState" && t.IsEnum)
            ?? throw new InvalidOperationException(
                "AdminPortal must define AdminPortalGdprOperationState for bounded GDPR UI states.");

        string[] required =
        [
            "NotLoaded",
            "Loading",
            "Ready",
            "ConfirmationRequired",
            "Submitting",
            "Accepted",
            "ErasurePending",
            "VerificationPartial",
            "VerificationFailed",
            "Verified",
            "Erased",
            "MissingToken",
            "MissingTenant",
            "Forbidden",
            "DomainRejected",
            "TransientFailure",
        ];

        foreach (string name in required)
        {
            Enum.GetNames(state).ShouldContain(name,
                $"AdminPortalGdprOperationState must expose '{name}' for AC2/AC3/AC8.");
        }
    }

    [Fact]
    public void GdprStateCoordinator_ClearsSensitiveStateOnAuthTenantAndPartyBoundaryChanges()
    {
        Type coordinator = LoadCoordinator();

        string[] requiredMethods =
        [
            "ResetForTenantSwitch",
            "ResetForSignOut",
            "ResetForPartyChange",
            "ResetForAuthorizationFailure",
            "ResetForErasedTerminalState",
        ];

        foreach (string method in requiredMethods)
        {
            coordinator.GetMethod(method, BindingFlags.Public | BindingFlags.Instance)
                .ShouldNotBeNull($"GDPR state coordinator must expose {method} to clear sensitive operation state.");
        }
    }

    [Fact]
    public void GdprStateCoordinator_IgnoresStaleResponsesAfterTenantOrPartySwitch()
    {
        Type coordinator = LoadCoordinator();

        coordinator.GetMethod("TryApplyResponse", BindingFlags.Public | BindingFlags.Instance)
            .ShouldNotBeNull("Coordinator must guard in-flight responses by tenant id, party id, operation, and request version.");

        IEnumerable<string> propertyNames = coordinator.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name);

        propertyNames.ShouldContain("ActiveTenantId");
        propertyNames.ShouldContain("ActivePartyId");
        propertyNames.ShouldContain("RequestVersion");
    }

    [Fact]
    public void GdprStateCoordinator_DisablesConflictingWritesDuringErasureStates()
    {
        Type coordinator = LoadCoordinator();

        // Static or instance is a design choice; the spec intent is "mutation gating depends on
        // authoritative erasure state", which is a pure function over the state value.
        MethodInfo method = coordinator.GetMethod(
            "CanMutateParty",
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            ?? throw new InvalidOperationException("Coordinator must expose CanMutateParty.");

        method.ReturnType.ShouldBe(typeof(bool));
        method.GetParameters().Any(p => p.ParameterType.Name == "AdminPortalGdprOperationState")
            .ShouldBeTrue("Mutation gating must depend on authoritative erasure state.");
    }

    [Fact]
    public void AdminPortal_DoesNotInferGdprAuthorizationFromJwtTenantClaimOrLocalRoleParsers()
    {
        Assembly portal = LoadPortalAssembly();

        string[] forbidden =
        [
            "JwtTenantClaimParser",
            "TenantClaimAuthorityResolver",
            "LocalTenantRoleResolver",
            "ClientSideAdminPolicy",
        ];

        IEnumerable<string> classNames = portal.GetTypes().Where(t => t.IsClass).Select(t => t.Name);
        foreach (string forbiddenName in forbidden)
        {
            classNames.ShouldNotContain(forbiddenName,
                $"AdminPortal GDPR authorization must rely on backend Admin policy and Hexalith.Tenants, not '{forbiddenName}'.");
        }
    }

    private static Type LoadCoordinator()
        => LoadPortalAssembly().GetTypes()
            .FirstOrDefault(t => t.Name == "AdminPortalGdprStateCoordinator")
            ?? throw new InvalidOperationException("AdminPortal must expose AdminPortalGdprStateCoordinator.");

    private static Assembly LoadPortalAssembly()
    {
        Assembly? loaded = AppDomain.CurrentDomain
            .GetAssemblies()
            .FirstOrDefault(a => string.Equals(a.GetName().Name, AdminPortalAssemblyName, StringComparison.Ordinal));

        return loaded ?? Assembly.Load(new AssemblyName(AdminPortalAssemblyName));
    }
}
