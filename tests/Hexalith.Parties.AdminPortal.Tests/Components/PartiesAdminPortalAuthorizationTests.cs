using System.Reflection;
using System.Xml.Linq;

using Hexalith.Parties.AdminPortal.Components;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;

using Shouldly;

namespace Hexalith.Parties.AdminPortal.Tests.Components;

public sealed class PartiesAdminPortalAuthorizationTests
{
    [Fact]
    public void PartiesAdminPortal_RoutesAllAdminEntryPoints()
    {
        IReadOnlyCollection<string> routes = typeof(PartiesAdminPortal)
            .GetCustomAttributes<RouteAttribute>(inherit: false)
            .Select(static route => route.Template)
            .ToList();

        routes.ShouldContain("/admin/parties");
        routes.ShouldContain("/admin/parties/{RoutePartyId}");
        routes.ShouldContain("/admin/parties/{RoutePartyId}/gdpr");
    }

    [Fact]
    public void PartiesAdminPortal_RequiresExactlyTheAdminPolicyAtRouteLevel()
    {
        AuthorizeAttribute authorize = typeof(PartiesAdminPortal)
            .GetCustomAttributes<AuthorizeAttribute>(inherit: false)
            .ShouldHaveSingleItem();

        authorize.Policy.ShouldBe("Admin");
    }

    [Fact]
    public void AdminPortalProject_DoesNotReferenceUiHost()
    {
        XDocument project = XDocument.Load(ProjectRoot("src/Hexalith.Parties.AdminPortal/Hexalith.Parties.AdminPortal.csproj"));

        IEnumerable<string?> projectReferences = project.Descendants("ProjectReference")
            .Select(static reference => reference.Attribute("Include")?.Value);

        projectReferences
            .Where(static reference => reference != null && reference.Contains("Hexalith.Parties.UI", StringComparison.Ordinal))
            .ShouldBeEmpty();
    }

    private static string ProjectRoot(string relativePath)
    {
        string current = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(current, "Hexalith.Parties.slnx")))
        {
            DirectoryInfo? parent = Directory.GetParent(current);
            parent.ShouldNotBeNull();
            current = parent.FullName;
        }

        return Path.Combine(current, relativePath);
    }
}
