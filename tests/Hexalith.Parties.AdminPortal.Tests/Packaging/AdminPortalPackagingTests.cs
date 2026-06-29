using System.Xml.Linq;

using Shouldly;

namespace Hexalith.Parties.AdminPortal.Tests.Packaging;

public sealed class AdminPortalPackagingTests
{
    [Fact]
    public void AdminPortalProject_UsesRazorSdk_AndExpectedPackageMetadata()
    {
        XDocument project = XDocument.Load(ProjectRoot("src/Hexalith.Parties.AdminPortal/Hexalith.Parties.AdminPortal.csproj"));
        XElement root = project.Root.ShouldNotBeNull();

        root.Attribute("Sdk")?.Value.ShouldBe("Microsoft.NET.Sdk.Razor");
        project.Descendants("PackageId").Single().Value.ShouldBe("Hexalith.Parties.AdminPortal");
    }

    [Fact]
    public void AdminPortalProject_DoesNotReferenceTheUiHost_NorCreateCircularReferences()
    {
        XDocument project = XDocument.Load(ProjectRoot("src/Hexalith.Parties.AdminPortal/Hexalith.Parties.AdminPortal.csproj"));

        IReadOnlyCollection<string> references = project.Descendants("ProjectReference")
            .Select(static reference => reference.Attribute("Include")?.Value)
            .Where(static include => !string.IsNullOrWhiteSpace(include))
            .Select(static include => include!)
            .ToList();

        references.ShouldContain(@"..\Hexalith.Parties.Client\Hexalith.Parties.Client.csproj");
        references.ShouldContain(@"..\Hexalith.Parties.Contracts\Hexalith.Parties.Contracts.csproj");

        references.ShouldNotContain(static reference => reference.Contains("Hexalith.Parties.UI", StringComparison.Ordinal));
        references.ShouldNotContain(static reference => reference.Contains("Hexalith.Parties.Server", StringComparison.Ordinal));
        references.ShouldNotContain(static reference => reference.Contains("Hexalith.Parties.Projections", StringComparison.Ordinal));
    }

    [Fact]
    public void AdminPortalSource_DoesNotDependOnTheUiHostForFormatting()
    {
        string sourceRoot = ProjectRoot("src/Hexalith.Parties.AdminPortal");

        string allSource = string.Join(Environment.NewLine, ReadSourceFiles(sourceRoot)
            .Select(File.ReadAllText));

        allSource.ShouldNotContain("Hexalith.Parties.UI", Case.Sensitive);
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

    private static IEnumerable<string> ReadSourceFiles(string sourceRoot)
    {
        if (!Directory.Exists(sourceRoot))
        {
            yield break;
        }

        foreach (string file in Directory.EnumerateFiles(sourceRoot, "*.*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(sourceRoot, file);
            string[] segments = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (segments.Contains("bin", StringComparer.OrdinalIgnoreCase)
                || segments.Contains("obj", StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            string extension = Path.GetExtension(file);
            if (!extension.Equals(".cs", StringComparison.OrdinalIgnoreCase)
                && !extension.Equals(".razor", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return file;
        }
    }
}
