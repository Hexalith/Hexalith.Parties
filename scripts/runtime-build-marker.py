#!/usr/bin/env python3
"""Bind reusable runtime outputs to the successful solution build that visited them."""
import argparse
from contextlib import contextmanager
import json
import os
import re
from pathlib import Path
import runpy
import shutil
import tempfile
import time

ROOT = Path(__file__).resolve().parents[1]
COHORT = runpy.run_path(str(ROOT / "scripts/validate-aspire-runtime-cohort.py"))


def file_set_digest(paths):
    return COHORT["file_set_digest"](COHORT["file_hashes"](paths))


def source_files(solution, artifacts):
    """Include new, modified and deleted files in the standalone build's source trees."""
    files = set()
    references = solution.parent / "references"
    owners = [solution.parent]
    if references.is_dir():
        owners.extend(path for path in references.glob("Hexalith.*") if path.is_dir())
        files.update(path for path in references.iterdir() if path.is_file())
    for owner in owners:
        files.update(path for path in owner.iterdir() if path.is_file() and
                     (path.suffix.lower() in {".cs", ".csproj", ".slnx", ".props", ".targets", ".rsp", ".razor"}
                      or path.name.lower() in {".editorconfig", "global.json", "nuget.config", "packages.lock.json"}))
        for name in ("src", "tests", "test", "samples", "scripts", "Props", "module", "build"):
            for directory, children, names in os.walk(owner / name):
                base = Path(directory)
                children[:] = [child for child in children if child not in
                               {"bin", "obj", "artifacts", ".artifacts", ".git", "node_modules",
                                "__pycache__", "TestResults", "test-results", "playwright-report"}
                               and base / child != artifacts]
                files.update(base / name for name in names
                             if not name.endswith(".csproj.lscache") and (base / name).is_file())
    return files


def write_atomic(path, value):
    with tempfile.NamedTemporaryFile(mode="w", encoding="utf-8", dir=path.parent, delete=False) as stream:
        stream.write(value + "\n")
    Path(stream.name).replace(path)


def read_json(path):
    def unique_fields(pairs):
        result = dict(pairs)
        if len(result) != len(pairs):
            raise ValueError("duplicate build marker field")
        return result
    if path.is_symlink() or not path.is_file() or path.stat().st_size > 2_000_000:
        raise ValueError("missing or invalid build marker")
    return json.loads(path.read_text(), object_pairs_hook=unique_fields)


@contextmanager
def marker_lock(directory):
    with (directory / "solution-build.lock").open("a+b") as lock:
        if os.name == "nt":
            import msvcrt
            lock.seek(0)
            msvcrt.locking(lock.fileno(), msvcrt.LK_LOCK, 1)
        else:
            import fcntl
            fcntl.flock(lock.fileno(), fcntl.LOCK_EX)
        yield


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("phase", choices=("begin", "complete", "verify"))
    parser.add_argument("--configuration", required=True)
    parser.add_argument("--artifacts-path", default="")
    parser.add_argument("--solution", type=Path, default=ROOT / "Hexalith.Parties.Standalone.slnx")
    parser.add_argument("--attempt")
    parser.add_argument("--expected-start", type=int)
    parser.add_argument("--publish-manifest", type=Path)
    args = parser.parse_args()
    solution = args.solution.resolve(strict=True)
    artifacts = Path(args.artifacts_path) if args.artifacts_path else solution.parent / "artifacts"
    if not artifacts.is_absolute():
        raise ValueError("the build artifact root must be absolute")
    directory = artifacts / "runtime-profile" / args.configuration
    directory.mkdir(parents=True, exist_ok=True)
    with marker_lock(directory):
        process_marker(args, solution, artifacts, directory)


def process_marker(args, solution, artifacts, directory):
    started = directory / "solution-build-started-ns"
    pending = directory / "solution-build-pending.json"
    completed = directory / "solution-build-completed.json"
    if args.phase != "verify" and not re.fullmatch(r"[A-Za-z0-9_-]{1,64}", args.attempt or ""):
        raise ValueError("a solution build attempt identifier is required")
    if args.phase == "begin":
        (directory / "attempts" / args.attempt).mkdir(parents=True, exist_ok=False)
        (directory / "prepared.json").unlink(missing_ok=True)
        completed.unlink(missing_ok=True)
        started.unlink(missing_ok=True)
        begin = time.time_ns()
        marker = dict(configuration=args.configuration, buildStartedNs=begin, attempt=args.attempt,
                      solution=str(solution), sourceSha256=file_set_digest(source_files(solution, artifacts)))
        write_atomic(pending, json.dumps(marker))
        write_atomic(started, str(begin))
        return

    if started.is_symlink() or not started.is_file() or started.stat().st_size > 32:
        raise ValueError("missing or invalid build start")
    begin = int(started.read_text())
    marker = read_json(pending if args.phase == "complete" else completed)
    fields = {"configuration", "buildStartedNs", "attempt", "solution", "sourceSha256"}
    if args.phase == "verify":
        fields.update(("completedNs", "projects", "additionalInputs", "additionalSha256"))
    if (not isinstance(marker, dict) or set(marker) != fields or marker["configuration"] != args.configuration
            or marker["solution"] != str(solution) or type(marker["buildStartedNs"]) is not int
            or not 0 < begin == marker["buildStartedNs"] <= time.time_ns()):
        raise ValueError("mismatched build marker")
    if args.expected_start is not None and begin != args.expected_start:
        raise ValueError("another solution build started during preparation")
    if args.phase == "complete" and marker["attempt"] != args.attempt:
        raise ValueError("another solution build superseded this attempt")
    if args.phase == "verify" and (type(marker["completedNs"]) is not int
            or not begin <= marker["completedNs"] <= time.time_ns() or not isinstance(marker["projects"], dict)):
        raise ValueError("invalid build completion")
    files = source_files(solution, artifacts)
    if marker["sourceSha256"] != file_set_digest(files):
        raise ValueError("solution build inputs changed")

    if args.phase == "complete":
        projects = {}
        inputs = {}
        attempt_directory = directory / "attempts" / args.attempt
        for entry in attempt_directory.glob("*.output"):
            project, target, assets, *copy_rows = entry.read_text(encoding="utf-8-sig").splitlines()
            target = Path(target).resolve()
            if target.with_suffix(".deps.json").is_file():
                mappings = [row.rsplit("|", 1) for row in copy_rows]
                candidates = {}
                for source, destination in mappings:
                    path = Path(source)
                    checksum = COHORT["digest"](path)
                    if inputs.setdefault(path, checksum) != checksum:
                        raise ValueError(f"copy source changed during the build: {path}")
                    candidates.setdefault(destination, set()).add(checksum)
                copies = sorted(candidates)
                hashes = COHORT["output_hashes"](Path(project), target, Path(assets), copies, COHORT["runtime_inputs"](target))
                # Referenced hosts can contribute distinct appsettings files to the same destination.
                # Bind every candidate and the particular bytes actually deployed by MSBuild.
                for destination, checksums in candidates.items():
                    if hashes[str(target.parent / destination)] not in checksums:
                        raise ValueError(f"deployed copy differs from its build inputs: {target.parent / destination}")
                projects[str(target)] = dict(copies=copies, sha256=COHORT["file_set_digest"](hashes))
            for row in entry.with_suffix(".inputs").read_text(encoding="utf-8-sig").splitlines():
                checksum, filename = row.split(" ", 1)
                path = Path(filename)
                if inputs.setdefault(path, checksum.lower()) != checksum.lower():
                    raise ValueError(f"build input changed between projects: {path}")
        for path, expected in inputs.items():
            if COHORT["digest"](path) != expected:
                raise ValueError(f"build input changed after compilation: {path}")
        additional = set(inputs) - files
        marker.update(completedNs=time.time_ns(), projects=projects,
                      additionalInputs=sorted(map(str, additional)),
                      additionalSha256=file_set_digest(additional))
        shutil.rmtree(attempt_directory)
        write_atomic(completed, json.dumps(marker))
    else:
        if not marker["projects"] or not isinstance(marker["additionalInputs"], list):
            raise ValueError("incomplete solution build inventory")
        if marker["additionalSha256"] != file_set_digest({Path(name) for name in marker["additionalInputs"]}):
            raise ValueError("additional solution build inputs changed")
        if args.publish_manifest:
            args.publish_manifest.replace(directory / "prepared.json")
        print(begin)


if __name__ == "__main__":
    try:
        main()
    except (OSError, ValueError, KeyError) as error:
        raise SystemExit(f"Runtime build verification failed: {error}")
