#!/usr/bin/env python3
"""Validate Hexalith.Parties NuGet packages before publishing."""

from __future__ import annotations

import argparse
import sys
import zipfile
from dataclasses import dataclass
from pathlib import Path
from xml.etree import ElementTree

from msbuild_properties import (
    MsbuildPropertyResolutionError,
    resolve_hexalith_commons_version,
    resolve_hexalith_event_store_version,
)


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

REQUIRED_COMMONS_HTTP_DEPENDENCY_PACKAGES = frozenset(
    {
        "Hexalith.Parties.Client",
        "Hexalith.Parties.Security",
    }
)

EVENT_STORE_DEPENDENCY_PREFIX = "Hexalith.EventStore."
REQUIRED_EVENT_STORE_DEPENDENCY_ID = "Hexalith.EventStore.Contracts"


@dataclass(frozen=True)
class DependencyMetadata:
    package_id: str
    version: str


@dataclass(frozen=True)
class DependencyGroupMetadata:
    target_framework: str | None
    dependencies: tuple[DependencyMetadata, ...]


@dataclass(frozen=True)
class PackageMetadata:
    package_id: str
    version: str
    readme: str
    has_license: bool
    dependencies: frozenset[DependencyMetadata]
    dependency_groups: tuple[DependencyGroupMetadata, ...] = ()


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

        def dependency_metadata(elements: list[ElementTree.Element]) -> tuple[DependencyMetadata, ...]:
            return tuple(
                DependencyMetadata(
                    dependency.attrib["id"].strip(),
                    dependency.attrib.get("version", "").strip(),
                )
                for dependency in elements
                if dependency.attrib.get("id", "").strip()
            )

        dependency_groups: list[DependencyGroupMetadata] = []
        dependencies_elements = find_elements(".//n:metadata/n:dependencies")
        if not dependencies_elements:
            dependency_groups.append(DependencyGroupMetadata(None, ()))
        for dependencies_element in dependencies_elements:
            group_elements = [
                child
                for child in dependencies_element
                if child.tag.rsplit("}", 1)[-1] == "group"
            ]
            direct_dependencies = dependency_metadata(
                [
                    child
                    for child in dependencies_element
                    if child.tag.rsplit("}", 1)[-1] == "dependency"
                ]
            )
            if direct_dependencies or not group_elements:
                dependency_groups.append(DependencyGroupMetadata(None, direct_dependencies))
            for group in group_elements:
                dependency_groups.append(
                    DependencyGroupMetadata(
                        group.attrib.get("targetFramework"),
                        dependency_metadata(
                            [
                                child
                                for child in group
                                if child.tag.rsplit("}", 1)[-1] == "dependency"
                            ]
                        ),
                    )
                )

        dependencies = frozenset(
            dependency
            for group in dependency_groups
            for dependency in group.dependencies
        )

        return PackageMetadata(
            package_id,
            version,
            readme,
            bool(license_value or license_file),
            dependencies,
            tuple(dependency_groups),
        )


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


def validate_expected_dependency_versions(
    package_path: Path,
    metadata: PackageMetadata,
    expected_dependency_versions: dict[str, str],
) -> None:
    """Validate dependency versions that are coupled to source ProjectReference packaging."""
    dependency_versions = {
        dependency.package_id: sorted(
            item.version for item in metadata.dependencies if item.package_id == dependency.package_id
        )
        for dependency in metadata.dependencies
    }

    for dependency_id, expected_version in expected_dependency_versions.items():
        versions = dependency_versions.get(dependency_id, [])
        if versions and versions != [expected_version]:
            raise ValueError(
                f"{package_path.name}: expected {dependency_id} dependency version {expected_version}, found {versions}"
            )

    if metadata.package_id in REQUIRED_COMMONS_HTTP_DEPENDENCY_PACKAGES:
        versions = dependency_versions.get("Hexalith.Commons.Http", [])
        expected_version = expected_dependency_versions["Hexalith.Commons.Http"]
        if versions != [expected_version]:
            raise ValueError(
                f"{package_path.name}: expected Hexalith.Commons.Http dependency version {expected_version}, "
                f"found {versions or '<missing>'}"
            )


def validate_event_store_dependency_versions(
    package_path: Path,
    metadata: PackageMetadata,
    expected_version: str,
) -> None:
    """Require every dependency group to use the evaluated central EventStore version."""
    groups = metadata.dependency_groups or (
        DependencyGroupMetadata(None, tuple(metadata.dependencies)),
    )
    event_store_prefix = EVENT_STORE_DEPENDENCY_PREFIX.casefold()
    required_id = REQUIRED_EVENT_STORE_DEPENDENCY_ID.casefold()

    for index, group in enumerate(groups, start=1):
        group_label = group.target_framework or f"unnamed group {index}"
        event_store_dependencies = sorted(
            (
                dependency
                for dependency in group.dependencies
                if dependency.package_id.casefold().startswith(event_store_prefix)
            ),
            key=lambda dependency: (dependency.package_id.casefold(), dependency.version),
        )
        required_dependencies = [
            dependency
            for dependency in event_store_dependencies
            if dependency.package_id.casefold() == required_id
        ]
        required_versions = [dependency.version or "<missing>" for dependency in required_dependencies]
        if len(required_dependencies) != 1 or required_dependencies[0].version != expected_version:
            raise ValueError(
                f"{package_path.name}: dependency group '{group_label}' must contain exactly one required "
                f"{REQUIRED_EVENT_STORE_DEPENDENCY_ID} dependency at version {expected_version}, "
                f"found {required_versions or '<missing>'}"
            )

        invalid_dependencies = [
            f"{dependency.package_id}={dependency.version or '<missing>'}"
            for dependency in event_store_dependencies
            if dependency.version != expected_version
        ]
        if invalid_dependencies:
            raise ValueError(
                f"{package_path.name}: every Hexalith.EventStore.* dependency in group '{group_label}' "
                f"must use version {expected_version}, found {invalid_dependencies}"
            )


def main() -> int:
    parser = argparse.ArgumentParser(description="Validate Hexalith.Parties NuGet package output.")
    parser.add_argument("package_directory", type=Path, help="Directory containing .nupkg files.")
    args = parser.parse_args()

    commons_version = resolve_hexalith_commons_version()
    event_store_version = resolve_hexalith_event_store_version()
    expected_dependency_versions = {
        "Hexalith.Commons.Http": commons_version,
        "Hexalith.Commons.UniqueIds": commons_version,
    }
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
        validate_expected_dependency_versions(package, metadata, expected_dependency_versions)
        validate_event_store_dependency_versions(package, metadata, event_store_version)

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
    except (MsbuildPropertyResolutionError, ValueError) as exc:
        print(f"Package validation failed: {exc}", file=sys.stderr)
        raise SystemExit(1)
