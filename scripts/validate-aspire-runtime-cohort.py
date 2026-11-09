#!/usr/bin/env python3
"""Bind a completed packaged build to its actual runtime files and official archives."""

import argparse
import hashlib
import json
import re
import time
import urllib.error
import urllib.request
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path

SDK_IDS = {
    "Hexalith.EventStore.Admin.Abstractions", "Hexalith.EventStore.Admin.Cli", "Hexalith.EventStore.Admin.Server",
    "Hexalith.EventStore.Aspire", "Hexalith.EventStore.Client", "Hexalith.EventStore.Contracts",
    "Hexalith.EventStore.DomainService", "Hexalith.EventStore.Gateway",
    "Hexalith.EventStore.RestApi.Generators", "Hexalith.EventStore.Server",
    "Hexalith.EventStore.ServiceDefaults", "Hexalith.EventStore.SignalR",
    "Hexalith.EventStore.Testing", "Hexalith.EventStore.Testing.Integration",
}
CORE_IDS = {"Hexalith.EventStore.Client", "Hexalith.EventStore.Contracts", "Hexalith.EventStore.ServiceDefaults"}
ADMIN_SERVER_IDS = {"Hexalith.EventStore.Admin.Server", "Hexalith.EventStore.Admin.Abstractions",
                    "Hexalith.EventStore.Contracts", "Hexalith.EventStore.ServiceDefaults"}
ADMIN_SERVER_PROJECT = (Path(__file__).resolve().parents[1] / "references/Hexalith.EventStore/src/"
                        "Hexalith.EventStore.Admin.Server.Host/Hexalith.EventStore.Admin.Server.Host.csproj")
SAMPLE_CONTRACTS_PROJECT = (Path(__file__).resolve().parents[1] / "tests/"
                            "Hexalith.Parties.EventStoreSample.Contracts.TestArtifact/"
                            "Hexalith.Parties.EventStoreSample.Contracts.TestArtifact.csproj")
NATIVE_SAMPLE_CONTRACTS_PROJECT = (Path(__file__).resolve().parents[1] / "references/Hexalith.EventStore/samples/"
                                   "Hexalith.EventStore.Sample.Contracts/Hexalith.EventStore.Sample.Contracts.csproj")
PRIVATE_ADMIN_UI_PROJECT = (Path(__file__).resolve().parents[1] / "tests/"
                            "Hexalith.Parties.EventStoreAdminUI.TestHost/Hexalith.Parties.EventStoreAdminUI.TestHost.csproj")
PARTIES_UI_PROJECT = (Path(__file__).resolve().parents[1] / "src/"
                      "Hexalith.Parties.UI/Hexalith.Parties.UI.csproj")
PARTIES_UI_IDS = {"Hexalith.EventStore.Client", "Hexalith.EventStore.Contracts", "Hexalith.EventStore.SignalR"}


def select_sdk(libraries: dict, project: Path | None = None, restored: set[str] | None = None,
               entry: str | None = None) -> dict:
    selected = {}
    for identity, metadata in libraries.items():
        package_id = identity.rsplit("/", 1)[0]
        if not package_id.casefold().startswith("hexalith.eventstore."):
            continue
        if package_id in SDK_IDS:
            selected[identity] = metadata
            continue
        if package_id == entry and metadata.get("type") == "project":
            continue
        if (package_id == "Hexalith.EventStore.Admin.UI" and metadata.get("type") == "reference"
                and project is not None and project.resolve() == PRIVATE_ADMIN_UI_PROJECT):
            continue
        if (package_id == "Hexalith.EventStore.Sample.Contracts.Reference" and metadata.get("type") == "reference"
                and restored is not None and any(name.rsplit("/", 1)[0] == "Hexalith.EventStore.Sample.Contracts" for name in restored)):
            continue
        # The linked native sample is an application artifact, not an SDK library.
        # Restore binds its actual declared project; deps readback must retain that identity.
        if package_id == "Hexalith.EventStore.Sample.Contracts" and metadata.get("type") == "project":
            if restored is not None and identity in restored:
                continue
            path = metadata.get("msbuildProject")
            if (project is not None and isinstance(path, str)
                    and (project.parent / path).resolve() in {SAMPLE_CONTRACTS_PROJECT, NATIVE_SAMPLE_CONTRACTS_PROJECT}):
                continue
        raise ValueError("Unqualified runtime SDK family member: " + identity)
    return selected


def required_sdk_ids(project: Path | None = None) -> set[str]:
    # This host uses Admin.Server's Dapr transport, not the domain SDK Client.
    # Bind the exception to the declared project, never a supplied name or missing assembly.
    if project is not None and project.resolve() == PARTIES_UI_PROJECT:
        return PARTIES_UI_IDS
    return ADMIN_SERVER_IDS if project is not None and project.resolve() == ADMIN_SERVER_PROJECT else CORE_IDS

PUBLIC_FAMILIES = {
    "HexalithTenantsVersion": {"Hexalith.Tenants." + name for name in ("Contracts", "Client", "Server", "Testing", "Aspire")},
    "HexalithMemoriesVersion": {"Hexalith.Memories." + name for name in ("Aspire", "Contracts", "Client.Rest", "Redis", "Cli", "Mcp", "ServiceDefaults", "EventStore", "Telemetry", "EventStoreExtensions")},
    "HexalithFrontComposerVersion": {"Hexalith.FrontComposer." + name for name in ("Cli", "Contracts", "Contracts.UI", "Mcp", "Schema", "Shell", "SourceTools", "Testing")},
    "HexalithMcpCliVersion": {"Hexalith.McpCli.Abstractions"},
}


def validate_families(assets: dict, families: dict) -> None:
    for property_name, ids in PUBLIC_FAMILIES.items():
        if property_name not in families:
            continue
        for identity, metadata in assets["libraries"].items():
            package_id, version = identity.rsplit("/", 1)
            if package_id in ids and (version != families[property_name] or metadata.get("type") != "package"):
                raise ValueError("Prepared public family differs from selected root version: " + identity)


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def load(path: Path) -> dict:
    value = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(value, dict):
        raise ValueError("Preparation evidence must be an object: " + str(path))
    return value


def write(path: Path, value: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + ".pending")
    temporary.write_text(json.dumps(value, indent=2) + "\n", encoding="utf-8")
    temporary.replace(path)


def validate(assets_path: Path, expected_version: str, project: Path | None = None) -> dict:
    assets = load(assets_path)
    selected = list(select_sdk(assets["libraries"], project).items())
    if not selected:
        raise ValueError("No actual restored EventStore SDK libraries were found")
    missing = required_sdk_ids(project) - {identity.rsplit("/", 1)[0] for identity, _ in selected}
    if missing:
        raise ValueError("Actual runtime SDK restore is incomplete: " + ", ".join(sorted(missing)))
    drifted = [identity + " (type=" + str(metadata.get("type")) + ")" for identity, metadata in selected
               if identity.rsplit("/", 1)[1] != expected_version or metadata.get("type") != "package"]
    if drifted:
        raise ValueError("Restored runtime SDK cohort differs from exact package " + expected_version + ": " + ", ".join(sorted(drifted)))
    return assets


def archive_identity(path: Path, package_id: str, version: str, repository: str) -> dict:
    with zipfile.ZipFile(path) as archive:
        nuspecs = [entry for entry in archive.namelist() if entry.lower().endswith(".nuspec")]
        if len(nuspecs) != 1:
            raise ValueError("The actual archive must contain exactly one nuspec")
        root = ET.fromstring(archive.read(nuspecs[0]))
        metadata = next(node for node in root if node.tag.rsplit("}", 1)[-1] == "metadata")
        values = {node.tag.rsplit("}", 1)[-1]: node for node in metadata}
        actual_id, actual_version = values["id"].text, values["version"].text
        source = values["repository"].attrib
        if actual_id.lower() != package_id.lower() or actual_version != version:
            raise ValueError("Archive identity differs from selected package " + package_id + "/" + version)
        if source.get("type") != "git" or source.get("url", "").removesuffix(".git").rstrip("/") != repository:
            raise ValueError("The actual archive has a foreign source repository")
        if not re.fullmatch(r"[0-9a-f]{40}", source.get("commit", "")):
            raise ValueError("The actual archive has no exact source commit")
    return {"path": str(path.resolve()), "sha256": digest(path), "id": actual_id, "version": actual_version,
            "repository": repository, "commit": source["commit"]}


def sdk_proof(assets: dict, target: Path, version: str, families: dict) -> tuple[list[dict], list[dict]]:
    archives, binaries = [], []
    expected = {package: version for package in SDK_IDS}
    for property_name, ids in PUBLIC_FAMILIES.items():
        if property_name in families:
            expected.update({package: families[property_name] for package in ids})
    for identity, library in sorted(assets["libraries"].items()):
        package_id = identity.rsplit("/", 1)[0]
        if package_id not in expected:
            continue
        package_version = expected[package_id]
        candidates = [Path(folder) / library["path"] / (package_id.lower() + "." + package_version + ".nupkg")
                      for folder in assets["packageFolders"]]
        available = [path for path in candidates if path.is_file()]
        if len(available) != 1:
            raise ValueError("One actual restored SDK archive is required for " + identity)
        archive_path = available[0]
        repository = next("https://github.com/Hexalith/" + name for name in
                          ("Hexalith.EventStore", "Hexalith.Tenants", "Hexalith.Memories", "Hexalith.FrontComposer", "Hexalith.McpCli")
                          if package_id.startswith(name + "."))
        proof = archive_identity(archive_path, package_id, package_version, repository)
        archives.append(proof)
        runtime = set()
        for framework in assets["targets"].values():
            metadata = framework.get(identity, {})
            runtime.update(name for name in metadata.get("runtime", {}) if name.endswith((".dll", ".exe")))
            runtime.update(name for name, entry in metadata.get("runtimeTargets", {}).items()
                           if entry.get("assetType") == "runtime" and name.endswith((".dll", ".exe")))
        with zipfile.ZipFile(archive_path) as archive:
            for entry in sorted(runtime):
                deployed = target.parent / Path(entry).name
                if not deployed.is_file() or deployed.read_bytes() != archive.read(entry):
                    raise ValueError("Prepared SDK binary differs from its actual archive: " + str(deployed))
                binaries.append({"path": str(deployed.resolve()), "archive": str(archive_path.resolve()),
                                 "entry": entry, "sha256": digest(deployed)})
    if len({proof["commit"] for proof in archives if proof["id"] in SDK_IDS}) != 1:
        raise ValueError("Prepared SDK archives have different source commits")
    if not binaries:
        raise ValueError("No actual runtime SDK binary was bound to an archive")
    return archives, binaries


def validate_deps(path: Path, version: str, sdk_libraries: set[str], families: dict, project: Path | None = None) -> None:
    deps = load(path)
    selected = set(select_sdk(deps["libraries"], project, sdk_libraries, path.name.removesuffix(".deps.json")))
    if not required_sdk_ids(project).issubset({identity.rsplit("/", 1)[0] for identity in selected}) or not selected.issubset(sdk_libraries):
        raise ValueError("Deployed dependency manifest differs from the restored SDK graph")
    if any(identity.rsplit("/", 1)[1] != version or deps["libraries"][identity].get("type") != "package" for identity in selected):
        raise ValueError("Deployed dependency manifest has a foreign SDK version/type")
    validate_families(deps, families)
    public_ids = set().union(*(PUBLIC_FAMILIES[name] for name in families if name in PUBLIC_FAMILIES))
    for identity in deps["libraries"]:
        if identity.rsplit("/", 1)[0] in public_ids and identity not in sdk_libraries:
            raise ValueError("Deployed public dependency manifest differs from actual restore")


def runtime_inputs(target: Path) -> set[Path]:
    """Enumerate the runtime files whose additions, deletions and changes require preparation."""
    paths = {target, target.with_suffix(".deps.json")}
    apphost = target.with_suffix("")
    if apphost.is_file():
        paths.add(apphost)
    runtimeconfig = target.with_suffix(".runtimeconfig.json")
    if runtimeconfig.is_file():
        paths.add(runtimeconfig)
    paths.update(path.resolve() for path in target.parent.rglob("*") if path.is_file()
                 and (path.suffix.lower() in {".dll", ".exe", ".so", ".dylib"} or ".so." in path.name.lower()))
    paths.update(path.resolve() for path in target.parent.glob("appsettings*.json"))
    paths.update(path.resolve() for name in ("wwwroot", "native-ui-artifact") for path in (target.parent / name).rglob("*") if path.is_file())
    paths.update(path.resolve() for path in target.parent.glob("*staticwebassets*.json"))
    paths.update(path.resolve() for path in target.parent.glob("native-admin-ui-artifact.tsv"))
    for manifest in target.parent.glob("*staticwebassets*.json"):
        document = load(manifest)
        if not isinstance(document, dict) or "ContentRoots" not in document:
            continue
        roots = document["ContentRoots"]
        if not isinstance(roots, list) or any(not isinstance(value, str) for value in roots):
            raise ValueError("Static web asset content roots must be an explicit path list")
        for value in roots:
            content = Path(value)
            if not content.is_absolute() or content.is_symlink() or not content.is_dir():
                raise ValueError("Static web asset content root is unavailable or linked: " + value)
            for asset in content.rglob("*"):
                if asset.is_symlink():
                    raise ValueError("Linked static web assets cannot qualify a prepared runtime: " + str(asset))
                if asset.is_file():
                    paths.add(asset.resolve())
    return paths


def record(metadata: dict, project: Path, configuration: str, version: str, started_ns: int, role: str, families: dict) -> dict:
    properties = metadata.get("Properties", metadata)
    if properties["Configuration"] != configuration or properties["HexalithEventStoreVersion"] != version:
        raise ValueError("Evaluated build configuration/cohort differs from the completed build")
    target, assets_path = Path(properties["TargetPath"]).resolve(), Path(properties["ProjectAssetsFile"]).resolve()
    if not target.is_file() or target.stat().st_mtime_ns < started_ns:
        raise ValueError("The completed rebuild did not produce its actual runtime entry PE")
    deps = target.with_suffix(".deps.json")
    runtimeconfig = target.with_suffix(".runtimeconfig.json")
    if not deps.is_file() or role == "runtime" and not runtimeconfig.is_file():
        raise ValueError("The prepared runtime dependency/configuration output is missing")
    assets = validate(assets_path, version, project)
    validate_families(assets, families)
    validate_deps(deps, version, set(assets["libraries"]), families, project)
    archives, binaries = sdk_proof(assets, target, version, families)
    paths = runtime_inputs(target) | {assets_path, project.resolve()}
    for proof in target.parent.glob("native-admin-ui-artifact.tsv"):
        paths.add(proof.resolve())
        for row in proof.read_text(encoding="utf-8").splitlines():
            filename, separator, expected = row.partition("|")
            if separator and Path(filename).is_absolute():
                source = Path(filename).resolve()
                if not re.fullmatch(r"[0-9a-fA-F]{64}", expected) or digest(source) != expected.lower():
                    raise ValueError("Native UI artifact provenance differs from its actual file: " + filename)
                paths.add(source)
    return {"schemaVersion": 1, "completed": True, "project": str(project.resolve()), "configuration": configuration,
            "sdkVersion": version, "familyVersions": families, "role": role, "targetPath": str(target), "assetsPath": str(assets_path),
            "buildStartedNs": started_ns, "completedNs": time.time_ns(),
            "runtimeInputs": [str(path) for path in sorted(runtime_inputs(target))],
            "files": {str(path): digest(path) for path in sorted(paths)},
            "sdkArchives": archives, "sdkBinaries": binaries}


def verify_receipt(receipt: dict, project: Path, configuration: str, version: str, families: dict | None = None) -> dict:
    if families is not None and receipt.get("familyVersions") != families:
        raise ValueError("Prepared public family selection differs from the actual launch")
    if receipt.get("schemaVersion") != 1 or receipt.get("completed") is not True or receipt.get("project") != str(project.resolve()):
        raise ValueError("The completed preparation receipt does not bind this project")
    if receipt.get("configuration") != configuration or receipt.get("sdkVersion") != version:
        raise ValueError("Prepared runtime configuration/cohort differs from the actual launch")
    for filename, expected in receipt["files"].items():
        if digest(Path(filename)) != expected:
            raise ValueError("Prepared runtime file drifted after completed build: " + filename)
    target, assets_path = Path(receipt["targetPath"]), Path(receipt["assetsPath"])
    if not isinstance(receipt.get("runtimeInputs"), list):
        raise ValueError("Prepared runtime input inventory is missing")
    recorded_inputs = {Path(filename) for filename in receipt["runtimeInputs"]}
    if not recorded_inputs.issubset({Path(filename) for filename in receipt["files"]}):
        raise ValueError("Prepared runtime inventory contains unbound inputs")
    if runtime_inputs(target) != recorded_inputs:
        raise ValueError("Prepared runtime file inventory drifted after completed build")
    assets = validate(assets_path, version, project)
    validate_families(assets, receipt["familyVersions"])
    validate_deps(target.with_suffix(".deps.json"), version, set(assets["libraries"]), receipt["familyVersions"], project)
    archives, binaries = sdk_proof(assets, target, version, receipt["familyVersions"])
    if archives != receipt["sdkArchives"] or binaries != receipt["sdkBinaries"]:
        raise ValueError("Prepared runtime SDK archive/binary proof drifted")
    return receipt


def public_archive(package: str, cache: Path) -> dict:
    package_id, version = package.split("=", 1)
    repository = next(("https://github.com/Hexalith/" + name for name in ("Hexalith.EventStore", "Hexalith.Tenants", "Hexalith.Memories", "Hexalith.FrontComposer", "Hexalith.McpCli")
                       if package_id.startswith(name + ".")), None)
    if repository is None:
        raise ValueError("Unsupported public peer archive identity")
    cache.mkdir(parents=True, exist_ok=True)
    path = cache / (package_id.lower() + "." + version + ".nupkg")
    # A pre-existing local archive cannot authenticate its own contents or metadata.
    # Each completed preparation independently retrieves the official HTTPS bytes.
    url = "https://api.nuget.org/v3-flatcontainer/" + package_id.lower() + "/" + version + "/" + path.name
    for attempt in range(3):
        try:
            with urllib.request.urlopen(url, timeout=30) as response:
                content = response.read()
            break
        except urllib.error.HTTPError as failure:
            if failure.code not in {408, 429, 500, 502, 503, 504} or attempt == 2:
                raise
        except (urllib.error.URLError, TimeoutError, ConnectionError):
            if attempt == 2:
                raise
        time.sleep(attempt + 1)
    temporary = path.with_suffix(".pending")
    temporary.write_bytes(content)
    temporary.replace(path)
    return archive_identity(path, package_id, version, repository)


def complete(paths: list[Path], configuration: str, version: str, packages: list[str], cache: Path, families: dict) -> dict:
    projects = {}
    restored_archives = []
    for path in paths:
        receipt = load(path)
        verify_receipt(receipt, Path(receipt["project"]), configuration, version, families)
        if receipt["project"] in projects:
            raise ValueError("A prepared project appears more than once")
        projects[receipt["project"]] = {"receipt": str(path.resolve()), "sha256": digest(path)}
        restored_archives.extend(receipt["sdkArchives"])
    if not projects:
        raise ValueError("No completed builds were supplied")
    selections = sorted(set(packages) | {archive["id"] + "=" + archive["version"] for archive in restored_archives})
    public = [public_archive(package, cache) for package in selections]
    official = {(archive["id"], archive["version"]): archive for archive in public}
    for archive in restored_archives:
        if archive["sha256"] != official[archive["id"], archive["version"]]["sha256"]:
            raise ValueError("Restored archive differs from independently retrieved official package: " + archive["id"])
    return {"schemaVersion": 1, "completed": True, "configuration": configuration, "sdkVersion": version,
            "completedNs": time.time_ns(), "familyVersions": families, "projects": projects,
            "publicArchives": public}


def verify_manifest(path: Path, project: Path, configuration: str, version: str, families: dict | None = None) -> dict:
    manifest = load(path)
    if manifest.get("schemaVersion") != 1 or manifest.get("completed") is not True:
        raise ValueError("The packaged runtime preparation did not complete")
    if manifest.get("configuration") != configuration or manifest.get("sdkVersion") != version:
        raise ValueError("Completed preparation belongs to another configuration/cohort")
    if families is not None and manifest.get("familyVersions") != families:
        raise ValueError("Completed public family selection differs from the actual launch")
    bound = manifest["projects"][str(project.resolve())]
    receipt_path = Path(bound["receipt"])
    if digest(receipt_path) != bound["sha256"]:
        raise ValueError("The completed build receipt drifted")
    for proof in manifest["publicArchives"]:
        actual = archive_identity(Path(proof["path"]), proof["id"], proof["version"], proof["repository"])
        if actual != proof:
            raise ValueError("Public peer package identity/source proof drifted")
    receipt = verify_receipt(load(receipt_path), project, configuration, version, manifest["familyVersions"])
    official = {(archive["id"], archive["version"]): archive for archive in manifest["publicArchives"]}
    for archive in receipt["sdkArchives"]:
        proof = official.get((archive["id"], archive["version"]))
        if proof is None or archive["sha256"] != proof["sha256"]:
            raise ValueError("Restored runtime archive is not bound to independently retrieved official bytes")
    return receipt


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--assets", type=Path)
    parser.add_argument("--version", required=True)
    parser.add_argument("--configuration", choices=("Debug", "Release"), default="Release")
    parser.add_argument("--project", type=Path)
    parser.add_argument("--record", type=Path)
    parser.add_argument("--metadata-json")
    parser.add_argument("--build-started-ns", type=int)
    parser.add_argument("--role", choices=("runtime", "catalog"), default="runtime")
    parser.add_argument("--complete-manifest", type=Path)
    parser.add_argument("--receipt", type=Path, action="append", default=[])
    parser.add_argument("--public-package", action="append", default=[])
    parser.add_argument("--archive-cache", type=Path)
    parser.add_argument("--manifest", type=Path)
    parser.add_argument("--families-json", default="{}")
    args = parser.parse_args()
    try:
        families = json.loads(args.families_json)
        if args.record:
            value = record(json.loads(args.metadata_json), args.project, args.configuration, args.version, args.build_started_ns, args.role, families)
            write(args.record, value)
        elif args.complete_manifest:
            value = complete(args.receipt, args.configuration, args.version, args.public_package, args.archive_cache, families)
            write(args.complete_manifest, value)
        elif args.manifest:
            print(json.dumps(verify_manifest(args.manifest, args.project, args.configuration, args.version, families)))
        elif args.assets:
            validate(args.assets, args.version, args.project)
        else:
            raise ValueError("An actual assets, completed build or manifest selection is required")
    except (OSError, ValueError, KeyError, TypeError, zipfile.BadZipFile, ET.ParseError) as failure:
        parser.exit(1, "Runtime SDK qualification failed: " + str(failure) + "\n")


if __name__ == "__main__":
    main()
