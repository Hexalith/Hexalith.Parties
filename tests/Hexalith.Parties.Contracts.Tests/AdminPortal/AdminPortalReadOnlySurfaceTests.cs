// ATDD red-phase architectural fitness scaffolds for Story 10.1 — Admin Portal.
// These tests pin AC8 (FrontComposer-hosted Blazor portal, no parallel SPA stack) and
// the read-only non-goals listed in the story's Party-Mode Clarifications. They are
// skipped until the Hexalith.Parties.AdminPortal assembly is added in green phase;
// once present they must reflect over the loaded portal assembly without raising errors.
//
// Reflection-only checks deliberately avoid a compile-time reference to
// Microsoft.AspNetCore.Components — the contracts test project must stay framework-free.

using System.Linq;
using System.Reflection;

using Shouldly;

namespace Hexalith.Parties.Contracts.Tests.AdminPortal;

/// <summary>
/// Story 10.1 — AC8 + read-only constraints. Reflective fitness checks that prevent the
/// admin portal from drifting into a parallel TypeScript SPA, duplicating tenant-management
/// UX owned by Hexalith.Tenants, or shipping write/GDPR mutation surfaces that belong to
/// later stories (10.2, 10.3).
/// </summary>
public sealed class AdminPortalReadOnlySurfaceTests
{
    private const string AdminPortalAssemblyName = "Hexalith.Parties.AdminPortal";
    private const string FrontComposerContractsAssemblyName = "Hexalith.FrontComposer.Contracts";
    private const string ComponentBaseFullName = "Microsoft.AspNetCore.Components.ComponentBase";
    private const string RouteAttributeFullName = "Microsoft.AspNetCore.Components.RouteAttribute";

    [Fact]
    public void AdminPortal_AssemblyExists_AndReferencesFrontComposerContracts()
    {
        // AC8: the portal must compose on top of FrontComposer's Blazor/Fluent UI shell.
        Assembly portal = LoadPortalAssemblyOrThrow();
        AssemblyName[] referenced = portal.GetReferencedAssemblies();
        referenced.Select(a => a.Name)
            .ShouldContain(FrontComposerContractsAssemblyName);
    }

    [Fact]
    public void AdminPortal_DefinesRequiredBrowseAndDetailComponents()
    {
        // AC1, AC5, AC8: the browse and detail experiences must live as Blazor components
        // inside the portal assembly so they can be rendered in the FrontComposer shell.
        Assembly portal = LoadPortalAssemblyOrThrow();

        portal.GetTypes().Any(t => t.Name == "PartiesAdminPortal" && IsBlazorComponent(t))
            .ShouldBeTrue("Story 10.1 requires a Blazor PartiesAdminPortal component.");
    }

    [Fact]
    public void AdminPortal_DoesNotShipParallelSpaArtifacts()
    {
        // AC8: do not ship a separate TypeScript SPA, vite/webpack bundle, or React/Vue/Angular
        // root inside the Parties admin portal package.
        Assembly portal = LoadPortalAssemblyOrThrow();
        string assemblyDirectory = Path.GetDirectoryName(portal.Location)
            ?? throw new InvalidOperationException("AdminPortal assembly location is unavailable.");

        string[] forbidden =
        [
            "package.json",
            "vite.config.js",
            "vite.config.ts",
            "webpack.config.js",
            "next.config.js",
            "angular.json",
        ];

        foreach (string artifact in forbidden)
        {
            File.Exists(Path.Combine(assemblyDirectory, artifact))
                .ShouldBeFalse($"AdminPortal must not ship the parallel SPA artifact '{artifact}'.");
        }
    }

    [Fact]
    public void AdminPortal_DoesNotExposeMutationOrGdprComponents()
    {
        // Read-only constraint from story Party-Mode Clarifications: no create, edit,
        // delete, activate, invite, role assignment, tenant lifecycle, tenant configuration,
        // export, erasure, restriction, consent mutation, GDPR operation, or Memories search
        // management workflows belong in Story 10.1.
        Assembly portal = LoadPortalAssemblyOrThrow();

        string[] forbiddenNameFragments =
        [
            "Create", "Edit", "Update", "Delete", "Erase", "Restrict",
            "Export", "Invite", "AssignRole", "ConsentMutation",
            "TenantLifecycle", "TenantConfiguration", "GdprWorkflow",
            "MemoriesSearchManagement",
        ];

        IEnumerable<string> componentNames = portal.GetTypes()
            .Where(IsBlazorComponent)
            .Select(t => t.Name);

        foreach (string name in componentNames)
        {
            foreach (string fragment in forbiddenNameFragments)
            {
                name.Contains(fragment, StringComparison.Ordinal)
                    .ShouldBeFalse($"Component '{name}' implies mutation/GDPR scope reserved for Stories 10.2/10.3.");
            }
        }
    }

    [Fact]
    public void AdminPortal_DoesNotDuplicateTenantManagementSurface()
    {
        // AC8: tenant lifecycle, membership, role assignment, and configuration UI live in
        // Hexalith.Tenants. The Parties portal must not redefine those screens.
        Assembly portal = LoadPortalAssemblyOrThrow();

        string[] forbiddenComponents =
        [
            "TenantListPage",
            "TenantSettingsPage",
            "TenantMembershipEditor",
            "TenantRoleAssignmentDialog",
        ];

        IEnumerable<string> componentNames = portal.GetTypes()
            .Where(IsBlazorComponent)
            .Select(t => t.Name);

        foreach (string forbidden in forbiddenComponents)
        {
            componentNames.ShouldNotContain(forbidden,
                $"Tenant-management component '{forbidden}' belongs to Hexalith.Tenants, not Parties.");
        }
    }

    [Fact]
    public void AdminPortal_RoutesAreScopedToAdminPartiesPath()
    {
        // AC1: the browse view registers under an admin-scoped Parties route; tests must
        // confirm no top-level route hijacks the FrontComposer shell or competes with
        // Hexalith.Tenants routes.
        Assembly portal = LoadPortalAssemblyOrThrow();

        Type? routeAttribute = portal.GetReferencedAssemblies()
            .Select(name =>
            {
                try
                {
                    return Assembly.Load(name);
                }
                catch
                {
                    return null;
                }
            })
            .Where(a => a is not null)
            .Select(a => a!.GetType(RouteAttributeFullName, throwOnError: false))
            .FirstOrDefault(t => t is not null);

        routeAttribute.ShouldNotBeNull(
            $"Cannot resolve {RouteAttributeFullName}; AdminPortal must reference Microsoft.AspNetCore.Components.");

        IEnumerable<string> routes = portal.GetTypes()
            .SelectMany(t => t.GetCustomAttributes(routeAttribute!, inherit: false))
            .Select(attribute => (string)attribute.GetType()
                .GetProperty("Template", BindingFlags.Public | BindingFlags.Instance)!
                .GetValue(attribute)!);

        foreach (string template in routes)
        {
            template.StartsWith("/admin/parties", StringComparison.OrdinalIgnoreCase)
                .ShouldBeTrue($"Portal route '{template}' must be scoped under /admin/parties.");
        }
    }

    private static Assembly LoadPortalAssemblyOrThrow()
    {
        Assembly? loaded = AppDomain.CurrentDomain
            .GetAssemblies()
            .FirstOrDefault(a => string.Equals(a.GetName().Name, AdminPortalAssemblyName, StringComparison.Ordinal));

        if (loaded is not null)
        {
            return loaded;
        }

        return Assembly.Load(new AssemblyName(AdminPortalAssemblyName));
    }

    private static bool IsBlazorComponent(Type type)
    {
        if (type.IsAbstract || !type.IsClass)
        {
            return false;
        }

        Type? cursor = type.BaseType;
        while (cursor is not null)
        {
            if (cursor.FullName == ComponentBaseFullName)
            {
                return true;
            }

            cursor = cursor.BaseType;
        }

        return false;
    }
}
