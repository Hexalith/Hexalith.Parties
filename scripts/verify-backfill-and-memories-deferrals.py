#!/usr/bin/env python3
"""Reproduce the backfill and Memories 4.1.0 release checks on Linux."""

import argparse
import base64
from collections import Counter
import hashlib
import json
import os
from pathlib import Path
import shlex
import shutil
import signal
import socket
import subprocess
import tempfile
from urllib.parse import urlsplit
from urllib.request import ProxyHandler, Request, build_opener
from xml.etree import ElementTree as ET
from zipfile import ZipFile


PARTIES = Path(__file__).resolve().parent.parent
BACKFILL_PROJECT = "Hexalith.Parties.Runtime.IntegrationTests"
MEMORIES_PROJECT = "Hexalith.Memories.IntegrationTests"
NIGHTLY_PROJECT = "Hexalith.Memories.Integration.NightlyTests"
MEMORIES_VERSION = "4.1.0"
EVENTSTORE_VERSION = "5.4.0"
MEMORIES_REPOSITORY = "https://github.com/Hexalith/Hexalith.Memories"

BACKFILL_NAMESPACE = "Hexalith.Parties.Tests.Projections"
BACKFILL_RUNTIME = f"{BACKFILL_NAMESPACE}.PartyLegacyReadModelBackfillRuntimeTests"
BACKFILL_PROCESS = f"{BACKFILL_NAMESPACE}.PartyLegacyBackfillProcessRuntimeTests"
WORKLOAD_METHOD = f"{BACKFILL_PROCESS}.ActualProviderBackfillSurvivesSeparateWorkloadProcesses"
WORKLOAD_RECEIPT_PREFIX = "Backfill workload receipt (base64): "
WORKLOAD_SNAPSHOTS = {
    "seed": {"survivors.json", "canonical.json"},
    "interrupt": {"survivors.json", "canonical.json", "generation.json"},
    "rebuild": {"survivors.json", "canonical.json", "generation.json"},
    "stage": {"survivors.json"},
    "commit": {"survivors.json"},
    "verify": {"survivors.json", "generation.json"},
}
INDEX_RECEIPT_KEYS = frozenset(("present", "sha256", "etag"))


def boolean_cases(parameter):
    return tuple(f"({parameter}: {value})" for value in ("False", "True"))


FACT = ("",)
BACKFILL_METHODS = {
    f"{BACKFILL_RUNTIME}.ActualProviderRebuildPersistsAcrossFreshClientsAndPreservesErasureAndRollbackBytes": boolean_cases("existing"),
    f"{BACKFILL_RUNTIME}.ActualProviderIndexCompetitorAfterStagingSurvivesCommit": boolean_cases("existing"),
    f"{BACKFILL_RUNTIME}.ActualProviderCompetingWriteAfterPreparationRefusesPublicationAndPreservesEveryCanonicalKey": tuple(
        f'(target: "{target}", existing: {existing})'
        for target in ("detail", "processing", "index") for existing in ("False", "True")),
    f"{BACKFILL_PROCESS}.EvidenceRetentionFailureStillDeletesScratchAndPreservesPrimary": boolean_cases("qualificationFailed"),
    WORKLOAD_METHOD: FACT,
    f"{BACKFILL_PROCESS}.WorkloadReportRefusesSubstitutedOrMalformedEvidence": tuple(
        f'(defect: "{defect}")' for defect in ("phase", "target", "nested output", "identity", "counts")),
    f"{BACKFILL_PROCESS}.IndexReceiptRefusesMissingDriftedLeakedUnchangedOrMismatchedState": tuple(
        f'(existing: {existing}, defect: "{defect}")' for existing, defect in (
            ("False", "missing-object"),
            ("False", "duplicate-object"),
            ("False", "missing-field"),
            ("False", "extra-field"),
            ("False", "duplicate-field"),
            ("False", "absent-leaked-present"),
            ("False", "absent-leaked-etag"),
            ("False", "stage-leaked-candidate"),
            ("False", "commit-missing"),
            ("False", "commit-null-etag"),
            ("False", "invalid-sha256"),
            ("False", "blank-etag"),
            ("False", "untrimmed-etag"),
            ("False", "verify-mismatched-hash"),
            ("True", "existing-missing"),
            ("True", "interrupt-drifted-etag"),
            ("True", "rebuild-drifted-hash"),
            ("True", "stage-leaked-etag"),
            ("True", "stage-drifted-hash"),
            ("True", "commit-unchanged-hash"),
            ("True", "commit-unchanged-etag"),
            ("True", "verify-mismatched-etag"),
        )),
}
INGESTION = f"{MEMORIES_PROJECT}.Ingestion"
DISCOVERY = f"{INGESTION}.NaturalLanguageErasureIntegrationTests"
AUTHORITY = f"{INGESTION}.UnitOwnershipAuthorityIntegrationTests"
MEMORIES_METHODS = {
    "same-uri-refresh": {
        f"{INGESTION}.RefreshPublicationIntegrationTests.BothCompletionOrdersPreserveCurrentContentAndRetryableNewerRevision": boolean_cases("newerFirst"),
    },
    "bounded-discovery": {
        f"{DISCOVERY}.ModernDiscoveryUsesExactOwnershipAndMissingLegacyIndexRequiresReconciliation": FACT,
        f"{DISCOVERY}.LegacyNativeDiscoveryCoversExactUnitChunksAndBothNamespacesWhileOtherUnitsSurvive": FACT,
        f"{DISCOVERY}.LegacyDiscoveryRefusesPartialTimeoutPolicyWithoutSealingCompleteness": FACT,
        f"{DISCOVERY}.MissingOptionalLegacyTagRemainsUnqualifiedAndPreservesTheObligation": FACT,
    },
    "cross-source-authority": {
        f"{AUTHORITY}.UnconfiguredProducerCannotBorrowIssuedAuthorityForUnscheduledCleanup": FACT,
        f"{AUTHORITY}.IssuedAnnotationRetainsAuthorityAcrossDeletionPurgeAndSourceChange": FACT,
        f"{AUTHORITY}.HistoricalUnitRequiresExactEvidenceRegardlessOfSourceOrBareMarker": (
            "(bareMarker: null)", '(bareMarker: "modern")', '(bareMarker: "legacy-owner-reconciled-v1")'),
        f"{AUTHORITY}.HistoricalReconciliationRefusesForeignKeysChangedEvidenceAndReusedGeneration": FACT,
    },
    "concurrent-older-admission": {
        f"{INGESTION}.RetryWorkflowErasureIntegrationTests.ActualOlderProducerBetweenAbsentReadAndAdmissionCannotGainModernAuthority": boolean_cases("annotation"),
        f"{INGESTION}.MemoryErasureProcessRestartTests.ActualOlderProducerCompletesBetweenAbsentReadAndAdmissionAcrossReplacement": boolean_cases("annotation"),
    },
    "purged-identity-reuse": {
        f"{INGESTION}.RetryWorkflowErasureIntegrationTests.PurgedActualHistoricalWorkflowCannotGainCompletenessFromReusedIdentity": boolean_cases("crossSource"),
    },
}


# Freeze the required outcomes separately from the selectors used to launch each assembly.
REQUIRED_CASES = {
    lane: frozenset(method + args for method, variants in methods.items() for args in variants)
    for lane, methods in {"backfill": BACKFILL_METHODS, **MEMORIES_METHODS}.items()
}


def complete_summary(summary):
    checks = summary["checks"]
    if checks.keys() != REQUIRED_CASES.keys():
        raise ValueError("Missing or unexpected qualification lane")
    for lane, required in REQUIRED_CASES.items():
        outcome = checks[lane]
        if outcome["passed"] != len(required) or Counter(outcome["cases"]) != Counter(required):
            raise ValueError(f"{lane}: incomplete required case evidence")
    summary["status"] = "passed"


def git(repo, *args):
    return subprocess.check_output(["git", *args], cwd=repo, text=True).strip()


def builds_revision(repo):
    reference = "references/Hexalith.Builds"
    checkout = repo / reference
    revision = git(repo, "rev-parse", f"HEAD:{reference}")
    if git(checkout, "rev-parse", "HEAD") != revision or git(checkout, "status", "--porcelain"):
        raise ValueError(f"Use the clean, pinned Builds reference in {repo}")
    return revision


def run(command, cwd, env, log):
    print(f"Running {log.stem}; output in {log}", flush=True)
    with log.open("w", encoding="utf-8") as stream:
        stream.write(f"$ {shlex.join(command)}\n")
        stream.flush()
        with subprocess.Popen(command, cwd=cwd, env=env, stdin=subprocess.DEVNULL,
                              stdout=stream, stderr=subprocess.STDOUT, start_new_session=True) as process:
            try:
                status = process.wait()
            except KeyboardInterrupt:
                try:
                    os.killpg(process.pid, signal.SIGTERM)
                    process.wait(timeout=15)
                except ProcessLookupError:
                    pass
                except subprocess.TimeoutExpired:
                    os.killpg(process.pid, signal.SIGKILL)
                    process.wait()
                raise
        if status != 0:
            raise subprocess.CalledProcessError(status, command)


def build(repo, project, artifacts, env, results, extra=()):
    run(["dotnet", "build", f"tests/{project}/{project}.csproj", "-c", "Release", "-m:1",
         "--disable-build-servers", "--artifacts-path", str(artifacts),
         "-p:UseHexalithProjectReferences=false", "-p:UseNuGetDeps=true", *extra],
        repo, env, results / f"{project}-build.log")
    assembly = artifacts / "bin" / project / "release" / f"{project}.dll"
    shutil.copyfile(assembly.with_suffix(".deps.json"), results / f"{project}.deps.json")
    return assembly, artifacts / "obj" / project / "project.assets.json"


def file_hash(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def package_sources(assets, revision):
    restored = json.loads(assets.read_text())
    packages = {
        "Hexalith.EventStore.DomainService": EVENTSTORE_VERSION,
        "Hexalith.Memories.Client.Rest": MEMORIES_VERSION,
        "Hexalith.Memories.Contracts": MEMORIES_VERSION,
    }
    identities = {}
    for name, version in packages.items():
        package = restored["libraries"].get(f"{name}/{version}", {})
        if package.get("type") != "package":
            raise ValueError(f"Expected restored package {name} {version}")
        filename = f"{name.lower()}.{version}.nupkg"
        archive = next((path for folder in restored["packageFolders"]
                        if (path := Path(folder) / package["path"] / filename).is_file()), None)
        if archive is None:
            raise ValueError(f"Restored archive missing for {name} {version}")
        identity = {"version": version, "sha256": file_hash(archive)}
        if name.startswith("Hexalith.Memories."):
            with ZipFile(archive) as nupkg:
                nuspec = ET.fromstring(nupkg.read(f"{name}.nuspec"))
            source = nuspec.find("./{*}metadata/{*}repository")
            if (source is None or source.get("type") != "git"
                    or source.get("url") != MEMORIES_REPOSITORY or source.get("commit") != revision):
                raise ValueError(f"{name} {version} does not identify the supplied Memories checkout")
            identity["source"] = revision
        identities[name] = identity
    return identities


def check_pass_counts(report, node, count):
    for field in ("total", "passed", "failed", "skipped", "not-run"):
        if node.get(field) != str(count if field in ("total", "passed") else 0):
            raise ValueError(f"{report}: inconsistent xUnit {node.tag} {field} count")


def check_report(report, expected, project, *, expected_receipt=None):
    root = ET.parse(report).getroot()
    assemblies = root.findall("assembly")
    if root.tag != "assemblies" or len(assemblies) != 1 or Path(assemblies[0].get("name", "")).name != f"{project}.dll":
        raise ValueError(f"{report}: expected one {project} assembly")
    assembly = assemblies[0]
    if assembly.get("errors") != "0" or assembly.find("errors/error") is not None:
        raise ValueError(f"{report}: xUnit reported assembly errors")

    tests = assembly.findall("collection/test")
    if len(tests) != len(list(root.iter("test"))):
        raise ValueError(f"{report}: xUnit test outside assembly/collection")
    check_pass_counts(report, assembly, len(tests))
    for collection in assembly.findall("collection"):
        check_pass_counts(report, collection, len(collection.findall("test")))

    required = Counter((method, method + args) for method, variants in expected.items() for args in variants)
    observed = Counter()
    for test in tests:
        if test.get("result") != "Pass":
            raise ValueError(f"{report}: test did not pass: {test.get('name')}")
        method = f"{test.attrib['type']}.{test.attrib['method']}"
        name = test.attrib["name"].replace(r'\"', '"')
        observed[method, name] += 1
    if observed != required:
        raise ValueError(f"{report}: missing {list((required - observed).elements())}, unexpected {list((observed - required).elements())}")
    if expected_receipt is not None:
        outputs = [output for test in tests for output in test.findall("output")]
        if any(len(output) for output in outputs):
            raise ValueError(f"{report}: passing workload test must contain one matching XML receipt in plain output text")
        receipts = [line for output in outputs for line in (output.text or "").splitlines()
                    if line.startswith(WORKLOAD_RECEIPT_PREFIX)]
        encoded = base64.b64encode(expected_receipt).decode("ascii")
        if receipts != [WORKLOAD_RECEIPT_PREFIX + encoded]:
            raise ValueError(f"{report}: passing workload test must contain one matching XML receipt")
    return {"passed": len(tests), "cases": sorted(name for _, name in observed)}


def run_lane(lane, assembly, methods, repo, env, results):
    report = results / f"{lane}.xml"
    selectors = [argument for method in methods for argument in ("-method", method)]
    run(["dotnet", str(assembly), "-failSkips", "-result-xml", str(report), *selectors],
        repo, env, results / f"{lane}.log")
    return check_report(report, methods, assembly.stem)


def unique_receipt_object(pairs):
    result = dict(pairs)
    if len(result) != len(pairs):
        raise ValueError("Duplicate workload receipt property")
    return result


def is_sha256(value):
    return isinstance(value, str) and len(value) == 64 and all(ch in "0123456789abcdef" for ch in value)


def validate_index_receipt(target, phase, existing, index, baseline, committed):
    if not isinstance(index, dict) or index.keys() != INDEX_RECEIPT_KEYS:
        raise ValueError(f"{target}: missing or malformed {phase} workload index receipt")
    present = index["present"]
    sha256 = index["sha256"]
    etag = index["etag"]
    if type(present) is not bool:
        raise ValueError(f"{target}: invalid {phase} workload index presence")
    expected_present = True if phase in ("commit", "verify") else existing
    if present is not expected_present:
        raise ValueError(f"{target}: unexpected {phase} workload index presence")
    if not present:
        if sha256 is not None or etag is not None:
            raise ValueError(f"{target}: unexpected {phase} workload index digest or ETag while absent")
        return index
    if not is_sha256(sha256):
        raise ValueError(f"{target}: missing or invalid {phase} workload index SHA-256")
    if phase == "stage":
        if etag is not None:
            raise ValueError(f"{target}: stage workload index receipt must not expose an ETag")
        if sha256 != baseline["sha256"]:
            raise ValueError(f"{target}: stage workload index SHA-256 drifted from preexisting index")
        return index
    if not isinstance(etag, str) or not etag.strip() or etag != etag.strip():
        raise ValueError(f"{target}: missing or invalid {phase} workload index ETag")
    if phase in ("interrupt", "rebuild"):
        if sha256 != baseline["sha256"] or etag != baseline["etag"]:
            raise ValueError(f"{target}: {phase} workload index drifted from seed index")
    elif phase == "commit":
        if existing and (sha256 == baseline["sha256"] or etag == baseline["etag"]):
            raise ValueError(f"{target}: commit workload index did not change preexisting index bytes and ETag")
    elif phase == "verify":
        if sha256 != committed["sha256"] or etag != committed["etag"]:
            raise ValueError(f"{target}: verify workload index mismatched commit index")
    return index


def verify_workloads(directory):
    outcomes = {}
    for target in sorted(directory.iterdir()):
        manifest_bytes = (target / "input.json").read_bytes()
        manifest = json.loads(manifest_bytes)
        input_sha256 = hashlib.sha256(manifest_bytes).hexdigest()
        existing = manifest["existing"]
        if type(existing) is not bool or existing in outcomes or manifest["tenant"] != target.name:
            raise ValueError(f"{target}: unexpected backfill workload manifest")
        operation = manifest.get("indexOperation")
        if not isinstance(operation, str) or not operation.strip():
            raise ValueError(f"{target}: invalid backfill workload operation")
        pids = set()
        interrupted_pid = None
        snapshots = {}
        baseline_index = None
        committed_index = None
        index_by_phase = {}
        for phase, expected_snapshots in WORKLOAD_SNAPSHOTS.items():
            receipt_bytes = (target / f"{phase}.json").read_bytes()
            receipt = json.loads(receipt_bytes, object_pairs_hook=unique_receipt_object)
            if (receipt["tenant"], receipt["operation"], receipt["phase"]) != (target.name, operation, phase):
                raise ValueError(f"{target}: mismatched {phase} workload receipt")
            if receipt.get("inputSha256") != input_sha256:
                raise ValueError(f"{target}: missing or mismatched {phase} workload input SHA-256")
            pid = receipt["processId"]
            if type(pid) is not int or pid <= 0:
                raise ValueError(f"{target}: invalid {phase} workload process ID")
            if phase == "interrupt":
                interrupted_pid = pid
            check_report(target / f"{phase}.xml", {WORKLOAD_METHOD: FACT}, BACKFILL_PROJECT, expected_receipt=receipt_bytes)
            observations = receipt.get("snapshotSha256")
            if not isinstance(observations, dict) or observations.keys() != expected_snapshots:
                raise ValueError(f"{target}: missing or unexpected {phase} workload snapshot observations")
            for name, digest in observations.items():
                if snapshots.setdefault(name, digest) != digest:
                    raise ValueError(f"{target}: inconsistent workload observations for {name}")
            index = validate_index_receipt(target, phase, existing, receipt.get("index"), baseline_index, committed_index)
            index_by_phase[phase] = index
            if phase == "seed":
                baseline_index = index
            elif phase == "commit":
                committed_index = index
            pids.add(pid)
        if len(pids) != len(WORKLOAD_SNAPSHOTS):
            raise ValueError(f"{target}: expected a distinct process for each phase")
        for name, digest in snapshots.items():
            try:
                retained = (target / name).read_bytes()
            except FileNotFoundError as error:
                raise ValueError(f"{target}: missing retained workload snapshot {name}") from error
            if hashlib.sha256(retained).hexdigest() != digest:
                raise ValueError(f"{target}: retained workload snapshot SHA-256 mismatch for {name}")
            if name == "generation.json":
                generation = json.loads(retained, object_pairs_hook=unique_receipt_object)
                observed_pid = generation.get("interruptedProcess") if isinstance(generation, dict) else None
                if type(observed_pid) is not int or observed_pid != interrupted_pid:
                    raise ValueError(f"{target}: generation snapshot process differs from interruption receipt")
        outcomes[existing] = {"tenant": target.name, "inputSha256": input_sha256,
                              "phases": list(WORKLOAD_SNAPSHOTS), "snapshotSha256": snapshots,
                              "index": index_by_phase}
    if outcomes.keys() != {False, True}:
        raise ValueError(f"{directory}: expected workload receipts for the empty and existing targets")
    return {"empty": outcomes[False], "existing": outcomes[True]}


def backfill_provider(config):
    # The passing backfill tests have bound both loopback endpoints to this owned provider.
    settings = json.loads(config.read_text())
    opener = build_opener(ProxyHandler({}))
    request = Request(settings["daprHttpEndpoint"].rstrip("/") + "/v1.0/metadata",
                      headers={"dapr-api-token": settings["daprApiToken"]})
    with opener.open(request, timeout=5) as response:
        metadata = json.load(response)
    if metadata["id"] != settings["daprAppId"]:
        raise ValueError("Backfill Dapr identity changed after the rehearsal")
    endpoint = urlsplit("//" + settings["redisEndpoint"])
    with socket.create_connection((endpoint.hostname, endpoint.port), timeout=5) as redis:
        redis.sendall(b"*2\r\n$4\r\nINFO\r\n$6\r\nserver\r\n")
        with redis.makefile("rb") as stream:
            header = stream.readline(128)
            if not header.startswith(b"$") or not 0 < (size := int(header[1:])) <= 65536:
                raise ValueError("Backfill Redis did not return a bounded server identity")
            info = dict(line.split(":", 1) for line in stream.read(size).decode().splitlines() if ":" in line)
    return {"daprVersion": metadata["runtimeVersion"], "redisVersion": info["redis_version"]}


def daprd_override(source, scratch, daprd):
    if not daprd:
        return ()
    path = Path(daprd)
    if not path.is_absolute() or not path.is_file() or not os.access(path, os.X_OK):
        raise ValueError("PARTIES_MEMORIES_TEST_DAPRD must name an absolute executable path")
    fixture = source / "tests" / MEMORIES_PROJECT / "Fixtures" / "DaprStateSidecarFixture.cs"
    original = b'string candidate = Path.Combine(home, ".dapr", "bin", "daprd");'
    content = fixture.read_bytes()
    if content.count(original) != 1:
        raise ValueError("Released Memories fixture no longer has the expected Dapr path lookup")
    override = scratch / fixture.name
    override.write_bytes(content.replace(original, b'string candidate = Environment.GetEnvironmentVariable('
                                         b'"PARTIES_MEMORIES_TEST_DAPRD") ?? Path.Combine(home, ".dapr", "bin", "daprd");'))
    targets = scratch / "memories-fixture.targets"
    targets.write_text(f"""<Project>
  <Target Name="UseQualificationDaprPath" BeforeTargets="CoreCompile"
          Condition="'$(MSBuildProjectName)' == '{MEMORIES_PROJECT}' Or '$(MSBuildProjectName)' == '{NIGHTLY_PROJECT}'">
    <ItemGroup>
      <Compile Remove="@(Compile)" Condition="'%(Compile.Filename)%(Compile.Extension)' == '{fixture.name}'" />
      <Compile Include="$(MSBuildThisFileDirectory){fixture.name}" />
    </ItemGroup>
  </Target>
</Project>
""")
    return (f"-p:CustomAfterMicrosoftCommonTargets={targets}",)


def qualify(source, results, config, scratch):
    env = os.environ.copy()
    for key in ("PARTIES_BACKFILL_WORKLOAD_MANIFEST", "PARTIES_BACKFILL_WORKLOAD_PHASE", "PARTIES_BACKFILL_WORKLOAD_HASH",
                "MEMORIES_TEST_OWNED_NATIVE", "HexalithBuildsRoot", "HexalithSourceMappingMode", "HexalithVersionsLoaded",
                "Hexalith1BuildPackageProps", "Hexalith2BuildPackageProps", "Hexalith3BuildPackageProps"):
        env.pop(key, None)
    for key in ("MEMORIES_DAPR_PLACEMENT_HOST_ADDRESS", "MEMORIES_DAPR_SCHEDULER_HOST_ADDRESS"):
        endpoint = urlsplit("//" + env.get(key, ""))
        if (endpoint.hostname not in {"127.0.0.1", "::1", "localhost"} or not endpoint.port
                or endpoint.username is not None or endpoint.path or endpoint.query or endpoint.fragment):
            raise ValueError(f"{key} must identify the disposable loopback service as host:port")
    workloads = results / "backfill-workloads"
    workloads.mkdir()
    env.update(TMPDIR=str(scratch), PARTIES_BACKFILL_STATE_RUNTIME_CONFIG=str(config),
               PARTIES_BACKFILL_WORKLOAD_EVIDENCE=str(workloads))

    revision = git(source, "rev-parse", "HEAD")
    if git(source, "status", "--porcelain", "--ignore-submodules=none"):
        raise ValueError("Use a clean standalone Memories checkout with its pinned direct Builds reference")
    daprd = env.get("PARTIES_MEMORIES_TEST_DAPRD")
    override = daprd_override(source, scratch, daprd)
    runtime = Path(daprd) if daprd else Path.home() / ".dapr/bin/daprd"
    daprd_version = subprocess.check_output([str(runtime), "--version"], env=env, text=True, timeout=10).strip()
    summary = {
        "status": "incomplete",
        "partiesRevision": git(PARTIES, "rev-parse", "HEAD"),
        "partiesWorktree": git(PARTIES, "status", "--porcelain", "--ignore-submodules=none").splitlines(),
        "partiesBuildsRevision": builds_revision(PARTIES),
        "memoriesRevision": revision,
        "memoriesBuildsRevision": builds_revision(source),
        "memoriesDaprd": {"version": daprd_version, "sha256": file_hash(runtime), "pathOverride": bool(daprd)},
        "runnerSha256": file_hash(Path(__file__).resolve()),
        "dotnetVersion": subprocess.check_output(["dotnet", "--version"], cwd=PARTIES, env=env, text=True).strip(),
        "assemblies": {},
        "checks": {},
    }
    def retain_summary():
        (results / "summary.json").write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")

    retain_summary()
    backfill, assets = build(PARTIES, BACKFILL_PROJECT, scratch / "parties", env, results,
                             (f"-p:HexalithEventStoreVersion={EVENTSTORE_VERSION}",
                              f"-p:HexalithMemoriesVersion={MEMORIES_VERSION}"))
    summary["packages"] = package_sources(assets, revision)
    summary["assemblies"][BACKFILL_PROJECT] = file_hash(backfill)
    retain_summary()
    summary["checks"]["backfill"] = run_lane("backfill", backfill, BACKFILL_METHODS, PARTIES, env, results)
    summary["backfillWorkloads"] = verify_workloads(workloads)
    summary["backfillProvider"] = backfill_provider(config)
    retain_summary()

    for project, lanes in ((MEMORIES_PROJECT, ("same-uri-refresh", "bounded-discovery", "cross-source-authority")),
                           (NIGHTLY_PROJECT, ("purged-identity-reuse", "concurrent-older-admission"))):
        assembly, _ = build(source, project, scratch / "memories", env, results, override)
        summary["assemblies"][project] = file_hash(assembly)
        retain_summary()
        for lane in lanes:
            summary["checks"][lane] = run_lane(lane, assembly, MEMORIES_METHODS[lane], source, env, results)
            retain_summary()

    complete_summary(summary)
    retain_summary()
    for lane, outcome in summary["checks"].items():
        print(f"{lane}: {outcome['passed']} passed")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("memories", type=Path, help="clean standalone checkout matching Memories 4.1.0")
    parser.add_argument("results", type=Path, help="new directory for test evidence")
    args = parser.parse_args()
    config = Path(os.environ.get("PARTIES_BACKFILL_STATE_RUNTIME_CONFIG", ""))
    if not config.is_absolute() or not config.is_file():
        parser.error("PARTIES_BACKFILL_STATE_RUNTIME_CONFIG must name the disposable provider configuration file")
    results = args.results.resolve()
    def interrupt(_signum, _frame):
        raise KeyboardInterrupt

    signal.signal(signal.SIGTERM, interrupt)
    try:
        results.mkdir(mode=0o700)
        with tempfile.TemporaryDirectory(prefix="parties-backfill-memories-") as scratch:
            qualify(args.memories.resolve(strict=True), results, config, Path(scratch))
    except KeyboardInterrupt:
        parser.exit(130, "Verification interrupted\n")
    except (OSError, ValueError, KeyError, ET.ParseError, subprocess.SubprocessError) as error:
        parser.exit(1, f"Verification failed: {error}\n")


if __name__ == "__main__":
    main()
