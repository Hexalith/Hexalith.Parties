import json
import os
from pathlib import Path
import subprocess
import time

artifacts = Path(os.environ["QUAL_ARTIFACTS"])
receipts = artifacts / "runtime-profile/Release"
if (receipts / "prepared.json").exists():
    manifest = json.loads((receipts / "prepared.json").read_text())
else:
    records = {str(path): json.loads(path.read_text()) for path in receipts.glob("*.json") if path.name != "prepared.json"}
    manifest = {"projects": {item["project"]: {"receipt": path} for path, item in records.items()}, "familyVersions": next(iter(records.values()))["familyVersions"]}
wrapper = "tests/Hexalith.Parties.IntegrationTests/Hexalith.Parties.IntegrationTests.csproj"
properties = ["-p:Configuration=Release", "-p:UseArtifactsOutput=true", "-p:ArtifactsPath=" + str(artifacts),
              "-p:HexalithPartiesRuntimeTargetFramework=net11.0", "-p:CustomAfterMicrosoftCommonProps=" + str(Path.cwd() / "scripts/PartiesHostedRuntime.props"),
              "-p:HexalithUsePublishedSdkForHostedTests=true", "-p:HexalithEventStoreVersion=5.1.0", "-p:CopyLocalLockFileAssemblies=true", "-p:BuildInParallel=false"]
properties += ["-p:" + key + "=" + value for key, value in manifest["familyVersions"].items()]
validator = ["python3", "scripts/validate-aspire-runtime-cohort.py"]
common = ["--configuration", "Release", "--version", "5.1.0", "--target-framework", "net11.0", "--families-json", json.dumps(manifest["familyVersions"])]
started = time.time_ns()
(receipts / "prepared.json").unlink(missing_ok=True)
subprocess.run(["dotnet", "build", wrapper, "-t:Rebuild", "-m:1", "--disable-build-servers", *properties], check=True)
subprocess.run(["dotnet", "build", "src/Hexalith.Parties.EventStoreExtensions/Hexalith.Parties.EventStoreExtensions.csproj", "-t:Rebuild", "-m:1", "--disable-build-servers", *properties], check=True)
project_receipts = []
for project, value in sorted(manifest["projects"].items(), key=lambda item: item[0].endswith(wrapper)):
    record = Path(value["receipt"])
    old = json.loads(record.read_text())
    metadata = subprocess.check_output(["dotnet", "msbuild", project, "-nologo", *properties,
        "-getProperty:TargetPath,ProjectAssetsFile,Configuration,HexalithEventStoreVersion,TargetFramework,HexalithPartiesRuntimeTargetFramework,CustomAfterMicrosoftCommonProps"], text=True)
    extra = []
    if project.endswith(wrapper):
        ui = next(v["receipt"] for k, v in manifest["projects"].items() if k.endswith("tests/Hexalith.Parties.EventStoreAdminUI.TestHost/Hexalith.Parties.EventStoreAdminUI.TestHost.csproj"))
        extra += ["--private-admin-ui-receipt", ui]
    subprocess.run([*validator, "--record", str(record), "--project", project, "--metadata-json", metadata, *common,
        "--build-started-ns", str(started if project.endswith(wrapper) else old["buildStartedNs"]), "--role", old["role"], *extra], check=True)
    project_receipts += ["--receipt", str(record)]
subprocess.run([*validator, "--complete-manifest", str(receipts / "prepared.json"), *common, *project_receipts,
    "--archive-cache", str(receipts / "public-archives"), "--public-package", "Hexalith.Tenants.Contracts=8.0.0",
    "--public-package", "Hexalith.Tenants.Server=8.0.0", "--public-package", "Hexalith.Memories.EventStore=4.0.0",
    "--public-package", "Hexalith.Memories.EventStoreExtensions=4.0.0"], check=True)
