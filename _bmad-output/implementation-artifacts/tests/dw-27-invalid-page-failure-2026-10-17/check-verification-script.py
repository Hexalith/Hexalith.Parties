"""Exercise normal/optimized proof checks and reject a corrupt candidate binding."""
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile

output = Path(__file__).resolve().parent
root = output.parents[3]
manifest_path = output / "consumer-patch-manifest.json"
manifest = json.loads(manifest_path.read_text())
bound_paths = [root / path for path in manifest["files"]] + [manifest_path, output / "parties-consumer-adoption.patch"]
before = {str(path): hashlib.sha256(path.read_bytes()).hexdigest() for path in bound_paths}
results = []
with tempfile.TemporaryDirectory(prefix="dw-27-proof-fixtures-") as temporary:
    fixture_directory = Path(temporary)
    corrupt = json.loads(manifest_path.read_text())
    corrupt["files"][next(iter(corrupt["files"]))]["tested_candidate_sha256"] = "0" * 64
    corrupt_path = fixture_directory / "corrupt-candidate-manifest.json"
    corrupt_path.write_text(json.dumps(corrupt))
    for optimized in (False, True):
        for invalid in (False, True):
            name = ("optimized" if optimized else "normal") + ("-corrupt-candidate" if invalid else "-success")
            receipt = fixture_directory / (name + ".json")
            command = [sys.executable, str(output / "verify-compatibility-and-restoration.py"),
                       "--manifest", str(corrupt_path if invalid else manifest_path), "--receipt", str(receipt)]
            environment = os.environ.copy()
            environment.pop("PYTHONOPTIMIZE", None)
            if optimized:
                environment["PYTHONOPTIMIZE"] = "1"
            result = subprocess.run(command, cwd=root, env=environment, capture_output=True, text=True, timeout=30)
            expected_exit = 1 if invalid else 0
            if result.returncode != expected_exit:
                raise RuntimeError(f"{name}: unexpected exit {result.returncode}: {result.stdout}{result.stderr}")
            if invalid and (receipt.exists() or "Tested candidate hash mismatch:" not in result.stderr):
                raise RuntimeError(f"{name}: corrupt candidate was not rejected before receipt creation")
            if not invalid and not receipt.exists():
                raise RuntimeError(f"{name}: successful proof did not create a receipt")
            results.append({"case": name, "command": command, "pythonoptimize": "1" if optimized else None,
                            "exit_code": result.returncode, "expected_exit_code": expected_exit,
                            "stdout": result.stdout, "stderr": result.stderr})
after = {str(path): hashlib.sha256(path.read_bytes()).hexdigest() for path in bound_paths}
if before != after:
    raise RuntimeError("Proof fixtures changed tracked consumer files, patch, or manifest")
(output / "verification-script-tests.json").write_text(json.dumps({
    "command": f"python3 {Path(__file__).relative_to(root)}", "cwd": str(root),
    "exit_code": 0, "passed": len(results), "cases": results,
    "consumer_patch_and_manifest_hashes_unchanged": True, "before_sha256": before, "after_sha256": after,
}, indent=2) + "\n")
print(f"PASS: {len(results)} proof-script cases; tracked files, patch and manifest untouched")
