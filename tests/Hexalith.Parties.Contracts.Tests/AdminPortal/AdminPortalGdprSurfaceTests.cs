// ATDD red-phase architectural scaffolds for Story 10.2 — Admin Portal GDPR Operations.
// These checks describe the Blazor/FrontComposer GDPR operation surface without taking
// a compile-time dependency on the future AdminPortal assembly.

using System.Linq;
using System.Reflection;

using Shouldly;

namespace Hexalith.Parties.Contracts.Tests.AdminPortal;

/// <summary>
/// Story 10.2 — AC1, AC7, and AC10. The GDPR UI extends the Story 10.1 party detail
/// surface and remains a Parties-owned adapter over existing admin endpoints.
/// </summary>
public sealed class AdminPortalGdprSurfaceTests
{
    private const string AdminPortalAssemblyName = "Hexalith.Parties.AdminPortal";
    private const string FrontComposerShellAssemblyName = "Hexalith.FrontComposer.Shell";
    private const string ComponentBaseFullName = "Microsoft.AspNetCore.Components.ComponentBase";
    private const string RouteAttributeFullName = "Microsoft.AspNetCore.Components.RouteAttribute";

    [Fact]
    public void AdminPortalGdprSurface_ExtendsFrontComposerAdminPortalAssembly()
    {
        Assembly portal = LoadPortalAssembly();

        portal.GetReferencedAssemblies().Select(a => a.Name)
            .ShouldContain(FrontComposerShellAssemblyName,
                "AC10 requires extending the FrontComposer Blazor/Fluent UI shell, not a standalone SPA.");
    }

    [Fact]
    public void AdminPortalGdprSurface_DefinesRequiredOperationalComponents()
    {
        Assembly portal = LoadPortalAssembly();

        string[] requiredComponents =
        [
            "PartyGdprOperationsPanel",
            "ErasureStatusPanel",
            "ErasureVerificationReportPanel",
            "RestrictionActionsPanel",
            "ConsentManagementPanel",
            "PortabilityExportPanel",
            "ProcessingRecordsPanel",
            "DpoOperationalSummaryPanel",
        ];

        IEnumerable<string> components = portal.GetTypes().Where(IsBlazorComponent).Select(t => t.Name);
        foreach (string component in requiredComponents)
        {
            components.ShouldContain(component,
                $"Story 10.2 requires Blazor component {component} on the party detail/admin surface.");
        }
    }

    [Fact]
    public void AdminPortalGdprSurface_DoesNotIntroduceTenantManagementComponents()
    {
        Assembly portal = LoadPortalAssembly();

        string[] forbiddenComponents =
        [
            "TenantLifecyclePage",
            "TenantMembershipEditor",
            "TenantRoleAssignmentDialog",
            "TenantConfigurationPanel",
            "GlobalAdministratorManagement",
        ];

        IEnumerable<string> components = portal.GetTypes().Where(IsBlazorComponent).Select(t => t.Name);
        foreach (string forbidden in forbiddenComponents)
        {
            components.ShouldNotContain(forbidden,
                $"Tenant authority UI '{forbidden}' belongs to Hexalith.Tenants, not the Parties GDPR portal.");
        }
    }

    [Fact]
    public void AdminPortalGdprRoutes_RemainScopedUnderAdminParties()
    {
        Assembly portal = LoadPortalAssembly();
        Type routeAttribute = ResolveRouteAttribute(portal);

        IEnumerable<string> routes = portal.GetTypes()
            .SelectMany(t => t.GetCustomAttributes(routeAttribute, inherit: false))
            .Select(attribute => (string)attribute.GetType()
                .GetProperty("Template", BindingFlags.Public | BindingFlags.Instance)!
                .GetValue(attribute)!);

        foreach (string template in routes)
        {
            bool allowed = template.StartsWith("/admin/parties", StringComparison.OrdinalIgnoreCase)
                || template.StartsWith("/admin/gdpr", StringComparison.OrdinalIgnoreCase);

            allowed.ShouldBeTrue(
                $"GDPR route '{template}' must stay inside the Parties admin shell and active tenant context.");
        }
    }

    [Fact]
    public void AdminPortalGdprSurface_DoesNotShipParallelSpaArtifacts()
    {
        Assembly portal = LoadPortalAssembly();
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
                .ShouldBeFalse($"AdminPortal GDPR work must not ship parallel SPA artifact '{artifact}'.");
        }
    }

    private static Assembly LoadPortalAssembly()
    {
        Assembly? loaded = AppDomain.CurrentDomain
            .GetAssemblies()
            .FirstOrDefault(a => string.Equals(a.GetName().Name, AdminPortalAssemblyName, StringComparison.Ordinal));

        return loaded ?? Assembly.Load(new AssemblyName(AdminPortalAssemblyName));
    }

    private static Type ResolveRouteAttribute(Assembly portal)
        => portal.GetReferencedAssemblies()
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
            .FirstOrDefault(t => t is not null)
            ?? throw new InvalidOperationException(
                $"Cannot resolve {RouteAttributeFullName}; AdminPortal must reference Microsoft.AspNetCore.Components.");

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
