using System.Xml.Linq;

namespace Hexalith.Parties.Ci.Tests;

public sealed class CommonsHttpRestoreRoutingTests
{
    private const string CommonsHttpProjectReference = @"$(HexalithCommonsRoot)\src\libraries\Hexalith.Commons.Http\Hexalith.Commons.Http.csproj";
    private const string PackageCondition = "'$(HexalithCommonsHttpFromSource)' != 'true'";
    private const string PackageId = "Hexalith.Commons.Http";
    private const string SourceCondition = "'$(HexalithCommonsHttpFromSource)' == 'true'";
    private const string SourceProperty = "HexalithCommonsHttpFromSource";

    [Fact]
    public void DirectoryBuildPropsDeclaresNarrowCommonsHttpSourceFallback()
    {
        XDocument props = XDocument.Load(CiTestPaths.RepoFile("Directory.Build.props"));
        XElement property = props.Descendants(SourceProperty).SingleOrDefault()
            ?? throw new InvalidOperationException($"{SourceProperty} was not found in Directory.Build.props.");
        XElement packageVersion = props.Descendants("HexalithCommonsHttpPackageVersion").SingleOrDefault()
            ?? throw new InvalidOperationException("HexalithCommonsHttpPackageVersion was not found in Directory.Build.props.");
        string centralPackageVersion = ReadCentralCommonsHttpPackageVersion();

        property.Value.ShouldBe("true");
        packageVersion.Value.ShouldBe(centralPackageVersion);
        string condition = property.Attribute("Condition")?.Value ?? string.Empty;

        condition.ShouldBe($"'$({SourceProperty})' == '' and Exists('{CommonsHttpProjectReference}')");
        condition.ShouldNotContain("UseHexalithProjectReferences");
        condition.ShouldNotContain("HexalithCommonsFromSource");
    }

    [Fact]
    public void CommonsHttpPackageReferencesHaveMatchingSourceFallback()
    {
        List<string> failures = [];
        int packageReferenceCount = 0;

        foreach (string projectFile in FindOwnedProjectFiles())
        {
            XDocument project = XDocument.Load(projectFile);
            List<XElement> packageReferences = project
                .Descendants("PackageReference")
                .Where(element => element.Attribute("Include")?.Value == PackageId)
                .ToList();

            if (packageReferences.Count == 0)
            {
                continue;
            }

            packageReferenceCount += packageReferences.Count;
            string relativeProject = ToRepoRelativePath(projectFile);

            foreach (XElement packageReference in packageReferences)
            {
                string condition = packageReference.Attribute("Condition")?.Value ?? string.Empty;
                if (condition != PackageCondition)
                {
                    failures.Add($"{relativeProject} has {PackageId} package condition '{condition}', expected '{PackageCondition}'.");
                }
            }

            bool hasSourceFallback = project
                .Descendants("ProjectReference")
                .Any(element =>
                    element.Attribute("Include")?.Value == CommonsHttpProjectReference
                    && element.Attribute("Condition")?.Value == SourceCondition);

            if (!hasSourceFallback)
            {
                failures.Add($"{relativeProject} must source-reference {PackageId} with condition '{SourceCondition}'.");
            }
        }

        packageReferenceCount.ShouldBeGreaterThan(0);
        failures.ShouldBeEmpty(string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void ReleasePackingKeepsCommonsHttpDependencyOnPublishedSupportVersion()
    {
        XDocument targets = XDocument.Load(CiTestPaths.RepoFile("references/Directory.Build.targets"));
        XElement packageVersion = targets.Descendants("PackageVersion").SingleOrDefault()
            ?? throw new InvalidOperationException("PackageVersion override was not found in references/Directory.Build.targets.");
        string packScript = CiTestPaths.ReadRepoFile("scripts/pack-release-packages.py");
        string validationScript = CiTestPaths.ReadRepoFile("scripts/validate-nuget-packages.py");
        string centralPackageVersion = ReadCentralCommonsHttpPackageVersion();

        packageVersion.Value.ShouldBe("$(HexalithCommonsHttpPackageVersion)");
        (packageVersion.Parent?.Attribute("Condition")?.Value ?? string.Empty)
            .ShouldBe("'$(MSBuildProjectName)' == 'Hexalith.Commons.Http' and '$(HexalithCommonsHttpPackageVersion)' != ''");
        packScript.ShouldContain("-p:HexalithPartiesPackageVersion={args.version}");
        packScript.ShouldContain($"-p:HexalithCommonsHttpPackageVersion={centralPackageVersion}");
        packScript.ShouldNotContain("-p:Version={args.version}");
        packScript.ShouldNotContain("-p:PackageVersion={args.version}");
        packScript.ShouldNotContain("-p:MinVerVersionOverride={args.version}");
        validationScript.ShouldContain($"\"{PackageId}\": \"{centralPackageVersion}\"");
        validationScript.ShouldContain("REQUIRED_COMMONS_HTTP_DEPENDENCY_PACKAGES");
    }

    private static string ReadCentralCommonsHttpPackageVersion()
    {
        XDocument props = XDocument.Load(CiTestPaths.RepoFile("references/Hexalith.Builds/Props/Directory.Packages.props"));
        return props
            .Descendants("PackageVersion")
            .Single(element => element.Attribute("Include")?.Value == PackageId)
            .Attribute("Version")?.Value
            ?? throw new InvalidOperationException($"{PackageId} version was not found in shared package props.");
    }

    private static IEnumerable<string> FindOwnedProjectFiles()
    {
        foreach (string root in new[] { "src", "samples", "tests" }.Select(CiTestPaths.RepoFile).Where(Directory.Exists))
        {
            foreach (string projectFile in Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories))
            {
                string relativePath = ToRepoRelativePath(projectFile);
                if (!relativePath.Contains("/bin/", StringComparison.Ordinal)
                    && !relativePath.Contains("/obj/", StringComparison.Ordinal))
                {
                    yield return projectFile;
                }
            }
        }
    }

    private static string ToRepoRelativePath(string path)
        => Path.GetRelativePath(CiTestPaths.RepositoryRoot, path).Replace('\\', '/');
}
