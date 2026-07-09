#!/usr/bin/env python3
"""Validate Hexalith.Parties NuGet packages before publishing."""

from __future__ import annotations

import argparse
import sys
import zipfile
from dataclasses import dataclass
from pathlib import Path
from xml.etree import ElementTree


EXPECTED_PACKAGE_IDS = frozenset(
    {
        "Hexalith.Parties.Contracts",
        "Hexalith.Parties.Client",
        "Hexalith.Parties.AdminPortal",
        "Hexalith.Parties.ConsumerPortal",
        "Hexalith.Parties.Picker",
        "Hexalith.Parties.Authentication",
        "Hexalith.Parties.Projections",
        "Hexalith.Parties.Security",
        "Hexalith.Parties.Testing",
    }
)

FORBIDDEN_DEPENDENCY_IDS = frozenset(
    {
        "Hexalith.Parties",
        "Hexalith.Parties.AppHost",
        "Hexalith.Parties.Mcp",
        "Hexalith.Parties.UI",
    }
)

FORBIDDEN_DEPENDENCY_FRAGMENTS = (
    ".Tests",
    ".Test",
    ".Sample",
    ".Samples",
    ".AppHost",
)

EXPECTED_DEPENDENCY_VERSIONS = {
    "Hexalith.Commons.Http": "2.27.0",
}

REQUIRED_COMMONS_HTTP_DEPENDENCY_PACKAGES = frozenset(
    {
        "Hexalith.Parties.Client",
        "Hexalith.Parties.Security",
    }
)


@dataclass(frozen=True)
class DependencyMetadata:
    package_id: str
    version: str


@dataclass(frozen=True)
class PackageMetadata:
    package_id: str
    version: str
    readme: str
    has_license: bool
    dependencies: frozenset[DependencyMetadata]


def get_metadata(package_path: Path) -> PackageMetadata:
    """Return package id, version, metadata flags, and dependency ids."""
    with zipfile.ZipFile(package_path) as package:
        nuspec_names = [name for name in package.namelist() if name.endswith(".nuspec")]
        if len(nuspec_names) != 1:
            raise ValueError(f"{package_path.name}: expected exactly one .nuspec file")

        root = ElementTree.fromstring(package.read(nuspec_names[0]))
        ns = {"n": root.tag.split("}")[0].strip("{")} if root.tag.startswith("{") else {}

        def find_text(name: str) -> str | None:
            element = root.find(f".//n:metadata/n:{name}", ns) if ns else root.find(f".//metadata/{name}")
            return element.text.strip() if element is not None and element.text else None

        def find_elements(path: str) -> list[ElementTree.Element]:
            return root.findall(path, ns) if ns else root.findall(path.replace("n:", ""))

        package_id = find_text("id")
        version = find_text("version")
        readme = find_text("readme")
        license_value = find_text("license")
        license_file = find_text("licenseFile")

        if not package_id:
            raise ValueError(f"{package_path.name}: missing nuspec package id")
        if not version:
            raise ValueError(f"{package_path.name}: missing nuspec version")
        if package_id not in EXPECTED_PACKAGE_IDS:
            raise ValueError(f"{package_path.name}: unexpected package id '{package_id}'")
        if not readme:
            raise ValueError(f"{package_path.name}: missing nuspec readme metadata")
        if readme not in package.namelist():
            raise ValueError(f"{package_path.name}: readme file '{readme}' is not in the package")

        dependencies = frozenset(
            DependencyMetadata(
                dependency.attrib["id"].strip(),
                dependency.attrib.get("version", "").strip(),
            )
            for dependency in find_elements(".//n:metadata/n:dependencies//n:dependency")
            if dependency.attrib.get("id", "").strip()
        )

        return PackageMetadata(package_id, version, readme, bool(license_value or license_file), dependencies)


def validate_dependency_boundaries(package_path: Path, metadata: PackageMetadata) -> None:
    """Validate package dependency metadata against the intended package boundaries."""
    forbidden_dependencies = sorted(
        dependency.package_id
        for dependency in metadata.dependencies
        if dependency.package_id in FORBIDDEN_DEPENDENCY_IDS
        or any(fragment in dependency.package_id for fragment in FORBIDDEN_DEPENDENCY_FRAGMENTS)
    )
    if forbidden_dependencies:
        raise ValueError(
            f"{package_path.name}: dependency boundary includes host, samples, tests, or other forbidden projects: "
            f"{forbidden_dependencies}"
        )


def validate_expected_dependency_versions(package_path: Path, metadata: PackageMetadata) -> None:
    """Validate dependency versions that are coupled to source ProjectReference packaging."""
    dependency_versions = {
        dependency.package_id: sorted(
            item.version for item in metadata.dependencies if item.package_id == dependency.package_id
        )
        for dependency in metadata.dependencies
    }

    for dependency_id, expected_version in EXPECTED_DEPENDENCY_VERSIONS.items():
        versions = dependency_versions.get(dependency_id, [])
        if versions and versions != [expected_version]:
            raise ValueError(
                f"{package_path.name}: expected {dependency_id} dependency version {expected_version}, found {versions}"
            )

    if metadata.package_id in REQUIRED_COMMONS_HTTP_DEPENDENCY_PACKAGES:
        versions = dependency_versions.get("Hexalith.Commons.Http", [])
        expected_version = EXPECTED_DEPENDENCY_VERSIONS["Hexalith.Commons.Http"]
        if versions != [expected_version]:
            raise ValueError(
                f"{package_path.name}: expected Hexalith.Commons.Http dependency version {expected_version}, "
                f"found {versions or '<missing>'}"
            )


def main() -> int:
    parser = argparse.ArgumentParser(description="Validate Hexalith.Parties NuGet package output.")
    parser.add_argument("package_directory", type=Path, help="Directory containing .nupkg files.")
    args = parser.parse_args()

    package_directory = args.package_directory
    packages = sorted(
        path
        for path in package_directory.glob("*.nupkg")
        if ".symbols." not in path.name and not path.name.endswith(".snupkg")
    )

    if len(packages) != len(EXPECTED_PACKAGE_IDS):
        package_list = ", ".join(path.name for path in packages) or "<none>"
        raise ValueError(f"Expected {len(EXPECTED_PACKAGE_IDS)} packages, found {len(packages)}: {package_list}")

    package_ids: set[str] = set()
    versions: set[str] = set()
    for package in packages:
        metadata = get_metadata(package)
        package_ids.add(metadata.package_id)
        versions.add(metadata.version)
        if not metadata.has_license:
            raise ValueError(f"{package.name}: missing license metadata")
        validate_dependency_boundaries(package, metadata)
        validate_expected_dependency_versions(package, metadata)

    if package_ids != EXPECTED_PACKAGE_IDS:
        missing = sorted(EXPECTED_PACKAGE_IDS - package_ids)
        unexpected = sorted(package_ids - EXPECTED_PACKAGE_IDS)
        raise ValueError(f"Package id mismatch. Missing: {missing}; unexpected: {unexpected}")

    if len(versions) != 1:
        raise ValueError(f"Expected all packages to share one version, found: {sorted(versions)}")

    version = next(iter(versions))
    print(f"Validated {len(packages)} NuGet packages at version {version}:")
    for package_id in sorted(package_ids):
        print(f"- {package_id}")

    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except ValueError as exc:
        print(f"Package validation failed: {exc}", file=sys.stderr)
        raise SystemExit(1)
