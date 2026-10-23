using System.Diagnostics;
using System.Xml.Linq;

using Hexalith.Parties.PackageTests;

namespace Hexalith.Parties.Ci.Tests;

public sealed class CommonsHttpRestoreRoutingTests
{
    private const string CommonsHttpProjectReference = @"$(HexalithCommonsRoot)\src\libraries\Hexalith.Commons.Http\Hexalith.Commons.Http.csproj";
    private const string EventStoreContractsPackageId = "Hexalith.EventStore.Contracts";
    private const string PackageCondition = "'$(HexalithCommonsHttpFromSource)' != 'true'";
    private const string PackageId = "Hexalith.Commons.Http";
    private const int PythonCaptureTimeoutMilliseconds = 5_000;
    private const int PythonProcessTimeoutMilliseconds = 30_000;
    private const string SourceCondition = "'$(HexalithCommonsHttpFromSource)' == 'true'";
    private const string SourceProperty = "HexalithCommonsHttpFromSource";

    [Fact]
    public void DirectoryBuildPropsLeavesSourceAndPackageSelectionToThePinnedTool()
    {
        XDocument props = XDocument.Load(CiTestPaths.RepoFile("Directory.Build.props"));
        props.Descendants(SourceProperty).ShouldBeEmpty();
        props.Descendants("UseHexalithProjectReferences").ShouldBeEmpty();
        props.Descendants("UseNuGetDeps").ShouldBeEmpty();
        props.Descendants("HexalithCommonsRoot").ShouldBeEmpty();
        props.Descendants("HexalithCommonsHttpPackageVersion").ShouldBeEmpty();
        string packages = CiTestPaths.ReadRepoFile("Directory.Packages.props");
        XDocument packageProps = XDocument.Parse(packages);
        packageProps.Descendants("PackageVersion").ShouldBeEmpty();
        packageProps.Descendants("HexalithBuildsRoot").ShouldBeEmpty();
        packages.ShouldContain("$(Hexalith1BuildPackageProps)");
        packages.ShouldContain("$(Hexalith2BuildPackageProps)");
        packages.ShouldContain("$(MSBuildThisFileDirectory)../Hexalith.Builds/Props/Directory.Packages.props");
        packages.ShouldContain("Exists('$(Hexalith1BuildPackageProps)') And '$(HexalithVersionsLoaded)' != 'true'");
        packages.ShouldContain("Exists('$(Hexalith2BuildPackageProps)') And '$(HexalithVersionsLoaded)' != 'true'");
        XElement buildsRoot = props.Descendants("HexalithBuildsRoot").Single();
        buildsRoot.Attribute("Condition")!.Value.ShouldBe("'$(HexalithBuildsRoot)' == ''");
        buildsRoot.Value.ShouldBe("$(MSBuildThisFileDirectory)references/Hexalith.Builds");
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
        (string eventStoreAlias, string eventStoreVersion) = ReadCentralPackageVersion(EventStoreContractsPackageId);

        centralAlias.ShouldBe("$(HexalithCommonsVersion)");
        centralVersion.ShouldNotBeNullOrWhiteSpace();
        centralVersion.ShouldNotContain("$(");
        eventStoreAlias.ShouldBe("$(HexalithEventStoreVersion)");
        eventStoreVersion.ShouldNotBeNullOrWhiteSpace();
        eventStoreVersion.ShouldNotContain("$(");
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
        validationScript.ShouldContain("resolve_hexalith_event_store_version");
        validationScript.ShouldContain("validate_event_store_dependency_versions");
        validationScript.ShouldContain("REQUIRED_EVENT_STORE_DEPENDENCY_ID");
        consumerValidationScript.ShouldContain("resolve_hexalith_commons_version");
        consumerValidationScript.ShouldContain("resolve_hexalith_event_store_version");
        consumerValidationScript.ShouldContain("validate_commons_support_packages");
        consumerValidationScript.ShouldContain("validate_event_store_support_packages");
        consumerValidationScript.ShouldContain("-p:WarningsNotAsErrors=NU1603");
        consumerValidationScript.ShouldNotContain("\"2.27.0\"");
        consumerValidationScript.ShouldNotContain("\"3.47.0\"");
    }

    [Fact]
    public void MsbuildPropertyResolverReturnsCurrentCentralVersion()
    {
        foreach (string packageId in new[]
        {
            PackageId,
            EventStoreContractsPackageId,
            "Hexalith.FrontComposer.Contracts",
            "Hexalith.Tenants.Contracts",
        })
        {
            (string alias, string centralVersion) = ReadCentralPackageVersion(packageId);
            (int exitCode, string output, string error) = RunPython(
                "scripts/msbuild_properties.py",
                "src/Hexalith.Parties.Client/Hexalith.Parties.Client.csproj",
                alias[2..^1]);

            exitCode.ShouldBe(0, error);
            output.Trim().ShouldBe(centralVersion);
        }
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
        const string probe = """
import sys
import tempfile
from pathlib import Path

sys.path.insert(0, sys.argv[1])
import msbuild_properties

resolvers = (
    ("HexalithCommonsVersion", msbuild_properties.resolve_hexalith_commons_version),
    ("HexalithEventStoreVersion", msbuild_properties.resolve_hexalith_event_store_version),
    ("HexalithFrontComposerVersion", msbuild_properties.resolve_hexalith_front_composer_version),
    ("HexalithTenantsVersion", msbuild_properties.resolve_hexalith_tenants_version),
)
with tempfile.TemporaryDirectory() as temporary_root:
    root = Path(temporary_root)
    project_file = root / "src/Hexalith.Parties.Client/Hexalith.Parties.Client.csproj"
    project_file.parent.mkdir(parents=True)
    project_file.write_text('<Project><Import Project="../../central.props" /></Project>', encoding="utf-8")
    for generation, versions in enumerate((
        ("9.8.7", "7.6.5", "6.5.4-preview.2", "5.4.3"),
        ("9.8.8", "7.6.6", "6.5.5", "5.4.4-preview.1"),
    )):
        properties = "".join(
            f"<{name} Condition=\"'$(Configuration)' == 'Release'\">{version}</{name}>"
            for (name, _), version in zip(resolvers, versions)
        )
        (root / "central.props").write_text(
            f"<Project><PropertyGroup>{properties}</PropertyGroup></Project>", encoding="utf-8"
        )
        selected = tuple(resolve(root) for _, resolve in resolvers)
        if selected != versions:
            raise SystemExit(f"Generation {generation} expected {versions!r}, found {selected!r}")
        print(f"central-generation|{generation}|{'|'.join(selected)}")
""";

        // Eight bounded MSBuild evaluations run sequentially inside this probe.
        int probeTimeoutMilliseconds = (8 * PythonProcessTimeoutMilliseconds) + PythonCaptureTimeoutMilliseconds;
        (int exitCode, string standardOutput, string standardError) = RunPython(
            probeTimeoutMilliseconds,
            "-c",
            probe,
            CiTestPaths.RepoFile("scripts"));

        exitCode.ShouldBe(0, standardError);
        standardOutput.ShouldContain("central-generation|0|9.8.7|7.6.5|6.5.4-preview.2|5.4.3");
        standardOutput.ShouldContain("central-generation|1|9.8.8|7.6.6|6.5.5|5.4.4-preview.1");
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

for project, version in module.support_package_projects("9.8.7", "7.6.5", "6.5.4", "5.4.3"):
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
    public void ConsumerSupportProjectsAreDirectSolutionMembers()
    {
        const string probe = """
import importlib.util
import sys
from pathlib import Path

scripts_directory = Path(sys.argv[1])
script_file = scripts_directory / "validate-consumer-package-references.py"
spec = importlib.util.spec_from_file_location("consumer_solution_inventory_probe", script_file)
if spec is None or spec.loader is None:
    raise SystemExit(f"Could not load {script_file}")
module = importlib.util.module_from_spec(spec)
sys.path.insert(0, str(scripts_directory))
sys.modules["consumer_solution_inventory_probe"] = module
spec.loader.exec_module(module)

for project, _ in module.support_package_projects("0.0.0", "0.0.0", "0.0.0", "0.0.0"):
    print(project)
""";

        (int exitCode, string standardOutput, string standardError) = RunPython(
            "-c",
            probe,
            CiTestPaths.RepoFile("scripts"));
        string[] supportProjectPaths = standardOutput
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static path => path.Replace('\\', '/'))
            .ToArray();
        HashSet<string> solutionProjectPaths = XDocument
            .Load(CiTestPaths.RepoFile("Hexalith.Parties.slnx"))
            .Descendants("Project")
            .Select(static element => element.Attribute("Path")?.Value)
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(static path => path!.Replace('\\', '/'))
            .ToHashSet(StringComparer.Ordinal);
        string[] omittedProjectPaths = supportProjectPaths
            .Where(path => !solutionProjectPaths.Contains(path))
            .Order(StringComparer.Ordinal)
            .ToArray();

        exitCode.ShouldBe(0, standardError);
        supportProjectPaths.Length.ShouldBe(8);
        supportProjectPaths
            .Distinct(StringComparer.Ordinal)
            .Count()
            .ShouldBe(supportProjectPaths.Length, "Support package projects must not be duplicated.");
        omittedProjectPaths.ShouldBeEmpty(
            $"Every support project returned by validate-consumer-package-references.py must be a direct "
            + $"Hexalith.Parties.slnx project so the Release configuration is applied. Omitted paths:{Environment.NewLine}"
            + string.Join(Environment.NewLine, omittedProjectPaths.Select(static path => $"- {path}")));
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
import msbuild_properties

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
commons_version = "9.8.7"
event_store_version = "7.6.5"
front_composer_version = "6.5.4-preview.2"
tenants_version = "5.4.3"

with tempfile.TemporaryDirectory() as temporary_root:
    temporary_root = Path(temporary_root)

    pack_commands = []
    pack.resolve_hexalith_commons_version = lambda: commons_version
    pack.subprocess.run = lambda command, **kwargs: pack_commands.append(command) or subprocess.CompletedProcess(command, 0)
    with patch.object(sys, "argv", ["pack-release-packages.py", str(temporary_root / "parties"), "1.2.3"]):
        if pack.main() != 0:
            raise SystemExit("Pack main did not succeed")
    expected_override = f"-p:HexalithCommonsHttpPackageVersion={commons_version}"
    if len(pack_commands) != len(pack.PACKAGE_PROJECTS) or any(expected_override not in command for command in pack_commands):
        raise SystemExit("Pack main did not propagate the synthetic Commons version")
    print(f"pack|{commons_version}")

    validation_directory = temporary_root / "validation"
    validation_directory.mkdir()
    for package_id in validator.EXPECTED_PACKAGE_IDS:
        (validation_directory / f"{package_id}.nupkg").touch()

    def package_metadata(
        package_path,
        unique_ids_version=commons_version,
        contracts_version=event_store_version,
        client_version=event_store_version,
        client_id="Hexalith.EventStore.Client",
        include_event_store_dependency=True,
    ):
        package_id = package_path.stem
        dependencies = [
            validator.DependencyMetadata("Hexalith.Commons.UniqueIds", unique_ids_version),
        ]
        if include_event_store_dependency:
            dependencies.append(
                validator.DependencyMetadata("Hexalith.EventStore.Contracts", contracts_version)
            )
        if package_id == "Hexalith.Parties.AdminPortal":
            dependencies.append(
                validator.DependencyMetadata(client_id, client_version)
            )
        if package_id in validator.REQUIRED_COMMONS_HTTP_DEPENDENCY_PACKAGES:
            dependencies.append(validator.DependencyMetadata("Hexalith.Commons.Http", commons_version))
        if package_id == "Hexalith.Parties.Aspire":
            dependencies = [validator.DependencyMetadata("Aspire.Hosting", "13.6.1"),
                            validator.DependencyMetadata("Hexalith.EventStore.Aspire", contracts_version)]
        return validator.PackageMetadata(
            package_id,
            "1.2.3",
            "README.md",
            True,
            frozenset(dependencies),
            (validator.DependencyGroupMetadata("net10.0", tuple(dependencies)),),
        )

    validator.resolve_hexalith_commons_version = lambda: commons_version
    validator.resolve_hexalith_event_store_version = lambda: event_store_version
    validator.get_metadata = package_metadata
    with patch.object(sys, "argv", ["validate-nuget-packages.py", str(validation_directory)]):
        if validator.main() != 0:
            raise SystemExit("NuGet validator main did not succeed")

    def wrong_unique_ids_metadata(package_path):
        version = "9.8.6" if package_path.stem == "Hexalith.Parties.Client" else commons_version
        return package_metadata(package_path, version)

    validator.get_metadata = wrong_unique_ids_metadata
    with patch.object(sys, "argv", ["validate-nuget-packages.py", str(validation_directory)]):
        try:
            validator.main()
        except ValueError as error:
            if "Hexalith.Commons.UniqueIds" not in str(error) or commons_version not in str(error):
                raise
        else:
            raise SystemExit("NuGet validator accepted the wrong direct UniqueIds version")
    def wrong_event_store_metadata(package_path):
        client_version = "7.6.4" if package_path.stem == "Hexalith.Parties.AdminPortal" else event_store_version
        return package_metadata(package_path, client_version=client_version)

    validator.get_metadata = wrong_event_store_metadata
    with patch.object(sys, "argv", ["validate-nuget-packages.py", str(validation_directory)]):
        try:
            validator.main()
        except ValueError as error:
            if "Hexalith.EventStore.Client=7.6.4" not in str(error) or event_store_version not in str(error):
                raise
        else:
            raise SystemExit("NuGet validator accepted a stale non-required EventStore dependency version")

    def empty_event_store_metadata(package_path):
        client_version = "" if package_path.stem == "Hexalith.Parties.AdminPortal" else event_store_version
        return package_metadata(package_path, client_version=client_version)

    validator.get_metadata = empty_event_store_metadata
    with patch.object(sys, "argv", ["validate-nuget-packages.py", str(validation_directory)]):
        try:
            validator.main()
        except ValueError as error:
            if "Hexalith.EventStore.Client=<missing>" not in str(error):
                raise
        else:
            raise SystemExit("NuGet validator accepted an empty non-required EventStore dependency version")

    def case_variant_event_store_metadata(package_path):
        client_version = "7.6.4" if package_path.stem == "Hexalith.Parties.AdminPortal" else event_store_version
        return package_metadata(
            package_path,
            client_version=client_version,
            client_id="hexalith.eventstore.client",
        )

    validator.get_metadata = case_variant_event_store_metadata
    with patch.object(sys, "argv", ["validate-nuget-packages.py", str(validation_directory)]):
        try:
            validator.main()
        except ValueError as error:
            if "hexalith.eventstore.client=7.6.4" not in str(error):
                raise
        else:
            raise SystemExit("NuGet validator allowed case variation to bypass EventStore validation")

    def missing_event_store_metadata(package_path):
        return package_metadata(
            package_path,
            include_event_store_dependency=package_path.stem != "Hexalith.Parties.Testing",
        )

    validator.get_metadata = missing_event_store_metadata
    with patch.object(sys, "argv", ["validate-nuget-packages.py", str(validation_directory)]):
        try:
            validator.main()
        except ValueError as error:
            if "Hexalith.EventStore.Contracts" not in str(error) or "<missing>" not in str(error):
                raise
        else:
            raise SystemExit("NuGet validator accepted a missing required EventStore dependency")
    print(f"validator|{commons_version}|{event_store_version}")

    captured_consumer_versions = []
    captured_consumer_validations = []
    central_versions = {
        "HexalithCommonsVersion": commons_version,
        "HexalithEventStoreVersion": event_store_version,
        "HexalithFrontComposerVersion": front_composer_version,
        "HexalithTenantsVersion": tenants_version,
    }
    resolved_properties = []
    def resolve_property(project_file, property_name, **kwargs):
        if project_file != consumer.REPO_ROOT / "src/Hexalith.Parties.Client/Hexalith.Parties.Client.csproj":
            raise SystemExit(f"Wrong version evaluation project: {project_file}")
        if kwargs.get("working_directory") != consumer.REPO_ROOT:
            raise SystemExit("Version evaluation did not use the repository root")
        resolved_properties.append(property_name)
        return central_versions[property_name]
    msbuild_properties.resolve_msbuild_property = resolve_property
    consumer.package_versions = lambda package_directory: {
        package_id: "1.2.3" for package_id in consumer.PACKAGE_IDS
    }

    def capture_support_pack(output_directory, *versions):
        captured_consumer_versions.append(versions)
        output_directory.mkdir(parents=True)
        return output_directory

    consumer.pack_support_packages = capture_support_pack
    consumer.write_nuget_config = lambda *args: temporary_root / "NuGet.Config"
    consumer.write_client_consumer = lambda root, version: root / "ClientConsumer.csproj"
    consumer.write_portal_consumer = lambda root, version: root / "PortalConsumer.csproj"
    consumer.validate_consumer = lambda project_file, version, required, front, tenants, **options: captured_consumer_validations.append(
        (project_file.name, version, required, front, tenants, options)
    )
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
    if captured_consumer_versions != [(commons_version, event_store_version, front_composer_version, tenants_version)]:
        raise SystemExit(f"Consumer main propagated {captured_consumer_versions!r}")
    if resolved_properties != list(central_versions):
        raise SystemExit(f"Consumer main did not resolve every central property: {resolved_properties!r}")
    if captured_consumer_validations != [
        ("ClientConsumer.csproj", event_store_version, consumer.CLIENT_REQUIRED_EVENT_STORE_PACKAGE_IDS,
         front_composer_version, tenants_version, {"require_portal_packages": False}),
        ("PortalConsumer.csproj", event_store_version, consumer.PORTAL_REQUIRED_EVENT_STORE_PACKAGE_IDS,
         front_composer_version, tenants_version, {"require_portal_packages": True}),
    ]:
        raise SystemExit(f"Consumer asset validations were not wired correctly: {captured_consumer_validations!r}")
    print(f"consumer|{commons_version}|{event_store_version}")

    for failed_property in ("HexalithFrontComposerVersion", "HexalithTenantsVersion"):
        sentinel = temporary_root / "consumer-work" / "keep-on-resolution-failure.txt"
        sentinel.write_text("existing workspace", encoding="utf-8")
        def fail_resolution(project_file, property_name, **kwargs):
            if property_name == failed_property:
                raise msbuild_properties.MsbuildPropertyResolutionError(f"Unavailable {property_name}")
            return resolve_property(project_file, property_name, **kwargs)
        msbuild_properties.resolve_msbuild_property = fail_resolution
        with patch.object(sys, "argv", [
            "validate-consumer-package-references.py", str(consumer_packages),
            "--work-directory", str(temporary_root / "consumer-work"),
        ]):
            try:
                consumer.main()
            except msbuild_properties.MsbuildPropertyResolutionError as error:
                if failed_property not in str(error):
                    raise
            else:
                raise SystemExit(f"Consumer continued after {failed_property} resolution failed")
        if sentinel.read_text(encoding="utf-8") != "existing workspace" or len(captured_consumer_versions) != 1:
            raise SystemExit("Consumer modified the workspace or packed after version resolution failed")
        print(f"resolution-failure|{failed_property}|preserved-workspace")
""";

        (int exitCode, string standardOutput, string standardError) = RunPython(
            "-c",
            probe,
            CiTestPaths.RepoFile("scripts"));

        exitCode.ShouldBe(0, standardError);
        standardOutput.ShouldContain("pack|9.8.7");
        standardOutput.ShouldContain("validator|9.8.7|7.6.5");
        standardOutput.ShouldContain("consumer|9.8.7|7.6.5");
        standardOutput.ShouldContain("resolution-failure|HexalithFrontComposerVersion|preserved-workspace");
        standardOutput.ShouldContain("resolution-failure|HexalithTenantsVersion|preserved-workspace");
    }

    [Fact]
    public void ConsumerSourcesUsePublicDefaultOnlyWithoutExplicitOverrides()
    {
        const string script = """
import importlib.util
import sys
import tempfile
from pathlib import Path
from unittest.mock import patch
from xml.etree import ElementTree

scripts = Path(sys.argv[1])
sys.path.insert(0, str(scripts))
spec = importlib.util.spec_from_file_location("consumer_source_probe", scripts / "validate-consumer-package-references.py")
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
for name in ("commons", "event_store", "front_composer", "tenants"):
    setattr(module, f"resolve_hexalith_{name}_version", lambda root: "1.0.0")
module.package_versions = lambda directory: {package_id: "1.0.0" for package_id in module.PACKAGE_IDS}
module.pack_support_packages = lambda output, *versions: output
validated = []
module.validate_consumer = lambda project, *args, **kwargs: validated.append(project.name)

with tempfile.TemporaryDirectory() as directory:
    root = Path(directory)
    packages = root / "packages & artifacts"
    packages.mkdir()
    custom = "https://feed.example/v3/index.json?one=1&two=2"
    cases = [
        ([], ["https://api.nuget.org/v3/index.json"]),
        (["--nuget-source", custom], [custom]),
        (["--nuget-source", custom, "--nuget-source", "https://second.example/v3/index.json"],
         [custom, "https://second.example/v3/index.json"]),
        (["--nuget-source", custom, "--nuget-source", "https://api.nuget.org/v3/index.json"],
         [custom, "https://api.nuget.org/v3/index.json"]),
    ]
    for index, (arguments, expected) in enumerate(cases):
        work = root / f"work-{index}"
        with patch.object(sys, "argv", ["validator", str(packages), "--work-directory", str(work), *arguments]):
            assert module.main() == 0
        sources = ElementTree.parse(work / "NuGet.Config").getroot().find("packageSources")
        assert sources is not None
        assert [child.tag for child in sources] == ["clear", *(["add"] * (2 + len(expected)))], ElementTree.tostring(sources)
        assert [child.attrib["value"] for child in sources.findall("add")] == [
            str(packages.resolve()), str((work / "support-packages").resolve()), *expected
        ]
    assert validated == ["ClientConsumer.csproj", "PortalConsumer.csproj"] * len(cases), validated
print("source-selection-cases=4")
""";

        // Keep probe assertions enabled even when the caller sets PYTHONOPTIMIZE.
        (int exitCode, string standardOutput, string standardError) = RunPython(
            "-E", "-c", script, CiTestPaths.RepoFile("scripts"));

        exitCode.ShouldBe(0, standardError + standardOutput);
        standardOutput.ShouldContain("source-selection-cases=4");
    }

    [Fact]
    public void ConsumerRestoreCannotResolvePackagesFromUndeclaredInheritedFeed()
    {
        const string script = """"
import importlib.util
import json
import subprocess
import sys
import tempfile
import zipfile
from pathlib import Path
from xml.etree import ElementTree

scripts = Path(sys.argv[1])
sys.path.insert(0, str(scripts))
spec = importlib.util.spec_from_file_location("consumer_restore_source_probe", scripts / "validate-consumer-package-references.py")
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)

with tempfile.TemporaryDirectory() as directory:
    root = Path(directory)
    inherited = root / "undeclared-feed"
    inherited.mkdir()
    package_id = "DW69.InheritedOnly"
    with zipfile.ZipFile(inherited / f"{package_id}.1.0.0.nupkg", "w") as package:
        package.writestr(f"{package_id}.nuspec", f"""<package><metadata>
<id>{package_id}</id><version>1.0.0</version><authors>Tests</authors>
<description>Offline feed isolation probe</description></metadata></package>""")
    configuration = ElementTree.Element("configuration")
    sources = ElementTree.SubElement(configuration, "packageSources")
    ElementTree.SubElement(sources, "clear")
    ElementTree.SubElement(sources, "add", {"key": "undeclared-parent-feed", "value": str(inherited)})
    ElementTree.ElementTree(configuration).write(root / "NuGet.Config", encoding="utf-8", xml_declaration=True)

    for explicit in (False, True):
        work = root / ("explicit" if explicit else "isolated")
        work.mkdir()
        feeds = [work / "local-packages", work / "support-packages"]
        for feed in feeds:
            feed.mkdir()
        module.write_nuget_config(work, feeds, [str(inherited)] if explicit else [])
        project_dir = work / "consumer"
        project_dir.mkdir()
        project = project_dir / "Consumer.csproj"
        project.write_text(f"""<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>
<TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup>
<PackageReference Include="{package_id}" Version="[1.0.0]" />
</ItemGroup></Project>""", encoding="utf-8")
        try:
            module.run_dotnet(["restore", str(project), "--verbosity", "minimal", "-p:NuGetAudit=false"], project_dir)
        except subprocess.CalledProcessError:
            if explicit:
                raise
        else:
            assert explicit, "Undeclared inherited feed satisfied a missing consumer package"
        assets = json.loads((project_dir / "obj" / "project.assets.json").read_text(encoding="utf-8"))
        expected_sources = [str(feed.resolve()) for feed in feeds] + ([str(inherited)] if explicit else [])
        assert set(assets["project"]["restore"]["sources"]) == set(expected_sources), assets["project"]["restore"]["sources"]
        if explicit:
            assert f"{package_id}/1.0.0" in assets["libraries"], assets["libraries"]
        else:
            assert any(entry.get("code") == "NU1101" for entry in assets.get("logs", [])), assets.get("logs")
print("inherited-feed-denied-explicit-feed-restored")
"""";

        // Keep probe assertions enabled even when the caller sets PYTHONOPTIMIZE.
        (int exitCode, string standardOutput, string standardError) = RunPython(
            60_000, "-E", "-c", script, CiTestPaths.RepoFile("scripts"));

        exitCode.ShouldBe(0, standardError + standardOutput);
        standardOutput.ShouldContain("inherited-feed-denied-explicit-feed-restored");
    }

    [Fact]
    public void ConsumerAssetsRejectStaleSelectedEventStorePackage()
    {
        const string probe = """
import importlib.util
import json
import sys
import tempfile
from pathlib import Path

scripts_directory = Path(sys.argv[1])
sys.path.insert(0, str(scripts_directory))
script_file = scripts_directory / "validate-consumer-package-references.py"
spec = importlib.util.spec_from_file_location("consumer_assets_probe", script_file)
if spec is None or spec.loader is None:
    raise SystemExit(f"Could not load {script_file}")
module = importlib.util.module_from_spec(spec)
sys.modules["consumer_assets_probe"] = module
spec.loader.exec_module(module)

with tempfile.TemporaryDirectory() as temporary_root:
    assets_file = Path(temporary_root) / "project.assets.json"
    assets_file.write_text(json.dumps({
        "libraries": {
            "Hexalith.EventStore.Contracts/7.6.5": {"type": "package"},
            "hexalith.eventstore.client/7.6.4": {"type": "package"},
        }
    }), encoding="utf-8")
    try:
        module.validate_event_store_assets(
            assets_file,
            "7.6.5",
            module.PORTAL_REQUIRED_EVENT_STORE_PACKAGE_IDS,
        )
    except ValueError as error:
        message = str(error)
        if "hexalith.eventstore.client=7.6.4" not in message or "7.6.5" not in message:
            raise
        print("stale-selected|rejected")
    else:
        raise SystemExit("Consumer assets validator accepted a stale selected EventStore package")
""";

        (int exitCode, string standardOutput, string standardError) = RunPython(
            "-c",
            probe,
            CiTestPaths.RepoFile("scripts"));

        exitCode.ShouldBe(0, standardError);
        standardOutput.ShouldContain("stale-selected|rejected");
    }

    [Fact]
    public void NuspecValidatorPreservesAndValidatesEveryDependencyGroup()
    {
        const string probe = """"
import dataclasses
import importlib.util
import sys
import tempfile
import zipfile
from pathlib import Path

scripts_directory = Path(sys.argv[1])
sys.path.insert(0, str(scripts_directory))
script_file = scripts_directory / "validate-nuget-packages.py"
spec = importlib.util.spec_from_file_location("nuspec_groups_probe", script_file)
if spec is None or spec.loader is None:
    raise SystemExit(f"Could not load {script_file}")
module = importlib.util.module_from_spec(spec)
sys.modules["nuspec_groups_probe"] = module
spec.loader.exec_module(module)

with tempfile.TemporaryDirectory() as temporary_root:
    package_path = Path(temporary_root) / "Hexalith.Parties.Client.1.2.3.nupkg"
    nuspec = """<?xml version="1.0"?>
<package>
  <metadata>
    <id>Hexalith.Parties.Client</id>
    <version>1.2.3</version>
    <readme>README.md</readme>
    <license type="expression">MIT</license>
    <dependencies>
      <group targetFramework="net9.0">
        <dependency id="hexalith.eventstore.contracts" version="3.90.0" />
      </group>
      <group targetFramework="net10.0">
        <dependency id="Hexalith.EventStore.Contracts" version="3.90.0" />
        <dependency id="Hexalith.EventStore.Client" version="3.90.0" />
      </group>
    </dependencies>
  </metadata>
</package>
"""
    with zipfile.ZipFile(package_path, "w") as package:
        package.writestr("Hexalith.Parties.Client.nuspec", nuspec)
        package.writestr("README.md", "readme")

    metadata = module.get_metadata(package_path)
    if [group.target_framework for group in metadata.dependency_groups] != ["net9.0", "net10.0"]:
        raise SystemExit(f"Nuspec dependency groups were not preserved: {metadata.dependency_groups!r}")
    module.validate_event_store_dependency_versions(package_path, metadata, "3.90.0")

    missing_contracts = dataclasses.replace(
        metadata,
        dependency_groups=(
            metadata.dependency_groups[0],
            module.DependencyGroupMetadata(
                "net10.0",
                (module.DependencyMetadata("Hexalith.EventStore.Client", "3.90.0"),),
            ),
        ),
    )
    try:
        module.validate_event_store_dependency_versions(package_path, missing_contracts, "3.90.0")
    except ValueError as error:
        if "net10.0" not in str(error) or "exactly one" not in str(error):
            raise
        print("missing-group-contracts|rejected")
    else:
        raise SystemExit("Nuspec validator accepted a group without EventStore.Contracts")
"""";

        (int exitCode, string standardOutput, string standardError) = RunPython(
            "-c",
            probe,
            CiTestPaths.RepoFile("scripts"));

        exitCode.ShouldBe(0, standardError);
        standardOutput.ShouldContain("missing-group-contracts|rejected");
    }

    [Theory]
    [InlineData("Hexalith.Parties", true)]
    [InlineData("Hexalith.Parties.AppHost", true)]
    [InlineData("Hexalith.Parties.Mcp", true)]
    [InlineData("Hexalith.Parties.UI", true)]
    [InlineData("External.Tests.Helpers", true)]
    [InlineData("External.Test.Helpers", true)]
    [InlineData("External.Sample.Helpers", true)]
    [InlineData("External.Samples.Helpers", true)]
    [InlineData("External.AppHost.Helpers", true)]
    [InlineData("External.TeStTools", true)]
    [InlineData("External.SaMpLeTools", true)]
    [InlineData("External.ApPhOsTTools", true)]
    [InlineData("Hexalith.Parties;hExAlItH.pArTiEs.uI;ExTeRnAl.tEsT.sUpPoRt", true)]
    [InlineData("Hexalith.Parties.Client", false)]
    [InlineData("Hexalith.Parties.Security", false)]
    [InlineData("Hexalith.Parties.McpClient", false)]
    [InlineData("Hexalith.Parties.UITools", false)]
    [InlineData("External.UnitTesting.Helpers", false)]
    public void NuspecDependencyBoundariesIgnoreCaseAndPreserveDiagnostics(string dependencyIds, bool forbidden)
    {
        const string probe = """
import importlib.util
import sys
import tempfile
import zipfile
from pathlib import Path
from xml.etree import ElementTree

scripts_directory = Path(sys.argv[1])
sys.path.insert(0, str(scripts_directory))
script_file = scripts_directory / "validate-nuget-packages.py"
spec = importlib.util.spec_from_file_location("nuspec_boundaries_probe", script_file)
if spec is None or spec.loader is None:
    raise SystemExit(f"Could not load {script_file}")
module = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = module
spec.loader.exec_module(module)

dependency_ids = sys.argv[2]
forbidden = sys.argv[3] == "True"
variants = (dependency_ids, dependency_ids.lower(), dependency_ids.upper(), dependency_ids.swapcase())
checked = 0
with tempfile.TemporaryDirectory() as temporary_root:
    package_path = Path(temporary_root) / "Hexalith.Parties.Client.1.2.3.nupkg"
    for candidate in variants:
        for layout in ("direct", "first-group", "last-group"):
            root = ElementTree.Element("package")
            metadata_element = ElementTree.SubElement(root, "metadata")
            for key, value in (("id", "Hexalith.Parties.Client"), ("version", "1.2.3"), ("readme", "README.md")):
                ElementTree.SubElement(metadata_element, key).text = value
            dependencies = ElementTree.SubElement(metadata_element, "dependencies")
            if layout != "direct":
                first_group = ElementTree.SubElement(dependencies, "group", targetFramework="net9.0")
                last_group = ElementTree.SubElement(dependencies, "group", targetFramework="net10.0")
                dependencies, other_group = (
                    (first_group, last_group) if layout == "first-group" else (last_group, first_group)
                )
                ElementTree.SubElement(other_group, "dependency", id="Hexalith.Parties.Contracts", version="1.2.3")
            original_ids = candidate.split(";")
            for original_id in original_ids:
                ElementTree.SubElement(dependencies, "dependency", id=original_id, version="1.2.3")
            with zipfile.ZipFile(package_path, "w") as package:
                package.writestr("Hexalith.Parties.Client.nuspec", ElementTree.tostring(root))
                package.writestr("README.md", "readme")

            metadata = module.get_metadata(package_path)
            try:
                module.validate_dependency_boundaries(package_path, metadata)
            except ValueError as error:
                if not forbidden:
                    raise SystemExit(f"Allowed dependency {candidate!r} was rejected: {error}")
                message = str(error)
                if (
                    "dependency boundary" not in message
                    or any(repr(original_id) not in message for original_id in original_ids)
                    or package_path.name not in message
                ):
                    raise SystemExit(f"Forbidden dependency diagnostic lost original identity: {message}")
            else:
                if forbidden:
                    raise SystemExit(f"Forbidden dependency {candidate!r} was accepted (layout={layout})")
            checked += 1
if checked != 12:
    raise SystemExit(f"Expected twelve package fixtures, checked {checked}")
print(f"boundaries|checked={checked}")
""";

        (int exitCode, string standardOutput, string standardError) = RunPython(
            "-E",
            "-c",
            probe,
            CiTestPaths.RepoFile("scripts"),
            dependencyIds,
            forbidden.ToString());

        exitCode.ShouldBe(0, standardError);
        standardOutput.ShouldContain("boundaries|checked=12");
    }

    /// <summary>
    /// Executes offline support-version, restored-graph and inherited-configuration regressions.
    /// </summary>
    /// <param name="suite">The Python regression suite to execute.</param>
    [Theory]
    [InlineData("SupportVersions")]
    [InlineData("RestoredVersions")]
    [InlineData("RestoreIsolation")]
    public void ConsumerValidationQualifiesNuGetIdentityAndRestoreIsolation(string suite)
    {
        (int exitCode, string output, string error) = RunPython(
            180_000,
            "-E",
            CiTestPaths.RepoFile("tests/Hexalith.Parties.Ci.Tests/consumer_validation_cases.py"),
            suite);

        exitCode.ShouldBe(0, error + output);
        error.ShouldContain("OK");
    }

    [Fact]
    public void SupportPackingRequiresExactProducedPackageMetadata()
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

commons_version = "9.8.7"
event_store_version = "7.6.5"
front_composer_version = "6.5.4-preview.2"
tenants_version = "5.4.3"
mode = {"value": "exact"}
fault_package = {"value": ""}
expected_versions = {
    "Hexalith.Commons.Http": commons_version,
    "Hexalith.Commons.UniqueIds": commons_version,
    "Hexalith.EventStore.Client": event_store_version,
    "Hexalith.EventStore.Contracts": event_store_version,
    "Hexalith.FrontComposer.Contracts": front_composer_version,
    "Hexalith.FrontComposer.Shell": front_composer_version,
    "Hexalith.Tenants.Client": tenants_version,
    "Hexalith.Tenants.Contracts": tenants_version,
}
packed_projects = []

def write_package(output_directory, package_id, file_version, metadata_version, metadata_id=None):
    package_path = output_directory / f"{package_id}.{file_version}.nupkg"
    nuspec = (
        "<package><metadata>"
        f"<id>{metadata_id or package_id}</id><version>{metadata_version}</version>"
        "</metadata></package>"
    )
    with zipfile.ZipFile(package_path, "w") as package:
        package.writestr(f"{package_id}.nuspec", nuspec)
    return package_path

def fake_pack(command, **kwargs):
    if command[:2] != ["dotnet", "pack"]:
        raise SystemExit(f"Support package packing must invoke dotnet pack: {command!r}")
    if "--no-build" not in command:
        raise SystemExit(f"Support package packing must use --no-build: {command!r}")
    if command.count("--configuration") != 1:
        raise SystemExit(f"Support package packing must declare exactly one configuration: {command!r}")
    configuration = command[command.index("--configuration") + 1]
    if configuration != "Release":
        raise SystemExit(f"Support package packing must use Release, found {configuration!r}")

    project_name = Path(command[2]).stem
    expected_version = expected_versions[project_name]
    packed_projects.append(project_name)
    for property_name in ("Version", "MinVerVersionOverride", "PackageVersion"):
        expected_argument = f"-p:{property_name}={expected_version}"
        if expected_argument not in command:
            raise SystemExit(f"Incorrect {project_name} {property_name}: {command!r}")
    if mode["value"] == "missing-http" and project_name == "Hexalith.Commons.Http":
        return subprocess.CompletedProcess(command, 0)
    if mode["value"] == "missing-eventstore" and project_name == "Hexalith.EventStore.Contracts":
        return subprocess.CompletedProcess(command, 0)
    if mode["value"] == "missing-family-package" and project_name == fault_package["value"]:
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
        else "7.6.4"
        if mode["value"] == "wrong-eventstore" and project_name == "Hexalith.EventStore.Client"
        else "0.0.1"
        if mode["value"] == "wrong-family-package" and project_name == fault_package["value"]
        else requested_version
    )
    is_fault_target = project_name == fault_package["value"]
    metadata_id = "Hexalith.Unexpected.Package" if is_fault_target and mode["value"] == "wrong-family-id" else project_name
    package_path = write_package(output_directory, project_name, requested_version, metadata_version, metadata_id)
    if is_fault_target and mode["value"] == "wrong-family-filename":
        package_path.rename(package_path.with_suffix(".unexpected.nupkg"))
    if is_fault_target and mode["value"] == "duplicate-family-package":
        duplicate = write_package(output_directory, project_name, "duplicate", metadata_version)
        if duplicate == package_path:
            raise SystemExit("Duplicate fixture overwrote the original package")
    return subprocess.CompletedProcess(command, 0)

with tempfile.TemporaryDirectory() as temporary_root:
    temporary_root = Path(temporary_root)
    with patch.object(module.subprocess, "run", side_effect=fake_pack):
        module.pack_support_packages(
            temporary_root / "exact", commons_version, event_store_version, front_composer_version, tenants_version
        )
        if sorted(packed_projects) != sorted(expected_versions):
            raise SystemExit(f"Support packing omitted or duplicated projects: {packed_projects!r}")
        print("exact|accepted")

        mode["value"] = "wrong-uniqueids"
        try:
            module.pack_support_packages(
                temporary_root / "wrong", commons_version, event_store_version, front_composer_version, tenants_version
            )
        except ValueError as error:
            if "Hexalith.Commons.UniqueIds" not in str(error) or "metadata version 9.8.6" not in str(error):
                raise
            print("wrong|rejected")
        else:
            raise SystemExit("Wrong Commons.UniqueIds metadata was accepted")

        mode["value"] = "missing-http"
        try:
            module.pack_support_packages(
                temporary_root / "missing", commons_version, event_store_version, front_composer_version, tenants_version
            )
        except ValueError as error:
            if "Hexalith.Commons.Http" not in str(error) or "<none>" not in str(error):
                raise
            print("missing|rejected")
        else:
            raise SystemExit("Missing exact Commons.Http package was accepted")

        mode["value"] = "wrong-eventstore"
        try:
            module.pack_support_packages(
                temporary_root / "wrong-eventstore", commons_version, event_store_version, front_composer_version, tenants_version
            )
        except ValueError as error:
            if "Hexalith.EventStore.Client" not in str(error) or "metadata version 7.6.4" not in str(error):
                raise
            print("wrong-eventstore|rejected")
        else:
            raise SystemExit("Wrong EventStore.Client metadata was accepted")

        mode["value"] = "missing-eventstore"
        try:
            module.pack_support_packages(
                temporary_root / "missing-eventstore", commons_version, event_store_version, front_composer_version, tenants_version
            )
        except ValueError as error:
            if "Hexalith.EventStore.Contracts" not in str(error) or "<none>" not in str(error):
                raise
            print("missing-eventstore|rejected")
        else:
            raise SystemExit("Missing exact EventStore.Contracts package was accepted")

        for package_id in (
            "Hexalith.FrontComposer.Contracts", "Hexalith.FrontComposer.Shell",
            "Hexalith.Tenants.Client", "Hexalith.Tenants.Contracts",
        ):
            fault_package["value"] = package_id
            expected_details = {
                "missing-family-package": "<none>",
                "wrong-family-package": "metadata version 0.0.1",
                "wrong-family-filename": ".unexpected.nupkg",
                "wrong-family-id": "<none>",
                "duplicate-family-package": ".duplicate.nupkg",
            }
            for failure_mode, expected_detail in expected_details.items():
                mode["value"] = failure_mode
                try:
                    module.pack_support_packages(
                        temporary_root / f"{package_id}-{failure_mode}",
                        commons_version, event_store_version, front_composer_version, tenants_version,
                    )
                except ValueError as error:
                    if (package_id not in str(error) or expected_versions[package_id] not in str(error)
                            or expected_detail not in str(error)):
                        raise
                    print(f"{package_id}|{failure_mode}|rejected")
                else:
                    raise SystemExit(f"Invalid {package_id} support package was accepted: {failure_mode}")
""";

        (int exitCode, string standardOutput, string standardError) = RunPython(
            "-c",
            probe,
            CiTestPaths.RepoFile("scripts"));

        exitCode.ShouldBe(0, standardError);
        standardOutput.ShouldContain("exact|accepted");
        standardOutput.ShouldContain("wrong|rejected");
        standardOutput.ShouldContain("missing|rejected");
        standardOutput.ShouldContain("wrong-eventstore|rejected");
        standardOutput.ShouldContain("missing-eventstore|rejected");
        foreach (string packageId in new[]
        {
            "Hexalith.FrontComposer.Contracts",
            "Hexalith.FrontComposer.Shell",
            "Hexalith.Tenants.Client",
            "Hexalith.Tenants.Contracts",
        })
        {
            standardOutput.ShouldContain($"{packageId}|missing-family-package|rejected");
            standardOutput.ShouldContain($"{packageId}|wrong-family-package|rejected");
            standardOutput.ShouldContain($"{packageId}|wrong-family-filename|rejected");
            standardOutput.ShouldContain($"{packageId}|wrong-family-id|rejected");
            standardOutput.ShouldContain($"{packageId}|duplicate-family-package|rejected");
        }
    }

    /// <summary>Validates the helper platform dependency and rejects implementation packages.</summary>
    [Fact]
    public void AspireHelperRequiresPlatformAndRejectsEveryImplementationDependency()
    {
        const string probe = """
import dataclasses
import importlib.util
import sys
from pathlib import Path
scripts = Path(sys.argv[1])
sys.path.insert(0, str(scripts))
spec = importlib.util.spec_from_file_location("aspire_validator_probe", scripts / "validate-nuget-packages.py")
module = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = module
spec.loader.exec_module(module)
path = Path("Hexalith.Parties.Aspire.1.6.0.nupkg")
dependencies = frozenset([module.DependencyMetadata("Aspire.Hosting", "13.6.1"),
                          module.DependencyMetadata("Hexalith.EventStore.Aspire", "3.126.1")])
metadata = module.PackageMetadata("Hexalith.Parties.Aspire", "1.6.0", "README.md", True, dependencies)
module.validate_dependency_boundaries(path, metadata)
module.validate_event_store_dependency_versions(path, metadata, "3.126.1")
for package_id in ["Hexalith.Parties", "Hexalith.Parties.Client", "Hexalith.Parties.Projections",
                   "Hexalith.EventStore.Server", "Hexalith.EventStore.Client", "Unexpected.Package"]:
    invalid = dataclasses.replace(metadata, dependencies=dependencies | {module.DependencyMetadata(package_id, "1.0.0")})
    try:
        module.validate_dependency_boundaries(path, invalid)
    except ValueError as error:
        if package_id not in str(error):
            raise
    else:
        raise SystemExit(f"Helper admitted forbidden dependency {package_id}")
for replacement in [frozenset(), frozenset([module.DependencyMetadata("Hexalith.EventStore.Contracts", "3.126.1")]),
                    frozenset([module.DependencyMetadata("Hexalith.EventStore.Aspire", "0.0.1")])]:
    try:
        module.validate_event_store_dependency_versions(path, dataclasses.replace(metadata, dependencies=replacement), "3.126.1")
    except ValueError as error:
        if "Hexalith.EventStore.Aspire" not in str(error):
            raise
    else:
        raise SystemExit("Helper admitted a missing or mismatched platform dependency")
print("aspire-boundary|verified")
""";
        (int exitCode, string output, string error) = RunPython("-c", probe, CiTestPaths.RepoFile("scripts"));
        exitCode.ShouldBe(0, error);
        output.ShouldContain("aspire-boundary|verified");
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
        => ReadCentralPackageVersion(PackageId);

    private static (string Alias, string Version) ReadCentralPackageVersion(string packageId)
    {
        XDocument props = XDocument.Load(CiTestPaths.RepoFile("references/Hexalith.Builds/Props/Directory.Packages.props"));
        string alias = props
            .Descendants("PackageVersion")
            .Single(element => element.Attribute("Include")?.Value == packageId)
            .Attribute("Version")?.Value
            ?? throw new InvalidOperationException($"{packageId} version was not found in shared package props.");
        string propertyName = alias.StartsWith("$(", StringComparison.Ordinal) && alias.EndsWith(')')
            ? alias[2..^1]
            : throw new InvalidOperationException($"{packageId} version '{alias}' is not a central property alias.");
        string version = EffectivePackageVersions.ReadProperty(CiTestPaths.RepositoryRoot, propertyName);
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
