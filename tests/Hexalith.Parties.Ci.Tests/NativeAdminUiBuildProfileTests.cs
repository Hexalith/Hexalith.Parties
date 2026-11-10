using System.Text.Json;
using System.Xml.Linq;

using Hexalith.Parties.PackageTests;

namespace Hexalith.Parties.Ci.Tests;

/// <summary>Checks the properties and artifact returned by the actual nested native UI build target.</summary>
public sealed class NativeAdminUiBuildProfileTests
{
    /// <summary>Source mapping keeps its selectors while package and unmapped builds retain the hosted package profile.</summary>
    /// <param name="mode">The active mapping mode, or empty for an ordinary build.</param>
    /// <param name="configuration">The configuration selected for this build.</param>
    /// <param name="projectReferences">The expected native dependency selector.</param>
    /// <param name="nugetDependencies">The expected native package selector.</param>
    [Theory]
    [InlineData("source", "Debug", "true", "false")]
    [InlineData("package", "Release", "false", "true")]
    [InlineData("", "Release", "false", "true")]
    [InlineData("", "Debug", "false", "true")]
    public void NestedBuildPreservesTheSelectedDependencyMode(string mode, string configuration, string projectReferences, string nugetDependencies)
    {
        const string SdkVersion = "99.1.2-profile";
        string root = Directory.CreateTempSubdirectory("parties-native-ui-profile-").FullName;
        try
        {
            File.Copy(CiTestPaths.RepoFile("global.json"), Path.Combine(root, "global.json"));
            string native = Path.Combine(root, "src", "Hexalith.EventStore.Admin.UI");
            Directory.CreateDirectory(native);
            // A tiny native producer observes the real MSBuild task's global properties.
            // Its local source defaults must survive source mode and be overridden in package mode.
            File.WriteAllText(Path.Combine(native, "Hexalith.EventStore.Admin.UI.csproj"), """
                <Project>
                  <PropertyGroup>
                    <UseHexalithProjectReferences>true</UseHexalithProjectReferences>
                    <UseNuGetDeps>false</UseNuGetDeps>
                  </PropertyGroup>
                  <Target Name="Restore">
                    <WriteLinesToFile File="$(MSBuildThisFileDirectory)profile.txt" Overwrite="true"
                      Lines="Restore|$(Configuration)|$(UseHexalithProjectReferences)|$(UseNuGetDeps)|$(HexalithUsePublishedSdkForHostedTests)|$(HexalithEventStoreVersion)|$(StaticWebAssetsEnabled)|$(CopyLocalLockFileAssemblies)" />
                  </Target>
                  <Target Name="Build">
                    <WriteLinesToFile File="$(MSBuildThisFileDirectory)profile.txt"
                      Lines="Build|$(Configuration)|$(UseHexalithProjectReferences)|$(UseNuGetDeps)|$(HexalithUsePublishedSdkForHostedTests)|$(HexalithEventStoreVersion)|$(StaticWebAssetsEnabled)|$(CopyLocalLockFileAssemblies)" />
                    <Touch Files="$(MSBuildThisFileDirectory)Native.dll" AlwaysCreate="true" />
                  </Target>
                  <Target Name="GetTargetPath" Returns="$(MSBuildThisFileDirectory)Native.dll" />
                </Project>
                """);
            foreach (string name in new[] { "appsettings.json", "appsettings.Development.json", "Properties/launchSettings.json",
                "wwwroot/css/app.css", "wwwroot/js/interop.js", "wwwroot/js/download.js" })
            {
                string path = Path.Combine(native, name);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, "fixture");
            }

            XDocument source = XDocument.Load(CiTestPaths.RepoFile("tests/Hexalith.Parties.EventStoreAdminUI.TestHost/Hexalith.Parties.EventStoreAdminUI.TestHost.csproj"));
            string project = Path.Combine(root, "Host.proj");
            new XDocument(new XElement("Project",
                new XElement("PropertyGroup", new XElement("_AdminUiEventStoreRoot", root)),
                source.Root!.Elements("Target").Where(target => (string?)target.Attribute("Name") is "ValidateAdminUiSettingsRoot" or "BuildNativeAdminUiArtifact")))
                .Save(project);
            PackageTestProcessResult result = PackageTestProcess.Run("dotnet",
                ["msbuild", project, "-target:BuildNativeAdminUiArtifact", "-getItem:Reference", "-nologo", "-m:1", "-nr:false",
                    "-p:Configuration=" + configuration, "-p:HexalithSourceMappingMode=" + mode, "-p:HexalithEventStoreVersion=" + SdkVersion], root);
            result.ExitCode.ShouldBe(0, result.Diagnostic);
            string profile = $"{configuration}|{projectReferences}|{nugetDependencies}|true|{SdkVersion}|false|true";
            File.ReadAllLines(Path.Combine(native, "profile.txt")).ShouldBe(new[] { "Restore|" + profile, "Build|" + profile });
            using JsonDocument output = JsonDocument.Parse(result.StandardOutput);
            JsonElement reference = output.RootElement.GetProperty("Items").GetProperty("Reference").EnumerateArray().Single();
            Path.GetFullPath(reference.GetProperty("Identity").GetString()!).ShouldBe(Path.Combine(native, "Native.dll"));
            reference.GetProperty("Private").GetString().ShouldBe("true");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
