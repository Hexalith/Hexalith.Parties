"""Reproduce the final gates from the repository root using run-with-temp."""

import gzip
import hashlib
import json
import os
from pathlib import Path
import subprocess
import time
import uuid
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[4]
OUTPUT = Path(os.environ.get("PARTIES_ERASURE_RESULTS", str(Path(__file__).parent / "parent-final"))).resolve()
OUTPUT.mkdir(parents=True, exist_ok=False)
JOB = Path(os.environ["TMPDIR"])
BASE_ENV = os.environ.copy()
COMMANDS = []
HOOKS = set()


def normalize(value):
    if isinstance(value, str):
        return value.replace(str(JOB), "${JOB}").replace(str(ROOT), "${REPOSITORY}")
    if isinstance(value, list):
        return [normalize(item) for item in value]
    if isinstance(value, dict):
        return {key: normalize(item) for key, item in value.items()}
    return value


def run(name, arguments, environment=None):
    environment = BASE_ENV.copy() if environment is None else environment
    result = subprocess.run(arguments, cwd=ROOT, env=environment, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    captured = normalize(result.stdout.decode(errors="replace"))
    (OUTPUT / (name + ".log.gz")).write_bytes(gzip.compress(captured.encode(), mtime=0))
    selected = {key: value for key, value in environment.items()
                if key in HOOKS or key.startswith("PARTIES_MEMORY_OWNER_DEPENDENCY_")
                or key == "MEMORIES_ERASURE_RECEIPT_DIR"}
    COMMANDS.append({"Name": name, "Arguments": normalize(arguments), "Environment": normalize(selected), "ExitCode": result.returncode})
    (OUTPUT / "commands.json").write_text(json.dumps(COMMANDS, indent=2) + "\n")
    print(name, result.returncode, flush=True)
    if result.returncode:
        print(captured[-5000:], flush=True)
        result.check_returncode()
    return result.stdout


def build(name, project, configuration, artifact_directory, properties=(), environment=None):
    run(name, ["dotnet", "build", project, "-c", configuration, "-m:1", "--disable-build-servers",
               "-p:UseArtifactsOutput=true", "-p:ArtifactsPath=" + str(artifact_directory), *properties], environment)


def tests(name, assembly, classes, namespace, environment=None, extra=()):
    arguments = ["dotnet", str(assembly)]
    for value in classes:
        arguments += ["-class", namespace + value]
    run(name, arguments + list(extra) + ["-parallelMode", "none", "-failSkips", "-result-xml", str(OUTPUT / (name + ".xml"))], environment)


selection = json.loads(run("prepare", ["dotnet", "tool", "run", "hexalith-module", "--", "prepare-build",
                                      "--manifest", "module/hexalith.module-manifest.v2.json", "--workspace", str(JOB / "mapping"),
                                      "--mode", "source", "--output", "json"]))
(OUTPUT / "prepare.json").write_text(json.dumps(normalize(selection), indent=2) + "\n")
mapping = selection["buildPreparation"]["environment"]
HOOKS.update(mapping)
source_environment = BASE_ENV.copy()
source_environment.update(mapping)
source_environment["MEMORIES_ERASURE_RECEIPT_DIR"] = str(OUTPUT / "process-receipts")
build("owner-source-build", "references/Hexalith.Memories/tests/Hexalith.Memories.IntegrationTests/Hexalith.Memories.IntegrationTests.csproj", "Debug", JOB / "source", environment=source_environment)
owner = JOB / "source/bin/Hexalith.Memories.IntegrationTests/debug/Hexalith.Memories.IntegrationTests.dll"
tests("owner-full", owner, ["NaturalLanguageErasureIntegrationTests", "RetryWorkflowErasureIntegrationTests"], "Hexalith.Memories.IntegrationTests.Ingestion.", source_environment)
tests("process", owner, ["MemoryErasureProcessRestartTests", "MemoryErasureProcessProtocolTests"], "Hexalith.Memories.IntegrationTests.Ingestion.", source_environment)

cohort = ["-p:HexalithEventStoreVersion=5.1.0", "-p:HexalithMemoriesVersion=4.0.0", "-p:HexalithFrontComposerVersion=6.0.0"]
for project in ["Hexalith.Parties.Tests", "Hexalith.Parties.IntegrationTests"]:
    build(project + "-build", "tests/" + project + "/" + project + ".csproj", "Release", JOB / "packages", [*cohort, "-p:MinVerVersionOverride=1.0.0"])
consumer = JOB / "packages/bin/Hexalith.Parties.Tests/release/Hexalith.Parties.Tests.dll"
integration = JOB / "packages/bin/Hexalith.Parties.IntegrationTests/release/Hexalith.Parties.IntegrationTests.dll"
tests("consumer", consumer, ["PartyMemoryProducerRestartProtocolTests", "PartyMemoryUnitAuthorityRuntimeTests", "PartyMemorySourceInventoryTests", "PartyMemoryRuntimeTests"], "Hexalith.Parties.Tests.Search.", extra=["-method-", "*SelectedMemories*"])
tests("cleanup", integration, ["PartyMemoryProducerRestartSignalTests", "PartyMemoryOwnedCleanupTests"], "Hexalith.Parties.IntegrationTests.Topology.")
build("b7-owner-build", "references/Hexalith.Memories/src/Hexalith.Memories.Server/Hexalith.Memories.Server.csproj", "Release", JOB / "b7", [*cohort, "-p:Version=4.0.0"])
b7 = JOB / "b7/bin/Hexalith.Memories.Server/release/Hexalith.Memories.Server.dll"
image = "redis/redis-stack:latest@sha256:880df9c228597cb0d15b585f39a4327d6ee2d8b0d0f155e3f75dba9a761d4ec3"
container = run("b7-redis-start", ["docker", "run", "-d", "--name", "parties-b7-final-" + uuid.uuid4().hex, "-p", "127.0.0.1::6379", image]).decode().strip()
try:
    endpoint = run("b7-redis-endpoint", ["docker", "port", container, "6379/tcp"]).decode().strip()
    deadline = time.monotonic() + 30
    while subprocess.run(["docker", "exec", container, "redis-cli", "ping"], capture_output=True).returncode:
        if time.monotonic() > deadline:
            raise TimeoutError("Owned Redis readiness deadline")
        time.sleep(0.1)
    environment = BASE_ENV.copy()
    environment.update(PARTIES_MEMORY_OWNER_DEPENDENCY_SERVER=str(b7), PARTIES_MEMORY_OWNER_DEPENDENCY_REDIS=endpoint, MEMORIES_ERASURE_RECEIPT_DIR=str(OUTPUT / "dependency-receipts"))
    tests("dependency", consumer, [], "", environment, ["-method", "Hexalith.Parties.Tests.Search.PartyMemoryOwnerDependencyTests.ActualOwnerDependencyTraceQualifiesDivergentAssetCounterexamples"])
finally:
    removed = subprocess.run(["docker", "rm", "-f", container], capture_output=True, text=True)
    inspected = subprocess.run(["docker", "inspect", container], capture_output=True, text=True)
    (OUTPUT / "b7-cleanup.json").write_text(json.dumps({"Container": container, "RemovalExit": removed.returncode, "RemovalOutput": removed.stdout + removed.stderr, "PostRemovalInspectionExit": inspected.returncode}, indent=2) + "\n")
    assert removed.returncode == 0 and inspected.returncode != 0

summary = {}
for label in ["owner-full", "process", "consumer", "cleanup", "dependency"]:
    assembly = next(ET.parse(OUTPUT / (label + ".xml")).getroot().iter("assembly"))
    summary[label] = {key: int(assembly.get(key, "0")) for key in ["total", "passed", "failed", "skipped", "errors"]}
    assert summary[label]["total"] == summary[label]["passed"] > 0
    assert not sum(summary[label][key] for key in ["failed", "skipped", "errors"])
identities = {label: {file.name: hashlib.sha256(file.read_bytes()).hexdigest() for file in assembly.parent.glob("*.dll")}
              for label, assembly in [("owner", owner), ("consumer", consumer), ("integration", integration), ("b7", b7)]}
(OUTPUT / "executed-identities.json").write_text(json.dumps(identities, indent=2) + "\n")
summary.update(MemoriesCommit=subprocess.check_output(["git", "-C", "references/Hexalith.Memories", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip(), SourceMappingHash=mapping["HEXALITH_SOURCE_MAPPING_HASH"], B7ContainerRemoved=True)
(OUTPUT / "summary.json").write_text(json.dumps(summary, indent=2) + "\n")
print(json.dumps(summary), flush=True)
for file in [*OUTPUT.glob("*.xml"), OUTPUT / "executed-identities.json", *(OUTPUT / "process-receipts").glob("*.json"), *(OUTPUT / "dependency-receipts").glob("*.json")]:
    file.with_suffix(file.suffix + ".gz").write_bytes(gzip.compress(normalize(file.read_text()).encode(), mtime=0))
    file.unlink()
