using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;

using Hexalith.Parties.PackageTests;

namespace Hexalith.Parties.Ci.Tests;

/// <summary>Checks the properties and artifact returned by the actual nested native UI build target.</summary>
public sealed class NativeAdminUiBuildProfileTests
{
    /// <summary>The actual native compiler and official SDK archives qualify the private artifact despite the owner's different ordinary catalog.</summary>
    /// <param name="sdkVersion">The exact SDK selected for the mapped host.</param>
    [Theory]
    [InlineData("4.2.0")]
    [InlineData("5.4.0")]
    public void MappedPackageArtifactQualifiesTheActualOwnerOutsideTheRootCatalog(string sdkVersion)
    {
        string root = Directory.CreateTempSubdirectory("parties-native-ui-boundary-").FullName;
        try
        {
            File.Copy(CiTestPaths.RepoFile("global.json"), Path.Combine(root, "global.json"));
            string owner = CiTestPaths.RepoFile("references/Hexalith.EventStore");
            string imports = Path.Combine(root, "RootCatalog.targets");
            // Model the demonstrated global SDK5 view at its real import boundary.
            // The native application itself is the complete selected owner, not a tiny producer.
            File.WriteAllText(imports, """
                <Project>
                  <Target Name="RejectPrivateSdkInOrdinaryOwnerContext" BeforeTargets="PrepareForBuild">
                    <Error Condition="'$(MSBuildProjectName)' == 'Hexalith.EventStore.Admin.UI'"
                      Text="HXW012: the ordinary native owner requires the global SDK5 catalog" />
                  </Target>
                </Project>
                """);
            string nativeImport = Path.Combine(root, "NativeInput.targets");
            File.WriteAllText(nativeImport, "<Project><PropertyGroup><NativeInputMarker>original</NativeInputMarker></PropertyGroup></Project>");
            XDocument source = XDocument.Load(CiTestPaths.RepoFile("tests/Hexalith.Parties.EventStoreAdminUI.TestHost/Hexalith.Parties.EventStoreAdminUI.TestHost.csproj"));
            string project = Path.Combine(root, "Host.proj");
            new XDocument(new XElement("Project",
                new XElement("PropertyGroup", new XElement("_AdminUiEventStoreRoot", owner),
                    new XElement("_AdminUiRepositoryRoot", CiTestPaths.RepositoryRoot), new XElement("TargetFramework", "net11.0"),
                    new XElement("IntermediateOutputPath", Path.Combine(root, "obj") + Path.DirectorySeparatorChar),
                    new XElement("Configuration", "Release"), new XElement("HexalithEventStoreVersion", sdkVersion),
                    new XElement("HexalithSourceMappingApplies", "true"), new XElement("HexalithSourceMappingMode", "package")),
                source.Root!.Elements("Target").Where(target => (string?)target.Attribute("Name") is "ValidateAdminUiSettingsRoot" or "BuildNativeAdminUiArtifact"),
                new XElement("Target", new XAttribute("Name", "CaptureArtifact"), new XAttribute("AfterTargets", "BuildNativeAdminUiArtifact"),
                    new XElement("WriteLinesToFile", new XAttribute("File", Path.Combine(root, "reference.txt")), new XAttribute("Lines", "@(Reference)")))))
                .Save(project);
            var start = new ProcessStartInfo("python3");
            start.ArgumentList.Add("-c");
            start.ArgumentList.Add("import subprocess,sys; sys.exit(subprocess.call(sys.argv[1:]))");
            foreach (string argument in new[] { "dotnet", "msbuild", project, "-target:BuildNativeAdminUiArtifact", "-nologo", "-m:1", "-nr:false" })
            {
                start.ArgumentList.Add(argument);
            }
            PackageTestProcess.RemoveSourceMappingEnvironment(start);
            foreach (string key in new[] { "CustomBeforeDirectoryBuildProps", "CustomAfterMicrosoftCommonProps",
                "CustomBeforeDirectoryBuildTargets", "CustomAfterDirectoryBuildTargets", "AfterMicrosoftNETSdkTargets" })
            {
                start.Environment[key] = imports;
            }
            start.Environment["CustomAfterMicrosoftCommonTargets"] = nativeImport;
            // Five independently downloaded archives each support 93 seconds of retries;
            // retain that budget plus bounded restore, compilation and validation time.
            PackageTestProcessResult result = PackageTestProcess.Run(start, root, 600_000);
            result.ExitCode.ShouldBe(0, result.Diagnostic);
            string artifact = File.ReadAllText(Path.Combine(root, "reference.txt")).Trim();
            Path.GetFileName(artifact).ShouldBe("Hexalith.EventStore.Admin.UI.dll");
            Path.GetFullPath(artifact).ShouldStartWith(root + Path.DirectorySeparatorChar);
            string boundary = Path.Combine(root, "obj", "native-admin-ui", "qualified");
            using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(boundary, "prepared.json")));
            manifest.RootElement.GetProperty("sdkVersion").GetString().ShouldBe(sdkVersion);
            manifest.RootElement.GetProperty("targetFramework").GetString().ShouldBe("net11.0");
            manifest.RootElement.GetProperty("publicArchives").EnumerateArray().ShouldNotBeEmpty();
            using JsonDocument receipt = JsonDocument.Parse(File.ReadAllText(Path.Combine(boundary, "native-owner.json")));
            receipt.RootElement.GetProperty("targetPath").GetString().ShouldBe(artifact);
            JsonElement files = receipt.RootElement.GetProperty("files");
            foreach (string input in new[] { Path.Combine(owner, "src", "Hexalith.EventStore.Admin.UI", "Program.cs"), nativeImport,
                CiTestPaths.RepoFile("references/Hexalith.Builds/Props/Directory.Packages.props"),
                CiTestPaths.RepoFile("references/Hexalith.Builds/Props/Logging.Build.targets") })
            {
                files.TryGetProperty(input, out _).ShouldBeTrue("The actual evaluated import/source closure must bind " + input);
            }
            File.WriteAllText(nativeImport, "<Project><PropertyGroup><NativeInputMarker>changed</NativeInputMarker></PropertyGroup></Project>");
            PackageTestProcessResult changedImport = PackageTestProcess.Run("python3",
                [CiTestPaths.RepoFile("scripts/validate-aspire-runtime-cohort.py"), "--manifest", Path.Combine(boundary, "prepared.json"),
                    "--project", Path.Combine(owner, "src", "Hexalith.EventStore.Admin.UI", "Hexalith.EventStore.Admin.UI.csproj"),
                    "--version", sdkVersion, "--configuration", "Release", "--target-framework", "net11.0"], root);
            changedImport.ExitCode.ShouldNotBe(0);
            changedImport.Diagnostic.ShouldContain("drifted after completed build");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

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
                    "-p:Configuration=" + configuration, "-p:HexalithSourceMappingMode=" + mode,
                    "-p:HexalithSourceMappingApplies=" + (mode == "source" ? "true" : "false"), "-p:HexalithEventStoreVersion=" + SdkVersion], root);
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
