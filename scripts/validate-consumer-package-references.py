#!/usr/bin/env python3
"""Build isolated consumers against local Hexalith.Parties NuGet packages."""

from __future__ import annotations

import argparse
import json
import os
import shutil
import subprocess
import sys
import zipfile
from pathlib import Path
from xml.etree import ElementTree

from msbuild_properties import (
    MsbuildPropertyResolutionError,
    resolve_hexalith_commons_version,
    resolve_hexalith_event_store_version,
    resolve_hexalith_front_composer_version,
    resolve_hexalith_tenants_version,
    validate_msbuild_property,
)


PACKAGE_IDS = [
    "Hexalith.Parties.Contracts",
    "Hexalith.Parties.Client",
    "Hexalith.Parties.AdminPortal",
    "Hexalith.Parties.ConsumerPortal",
    "Hexalith.Parties.Picker",
]

REPO_ROOT = Path(__file__).resolve().parents[1]

COMMONS_SUPPORT_PACKAGE_PROJECTS = [
    "references/Hexalith.Commons/src/libraries/Hexalith.Commons.UniqueIds/Hexalith.Commons.UniqueIds.csproj",
    "references/Hexalith.Commons/src/libraries/Hexalith.Commons.Http/Hexalith.Commons.Http.csproj",
]
COMMONS_SUPPORT_PACKAGE_IDS = frozenset(
    {
        "Hexalith.Commons.Http",
        "Hexalith.Commons.UniqueIds",
    }
)

EVENT_STORE_SUPPORT_PACKAGE_PROJECTS = [
    "references/Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.csproj",
    "references/Hexalith.EventStore/src/Hexalith.EventStore.Client/Hexalith.EventStore.Client.csproj",
]
EVENT_STORE_SUPPORT_PACKAGE_IDS = frozenset(
    {
        "Hexalith.EventStore.Client",
        "Hexalith.EventStore.Contracts",
    }
)

CLIENT_REQUIRED_EVENT_STORE_PACKAGE_IDS = frozenset(
    {
        "Hexalith.EventStore.Contracts",
    }
)

PORTAL_REQUIRED_EVENT_STORE_PACKAGE_IDS = frozenset(
    {
        "Hexalith.EventStore.Client",
        "Hexalith.EventStore.Contracts",
    }
)

# This inventory packs direct support dependencies; remaining transitive packages,
# including FrontComposer.Contracts.UI, are restored from the configured feeds.
FRONT_COMPOSER_SUPPORT_PACKAGE_PROJECTS = [
    "references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Contracts/Hexalith.FrontComposer.Contracts.csproj",
    "references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Shell/Hexalith.FrontComposer.Shell.csproj",
]
FRONT_COMPOSER_SUPPORT_PACKAGE_IDS = frozenset(
    {
        "Hexalith.FrontComposer.Contracts",
        "Hexalith.FrontComposer.Shell",
    }
)

TENANTS_SUPPORT_PACKAGE_PROJECTS = [
    "references/Hexalith.Tenants/src/Hexalith.Tenants.Contracts/Hexalith.Tenants.Contracts.csproj",
    "references/Hexalith.Tenants/src/Hexalith.Tenants.Client/Hexalith.Tenants.Client.csproj",
]
TENANTS_SUPPORT_PACKAGE_IDS = frozenset(
    {
        "Hexalith.Tenants.Client",
        "Hexalith.Tenants.Contracts",
    }
)


def support_package_projects(
    commons_version: str,
    event_store_version: str,
    front_composer_version: str,
    tenants_version: str,
) -> list[tuple[str, str]]:
    """Return support projects with central package versions applied."""
    return [
        *((project, commons_version) for project in COMMONS_SUPPORT_PACKAGE_PROJECTS),
        *((project, event_store_version) for project in EVENT_STORE_SUPPORT_PACKAGE_PROJECTS),
        *((project, front_composer_version) for project in FRONT_COMPOSER_SUPPORT_PACKAGE_PROJECTS),
        *((project, tenants_version) for project in TENANTS_SUPPORT_PACKAGE_PROJECTS),
    ]


def package_identity(package_path: Path) -> tuple[str, str]:
    """Read a package id and version from its single nuspec."""
    with zipfile.ZipFile(package_path) as package:
        nuspec_names = [name for name in package.namelist() if name.endswith(".nuspec")]
        if len(nuspec_names) != 1:
            raise ValueError(f"{package_path.name}: expected exactly one .nuspec file")

        root = ElementTree.fromstring(package.read(nuspec_names[0]))
        ns = {"n": root.tag.split("}")[0].strip("{")} if root.tag.startswith("{") else {}
        id_element = root.find(".//n:metadata/n:id", ns) if ns else root.find(".//metadata/id")
        version_element = root.find(".//n:metadata/n:version", ns) if ns else root.find(".//metadata/version")
        if id_element is None or version_element is None or not id_element.text or not version_element.text:
            raise ValueError(f"{package_path.name}: missing id or version metadata")
        return id_element.text.strip(), version_element.text.strip()


def package_versions(package_directory: Path) -> dict[str, str]:
    versions: dict[str, str] = {}
    for package_path in package_directory.glob("*.nupkg"):
        if ".symbols." in package_path.name or package_path.name.endswith(".snupkg"):
            continue

        package_id, version = package_identity(package_path)
        versions[package_id] = version

    missing = sorted(set(PACKAGE_IDS) - set(versions))
    if missing:
        raise ValueError(f"Missing local packages required for consumer smoke tests: {missing}")

    distinct_versions = set(versions[package_id] for package_id in PACKAGE_IDS)
    if len(distinct_versions) != 1:
        raise ValueError(f"Expected Parties packages to share one version, found {sorted(distinct_versions)}")

    return versions


def normalize_nuget_version(version: str) -> str:
    """Normalize package identity: pad the core, omit zero revision and metadata."""
    concrete = validate_msbuild_property("NuGet package version", version)
    core, separator, release = concrete.split("+", 1)[0].partition("-")
    components = core.split(".")
    components += ["0"] * max(0, 3 - len(components))
    if len(components) == 4 and components[-1] == "0":
        components.pop()
    return ".".join(components) + (separator + release if separator else "")


def validate_support_packages(
    package_directory: Path,
    package_ids: frozenset[str],
    expected_version: str,
) -> None:
    """Require one NuGet-equivalent version and its normalized archive filename."""
    normalized_version = normalize_nuget_version(expected_version)
    packages: dict[str, list[tuple[Path, str]]] = {
        package_id: [] for package_id in package_ids
    }
    for package_path in package_directory.glob("*.nupkg"):
        if ".symbols." in package_path.name or package_path.name.endswith(".snupkg"):
            continue

        package_id, version = package_identity(package_path)
        if package_id in packages:
            packages[package_id].append((package_path, version))

    for package_id, artifacts in packages.items():
        if len(artifacts) != 1:
            names = sorted(path.name for path, _ in artifacts)
            raise ValueError(
                f"Expected exactly one {package_id} {expected_version} support package, "
                f"found {names or '<none>'}"
            )

        package_path, metadata_version = artifacts[0]
        normalized_metadata = normalize_nuget_version(metadata_version)
        expected_filename = f"{package_id}.{normalized_metadata}.nupkg"
        if normalized_metadata.casefold() != normalized_version.casefold() or package_path.name != expected_filename:
            raise ValueError(
                f"Expected exact {package_id} support package {expected_filename} with metadata version "
                f"{expected_version}, found {package_path.name} with metadata version {metadata_version}"
            )


def validate_commons_support_packages(package_directory: Path, commons_version: str) -> None:
    """Require exact central-version Commons support packages."""
    validate_support_packages(package_directory, COMMONS_SUPPORT_PACKAGE_IDS, commons_version)


def validate_event_store_support_packages(package_directory: Path, event_store_version: str) -> None:
    """Require exact central-version EventStore support packages."""
    validate_support_packages(package_directory, EVENT_STORE_SUPPORT_PACKAGE_IDS, event_store_version)


def run_dotnet(args: list[str], working_directory: Path) -> None:
    env = os.environ.copy()
    env.setdefault("DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER", "1")
    env.setdefault("MSBUILDDISABLENODEREUSE", "1")
    env["NUGET_PACKAGES"] = str(working_directory.parent / ".nuget" / "packages")
    env.pop("NUGET_FALLBACK_PACKAGES", None)
    subprocess.run(["dotnet", *args], cwd=working_directory, check=True, env=env)


def assert_package_only(project_file: Path, required_package_ids: list[str]) -> None:
    project_text = project_file.read_text(encoding="utf-8")
    if "ProjectReference" in project_text:
        raise ValueError(f"{project_file}: consumer projects must not use ProjectReference")

    for package_id in required_package_ids:
        if f'PackageReference Include="{package_id}"' not in project_text:
            raise ValueError(f"{project_file}: missing PackageReference for {package_id}")


def pack_support_packages(
    output_directory: Path,
    commons_version: str,
    event_store_version: str,
    front_composer_version: str,
    tenants_version: str,
) -> Path:
    output_directory.mkdir(parents=True, exist_ok=True)
    for project, version in support_package_projects(
        commons_version, event_store_version, front_composer_version, tenants_version
    ):
        project_path = REPO_ROOT / project
        if not project_path.exists():
            raise ValueError(f"Support package project not found: {project_path}")

        subprocess.run(
            [
                "dotnet",
                "pack",
                str(project_path),
                "--no-build",
                "--configuration",
                "Release",
                "--output",
                str(output_directory),
                f"-p:Version={version}",
                f"-p:MinVerVersionOverride={version}",
                f"-p:PackageVersion={version}",
                "/m:1",
                "/nr:false",
            ],
            cwd=REPO_ROOT,
            check=True,
        )

    validate_commons_support_packages(output_directory, commons_version)
    validate_event_store_support_packages(output_directory, event_store_version)
    validate_support_packages(output_directory, FRONT_COMPOSER_SUPPORT_PACKAGE_IDS, front_composer_version)
    validate_support_packages(output_directory, TENANTS_SUPPORT_PACKAGE_IDS, tenants_version)
    return output_directory


def write_nuget_config(root: Path, package_directories: list[Path], additional_sources: list[str]) -> Path:
    """Use selected feeds without inherited fallback caches or disabled sources."""
    config_file = root / "NuGet.Config"
    configuration = ElementTree.Element("configuration")
    package_sources = ElementTree.SubElement(configuration, "packageSources")
    ElementTree.SubElement(package_sources, "clear")
    for index, package_directory in enumerate(package_directories, start=1):
        ElementTree.SubElement(
            package_sources,
            "add",
            {"key": f"local-package-feed-{index}", "value": str(package_directory.resolve())},
        )
    for index, source in enumerate(additional_sources, start=1):
        ElementTree.SubElement(package_sources, "add", {"key": f"additional-source-{index}", "value": source})

    for setting in ("fallbackPackageFolders", "disabledPackageSources"):
        ElementTree.SubElement(ElementTree.SubElement(configuration, setting), "clear")

    ElementTree.ElementTree(configuration).write(config_file, encoding="utf-8", xml_declaration=True)
    return config_file


def write_client_consumer(root: Path, version: str) -> Path:
    project_dir = root / "client-consumer"
    project_dir.mkdir(parents=True)
    project_file = project_dir / "ClientConsumer.csproj"
    project_file.write_text(
        f"""<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Hexalith.Parties.Contracts" Version="{version}" />
    <PackageReference Include="Hexalith.Parties.Client" Version="{version}" />
  </ItemGroup>
</Project>
""",
        encoding="utf-8",
    )
    (project_dir / "Program.cs").write_text(
        """using Hexalith.Parties.Client.Abstractions;
using Hexalith.Parties.Client.Extensions;
using Hexalith.Parties.Contracts.Commands;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

IConfiguration configuration = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Parties:BaseUrl"] = "https://eventstore.example",
        ["Parties:Tenant"] = "tenant-a",
    })
    .Build();

IServiceCollection services = new ServiceCollection();
services.AddPartiesClient(configuration);
_ = services.Single(static descriptor => descriptor.ServiceType == typeof(IPartiesCommandClient));
_ = services.Single(static descriptor => descriptor.ServiceType == typeof(IPartiesQueryClient));
_ = typeof(CreateParty).FullName ?? throw new InvalidOperationException("CreateParty contract type is unavailable.");
""",
        encoding="utf-8",
    )
    assert_package_only(project_file, ["Hexalith.Parties.Contracts", "Hexalith.Parties.Client"])
    return project_file


def write_portal_consumer(root: Path, version: str) -> Path:
    project_dir = root / "portal-consumer"
    project_dir.mkdir(parents=True)
    project_file = project_dir / "PortalConsumer.csproj"
    project_file.write_text(
        f"""<Project Sdk="Microsoft.NET.Sdk.Razor">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
    <PackageReference Include="Hexalith.Parties.AdminPortal" Version="{version}" />
    <PackageReference Include="Hexalith.Parties.ConsumerPortal" Version="{version}" />
    <PackageReference Include="Hexalith.Parties.Picker" Version="{version}" />
  </ItemGroup>
</Project>
""",
        encoding="utf-8",
    )
    assert_package_only(project_file, ["Hexalith.Parties.AdminPortal", "Hexalith.Parties.ConsumerPortal", "Hexalith.Parties.Picker"])
    return project_file


def validate_family_assets(
    assets_file: Path,
    expected_version: str,
    required_package_ids: frozenset[str],
    family: str,
) -> None:
    """Validate all restored family versions and require package-backed identities."""
    normalized_version = normalize_nuget_version(expected_version).casefold()
    if not assets_file.is_file():
        raise ValueError(f"Restore did not produce assets file: {assets_file}")

    assets = json.loads(assets_file.read_text(encoding="utf-8"))
    libraries = assets.get("libraries")
    if not isinstance(libraries, dict):
        raise ValueError(f"{assets_file}: missing NuGet libraries object")

    selected: list[tuple[str, str]] = []
    for identity, library in libraries.items():
        if not isinstance(identity, str) or "/" not in identity:
            continue
        package_id, version = identity.rsplit("/", 1)
        if package_id.casefold().startswith(f"hexalith.{family}.".casefold()):
            if not isinstance(library, dict) or library.get("type") != "package":
                raise ValueError(f"{assets_file}: selected {package_id} must be a NuGet package")
            selected.append((package_id, version))

    invalid = sorted(
        f"{package_id}={version or '<missing>'}"
        for package_id, version in selected
        if not version or normalize_nuget_version(version).casefold() != normalized_version
    )
    if invalid:
        raise ValueError(
            f"{assets_file}: every selected Hexalith.{family}.* package must use version "
            f"{expected_version}, found {invalid}"
        )

    selected_ids = {package_id.casefold() for package_id, _ in selected}
    missing = sorted(
        package_id
        for package_id in required_package_ids
        if package_id.casefold() not in selected_ids
    )
    if missing:
        raise ValueError(
            f"{assets_file}: missing required selected {family} packages: {missing}"
        )


def validate_event_store_assets(
    assets_file: Path,
    expected_version: str,
    required_package_ids: frozenset[str],
) -> None:
    """Validate the EventStore package versions selected by NuGet restore."""
    validate_family_assets(assets_file, expected_version, required_package_ids, "EventStore")


def validate_consumer(
    project_file: Path,
    event_store_version: str,
    required_event_store_package_ids: frozenset[str],
    front_composer_version: str,
    tenants_version: str,
    *,
    require_portal_packages: bool,
    commons_version: str,
) -> None:
    run_dotnet(["restore", str(project_file)], project_file.parent)
    assets_file = project_file.parent / "obj" / "project.assets.json"
    validate_family_assets(assets_file, commons_version, COMMONS_SUPPORT_PACKAGE_IDS, "Commons")
    validate_event_store_assets(
        assets_file,
        event_store_version,
        required_event_store_package_ids,
    )
    validate_family_assets(
        assets_file, front_composer_version,
        FRONT_COMPOSER_SUPPORT_PACKAGE_IDS if require_portal_packages else frozenset(), "FrontComposer",
    )
    validate_family_assets(
        assets_file, tenants_version,
        TENANTS_SUPPORT_PACKAGE_IDS if require_portal_packages else frozenset(), "Tenants",
    )
    run_dotnet(
        [
            "build",
            str(project_file),
            "--no-restore",
            "--configuration",
            "Release",
            "-warnaserror",
            "-p:WarningsNotAsErrors=NU1603",
        ],
        project_file.parent,
    )


def main() -> int:
    parser = argparse.ArgumentParser(description="Validate package-only consumer restore/build experience.")
    parser.add_argument("package_directory", type=Path, help="Directory containing local Hexalith.Parties .nupkg files.")
    parser.add_argument("--work-directory", type=Path, default=Path("/tmp/hexalith-parties-consumer-package-smoke"))
    parser.add_argument(
        "--nuget-source",
        action="append",
        help="NuGet source to use alongside local feeds. Repeat for multiple sources; replaces the nuget.org default.",
    )
    parser.add_argument(
        "--source-support-packages",
        action="store_true",
        help="Pack diagnostic support packages from prebuilt Release source outputs instead of using published dependencies.",
    )
    args = parser.parse_args()

    commons_version = resolve_hexalith_commons_version(REPO_ROOT)
    event_store_version = resolve_hexalith_event_store_version(REPO_ROOT)
    front_composer_version = resolve_hexalith_front_composer_version(REPO_ROOT)
    tenants_version = resolve_hexalith_tenants_version(REPO_ROOT)
    package_directory = args.package_directory.resolve()
    versions = package_versions(package_directory)
    version = versions["Hexalith.Parties.Contracts"]

    work_directory = args.work_directory
    if work_directory.exists():
        shutil.rmtree(work_directory)
    work_directory.mkdir(parents=True)
    package_feeds = [package_directory]
    if args.source_support_packages:
        package_feeds.append(pack_support_packages(
            work_directory / "support-packages",
            commons_version,
            event_store_version,
            front_composer_version,
            tenants_version,
        ))
    sources = args.nuget_source if args.nuget_source is not None else ["https://api.nuget.org/v3/index.json"]
    write_nuget_config(work_directory, package_feeds, sources)

    validate_consumer(
        write_client_consumer(work_directory, version),
        event_store_version,
        CLIENT_REQUIRED_EVENT_STORE_PACKAGE_IDS,
        front_composer_version,
        tenants_version,
        require_portal_packages=False,
        commons_version=commons_version,
    )
    validate_consumer(
        write_portal_consumer(work_directory, version),
        event_store_version,
        PORTAL_REQUIRED_EVENT_STORE_PACKAGE_IDS,
        front_composer_version,
        tenants_version,
        require_portal_packages=True,
        commons_version=commons_version,
    )

    print(f"Validated package-only consumers for Hexalith.Parties packages at version {version}.")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (MsbuildPropertyResolutionError, ValueError, subprocess.CalledProcessError) as exc:
        print(f"Consumer package validation failed: {exc}", file=sys.stderr)
        raise SystemExit(1)
