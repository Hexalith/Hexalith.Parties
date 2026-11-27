#!/usr/bin/env python3
"""Qualify the private native UI artifact outside the root package catalog context."""

import argparse
import importlib.util
import json
import os
import subprocess
import time
import xml.etree.ElementTree as ET
from pathlib import Path

SCRIPT_ROOT = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location("runtime_cohort", SCRIPT_ROOT / "validate-aspire-runtime-cohort.py")
cohort = importlib.util.module_from_spec(spec)
spec.loader.exec_module(cohort)


def evaluated_imports(project: Path, properties: list[str], environment: dict, output: Path) -> set[Path]:
    """Read the resolved import boundaries emitted by MSBuild's evaluated preprocessor."""
    subprocess.run(["dotnet", "msbuild", str(project), "-nologo", *properties, "-preprocess:" + str(output)],
                   env=environment, check=True)
    paths = {project.resolve()}
    for _, comment in ET.iterparse(output, events=("comment",)):
        lines = [line.strip() for line in (comment.text or "").splitlines() if line.strip()]
        if len(lines) >= 3 and set(lines[0]) == {"="} and set(lines[-1]) == {"="}:
            path = Path(lines[-2])
            if path.is_absolute():
                if not path.is_file():
                    raise ValueError("An evaluated native UI import is unavailable: " + str(path))
                paths.add(path.resolve())
    return paths


def prepare(owner: Path, output: Path, configuration: str, version: str, framework: str) -> None:
    """Build the real application, then authenticate it before exposing its TargetPath."""
    output = output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    manifest = output / "prepared.json"
    target_file = output / "target.txt"
    for previous in (manifest, target_file):
        previous.unlink(missing_ok=True)
    if version not in {"4.2.0", "5.4.0", "5.5.2", "5.6.0"}:
        raise ValueError("The mapped private native UI boundary requires exact SDK 4.2.0, 5.4.0, 5.5.2 or 5.6.0")
    project = owner.resolve() / "src/Hexalith.EventStore.Admin.UI/Hexalith.EventStore.Admin.UI.csproj"
    if not project.is_file():
        raise ValueError("The complete selected native Admin UI project is required")
    # These imports authenticate Root's mapped projects against Root's catalog.
    # The external application retains its original identity and owner imports;
    # its private package artifact is authenticated below using official archives.
    environment = os.environ.copy()
    inherited_imports = set()
    for name in ("CustomBeforeDirectoryBuildProps", "CustomAfterMicrosoftCommonProps",
                 "CustomBeforeDirectoryBuildTargets", "CustomAfterDirectoryBuildTargets",
                 "AfterMicrosoftNETSdkTargets", "HEXALITH_SOURCE_MAPPING_HASH", "Configuration"):
        value = environment.pop(name, None)
        if name not in {"HEXALITH_SOURCE_MAPPING_HASH", "Configuration"} and value:
            inherited_imports.add(Path(value).resolve())
    properties = ["-p:Configuration=" + configuration, "-p:HexalithUsePublishedSdkForHostedTests=true",
                  "-p:HexalithEventStoreVersion=" + version, "-p:UseHexalithProjectReferences=false",
                  "-p:UseNuGetDeps=true", "-p:StaticWebAssetsEnabled=false", "-p:CopyLocalLockFileAssemblies=true",
                  "-p:UseArtifactsOutput=true", "-p:ArtifactsPath=" + str(output / "build"),
                  "-p:HexalithPartiesRuntimeTargetFramework=" + framework,
                  "-p:CustomAfterMicrosoftCommonProps=" + str(cohort.RUNTIME_PROFILE)]
    subprocess.run(["dotnet", "restore", str(project), "-m:1", "-nr:false", "-warnaserror", *properties],
                   env=environment, check=True)
    metadata = json.loads(subprocess.check_output([
        "dotnet", "msbuild", str(project), "-nologo", *properties,
        "-getProperty:TargetPath,ProjectAssetsFile,Configuration,HexalithEventStoreVersion,TargetFramework,"
        "HexalithPartiesRuntimeTargetFramework,CustomAfterMicrosoftCommonProps"], env=environment, text=True))
    target = Path(metadata["Properties"]["TargetPath"]).resolve()
    sources = cohort.native_ui_owner_sources(project.parent)
    sources.update(evaluated_imports(project, properties, environment, output / "native-owner.expanded.proj"))
    sources.update(path for path in owner.resolve().iterdir() if path.is_file()
                   and (path.name.startswith("Directory.") or path.name in {".editorconfig", "global.json"}))
    sources.update({Path(__file__).resolve(), SCRIPT_ROOT / "validate-aspire-runtime-cohort.py", cohort.RUNTIME_PROFILE})
    # Bind the incoming authenticated context as provenance, although only Root's
    # own build uses those imports. Never synthesize or modify a catalog view.
    sources.update(inherited_imports)
    for imported in inherited_imports:
        sources.update(path for path in imported.parent.glob("SourceMapping.*") if path.is_file())
        sources.update(path for name in ("mapping.json", "catalog-versions.json")
                       if (path := imported.parent / name).is_file())
    original = {path: cohort.digest(path) for path in sources}
    started = time.time_ns()
    subprocess.run(["dotnet", "build", str(project), "--no-restore", "-t:Rebuild", "-m:1", "-nr:false", "-warnaserror", *properties],
                   env=environment, check=True)
    if cohort.native_ui_owner_sources(project.parent) != {path for path in sources if path.is_relative_to(project.parent)} \
            or any(cohort.digest(path) != expected for path, expected in original.items()):
        raise ValueError("Native Admin UI source inputs changed during compilation")
    # This inventory is checked again by the ordinary runtime receipt reader.
    provenance = target.parent / "native-admin-ui-artifact.tsv"
    provenance.write_text("nativeSourceDirectory|" + str(project.parent) + "\n" + "\n".join(
        str(path) + "|" + original[path] for path in sorted(sources)) + "\n", encoding="utf-8")
    receipt = output / "native-owner.json"
    cohort.write(receipt, cohort.record(metadata, project, configuration, version, started, "runtime", {}, target_framework=framework))
    cohort.write(manifest, cohort.complete([receipt], configuration, version, [], output / "public-archives", {}, framework))
    qualified = cohort.verify_manifest(manifest, project, configuration, version, {}, framework)
    evidence = {Path(path) for path in qualified["files"]} | {manifest, receipt}
    evidence.update(Path(archive["path"]) for archive in cohort.load(manifest)["publicArchives"])
    (output / "evidence.txt").write_text("\n".join(str(path) for path in sorted(evidence)) + "\n", encoding="utf-8")
    target_file.write_text(str(target) + "\n", encoding="utf-8")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--owner-root", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--configuration", choices=("Debug", "Release"), required=True)
    parser.add_argument("--version", required=True)
    parser.add_argument("--target-framework", choices=("net10.0", "net11.0"), required=True)
    args = parser.parse_args()
    try:
        prepare(args.owner_root, args.output, args.configuration, args.version, args.target_framework)
    except (OSError, ValueError, KeyError, TypeError, ET.ParseError, subprocess.CalledProcessError) as failure:
        parser.exit(1, "Private native Admin UI qualification failed: " + str(failure) + "\n")


if __name__ == "__main__":
    main()
