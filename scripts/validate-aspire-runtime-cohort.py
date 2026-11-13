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

RUNTIME_PROFILE = Path(__file__).with_name("PartiesHostedRuntime.props").resolve()

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
MEMORY_QUALIFICATION_PROJECT = (Path(__file__).resolve().parents[1] / "tests/"
                                "Hexalith.Parties.IntegrationTests/Hexalith.Parties.IntegrationTests.csproj")
PARTIES_UI_PROJECT = (Path(__file__).resolve().parents[1] / "src/"
                      "Hexalith.Parties.UI/Hexalith.Parties.UI.csproj")
PARTIES_UI_IDS = {"Hexalith.EventStore.Client", "Hexalith.EventStore.Contracts", "Hexalith.EventStore.SignalR"}


def select_sdk(libraries: dict, project: Path | None = None, restored: set[str] | None = None,
               entry: str | None = None, native_ui_bound: bool = False) -> dict:
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
                and project is not None and (project.resolve() == PRIVATE_ADMIN_UI_PROJECT
                    or project.resolve() == MEMORY_QUALIFICATION_PROJECT and native_ui_bound)):
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


def validate_deps(path: Path, version: str, sdk_libraries: set[str], families: dict, project: Path | None = None,
                  native_ui_bound: bool = False) -> None:
    deps = load(path)
    selected = set(select_sdk(deps["libraries"], project, sdk_libraries, path.name.removesuffix(".deps.json"), native_ui_bound))
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


def bind_native_ui(target: Path, project: Path, configuration: str, version: str, families: dict,
                   owner_receipt: Path | None, target_framework: str | None = None) -> dict | None:
    """Bind the wrapper's transitive UI reference to the exact separately qualified host artifact."""
    if project.resolve() != MEMORY_QUALIFICATION_PROJECT:
        if owner_receipt is not None:
            raise ValueError("Native UI receipt is only supported for the explicit memory qualification wrapper")
        return None
    references = {name: value for name, value in load(target.with_suffix(".deps.json"))["libraries"].items()
                  if name.rsplit("/", 1)[0] == "Hexalith.EventStore.Admin.UI"}
    if not references:
        return None
    if owner_receipt is None:
        raise ValueError("Qualification wrapper requires the completed private Admin UI artifact receipt")
    owner = verify_receipt(load(owner_receipt), PRIVATE_ADMIN_UI_PROJECT, configuration, version, families, target_framework)
    owner_target = Path(owner["targetPath"])
    source_proof = owner_target.parent / "native-admin-ui-artifact.tsv"
    if str(source_proof) not in owner["files"]:
        raise ValueError("Private Admin UI artifact source provenance is missing")
    owner_references = {name: value for name, value in load(owner_target.with_suffix(".deps.json"))["libraries"].items()
                        if name.rsplit("/", 1)[0] == "Hexalith.EventStore.Admin.UI"}
    if references != owner_references or any(value.get("type") != "reference" for value in references.values()):
        raise ValueError("Qualification wrapper native UI reference differs from its qualified owner")
    artifact = "Hexalith.EventStore.Admin.UI.dll"
    if digest(target.parent / artifact) != digest(owner_target.parent / artifact):
        raise ValueError("Qualification wrapper native UI bytes differ from its qualified owner")
    return {"path": str(owner_receipt.resolve()), "sha256": digest(owner_receipt)}


def native_ui_sources(target: Path) -> set[Path]:
    """Require actual absolute-source/hash evidence while preserving the artifact metadata headers."""
    paths = set()
    for proof in target.parent.glob("native-admin-ui-artifact.tsv"):
        sources = set()
        for row in proof.read_text(encoding="utf-8").splitlines():
            filename, separator, expected = row.partition("|")
            if separator and Path(filename).is_absolute():
                source = Path(filename).resolve()
                if not re.fullmatch(r"[0-9a-fA-F]{64}", expected) or digest(source) != expected.lower():
                    raise ValueError("Native UI artifact provenance differs from its actual file: " + filename)
                sources.add(source)
        if not sources:
            raise ValueError("Native UI artifact provenance requires at least one absolute source/hash row")
        paths.update(sources)
        paths.add(proof.resolve())
    return paths


def require_framework(value: str) -> str:
    """Keep the explicit hosted compatibility profile finite and separate from package family identity."""
    if value not in {"net10.0", "net11.0"}:
        raise ValueError("The prepared runtime requires an exact qualified target framework")
    return value


def validate_framework(assets: dict, target: Path, framework: str, role: str) -> None:
    """Bind evaluated, restored and deployed frameworks before any prepared PE can launch."""
    framework = require_framework(framework)
    if set(assets.get("project", {}).get("frameworks", {})) != {framework}:
        raise ValueError("Restored runtime target framework differs from the evaluated build profile")
    names = {framework, ".NETCoreApp,Version=v" + framework.removeprefix("net")}
    if not assets.get("targets") or any(name.split("/", 1)[0] not in names for name in assets["targets"]):
        raise ValueError("Restored runtime target graph differs from the evaluated build profile")
    deps = load(target.with_suffix(".deps.json"))
    if deps.get("runtimeTarget", {}).get("name", "").split("/", 1)[0] not in names \
            or not deps.get("targets") or any(name.split("/", 1)[0] not in names for name in deps["targets"]):
        raise ValueError("Deployed runtime target framework differs from the evaluated build profile")
    runtimeconfig = target.with_suffix(".runtimeconfig.json")
    if role == "runtime" or runtimeconfig.is_file():
        options = load(runtimeconfig).get("runtimeOptions")
        if not isinstance(options, dict) or options.get("tfm") != framework:
            raise ValueError("Deployed runtime configuration differs from the evaluated build profile")
        if ("framework" in options) == ("frameworks" in options):
            raise ValueError("Deployed runtime shared framework declarations are missing or ambiguous")
        declarations = [options["framework"]] if "framework" in options else options["frameworks"]
        if not isinstance(declarations, list) or not declarations:
            raise ValueError("Deployed runtime shared framework declarations are malformed")
        references = assets["project"]["frameworks"][framework].get("frameworkReferences", {})
        if not isinstance(references, dict):
            raise ValueError("Restored runtime shared framework references are malformed")
        required = {"Microsoft.NETCore.App"} | set(references)
        declared = set()
        generation = framework.removeprefix("net")
        for declaration in declarations:
            if not isinstance(declaration, dict) or not isinstance(declaration.get("name"), str) \
                    or not isinstance(declaration.get("version"), str) \
                    or re.fullmatch(re.escape(generation) + r"\.(0|[1-9][0-9]*)", declaration["version"]) is None:
                raise ValueError("Deployed runtime shared framework version differs from the evaluated build profile")
            name = declaration["name"]
            if name in declared:
                raise ValueError("Deployed runtime shared framework declarations contain duplicates")
            declared.add(name)
        if declared != required:
            raise ValueError("Deployed runtime shared framework declarations differ from actual restored references")


def record(metadata: dict, project: Path, configuration: str, version: str, started_ns: int, role: str, families: dict,
           private_admin_ui_receipt: Path | None = None, target_framework: str | None = None) -> dict:
    properties = metadata.get("Properties", metadata)
    if properties["Configuration"] != configuration or properties["HexalithEventStoreVersion"] != version:
        raise ValueError("Evaluated build configuration/cohort differs from the completed build")
    framework = require_framework(properties.get("TargetFramework"))
    if target_framework is not None and framework != require_framework(target_framework):
        raise ValueError("Evaluated runtime target framework differs from the selected build profile")
    if properties.get("HexalithPartiesRuntimeTargetFramework") != framework \
            or Path(properties.get("CustomAfterMicrosoftCommonProps", "")).resolve() != RUNTIME_PROFILE:
        raise ValueError("Evaluated runtime build profile differs from the selected hosted profile")
    target, assets_path = Path(properties["TargetPath"]).resolve(), Path(properties["ProjectAssetsFile"]).resolve()
    if not target.is_file() or target.stat().st_mtime_ns < started_ns:
        raise ValueError("The completed rebuild did not produce its actual runtime entry PE")
    deps = target.with_suffix(".deps.json")
    runtimeconfig = target.with_suffix(".runtimeconfig.json")
    if not deps.is_file() or role == "runtime" and not runtimeconfig.is_file():
        raise ValueError("The prepared runtime dependency/configuration output is missing")
    assets = validate(assets_path, version, project)
    validate_framework(assets, target, framework, role)
    validate_families(assets, families)
    native_ui = bind_native_ui(target, project, configuration, version, families, private_admin_ui_receipt, framework)
    validate_deps(deps, version, set(assets["libraries"]), families, project, native_ui is not None)
    archives, binaries = sdk_proof(assets, target, version, families)
    paths = runtime_inputs(target) | {assets_path, project.resolve(), RUNTIME_PROFILE}
    paths.update(native_ui_sources(target))
    return {"schemaVersion": 1, "completed": True, "project": str(project.resolve()), "configuration": configuration,
            "sdkVersion": version, "targetFramework": framework, "familyVersions": families, "role": role, "targetPath": str(target), "assetsPath": str(assets_path),
            "buildStartedNs": started_ns, "completedNs": time.time_ns(),
            "runtimeInputs": [str(path) for path in sorted(runtime_inputs(target))],
            "files": {str(path): digest(path) for path in sorted(paths)},
            "sdkArchives": archives, "sdkBinaries": binaries, "nativeAdminUi": native_ui}


def verify_receipt(receipt: dict, project: Path, configuration: str, version: str, families: dict | None = None,
                   target_framework: str | None = None) -> dict:
    if families is not None and receipt.get("familyVersions") != families:
        raise ValueError("Prepared public family selection differs from the actual launch")
    if receipt.get("schemaVersion") != 1 or receipt.get("completed") is not True or receipt.get("project") != str(project.resolve()):
        raise ValueError("The completed preparation receipt does not bind this project")
    if receipt.get("configuration") != configuration or receipt.get("sdkVersion") != version:
        raise ValueError("Prepared runtime configuration/cohort differs from the actual launch")
    framework = require_framework(receipt.get("targetFramework"))
    if target_framework is not None and framework != require_framework(target_framework):
        raise ValueError("Prepared runtime target framework differs from the actual launch")
    for filename, expected in receipt["files"].items():
        if digest(Path(filename)) != expected:
            raise ValueError("Prepared runtime file drifted after completed build: " + filename)
    if str(RUNTIME_PROFILE) not in receipt["files"]:
        raise ValueError("Prepared runtime build profile is not bound to its actual source")
    target, assets_path = Path(receipt["targetPath"]), Path(receipt["assetsPath"])
    if not isinstance(receipt.get("runtimeInputs"), list):
        raise ValueError("Prepared runtime input inventory is missing")
    recorded_inputs = {Path(filename) for filename in receipt["runtimeInputs"]}
    if not recorded_inputs.issubset({Path(filename) for filename in receipt["files"]}):
        raise ValueError("Prepared runtime inventory contains unbound inputs")
    if runtime_inputs(target) != recorded_inputs:
        raise ValueError("Prepared runtime file inventory drifted after completed build")
    if not native_ui_sources(target).issubset({Path(filename) for filename in receipt["files"]}):
        raise ValueError("Native UI artifact provenance contains unbound source files")
    assets = validate(assets_path, version, project)
    validate_framework(assets, target, framework, receipt["role"])
    validate_families(assets, receipt["familyVersions"])
    bound_ui = receipt.get("nativeAdminUi")
    if bound_ui is not None and digest(Path(bound_ui["path"])) != bound_ui["sha256"]:
        raise ValueError("Qualification wrapper private Admin UI receipt drifted")
    native_ui = bind_native_ui(target, project, configuration, version, receipt["familyVersions"],
                              Path(bound_ui["path"]) if bound_ui is not None else None, framework)
    validate_deps(target.with_suffix(".deps.json"), version, set(assets["libraries"]), receipt["familyVersions"], project, native_ui is not None)
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


def complete(paths: list[Path], configuration: str, version: str, packages: list[str], cache: Path, families: dict,
             target_framework: str | None = None) -> dict:
    projects = {}
    restored_archives = []
    for path in paths:
        receipt = load(path)
        if target_framework is None:
            target_framework = require_framework(receipt.get("targetFramework"))
        verify_receipt(receipt, Path(receipt["project"]), configuration, version, families, target_framework)
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
            "completedNs": time.time_ns(), "targetFramework": target_framework, "familyVersions": families, "projects": projects,
            "publicArchives": public}


def verify_manifest(path: Path, project: Path, configuration: str, version: str, families: dict | None = None,
                    target_framework: str | None = None) -> dict:
    manifest = load(path)
    if manifest.get("schemaVersion") != 1 or manifest.get("completed") is not True:
        raise ValueError("The packaged runtime preparation did not complete")
    if manifest.get("configuration") != configuration or manifest.get("sdkVersion") != version:
        raise ValueError("Completed preparation belongs to another configuration/cohort")
    if families is not None and manifest.get("familyVersions") != families:
        raise ValueError("Completed public family selection differs from the actual launch")
    framework = require_framework(manifest.get("targetFramework"))
    if target_framework is not None and framework != require_framework(target_framework):
        raise ValueError("Completed runtime target framework differs from the actual launch")
    bound = manifest["projects"][str(project.resolve())]
    receipt_path = Path(bound["receipt"])
    if digest(receipt_path) != bound["sha256"]:
        raise ValueError("The completed build receipt drifted")
    for proof in manifest["publicArchives"]:
        actual = archive_identity(Path(proof["path"]), proof["id"], proof["version"], proof["repository"])
        if actual != proof:
            raise ValueError("Public peer package identity/source proof drifted")
    receipt = verify_receipt(load(receipt_path), project, configuration, version, manifest["familyVersions"], framework)
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
    parser.add_argument("--private-admin-ui-receipt", type=Path)
    parser.add_argument("--build-started-ns", type=int)
    parser.add_argument("--role", choices=("runtime", "catalog"), default="runtime")
    parser.add_argument("--complete-manifest", type=Path)
    parser.add_argument("--receipt", type=Path, action="append", default=[])
    parser.add_argument("--public-package", action="append", default=[])
    parser.add_argument("--archive-cache", type=Path)
    parser.add_argument("--manifest", type=Path)
    parser.add_argument("--families-json", default="{}")
    parser.add_argument("--target-framework", choices=("net10.0", "net11.0"))
    args = parser.parse_args()
    try:
        families = json.loads(args.families_json)
        if (args.record or args.complete_manifest or args.manifest) and args.target_framework is None:
            raise ValueError("Completed runtime evidence requires the exact evaluated --target-framework")
        if args.record:
            value = record(json.loads(args.metadata_json), args.project, args.configuration, args.version, args.build_started_ns, args.role, families,
                           args.private_admin_ui_receipt, args.target_framework)
            write(args.record, value)
        elif args.complete_manifest:
            value = complete(args.receipt, args.configuration, args.version, args.public_package, args.archive_cache, families, args.target_framework)
            write(args.complete_manifest, value)
        elif args.manifest:
            print(json.dumps(verify_manifest(args.manifest, args.project, args.configuration, args.version, families, args.target_framework)))
        elif args.assets:
            validate(args.assets, args.version, args.project)
        else:
            raise ValueError("An actual assets, completed build or manifest selection is required")
    except (OSError, ValueError, KeyError, TypeError, zipfile.BadZipFile, ET.ParseError) as failure:
        parser.exit(1, "Runtime SDK qualification failed: " + str(failure) + "\n")


if __name__ == "__main__":
    main()
