"""Capture a fresh public NuGet observation in a new temporary directory."""

import concurrent.futures
import datetime
import hashlib
import io
import json
from pathlib import Path
import tempfile
import urllib.request
import xml.etree.ElementTree as ET
import zipfile


def download(url):
    """Read public artifact bytes without installing a dependency."""
    with urllib.request.urlopen(url, timeout=30) as response:
        return response.read()


def probe(package_id):
    """Retain the original index response and summarize its versions."""
    url = f"https://api.nuget.org/v3-flatcontainer/{package_id}/index.json"
    data = download(url)
    versions = json.loads(data)["versions"]
    return data, {
        "id": package_id,
        "index_url": url,
        "observed_at_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
        "index_sha256": hashlib.sha256(data).hexdigest(),
        "version_count": len(versions),
        "latest": versions[-1],
        "prereleases": [version for version in versions if "-" in version],
        "newer_than_3_115_0": [
            version for version in versions
            if "-" not in version
            and tuple(map(int, version.split("."))) > (3, 115, 0)
        ],
    }


def main():
    """Write new receipts without overwriting this historical observation."""
    output = Path(tempfile.mkdtemp(prefix="parties-eventstore-package-probe-"))
    indexes = output / "nuget-indexes"
    indexes.mkdir()
    identities = [
        f"hexalith.eventstore.{name}" for name in (
            "domainservice", "servicedefaults", "client", "contracts", "server",
            "testing", "aspire", "signalr", "gateway",
        )
    ]
    with concurrent.futures.ThreadPoolExecutor(max_workers=9) as pool:
        results = list(pool.map(probe, identities))
    for data, summary in results:
        (indexes / f"{summary['id']}.json").write_bytes(data)
    url = (
        "https://api.nuget.org/v3-flatcontainer/hexalith.eventstore.domainservice/"
        "3.115.0/hexalith.eventstore.domainservice.3.115.0.nupkg"
    )
    package_bytes = download(url)
    with zipfile.ZipFile(io.BytesIO(package_bytes)) as package:
        metadata = ET.fromstring(package.read("Hexalith.EventStore.DomainService.nuspec"))
        repository = metadata.find(".//{*}repository")
        dll_path = "lib/net10.0/Hexalith.EventStore.DomainService.dll"
        assembly = package.read(dll_path)
    receipt = {
        "observed_at_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
        "packages": [summary for _, summary in results],
        "domainservice_3_115_0": {
            "download_url": url,
            "nupkg_sha256": hashlib.sha256(package_bytes).hexdigest(),
            "repository_commit": repository.get("commit"),
            "dll": dll_path,
            "dll_sha256": hashlib.sha256(assembly).hexdigest(),
            "contains_require_event_store_sidecar_channel_metadata_name":
                b"RequireEventStoreSidecarChannel" in assembly,
        },
        "upstream_api_introducing_commit": "c4d5455a3b79ca1ba0113a286cf2432b2cace7fb",
    }
    (output / "package-probe.json").write_bytes(
        (json.dumps(receipt, indent=2) + "\n").replace("\n", "\r\n").encode()
    )
    print(output)


if __name__ == "__main__":
    main()
