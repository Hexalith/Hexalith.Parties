using System.Diagnostics;
using System.Xml.Linq;

namespace Hexalith.Parties.Ci.Tests;

public sealed class CommonsHttpRestoreRoutingTests
{
    private const string CommonsHttpProjectReference = @"$(HexalithCommonsRoot)\src\libraries\Hexalith.Commons.Http\Hexalith.Commons.Http.csproj";
    private const string PackageCondition = "'$(HexalithCommonsHttpFromSource)' != 'true'";
    private const string PackageId = "Hexalith.Commons.Http";
    private const int PythonCaptureTimeoutMilliseconds = 5_000;
    private const int PythonProcessTimeoutMilliseconds = 30_000;
    private const string SourceCondition = "'$(HexalithCommonsHttpFromSource)' == 'true'";
    private const string SourceProperty = "HexalithCommonsHttpFromSource";

    [Fact]
    public void DirectoryBuildPropsDeclaresNarrowCommonsHttpSourceFallback()
    {
        XDocument props = XDocument.Load(CiTestPaths.RepoFile("Directory.Build.props"));
        XElement property = props.Descendants(SourceProperty).SingleOrDefault()
            ?? throw new InvalidOperationException($"{SourceProperty} was not found in Directory.Build.props.");

        property.Value.ShouldBe("true");
        props.Descendants("HexalithCommonsHttpPackageVersion").ShouldBeEmpty();
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
        string consumerValidationScript = CiTestPaths.ReadRepoFile("scripts/validate-consumer-package-references.py");
        (string centralAlias, string centralVersion) = ReadCentralCommonsHttpPackageVersion();

        centralAlias.ShouldBe("$(HexalithCommonsVersion)");
        centralVersion.ShouldNotBeNullOrWhiteSpace();
        centralVersion.ShouldNotContain("$(");
        packageVersion.Value.ShouldBe("$(HexalithCommonsHttpPackageVersion)");
        (packageVersion.Parent?.Attribute("Condition")?.Value ?? string.Empty)
            .ShouldBe("'$(MSBuildProjectName)' == 'Hexalith.Commons.Http' and '$(HexalithCommonsHttpPackageVersion)' != ''");
        packScript.ShouldContain("-p:HexalithPartiesPackageVersion={args.version}");
        packScript.ShouldContain("-p:HexalithCommonsHttpPackageVersion={commons_version}");
        packScript.ShouldContain("resolve_hexalith_commons_version");
        packScript.ShouldNotContain("HexalithCommonsHttpPackageVersion=$(");
        packScript.ShouldNotContain("-p:Version={args.version}");
        packScript.ShouldNotContain("-p:PackageVersion={args.version}");
        packScript.ShouldNotContain("-p:MinVerVersionOverride={args.version}");
        validationScript.ShouldContain("resolve_hexalith_commons_version");
        validationScript.ShouldNotContain("\"Hexalith.Commons.Http\": \"$(");
        validationScript.ShouldContain("\"Hexalith.Commons.UniqueIds\": commons_version");
        validationScript.ShouldContain("REQUIRED_COMMONS_HTTP_DEPENDENCY_PACKAGES");
        consumerValidationScript.ShouldContain("resolve_hexalith_commons_version");
        consumerValidationScript.ShouldContain("validate_commons_support_packages");
        consumerValidationScript.ShouldContain("-p:WarningsNotAsErrors=NU1603");
        consumerValidationScript.ShouldNotContain("\"2.27.0\"");
    }

    [Fact]
    public void MsbuildPropertyResolverReturnsCurrentCentralVersion()
    {
        (_, string centralVersion) = ReadCentralCommonsHttpPackageVersion();
        (int exitCode, string standardOutput, string standardError) = RunPython(
            "scripts/msbuild_properties.py",
            "src/Hexalith.Parties.Client/Hexalith.Parties.Client.csproj",
            "HexalithCommonsVersion");

        exitCode.ShouldBe(0, standardError);
        standardOutput.Trim().ShouldBe(centralVersion);
    }

    [Fact]
    public void MsbuildPropertyResolverRejectsEmptyAndUnresolvedValues()
    {
        const string probe = """
import subprocess
import sys
from pathlib import Path
from unittest.mock import patch

sys.path.insert(0, sys.argv[1])
import msbuild_properties

project_file = Path(sys.argv[2])
cases = {
    "empty": "\n",
    "whitespace": " 9.8.7 \n",
    "property-expression": "$(StillUnresolved)\n",
    "property-function": "$[System.String]::Copy('9.8.7')\n",
    "item-metadata": "%(Identity)\n",
    "item-list": "@(Items)\n",
    "multiline": "9.8.7\nnoise\n",
    "invalid-version": "not-a-version\n",
}
for label, standard_output in cases.items():
    completed = subprocess.CompletedProcess(["dotnet"], 0, stdout=standard_output, stderr="")
    with patch.object(msbuild_properties.subprocess, "run", return_value=completed):
        try:
            msbuild_properties.resolve_msbuild_property(project_file, "HexalithCommonsVersion")
        except msbuild_properties.MsbuildPropertyResolutionError as error:
            print(f"{label}|{error}")
        else:
            raise SystemExit(f"Expected failure for {label}")
""";

        (int exitCode, string standardOutput, string standardError) = RunPython(
            "-c",
            probe,
            CiTestPaths.RepoFile("scripts"),
            CiTestPaths.RepoFile("src/Hexalith.Parties.Client/Hexalith.Parties.Client.csproj"));
        string[] failures = standardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        exitCode.ShouldBe(0, standardError);
        failures.Length.ShouldBe(8);
        standardOutput.ShouldContain("empty|");
        standardOutput.ShouldContain("whitespace|");
        standardOutput.ShouldContain("property-expression|");
        standardOutput.ShouldContain("property-function|");
        standardOutput.ShouldContain("item-metadata|");
        standardOutput.ShouldContain("item-list|");
        standardOutput.ShouldContain("multiline|");
        standardOutput.ShouldContain("invalid-version|");
        standardOutput.ShouldContain("evaluated to an empty value");
        standardOutput.ShouldContain("remained unresolved");
        standardOutput.ShouldContain("multiline output");
        standardOutput.ShouldContain("not a valid NuGet version");
    }

    [Fact]
    public void MsbuildPropertyResolverTranslatesLaunchAndTimeoutFailures()
    {
        const string probe = """
import subprocess
import sys
from pathlib import Path
from unittest.mock import patch

sys.path.insert(0, sys.argv[1])
import msbuild_properties

project_file = Path(sys.argv[2])
failures = (
    ("launch", OSError("launch failed " + ("x" * 2000))),
    ("timeout", subprocess.TimeoutExpired("dotnet", 30, output="o" * 2000, stderr="e" * 2000)),
)
for label, failure in failures:
    with patch.object(msbuild_properties.subprocess, "run", side_effect=failure):
        try:
            msbuild_properties.resolve_msbuild_property(project_file, "HexalithCommonsVersion")
        except msbuild_properties.MsbuildPropertyResolutionError as error:
            message = str(error)
            if len(message) > 900:
                raise SystemExit(f"Unbounded {label} diagnostic: {len(message)}")
            print(f"{label}|{message}")
        else:
            raise SystemExit(f"Expected translated {label} failure")
""";

        (int exitCode, string standardOutput, string standardError) = RunPython(
            "-c",
            probe,
            CiTestPaths.RepoFile("scripts"),
            CiTestPaths.RepoFile("src/Hexalith.Parties.Client/Hexalith.Parties.Client.csproj"));

        exitCode.ShouldBe(0, standardError);
        standardOutput.ShouldContain("launch|Could not launch dotnet msbuild");
        standardOutput.ShouldContain("timeout|Timed out after 30 seconds");
    }

    [Fact]
    public void MsbuildPropertyResolverTracksCentralVersionChanges()
    {
        string temporaryDirectory = Path.Combine(Path.GetTempPath(), $"parties-msbuild-property-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        try
        {
            string projectFile = Path.Combine(temporaryDirectory, "VersionProbe.proj");
            File.WriteAllText(
                projectFile,
                """
<Project>
  <PropertyGroup>
    <HexalithCommonsVersion>9.8.7</HexalithCommonsVersion>
  </PropertyGroup>
</Project>
""");

            (int exitCode, string standardOutput, string standardError) = RunPython(
                "scripts/msbuild_properties.py",
                projectFile,
                "HexalithCommonsVersion");

            exitCode.ShouldBe(0, standardError);
            standardOutput.Trim().ShouldBe("9.8.7");
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Fact]
    public void ConsumerSupportFeedUsesResolvedVersionForBothCommonsPackages()
    {
        const string probe = """
import importlib.util
import sys
from pathlib import Path

script_file = Path(sys.argv[1]) / "validate-consumer-package-references.py"
spec = importlib.util.spec_from_file_location("consumer_package_validation", script_file)
if spec is None or spec.loader is None:
    raise SystemExit(f"Could not load {script_file}")
module = importlib.util.module_from_spec(spec)
sys.path.insert(0, sys.argv[1])
spec.loader.exec_module(module)

for project, version in module.support_package_projects("9.8.7"):
    if project in (
        "references/Hexalith.Commons/src/libraries/Hexalith.Commons.Http/Hexalith.Commons.Http.csproj",
        "references/Hexalith.Commons/src/libraries/Hexalith.Commons.UniqueIds/Hexalith.Commons.UniqueIds.csproj",
    ):
        print(f"{project}|{version}")
""";

        (int exitCode, string standardOutput, string standardError) = RunPython(
            "-c",
            probe,
            CiTestPaths.RepoFile("scripts"));
        string[] supportPackages = standardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        exitCode.ShouldBe(0, standardError);
        supportPackages.Length.ShouldBe(2);
        supportPackages.ShouldAllBe(static package => package.EndsWith("|9.8.7", StringComparison.Ordinal));
        supportPackages.ShouldContain(static package => package.Contains("Hexalith.Commons.Http.csproj", StringComparison.Ordinal));
        supportPackages.ShouldContain(static package => package.Contains("Hexalith.Commons.UniqueIds.csproj", StringComparison.Ordinal));
    }

    [Fact]
    public void SyntheticCentralVersionFlowsThroughEveryPackageScriptMainPath()
    {
        const string probe = """
import importlib.util
import subprocess
import sys
import tempfile
from pathlib import Path
from unittest.mock import patch

scripts_directory = Path(sys.argv[1])
sys.path.insert(0, str(scripts_directory))

def load_module(name, file_name):
    script_file = scripts_directory / file_name
    spec = importlib.util.spec_from_file_location(name, script_file)
    if spec is None or spec.loader is None:
        raise SystemExit(f"Could not load {script_file}")
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module

pack = load_module("pack_release_main_probe", "pack-release-packages.py")
validator = load_module("validate_nuget_main_probe", "validate-nuget-packages.py")
consumer = load_module("validate_consumer_main_probe", "validate-consumer-package-references.py")
central_version = "9.8.7"

with tempfile.TemporaryDirectory() as temporary_root:
    temporary_root = Path(temporary_root)

    pack_commands = []
    pack.resolve_hexalith_commons_version = lambda: central_version
    pack.subprocess.run = lambda command, **kwargs: pack_commands.append(command) or subprocess.CompletedProcess(command, 0)
    with patch.object(sys, "argv", ["pack-release-packages.py", str(temporary_root / "parties"), "1.2.3"]):
        if pack.main() != 0:
            raise SystemExit("Pack main did not succeed")
    expected_override = f"-p:HexalithCommonsHttpPackageVersion={central_version}"
    if len(pack_commands) != len(pack.PACKAGE_PROJECTS) or any(expected_override not in command for command in pack_commands):
        raise SystemExit("Pack main did not propagate the synthetic Commons version")
    print(f"pack|{central_version}")

    validation_directory = temporary_root / "validation"
    validation_directory.mkdir()
    for package_id in validator.EXPECTED_PACKAGE_IDS:
        (validation_directory / f"{package_id}.nupkg").touch()

    def package_metadata(package_path, unique_ids_version=central_version):
        package_id = package_path.stem
        dependencies = {
            validator.DependencyMetadata("Hexalith.Commons.UniqueIds", unique_ids_version),
        }
        if package_id in validator.REQUIRED_COMMONS_HTTP_DEPENDENCY_PACKAGES:
            dependencies.add(validator.DependencyMetadata("Hexalith.Commons.Http", central_version))
        return validator.PackageMetadata(
            package_id,
            "1.2.3",
            "README.md",
            True,
            frozenset(dependencies),
        )

    validator.resolve_hexalith_commons_version = lambda: central_version
    validator.get_metadata = package_metadata
    with patch.object(sys, "argv", ["validate-nuget-packages.py", str(validation_directory)]):
        if validator.main() != 0:
            raise SystemExit("NuGet validator main did not succeed")

    def wrong_unique_ids_metadata(package_path):
        version = "9.8.6" if package_path.stem == "Hexalith.Parties.Client" else central_version
        return package_metadata(package_path, version)

    validator.get_metadata = wrong_unique_ids_metadata
    with patch.object(sys, "argv", ["validate-nuget-packages.py", str(validation_directory)]):
        try:
            validator.main()
        except ValueError as error:
            if "Hexalith.Commons.UniqueIds" not in str(error) or central_version not in str(error):
                raise
        else:
            raise SystemExit("NuGet validator accepted the wrong direct UniqueIds version")
    print(f"validator|{central_version}")

    captured_consumer_version = []
    consumer.resolve_hexalith_commons_version = lambda repository_root: central_version
    consumer.package_versions = lambda package_directory: {
        package_id: "1.2.3" for package_id in consumer.PACKAGE_IDS
    }

    def capture_support_pack(output_directory, commons_version):
        captured_consumer_version.append(commons_version)
        output_directory.mkdir(parents=True)
        return output_directory

    consumer.pack_support_packages = capture_support_pack
    consumer.write_nuget_config = lambda *args: temporary_root / "NuGet.Config"
    consumer.write_client_consumer = lambda root, version: root / "ClientConsumer.csproj"
    consumer.write_portal_consumer = lambda root, version: root / "PortalConsumer.csproj"
    consumer.validate_consumer = lambda project_file: None
    consumer_packages = temporary_root / "consumer-packages"
    consumer_packages.mkdir()
    with patch.object(
        sys,
        "argv",
        [
            "validate-consumer-package-references.py",
            str(consumer_packages),
            "--work-directory",
            str(temporary_root / "consumer-work"),
        ],
    ):
        if consumer.main() != 0:
            raise SystemExit("Consumer validator main did not succeed")
    if captured_consumer_version != [central_version]:
        raise SystemExit(f"Consumer main propagated {captured_consumer_version!r}")
    print(f"consumer|{central_version}")
""";

        (int exitCode, string standardOutput, string standardError) = RunPython(
            "-c",
            probe,
            CiTestPaths.RepoFile("scripts"));

        exitCode.ShouldBe(0, standardError);
        standardOutput.ShouldContain("pack|9.8.7");
        standardOutput.ShouldContain("validator|9.8.7");
        standardOutput.ShouldContain("consumer|9.8.7");
    }

    [Fact]
    public void CommonsSupportPackingRequiresExactProducedPackageMetadata()
    {
        const string probe = """
import importlib.util
import subprocess
import sys
import tempfile
import zipfile
from pathlib import Path
from unittest.mock import patch

scripts_directory = Path(sys.argv[1])
sys.path.insert(0, str(scripts_directory))
script_file = scripts_directory / "validate-consumer-package-references.py"
spec = importlib.util.spec_from_file_location("consumer_support_pack_probe", script_file)
if spec is None or spec.loader is None:
    raise SystemExit(f"Could not load {script_file}")
module = importlib.util.module_from_spec(spec)
sys.modules["consumer_support_pack_probe"] = module
spec.loader.exec_module(module)

central_version = "9.8.7"
mode = {"value": "exact"}

def write_package(output_directory, package_id, file_version, metadata_version):
    package_path = output_directory / f"{package_id}.{file_version}.nupkg"
    nuspec = (
        "<package><metadata>"
        f"<id>{package_id}</id><version>{metadata_version}</version>"
        "</metadata></package>"
    )
    with zipfile.ZipFile(package_path, "w") as package:
        package.writestr(f"{package_id}.nuspec", nuspec)

def fake_pack(command, **kwargs):
    project_name = Path(command[2]).stem
    if project_name not in module.COMMONS_SUPPORT_PACKAGE_IDS:
        return subprocess.CompletedProcess(command, 0)
    if mode["value"] == "missing-http" and project_name == "Hexalith.Commons.Http":
        return subprocess.CompletedProcess(command, 0)

    output_directory = Path(command[command.index("--output") + 1])
    requested_version = next(
        argument.split("=", 1)[1]
        for argument in command
        if argument.startswith("-p:PackageVersion=")
    )
    metadata_version = (
        "9.8.6"
        if mode["value"] == "wrong-uniqueids" and project_name == "Hexalith.Commons.UniqueIds"
        else requested_version
    )
    write_package(output_directory, project_name, requested_version, metadata_version)
    return subprocess.CompletedProcess(command, 0)

with tempfile.TemporaryDirectory() as temporary_root:
    temporary_root = Path(temporary_root)
    with patch.object(module.subprocess, "run", side_effect=fake_pack):
        module.pack_support_packages(temporary_root / "exact", central_version)
        print("exact|accepted")

        mode["value"] = "wrong-uniqueids"
        try:
            module.pack_support_packages(temporary_root / "wrong", central_version)
        except ValueError as error:
            if "Hexalith.Commons.UniqueIds" not in str(error) or "metadata version 9.8.6" not in str(error):
                raise
            print("wrong|rejected")
        else:
            raise SystemExit("Wrong Commons.UniqueIds metadata was accepted")

        mode["value"] = "missing-http"
        try:
            module.pack_support_packages(temporary_root / "missing", central_version)
        except ValueError as error:
            if "Hexalith.Commons.Http" not in str(error) or "<none>" not in str(error):
                raise
            print("missing|rejected")
        else:
            raise SystemExit("Missing exact Commons.Http package was accepted")
""";

        (int exitCode, string standardOutput, string standardError) = RunPython(
            "-c",
            probe,
            CiTestPaths.RepoFile("scripts"));

        exitCode.ShouldBe(0, standardError);
        standardOutput.ShouldContain("exact|accepted");
        standardOutput.ShouldContain("wrong|rejected");
        standardOutput.ShouldContain("missing|rejected");
    }

    [Fact]
    public void PythonProcessHarnessDrainsBothStreamsAndEnforcesTimeout()
    {
        (int exitCode, string standardOutput, string standardError) = RunPython(
            "-c",
            "import sys; sys.stdout.write('o' * 200000); sys.stderr.write('e' * 200000)");

        exitCode.ShouldBe(0);
        standardOutput.Length.ShouldBe(200_000);
        standardError.Length.ShouldBe(200_000);
        Should.Throw<TimeoutException>(() => RunPython(
            100,
            "-c",
            "import time; time.sleep(5)"));
    }

    private static (string Alias, string Version) ReadCentralCommonsHttpPackageVersion()
    {
        XDocument props = XDocument.Load(CiTestPaths.RepoFile("references/Hexalith.Builds/Props/Directory.Packages.props"));
        string alias = props
            .Descendants("PackageVersion")
            .Single(element => element.Attribute("Include")?.Value == PackageId)
            .Attribute("Version")?.Value
            ?? throw new InvalidOperationException($"{PackageId} version was not found in shared package props.");
        string propertyName = alias.StartsWith("$(", StringComparison.Ordinal) && alias.EndsWith(')')
            ? alias[2..^1]
            : throw new InvalidOperationException($"{PackageId} version '{alias}' is not a central property alias.");
        string version = props.Descendants(propertyName).SingleOrDefault()?.Value
            ?? throw new InvalidOperationException($"Central property {propertyName} was not found in shared package props.");

        return (alias, version);
    }

    private static (int ExitCode, string StandardOutput, string StandardError) RunPython(params string[] arguments)
        => RunPython(PythonProcessTimeoutMilliseconds, arguments);

    private static (int ExitCode, string StandardOutput, string StandardError) RunPython(
        int timeoutMilliseconds,
        params string[] arguments)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = "python3",
            WorkingDirectory = CiTestPaths.RepositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = new() { StartInfo = startInfo };
        process.Start().ShouldBeTrue();
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit(timeoutMilliseconds))
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // The process exited between the timeout and the kill request.
            }

            process.WaitForExit(PythonCaptureTimeoutMilliseconds);
            Task.WaitAll([standardOutput, standardError], PythonCaptureTimeoutMilliseconds);
            throw new TimeoutException($"Python process exceeded the {timeoutMilliseconds} ms test timeout.");
        }

        if (!Task.WaitAll([standardOutput, standardError], PythonCaptureTimeoutMilliseconds))
        {
            throw new TimeoutException("Python output capture did not complete after the process exited.");
        }

        return (process.ExitCode, standardOutput.Result, standardError.Result);
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
