using System.Text.Json;
using System.Xml.Linq;

using Hexalith.Parties.PackageTests;

namespace Hexalith.Parties.Ci.Tests;

/// <summary>Only the measured exact published SDK cohort selects the test-only managed runtime workaround.</summary>
public sealed class PartiesPublished42RuntimeSelectionTests
{
    /// <summary>Evaluates the actual Integration project profile with real MSBuild and preserves all other defaults.</summary>
    [Theory]
    [InlineData("4.2.0", "true", "false", true)]
    [InlineData("4.2.0", "false", "false", false)]
    [InlineData("4.2.0", "true", "true", false)]
    [InlineData("4.1.0", "true", "false", false)]
    [InlineData("4.2.1", "true", "false", false)]
    [InlineData("4.3.0", "true", "false", false)]
    [InlineData("5.0.0", "true", "false", false)]
    [InlineData("4.2.0-preview.1", "true", "false", false)]
    public void RuntimeWorkaroundRequiresExactPublished42(string sdk, string published, string source, bool selected)
    {
        string root = Directory.CreateTempSubdirectory("parties-runtime-selection-").FullName;
        try
        {
            File.Copy(CiTestPaths.RepoFile("global.json"), Path.Combine(root, "global.json"));
            XDocument definition = XDocument.Load(CiTestPaths.RepoFile("tests/Hexalith.Parties.IntegrationTests/Hexalith.Parties.IntegrationTests.csproj"));
            XElement profile = definition.Root!.Elements("PropertyGroup")
                .Single(group => group.Element("PartiesPublished42ManagedRuntime") is not null);
            string project = Path.Combine(root, "Profile.proj");
            new XDocument(new XElement("Project", new XElement("PropertyGroup",
                new XElement("RuntimeFrameworkVersion", "10.0.15")), new XElement(profile))).Save(project);
            PackageTestProcessResult result = PackageTestProcess.Run("dotnet", ["msbuild", project, "-nologo",
                "-getProperty:RuntimeFrameworkVersion,PartiesPublished42ManagedRuntime", "-p:HexalithEventStoreVersion=" + sdk,
                "-p:HexalithUsePublishedSdkForHostedTests=" + published, "-p:HexalithEventStoreFromSource=" + source], root);
            result.ExitCode.ShouldBe(0, result.Diagnostic);
            using JsonDocument output = JsonDocument.Parse(result.StandardOutput);
            JsonElement properties = output.RootElement.GetProperty("Properties");
            properties.GetProperty("RuntimeFrameworkVersion").GetString().ShouldBe(selected ? "11.0.0" : "10.0.15");
            properties.GetProperty("PartiesPublished42ManagedRuntime").GetString().ShouldBe(selected ? "true" : "");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
