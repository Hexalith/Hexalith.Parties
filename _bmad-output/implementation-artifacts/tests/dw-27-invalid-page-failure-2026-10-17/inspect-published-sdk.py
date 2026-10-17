"""Inventory public packages and selected assembly metadata; do not qualify a restore."""
import datetime
import hashlib
import io
import json
from pathlib import Path
import re
import subprocess
import tempfile
import urllib.request
import zipfile

output = Path(__file__).resolve().parent
root = output.parents[3]
selection_command = [
    "dotnet", "msbuild", "src/Hexalith.Parties/Hexalith.Parties.csproj", "-nologo",
    "-getProperty:HexalithEventStoreVersion",
    "-p:UseHexalithProjectReferences=false", "-p:UseNuGetDeps=true",
]
selection = subprocess.run(selection_command, cwd=root, check=True, capture_output=True, text=True)
selected_version = selection.stdout.strip()
if re.fullmatch(r"\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?", selected_version) is None:
    raise RuntimeError(f"MSBuild did not resolve a single package version: {selected_version!r}")
result = {
    "retrieved_at_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
    "scope": "Package inventory only; this does not establish a coherent restore or gateway qualification.",
    "selection": {
        "command": selection_command,
        "cwd": str(root),
        "exit_code": selection.returncode,
        "output": selection.stdout,
    },
    "selected_version": selected_version,
    "packages": {},
}
with tempfile.TemporaryDirectory(prefix="dw-27-package-inventory-") as temporary:
    for suffix in ("contracts", "server", "domainservice", "gateway"):
        package = "hexalith.eventstore." + suffix
        index_url = f"https://api.nuget.org/v3-flatcontainer/{package}/index.json"
        index = urllib.request.urlopen(index_url, timeout=30).read()
        (output / f"{package}-versions.json").write_bytes(index)
        stable = [version for version in json.loads(index)["versions"] if "-" not in version]
        latest = max(stable, key=lambda version: tuple(map(int, version.split("."))))
        item = {
            "index_url": index_url,
            "index_artifact": f"{package}-versions.json",
            "latest_stable": latest,
            "selected": {},
        }
        result["packages"][package] = item
        for version in dict.fromkeys((selected_version, latest)):
            package_url = f"https://api.nuget.org/v3-flatcontainer/{package}/{version}/{package}.{version}.nupkg"
            data = urllib.request.urlopen(package_url, timeout=30).read()
            archive = zipfile.ZipFile(io.BytesIO(data))
            xml = b"\n".join(archive.read(name) for name in archive.namelist() if name.endswith(".xml"))
            observation = {
                "version": version,
                "downloaded_package_url": package_url,
                "sha256": hashlib.sha256(data).hexdigest(),
                "documentation_observations": {
                    "xml_docs_present": bool(xml),
                    "documents_invalid_page_adapter": b"F:Hexalith.EventStore.Contracts.Queries.QueryAdapterFailureReason.InvalidPage" in xml,
                    "documents_adapter_failure_discriminator": b"P:Hexalith.EventStore.Server.Queries.QueryExecutionFailedException.AdapterFailureReason" in xml,
                },
            }
            if version == selected_version:
                item["selected"] = observation
            if version == latest:
                item["latest"] = observation
            if version == selected_version and suffix in ("contracts", "server"):
                package_path = Path(temporary) / f"{package}.{version}.nupkg"
                package_path.write_bytes(data)
                type_name = (
                    "Hexalith.EventStore.Contracts.Queries.QueryAdapterFailureReason"
                    if suffix == "contracts"
                    else "Hexalith.EventStore.Server.Queries.QueryExecutionFailedException"
                )
                command = ["dnx", "dotnet-inspect", "-y", "--", "member", type_name,
                           "--package", str(package_path), "--json", "--compact"]
                inspected = subprocess.run(command, cwd=root, check=True, capture_output=True)
                metadata = json.loads(inspected.stdout)
                receipt_name = f"selected-{suffix}-dotnet-inspect.json"
                (output / receipt_name).write_bytes(inspected.stdout)
                observation["assembly_metadata_inventory"] = {
                    "command": command,
                    "cwd": str(root),
                    "exit_code": inspected.returncode,
                    "output": receipt_name,
                    "output_sha256": hashlib.sha256(inspected.stdout).hexdigest(),
                    "stderr": inspected.stderr.decode(),
                    "inspected_type": metadata["full_name"],
                    "module_version_id": metadata["layout_details"]["module_version_id"],
                    "listed_members": [member["name"] for member in metadata["members"]],
                }
(output / "published-sdk-inspection.json").write_text(json.dumps(result, indent=2) + "\n")
print(f"Package inventory complete; MSBuild-selected version: {selected_version}")
