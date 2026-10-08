"""Capture the current consumer graph and complete root source/test input hashes."""

import datetime
import hashlib
import json
from pathlib import Path
import subprocess
import sys


root = Path.cwd()
output = Path(sys.argv[1]).resolve()
output.mkdir(parents=True, exist_ok=True)


def run(*command):
    return subprocess.check_output(command, cwd=root, text=True)


def write(name, value):
    (output / name).write_text(json.dumps(value, indent=2) + "\n")


graph = json.loads(run(
    "dotnet", "msbuild", "src/Hexalith.Parties/Hexalith.Parties.csproj", "-nologo",
    "-getProperty:UseNuGetDeps,UseHexalithProjectReferences,HexalithEventStoreFromSource,HexalithEventStoreVersion",
    "-getItem:PackageReference,ProjectReference",
))
write("dependency-mode.json", graph)
version = graph["Properties"]["HexalithEventStoreVersion"]
assets = {}
for directory in ["src", "tests", "samples"]:
    for file in sorted((root / directory).glob("**/obj/project.assets.json")):
        libraries = json.loads(file.read_text())["libraries"]
        family = sorted(name for name, metadata in libraries.items()
                        if name.startswith("Hexalith.EventStore.") and metadata["type"] == "package")
        if family:
            if any(not name.endswith("/" + version) for name in family):
                raise ValueError(f"{file}: mixed EventStore family {family}")
            assets[str(file.relative_to(root))] = family
write("resolved-family.json", assets)
paths = set(run("git", "ls-files", "--cached", "--others", "--exclude-standard", "--",
                "src", "tests", "samples").splitlines())
paths.update(["Directory.Packages.props", "Directory.Build.props", "Directory.Build.targets",
              "global.json", "Hexalith.Parties.slnx"])
inputs = {path: hashlib.sha256((root / path).read_bytes()).hexdigest()
          for path in sorted(paths) if (root / path).is_file()}
dependencies = {}
for line in run("git", "ls-tree", "HEAD", "references/").splitlines():
    mode, kind, identity, path = line.replace("\t", " ", 1).split(" ", 3)
    if mode == "160000":
        dependencies[path] = {
            "committed": identity,
            "checkout": run("git", "-C", path, "rev-parse", "HEAD").strip(),
            "status": run("git", "-C", path, "status", "--porcelain"),
        }
write("source-inputs.json", {
    "observedAtUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
    "partiesHead": run("git", "rev-parse", "HEAD").strip(),
    "sourceStatus": run("git", "status", "--porcelain", "--", "src", "tests", "samples",
                        "Directory.Packages.props", "Directory.Build.props", "Directory.Build.targets",
                        "global.json", "Hexalith.Parties.slnx"),
    "files": inputs,
    "rootDependencies": dependencies,
})
print(f"Captured EventStore {version}, {len(assets)} asset graphs, and {len(inputs)} source/build/test inputs.")
