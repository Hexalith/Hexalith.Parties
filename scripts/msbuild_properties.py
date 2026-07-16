#!/usr/bin/env python3
"""Resolve evaluated MSBuild properties for repository packaging scripts."""

from __future__ import annotations

import argparse
import re
import subprocess
import sys
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
COMMONS_VERSION_PROJECT = REPO_ROOT / "src/Hexalith.Parties.Client/Hexalith.Parties.Client.csproj"
MSBUILD_TIMEOUT_SECONDS = 30
MAX_DIAGNOSTIC_LENGTH = 500
MAX_PROPERTY_VALUE_DIAGNOSTIC_LENGTH = 120
UNRESOLVED_MSBUILD_MARKERS = ("$(", "$[", "%(", "@(")
NUGET_VERSION_PATTERN = re.compile(
    r"^(0|[1-9][0-9]*)(\.(0|[1-9][0-9]*)){1,3}"
    r"(-[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?"
    r"(\+[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?$"
)


class MsbuildPropertyResolutionError(RuntimeError):
    """Raised when an evaluated MSBuild property is unavailable or unresolved."""


def bounded_diagnostic(value: object, limit: int = MAX_DIAGNOSTIC_LENGTH) -> str:
    """Return single-line diagnostic text capped to a safe length."""
    diagnostic = str(value).replace("\r", "\\r").replace("\n", "\\n")
    return diagnostic if len(diagnostic) <= limit else f"{diagnostic[:limit]}..."


def validate_msbuild_property(property_name: str, value: object) -> str:
    """Return a concrete MSBuild property value or fail with a bounded diagnostic."""
    if not isinstance(value, str) or not value.strip():
        raise MsbuildPropertyResolutionError(
            f"MSBuild property '{property_name}' evaluated to an empty value."
        )

    if value != value.strip():
        raise MsbuildPropertyResolutionError(
            f"MSBuild property '{property_name}' contained leading or trailing whitespace."
        )
    if "\r" in value or "\n" in value:
        raise MsbuildPropertyResolutionError(
            f"MSBuild property '{property_name}' returned multiline output."
        )
    if any(marker in value for marker in UNRESOLVED_MSBUILD_MARKERS):
        raise MsbuildPropertyResolutionError(
            f"MSBuild property '{property_name}' remained unresolved: "
            f"'{bounded_diagnostic(value, MAX_PROPERTY_VALUE_DIAGNOSTIC_LENGTH)}'."
        )
    if NUGET_VERSION_PATTERN.fullmatch(value) is None:
        raise MsbuildPropertyResolutionError(
            f"MSBuild property '{property_name}' was not a valid NuGet version: "
            f"'{bounded_diagnostic(value, MAX_PROPERTY_VALUE_DIAGNOSTIC_LENGTH)}'."
        )

    return value


def remove_single_line_ending(value: str) -> str:
    """Remove the one line ending emitted by dotnet msbuild without hiding multiline output."""
    if value.endswith("\r\n"):
        return value[:-2]
    if value.endswith("\n"):
        return value[:-1]
    return value


def resolve_msbuild_property(
    project_file: Path,
    property_name: str,
    *,
    configuration: str = "Release",
    working_directory: Path | None = None,
    timeout_seconds: float = MSBUILD_TIMEOUT_SECONDS,
) -> str:
    """Query one evaluated MSBuild property from a project."""
    project_file = project_file.resolve()
    if not project_file.is_file():
        raise MsbuildPropertyResolutionError(f"MSBuild project was not found: {project_file}")

    try:
        result = subprocess.run(
            [
                "dotnet",
                "msbuild",
                str(project_file),
                "-nologo",
                f"-p:Configuration={configuration}",
                f"-getProperty:{property_name}",
            ],
            cwd=working_directory or project_file.parent,
            check=True,
            capture_output=True,
            text=True,
            timeout=timeout_seconds,
        )
    except subprocess.TimeoutExpired as exc:
        diagnostic = bounded_diagnostic(exc.stderr or exc.stdout or "no diagnostic output")
        raise MsbuildPropertyResolutionError(
            f"Timed out after {timeout_seconds:g} seconds while evaluating MSBuild property "
            f"'{property_name}' from {bounded_diagnostic(project_file)}: {diagnostic}"
        ) from exc
    except subprocess.CalledProcessError as exc:
        diagnostic = bounded_diagnostic(exc.stderr or exc.stdout or "no diagnostic output")
        raise MsbuildPropertyResolutionError(
            f"Could not evaluate MSBuild property '{property_name}' from {bounded_diagnostic(project_file)}: "
            f"dotnet msbuild exited with code {exc.returncode}: {diagnostic}"
        ) from exc
    except OSError as exc:
        raise MsbuildPropertyResolutionError(
            f"Could not launch dotnet msbuild while evaluating property '{property_name}' from "
            f"{bounded_diagnostic(project_file)}: {bounded_diagnostic(exc)}"
        ) from exc

    return validate_msbuild_property(property_name, remove_single_line_ending(result.stdout))


def resolve_hexalith_commons_version(repository_root: Path = REPO_ROOT) -> str:
    """Resolve the central Hexalith Commons version used by package publication."""
    project_file = repository_root / COMMONS_VERSION_PROJECT.relative_to(REPO_ROOT)
    return resolve_msbuild_property(
        project_file,
        "HexalithCommonsVersion",
        working_directory=repository_root,
    )


def main() -> int:
    """Resolve a requested property for command-line diagnostics and tests."""
    parser = argparse.ArgumentParser(description="Resolve an evaluated MSBuild property.")
    parser.add_argument("project_file", type=Path, help="MSBuild project to evaluate.")
    parser.add_argument("property_name", help="Property name to resolve.")
    parser.add_argument("--configuration", default="Release", help="MSBuild configuration to evaluate.")
    args = parser.parse_args()

    print(
        resolve_msbuild_property(
            args.project_file,
            args.property_name,
            configuration=args.configuration,
            working_directory=REPO_ROOT,
        )
    )
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except MsbuildPropertyResolutionError as exc:
        print(f"MSBuild property resolution failed: {exc}", file=sys.stderr)
        raise SystemExit(1)
