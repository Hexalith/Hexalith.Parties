using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;

using Hexalith.Parties.PackageTests;

namespace Hexalith.Parties.Ci.Tests;

/// <summary>Evaluates the owning build target without restoring packages or granting source/runtime qualification.</summary>
/// <param name="output">The actual evaluated build-policy diagnostics.</param>
public sealed class MemoriesRefreshCompilationTests(ITestOutputHelper output)
{
    /// <summary>Keeps exact package activation, unknown-version fallback and the existing source branch explicit.</summary>
    /// <param name="version">The candidate package version for this isolated policy evaluation.</param>
    /// <param name="source">The source selector branch to evaluate.</param>
    /// <param name="supported">Whether the owning projects must compile the refresh protocol.</param>
    [Theory]
    [InlineData("3.0.3", false, true)]
    [InlineData("3.0.10", false, true)]
    [InlineData("3.0.11", false, true)]
    [InlineData("3.0.12", false, true)]
    [InlineData("3.0.13", false, true)]
    [InlineData("3.0.18", false, true)]
    [InlineData("3.0.23", false, true)]
    [InlineData("3.0.24", false, true)]
    [InlineData("3.0.2", false, false)]
    [InlineData("3.0.22", false, false)]
    [InlineData("3.0.25", false, false)]
    [InlineData("3.1.0", false, false)]
    [InlineData("3.0.23-preview.1", false, false)]
    [InlineData("3.0.24-preview.1", false, false)]
    [InlineData("invalid", false, false)]
    [InlineData("", false, false)]
    [InlineData("3.0.24", true, true)]
    [InlineData("", true, true)]
    public void ExactSelectionControlsSymbolAndAssemblyMetadata(string version, bool source, bool supported)
    {
        string workspace = Directory.CreateTempSubdirectory("parties-refresh-compilation-").FullName;
        try
        {
            foreach (string name in new[] { "Hexalith.Parties", "Hexalith.Parties.Tests", "Unrelated" })
            {
                string project = Path.Combine(workspace, name + ".csproj");
                new XDocument(new XElement("Project",
                    new XElement("PropertyGroup", new XElement("DefineConstants", "UNCHANGED"),
                        new XElement("HexalithMemoriesVersion", version),
                        new XElement("HexalithMemoriesFromSource", source.ToString().ToLowerInvariant())),
                    new XElement("Import", new XAttribute("Project", CiTestPaths.RepoFile("Directory.Build.targets")))))
                    .Save(project);
                var start = new ProcessStartInfo("dotnet");
                foreach (string argument in new[] { "msbuild", project, "-nologo",
                    "-target:ConfigureMemoriesSourceCompilation",
                    "-getProperty:DefineConstants,MemoriesSourceRefreshSupported", "-getItem:AssemblyMetadata" })
                {
                    start.ArgumentList.Add(argument);
                }

                PackageTestProcessResult result = PackageTestProcess.Run(start, workspace, 30_000);
                result.ExitCode.ShouldBe(0, result.Diagnostic);
                using JsonDocument evaluation = JsonDocument.Parse(result.StandardOutput);
                JsonElement properties = evaluation.RootElement.GetProperty("Properties");
                bool owner = name != "Unrelated";
                string[] symbols = properties.GetProperty("DefineConstants").GetString()!.Split(';');
                symbols.ShouldContain("UNCHANGED");
                symbols.Contains("HEXALITH_MEMORIES_SOURCE_REFRESH", StringComparer.Ordinal).ShouldBe(owner && supported);
                properties.GetProperty("MemoriesSourceRefreshSupported").GetString()
                    .ShouldBe(owner ? supported ? "true" : "false" : string.Empty);
                JsonElement[] metadata = evaluation.RootElement.GetProperty("Items").GetProperty("AssemblyMetadata")
                    .EnumerateArray().Where(item => item.GetProperty("Identity").GetString() == "HexalithMemoriesSourceRefresh").ToArray();
                if (owner)
                {
                    metadata.ShouldHaveSingleItem().GetProperty("Value").GetString().ShouldBe(supported ? "true" : "false");
                }
                else
                {
                    metadata.ShouldBeEmpty();
                }

                output.WriteLine($"Build-policy evaluation only: project={name}; version={version}; source={source}; supported={owner && supported}; properties={properties}.");
            }
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }
}
