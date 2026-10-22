"""Reproduce DW-17 qualification in owned temporary directories (Linux x64)."""

import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import tarfile
import tempfile
import urllib.request


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def tree_digest(directory):
    entries = []
    for path in sorted(directory.rglob("*")):
        if path.is_file():
            entries.append([path.relative_to(directory).as_posix(), digest(path)])
    return hashlib.sha256(json.dumps(entries, separators=(",", ":")).encode()).hexdigest()


def require(condition, message):
    if not condition:
        raise RuntimeError(message)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project-root", type=Path, required=True)
    parser.add_argument("--wrong-node", type=Path, required=True)
    parser.add_argument("--wrong-npm-cli", type=Path, required=True)
    args = parser.parse_args()
    repo = args.project_root.resolve()
    output = Path(__file__).resolve().parent
    receipt = {"status": "running", "node": "24.22.1", "npm": "11.19.1", "checks": []}
    receipt_path = output / "node-reproducible-qualification.json"
    receipt_path.write_text(json.dumps(receipt, indent=2) + "\n")
    try:
        with tempfile.TemporaryDirectory(prefix="parties-node-qualification-") as scratch:
            work = Path(scratch)

            def clean(value):
                return str(value).replace(str(work), "<scratch>").replace(str(repo), "<project-root>").replace(str(Path.home()), "<user-home>")

            filename = "node-v24.22.1-linux-x64.tar.xz"
            base = "https://nodejs.org/dist/v24.22.1/"
            sums = urllib.request.urlopen(base + "SHASUMS256.txt", timeout=60).read().decode()
            expected = next(line.split()[0] for line in sums.splitlines() if line.split()[-1] == filename)
            archive = work / filename
            urllib.request.urlretrieve(base + filename, archive)
            actual = digest(archive)
            require(actual == expected, "Official archive checksum mismatch")
            receipt["archive"] = {"filename": filename, "url": base + filename, "checksum_url": base + "SHASUMS256.txt", "expected_sha256": expected, "observed_sha256": actual, "comparison": "hashlib.sha256(archive.read_bytes()).hexdigest() == official SHASUMS256 entry"}
            with tarfile.open(archive) as bundle:
                bundle.extractall(work, filter="data")
            distribution = work / "node-v24.22.1-linux-x64"
            node = distribution / "bin/node"
            npm = distribution / "lib/node_modules/npm/bin/npm-cli.js"
            env = dict(os.environ, PATH=str(distribution / "bin") + os.pathsep + os.environ.get("PATH", ""))
            require(subprocess.check_output([str(node), "--version"], text=True).strip() == "v24.22.1", "Unexpected Node version")
            require(subprocess.check_output([str(node), str(npm), "--version"], text=True).strip() == "11.19.1", "Unexpected npm version")

            def run(label, command, cwd, expected_exit=0):
                result = subprocess.run([str(value) for value in command], cwd=cwd, env=env, capture_output=True, text=True, timeout=300)
                (output / (label + ".txt")).write_text(clean(result.stdout + result.stderr))
                record = {"check": label, "command": [clean(value) for value in command], "cwd": clean(cwd), "exit_code": result.returncode, "output": label + ".txt"}
                receipt["checks"].append(record)
                require(result.returncode == expected_exit, label + " returned an unexpected exit code")
                return result, record

            for project, relative in (("root", "."), ("browser", "tests/e2e")):
                source = repo / relative
                target = work / project
                target.mkdir()
                for name in ("package.json", "package-lock.json", ".npmrc"):
                    shutil.copyfile(source / name, target / name)
                receipt[project + "_inputs"] = {name: digest(target / name) for name in ("package.json", "package-lock.json", ".npmrc")}
                modules = target / "node_modules"
                modules.mkdir()
                (modules / "qualification-sentinel").write_text("preserve before engine rejection\n")
                for kind, command in (("wrong-node", [args.wrong_node, npm, "ci", "--ignore-scripts"]), ("wrong-npm", [node, args.wrong_npm_cli, "ci", "--ignore-scripts"])):
                    before = {"lock_sha256": digest(target / "package-lock.json"), "modules_tree_sha256": tree_digest(modules)}
                    result, record = run(project + "-" + kind + "-reproduced", command, target, expected_exit=1)
                    after = {"lock_sha256": digest(target / "package-lock.json"), "modules_tree_sha256": tree_digest(modules)}
                    record.update(before=before, after=after)
                    require("EBADENGINE" in result.stderr, "Rejection was not an engine failure")
                    require(before == after, "Engine rejection changed dependency contents")
                before_lock = digest(target / "package-lock.json")
                command = [node, npm, "ci"] + (["--ignore-scripts"] if project == "root" else [])
                _, record = run(project + "-locked-install-reproduced", command, target)
                record.update(lock_sha256_before=before_lock, lock_sha256_after=digest(target / "package-lock.json"))
                require(record["lock_sha256_before"] == record["lock_sha256_after"], "Locked install changed the lock")
                if project == "root":
                    run("root-signatures-pinned", [node, npm, "audit", "signatures"], target)
                    run("root-semantic-release-version-pinned", [node, npm, "exec", "--no", "--", "semantic-release", "--version"], target)
                else:
                    for item in source.iterdir():
                        if item.name in {"node_modules", "package.json", "package-lock.json", ".npmrc", "test-results", "playwright-report"}:
                            continue
                        if item.is_dir():
                            shutil.copytree(item, target / item.name)
                        elif item.is_file():
                            shutil.copyfile(item, target / item.name)
                    run("browser-config-pinned", [node, npm, "run", "test:config"], target)
                    run("browser-typecheck-pinned", [node, npm, "run", "typecheck"], target)
            receipt["status"] = "passed"
    except Exception as error:
        receipt["status"] = "failed"
        receipt["error"] = type(error).__name__ + ": " + str(error)
        raise
    finally:
        receipt_path.write_text(json.dumps(receipt, indent=2) + "\n")


if __name__ == "__main__":
    main()
