#!/usr/bin/env python3
"""Pack the Hexalith.Parties NuGet packages published by semantic-release."""

from __future__ import annotations

import argparse
import subprocess
import sys
from pathlib import Path

from msbuild_properties import MsbuildPropertyResolutionError, resolve_hexalith_commons_version


PACKAGE_PROJECTS = [
    "src/Hexalith.Parties.Contracts/Hexalith.Parties.Contracts.csproj",
    "src/Hexalith.Parties.Client/Hexalith.Parties.Client.csproj",
    "src/Hexalith.Parties.AdminPortal/Hexalith.Parties.AdminPortal.csproj",
    "src/Hexalith.Parties.ConsumerPortal/Hexalith.Parties.ConsumerPortal.csproj",
    "src/Hexalith.Parties.Picker/Hexalith.Parties.Picker.csproj",
    "src/Hexalith.Parties.Authentication/Hexalith.Parties.Authentication.csproj",
    "src/Hexalith.Parties.Projections/Hexalith.Parties.Projections.csproj",
    "src/Hexalith.Parties.Security/Hexalith.Parties.Security.csproj",
    "src/Hexalith.Parties.Testing/Hexalith.Parties.Testing.csproj",
]


def main() -> int:
    parser = argparse.ArgumentParser(description="Pack Hexalith.Parties release packages.")
    parser.add_argument("output_directory", type=Path, help="Directory where .nupkg files are written.")
    parser.add_argument("version", help="Package version to apply.")
    args = parser.parse_args()

    commons_version = resolve_hexalith_commons_version()
    output_directory = args.output_directory
    output_directory.mkdir(parents=True, exist_ok=True)
    for package in output_directory.glob("*.nupkg"):
        package.unlink()
    for package in output_directory.glob("*.snupkg"):
        package.unlink()

    for project in PACKAGE_PROJECTS:
        subprocess.run(
            [
                "dotnet",
                "pack",
                project,
                "--no-build",
                "--configuration",
                "Release",
                "--output",
                str(output_directory),
                f"-p:HexalithPartiesPackageVersion={args.version}",
                f"-p:HexalithCommonsHttpPackageVersion={commons_version}",
                "/m:1",
                "/nr:false",
            ],
            check=True,
        )

    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except MsbuildPropertyResolutionError as exc:
        print(f"Package packing failed: {exc}", file=sys.stderr)
        raise SystemExit(1)
    except subprocess.CalledProcessError as exc:
        print(f"Package packing failed with exit code {exc.returncode}.", file=sys.stderr)
        raise SystemExit(exc.returncode)
