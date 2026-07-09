#!/usr/bin/env python3
"""Build isolated consumers against local Hexalith.Parties NuGet packages."""

from __future__ import annotations

import argparse
import os
import shutil
import subprocess
import sys
import zipfile
from pathlib import Path
from xml.etree import ElementTree


PACKAGE_IDS = [
    "Hexalith.Parties.Contracts",
    "Hexalith.Parties.Client",
    "Hexalith.Parties.AdminPortal",
    "Hexalith.Parties.ConsumerPortal",
    "Hexalith.Parties.Picker",
]

REPO_ROOT = Path(__file__).resolve().parents[1]

SUPPORT_PACKAGE_PROJECTS = [
    ("references/Hexalith.Commons/src/libraries/Hexalith.Commons.UniqueIds/Hexalith.Commons.UniqueIds.csproj", "2.27.0"),
    ("references/Hexalith.Commons/src/libraries/Hexalith.Commons.Http/Hexalith.Commons.Http.csproj", "2.27.0"),
    ("references/Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.csproj", "3.47.0"),
    ("references/Hexalith.EventStore/src/Hexalith.EventStore.Client/Hexalith.EventStore.Client.csproj", "3.47.0"),
    ("references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Contracts/Hexalith.FrontComposer.Contracts.csproj", "1.7.0"),
    ("references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Shell/Hexalith.FrontComposer.Shell.csproj", "1.7.0"),
    ("references/Hexalith.Tenants/src/Hexalith.Tenants.Contracts/Hexalith.Tenants.Contracts.csproj", "2.4.2"),
    ("references/Hexalith.Tenants/src/Hexalith.Tenants.Client/Hexalith.Tenants.Client.csproj", "2.4.2"),
]


def package_versions(package_directory: Path) -> dict[str, str]:
    versions: dict[str, str] = {}
    for package_path in package_directory.glob("*.nupkg"):
        if ".symbols." in package_path.name or package_path.name.endswith(".snupkg"):
            continue

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
            versions[id_element.text.strip()] = version_element.text.strip()

    missing = sorted(set(PACKAGE_IDS) - set(versions))
    if missing:
        raise ValueError(f"Missing local packages required for consumer smoke tests: {missing}")

    distinct_versions = set(versions[package_id] for package_id in PACKAGE_IDS)
    if len(distinct_versions) != 1:
        raise ValueError(f"Expected Parties packages to share one version, found {sorted(distinct_versions)}")

    return versions


def run_dotnet(args: list[str], working_directory: Path) -> None:
    env = os.environ.copy()
    env.setdefault("DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER", "1")
    env.setdefault("MSBUILDDISABLENODEREUSE", "1")
    env["NUGET_PACKAGES"] = str(working_directory.parent / ".nuget" / "packages")
    subprocess.run(["dotnet", *args], cwd=working_directory, check=True, env=env)


def assert_package_only(project_file: Path, required_package_ids: list[str]) -> None:
    project_text = project_file.read_text(encoding="utf-8")
    if "ProjectReference" in project_text:
        raise ValueError(f"{project_file}: consumer projects must not use ProjectReference")

    for package_id in required_package_ids:
        if f'PackageReference Include="{package_id}"' not in project_text:
            raise ValueError(f"{project_file}: missing PackageReference for {package_id}")


def pack_support_packages(output_directory: Path) -> Path:
    output_directory.mkdir(parents=True, exist_ok=True)
    for project, version in SUPPORT_PACKAGE_PROJECTS:
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

    return output_directory


def write_nuget_config(root: Path, package_directories: list[Path], additional_sources: list[str]) -> Path:
    """Add the local package directory while preserving explicitly supplied package sources."""
    config_file = root / "NuGet.Config"
    configuration = ElementTree.Element("configuration")
    package_sources = ElementTree.SubElement(configuration, "packageSources")
    for index, package_directory in enumerate(package_directories, start=1):
        ElementTree.SubElement(
            package_sources,
            "add",
            {"key": f"local-package-feed-{index}", "value": str(package_directory.resolve())},
        )
    for index, source in enumerate(additional_sources, start=1):
        ElementTree.SubElement(package_sources, "add", {"key": f"additional-source-{index}", "value": source})

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


def validate_consumer(project_file: Path) -> None:
    run_dotnet(["restore", str(project_file)], project_file.parent)
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
        default=["https://api.nuget.org/v3/index.json"],
        help="Additional NuGet package source to add. May be supplied more than once.",
    )
    args = parser.parse_args()

    package_directory = args.package_directory.resolve()
    versions = package_versions(package_directory)
    version = versions["Hexalith.Parties.Contracts"]

    work_directory = args.work_directory
    if work_directory.exists():
        shutil.rmtree(work_directory)
    work_directory.mkdir(parents=True)
    support_feed = pack_support_packages(work_directory / "support-packages")
    write_nuget_config(work_directory, [package_directory, support_feed], args.nuget_source)

    validate_consumer(write_client_consumer(work_directory, version))
    validate_consumer(write_portal_consumer(work_directory, version))

    print(f"Validated package-only consumers for Hexalith.Parties packages at version {version}.")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (ValueError, subprocess.CalledProcessError) as exc:
        print(f"Consumer package validation failed: {exc}", file=sys.stderr)
        raise SystemExit(1)
