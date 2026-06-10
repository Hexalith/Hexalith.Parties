using System.Xml.Linq;

using Shouldly;

namespace Hexalith.Parties.ConsumerPortal.Tests.Packaging;

public sealed class ConsumerPortalPackagingTests
{
    private static readonly string[] SourceFileExtensions =
    [
        ".cs",
        ".razor",
    ];

    private static readonly string[] StyleFileExtensions =
    [
        ".css",
    ];

    private static readonly string[] ForbiddenColorTokens =
    [
        "#",
        "rgb(",
        "rgba(",
        "hsl(",
        "hsla(",
    ];

    [Fact]
    public void ConsumerPortalProject_UsesRazorSdk_AndExpectedPackageMetadata()
    {
        XDocument project = XDocument.Load(ProjectRoot("src/Hexalith.Parties.ConsumerPortal/Hexalith.Parties.ConsumerPortal.csproj"));
        XElement root = project.Root.ShouldNotBeNull();

        root.Attribute("Sdk")?.Value.ShouldBe("Microsoft.NET.Sdk.Razor");
        project.Descendants("PackageId").Single().Value.ShouldBe("Hexalith.Parties.ConsumerPortal");
        project.Descendants("Description").Single().Value.ShouldContain("consumer portal");
        project.Descendants("TargetFramework").ShouldBeEmpty();
        project.Descendants("Nullable").ShouldBeEmpty();
        project.Descendants("ImplicitUsings").ShouldBeEmpty();
        project.Descendants("TreatWarningsAsErrors").ShouldBeEmpty();
    }

    [Fact]
    public void ConsumerPortalProject_ReferencesOnlyAllowedAdopterFacingProjects()
    {
        XDocument project = XDocument.Load(ProjectRoot("src/Hexalith.Parties.ConsumerPortal/Hexalith.Parties.ConsumerPortal.csproj"));

        project.Descendants("FrameworkReference")
            .Select(static reference => reference.Attribute("Include")?.Value)
            .ShouldContain("Microsoft.AspNetCore.App");

        project.Descendants("PackageReference")
            .Select(static reference => reference.Attribute("Include")?.Value)
            .ShouldContain("Microsoft.FluentUI.AspNetCore.Components");

        IReadOnlyCollection<string> references = project.Descendants("ProjectReference")
            .Select(static reference => reference.Attribute("Include")?.Value)
            .Where(static include => !string.IsNullOrWhiteSpace(include))
            .Select(static include => include!)
            .ToList();

        references.ShouldContain(@"..\Hexalith.Parties.Client\Hexalith.Parties.Client.csproj");
        references.ShouldContain(@"..\Hexalith.Parties.Contracts\Hexalith.Parties.Contracts.csproj");
        references.ShouldContain(@"$(HexalithFrontComposerRoot)\src\Hexalith.FrontComposer.Shell\Hexalith.FrontComposer.Shell.csproj");

        references.ShouldNotContain(static reference => reference.Contains("Hexalith.Parties.UI", StringComparison.Ordinal));
        references.ShouldNotContain(static reference => reference.Contains("Hexalith.Parties.Server", StringComparison.Ordinal));
        references.ShouldNotContain(static reference => reference.Contains("Hexalith.Parties.Projections", StringComparison.Ordinal));
        references.ShouldNotContain(static reference => reference.Contains("Hexalith.Parties.Security", StringComparison.Ordinal));
        references.ShouldNotContain(static reference => reference.Contains("Hexalith.Parties.Testing", StringComparison.Ordinal));
    }

    [Fact]
    public void ConsumerPortalSource_DoesNotUseFutureDataWorkflows()
    {
        string sourceRoot = ProjectRoot("src/Hexalith.Parties.ConsumerPortal");

        string allSource = string.Join(Environment.NewLine, ReadProjectFiles(sourceRoot, SourceFileExtensions)
            .Select(static file => File.ReadAllText(file.AbsolutePath)));

        allSource.ShouldNotContain("ListPartiesAsync", Case.Sensitive);
        allSource.ShouldNotContain("SearchPartiesAsync", Case.Sensitive);
        allSource.ShouldNotContain("ISelfScopedPartiesClient", Case.Sensitive);
        allSource.ShouldNotContain("GetPartyAsync", Case.Sensitive);
        allSource.ShouldNotContain("IAdminPortalGdprClient", Case.Sensitive);
    }

    [Fact]
    public void ConsumerPortalStyles_UseDesignTokens_NotRawColorLiterals()
    {
        string sourceRoot = ProjectRoot("src/Hexalith.Parties.ConsumerPortal");

        foreach ((string RelativePath, string AbsolutePath) file in ReadProjectFiles(sourceRoot, StyleFileExtensions))
        {
            string source = File.ReadAllText(file.AbsolutePath);

            foreach (string forbidden in ForbiddenColorTokens)
            {
                source.ShouldNotContain(forbidden, Case.Insensitive, $"Forbidden color literal '{forbidden}' found in {file.RelativePath}.");
            }

            source.ShouldNotContain("#0097A7", Case.Insensitive, $"Raw brand teal found in {file.RelativePath}.");
        }
    }

    [Fact]
    public void ConsumerPortalComponents_UseResourceLabelWrapper_ForRegulatedCopy()
    {
        string sourceRoot = ProjectRoot("src/Hexalith.Parties.ConsumerPortal");
        string resourcesRoot = Path.Combine(sourceRoot, "Resources");

        Directory.Exists(resourcesRoot).ShouldBeTrue();
        Directory.GetFiles(resourcesRoot, "*.resx", SearchOption.TopDirectoryOnly).ShouldNotBeEmpty();

        foreach (string component in Directory.GetFiles(Path.Combine(sourceRoot, "Components"), "*.razor", SearchOption.TopDirectoryOnly))
        {
            string source = File.ReadAllText(component);

            source.ShouldContain("ConsumerPortalLabels.");
            source.ShouldNotContain("under Article", Case.Insensitive);
            source.ShouldNotContain("within 30 days", Case.Insensitive);
        }
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

    private static IEnumerable<(string RelativePath, string AbsolutePath)> ReadProjectFiles(
        string sourceRoot,
        IReadOnlyCollection<string> extensions)
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

            if (!extensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return (relativePath, file);
        }
    }
}
