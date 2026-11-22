#!/usr/bin/env python3
"""Run the packaged preparation regression after an unchanged incremental Release solution build."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile


ROOT = Path(__file__).resolve().parents[1]


def fingerprint(path):
    with path.open("rb") as stream:
        return path.stat().st_mtime_ns, hashlib.file_digest(stream, "sha256").hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--artifacts-path", type=Path)
    args = parser.parse_args()
    artifacts = args.artifacts_path or ROOT / "artifacts"
    profile = artifacts / "runtime-profile/Release"
    marker = json.loads((profile / "solution-build-completed.json").read_text())
    before = {path: fingerprint(Path(path)) for path in marker["projects"]}
    dotnet = shutil.which("dotnet")
    if not dotnet:
        raise SystemExit("dotnet is required for real project evaluation")
    with tempfile.TemporaryDirectory(prefix="parties-evaluation-only-") as temporary:
        tools = Path(temporary)
        shim = tools / "dotnet"
        shim.write_text("""#!/usr/bin/env python3
import os, sys
from pathlib import Path
args = sys.argv[1:]
if (len(args) < 4 or args[0] != 'msbuild' or not args[1].endswith('.csproj')
        or not any(arg.startswith('-getProperty:') for arg in args[2:])
        or any(arg != '-nologo' and not arg.startswith(('-p:', '-getProperty:')) for arg in args[2:])):
    Path(os.environ['PARTIES_REFUSED_INVOCATION']).write_text(repr(args))
    sys.exit('Unexpected SDK invocation during build reuse: ' + repr(args))
os.execv(os.environ['PARTIES_EVALUATION_DOTNET'], [os.environ['PARTIES_EVALUATION_DOTNET'], *args])
""")
        shim.chmod(0o755)
        refused = tools / "refused"
        env = dict(os.environ, PATH=temporary + os.pathsep + os.environ["PATH"],
                   PARTIES_EVALUATION_DOTNET=dotnet, PARTIES_REFUSED_INVOCATION=str(refused))
        command = ["bash", "scripts/prepare-aspire-runtime-hosts.sh", "--reuse-build", "true",
                   "--include-memory-qualification", "true"]
        if args.artifacts_path:
            command.extend(("--artifacts-path", str(artifacts)))
        subprocess.run(command, cwd=ROOT, env=env, check=True)
        if refused.exists():
            raise ValueError("Preparation invoked a forbidden SDK operation: " + refused.read_text())

    manifest = json.loads((profile / "prepared.json").read_text())
    projects = manifest["projects"]
    common = (
        "tests/Hexalith.Parties.EventStoreGateway.TestHost", "tests/Hexalith.Parties.EventStoreSample.TestHost",
        "tests/Hexalith.Parties.Runtime.IntegrationTests", "tests/Hexalith.Parties.IntegrationTests",
        "src/Hexalith.Parties", "src/Hexalith.Parties.UI",
        "references/Hexalith.EventStore/src/Hexalith.EventStore.Admin.Server.Host",
        "references/Hexalith.EventStore/samples/Hexalith.EventStore.Sample.BlazorUI",
        "references/Hexalith.Tenants/src/Hexalith.Tenants",
        "references/Hexalith.Memories/src/Hexalith.Memories.Server",
        "references/Hexalith.Memories/src/Hexalith.Memories.DomainService",
    )
    def project_file(directory):
        return str(ROOT / directory / (Path(directory).name + ".csproj"))

    gateway = project_file(common[0])
    catalog_hosts = subprocess.check_output([dotnet, "msbuild", gateway, "-nologo", "-p:Configuration=Release",
                                              "-getProperty:HexalithEventStoreCatalogHostsSupported"], cwd=ROOT, text=True).strip()
    admin = ("tests/Hexalith.Parties.EventStoreAdminUI.TestHost" if catalog_hosts == "true"
             else "references/Hexalith.EventStore/src/Hexalith.EventStore.Admin.UI")
    expected = {project_file(directory): "runtime" for directory in (*common, admin)}
    expected[project_file("src/Hexalith.Parties.EventStoreExtensions")] = "catalog"
    if manifest["completed"] is not True or set(projects) != set(expected):
        raise ValueError("The manifest differs from the required packaged runtime and catalog projects")
    older = 0
    for project, binding in projects.items():
        receipt_path = Path(binding["receipt"])
        data = receipt_path.read_bytes()
        receipt = json.loads(data)
        target = Path(receipt["targetPath"])
        original = before[str(target)]
        if (hashlib.sha256(data).hexdigest() != binding["sha256"] or receipt["project"] != project
                or receipt["role"] != expected[project] or not receipt["completed"] or fingerprint(target) != original):
            raise ValueError("Receipt or binary changed unexpectedly: " + project)
        older += original[0] < marker["buildStartedNs"]
    if not older:
        raise ValueError("Run an unchanged incremental solution build before this regression")
    print(f"Verified {len(projects)} completed project receipts and unchanged binaries, "
          f"including {older} older than the incremental build.")


if __name__ == "__main__":
    main()
