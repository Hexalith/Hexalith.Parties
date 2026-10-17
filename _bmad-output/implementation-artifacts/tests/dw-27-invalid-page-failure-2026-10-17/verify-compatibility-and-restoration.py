"""Reconstruct the candidate in a temporary directory and verify byte bindings."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import tempfile


class VerificationFailure(RuntimeError):
    """A saved evidence binding does not match the inspected bytes."""


def require(condition, message):
    """Keep proof checks active even when Python optimization is enabled."""
    if not condition:
        raise VerificationFailure(message)


def sha256(data):
    return hashlib.sha256(data).hexdigest()


def baseline_bytes(repository, revision, relative_path):
    return subprocess.check_output(["git", "show", f"{revision}:{relative_path}"], cwd=repository)


def extract_legacy_test(data):
    signature = b"    [Fact]\n    public async Task QueryExecutionFailedExceptionHandler_InvalidCursor_"
    start = data.index(signature)
    closing_line = b"\n    }\n"
    end = data.index(closing_line, start) + len(closing_line)
    return data[start:end]


def extract_constructor(data):
    start = data.index(b"    public QueryExecutionFailedException(")
    end = data.index(b"        : base(detail)", start)
    return data[start:end]


def verify(manifest_path, receipt_path):
    output = Path(__file__).resolve().parent
    root = output.parents[3]
    eventstore = root / "references/Hexalith.EventStore"
    validation = json.loads((output / "validation.json").read_text())
    manifest = json.loads(manifest_path.read_text())
    expected_paths = {
        "src/Hexalith.Parties/Queries/PartySdkQueryService.cs",
        "tests/Hexalith.Parties.Tests/Gateway/PartySdkQueryHandlerTests.cs",
        "tests/Hexalith.Parties.Tests/Gateway/EventStoreGatewayRoutingTests.cs",
    }
    require(set(manifest["files"]) == expected_paths, "Manifest does not bind all three consumer files")
    patch_path = output / "parties-consumer-adoption.patch"
    require(sha256(patch_path.read_bytes()) == manifest["patch_sha256"], "Retained patch hash changed")
    statistics = subprocess.check_output(["git", "apply", "--numstat", str(patch_path)], cwd=root, text=True)
    patched_paths = {line.split("\t", 2)[2] for line in statistics.splitlines()}
    require(patched_paths == expected_paths, "Patch paths do not match the consumer manifest")

    legacy_path = "tests/Hexalith.EventStore.Server.Tests/ErrorHandling/QueryProblemDetailsTests.cs"
    original = extract_legacy_test(baseline_bytes(eventstore, validation["eventstore_baseline"], legacy_path))
    current = extract_legacy_test((eventstore / legacy_path).read_bytes())
    require(original == current, "Legacy cursor HTTP test raw bytes changed")
    legacy = {
        "repository": str(eventstore), "path": legacy_path,
        "baseline_revision": validation["eventstore_baseline"],
        "baseline_test_sha256": sha256(original), "current_test_sha256": sha256(current),
        "raw_bytes_equal": True, "test_source_utf8": original.decode("utf-8"),
    }
    constructor_path = "src/Hexalith.EventStore.Server/Queries/QueryExecutionFailedException.cs"
    original_constructor = extract_constructor(baseline_bytes(eventstore, validation["eventstore_baseline"], constructor_path))
    current_constructor = extract_constructor((eventstore / constructor_path).read_bytes())
    require(original_constructor == current_constructor, "Exception constructor signature changed")
    constructor = {
        "path": constructor_path,
        "baseline_signature_sha256": sha256(original_constructor),
        "current_signature_sha256": sha256(current_constructor), "raw_bytes_equal": True,
    }

    restored = {}
    candidates = {}
    with tempfile.TemporaryDirectory(prefix="dw-27-reconstruct-") as temporary:
        reconstructed = Path(temporary)
        for relative_path, binding in manifest["files"].items():
            baseline = baseline_bytes(root, validation["parties_baseline"], relative_path)
            current_file = (root / relative_path).read_bytes()
            require(baseline == current_file, f"Tracked consumer file differs: {relative_path}")
            require(sha256(baseline) == binding["baseline_sha256"], f"Baseline hash mismatch: {relative_path}")
            target = reconstructed / relative_path
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(baseline)
            restored[relative_path] = {
                "baseline_revision": validation["parties_baseline"],
                "baseline_sha256": sha256(baseline), "current_sha256": sha256(current_file),
                "raw_bytes_equal": True,
            }
        apply_command = ["git", "-c", "core.whitespace=cr-at-eol", "apply", str(patch_path)]
        subprocess.run(apply_command, cwd=reconstructed, check=True, capture_output=True)
        for relative_path, binding in manifest["files"].items():
            actual = sha256((reconstructed / relative_path).read_bytes())
            require(actual == binding["tested_candidate_sha256"], f"Tested candidate hash mismatch: {relative_path}")
            candidates[relative_path] = {
                "expected_sha256": binding["tested_candidate_sha256"],
                "reconstructed_sha256": actual, "hash_matches": True,
            }
    result = {
        "command": [sys.executable, str(Path(__file__).relative_to(root)), "--manifest", str(manifest_path)],
        "cwd": str(root), "exit_code": 0,
        "legacy_cursor_http_test": legacy, "exception_constructor_signature": constructor,
        "tracked_consumer_files": restored, "reconstructed_tested_candidate_files": candidates,
        "patch_sha256": sha256(patch_path.read_bytes()), "manifest_sha256": sha256(manifest_path.read_bytes()),
        "reconstruction": "Saved patch applied only to baseline copies in a temporary directory",
    }
    receipt_path.write_text(json.dumps(result, indent=2) + "\n")
    print("PASS: raw compatibility preserved; tracked consumers restored; reconstructed candidate hashes match")


def main():
    output = Path(__file__).resolve().parent
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", type=Path, default=output / "consumer-patch-manifest.json")
    parser.add_argument("--receipt", type=Path, default=output / "compatibility-and-restoration-check.json")
    options = parser.parse_args()
    try:
        verify(options.manifest.resolve(), options.receipt.resolve())
    except (VerificationFailure, OSError, ValueError, KeyError, subprocess.CalledProcessError) as error:
        print(f"FAIL: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
