"""Read existing snapshot pod metadata and public checksum-result log lines only."""

import datetime
import hashlib
import json
from pathlib import Path
import re
import subprocess


DESTINATION = Path(__file__).resolve().parent
BASE = [
    "kubectl", "--kubeconfig", "/home/administrator/.kube/hexalith-production",
    "--context", "jpiquot@local", "--request-timeout=10s",
    "--server", "https://192.168.1.30:6443",
    "--insecure-skip-tls-verify=false",
]


def now():
    return datetime.datetime.now(datetime.timezone.utc).isoformat()


def call(arguments):
    started = now()
    result = subprocess.run(BASE + arguments, capture_output=True, timeout=20)
    return result, {
        "argv": BASE + arguments,
        "startedAtUtc": started,
        "completedAtUtc": now(),
        "exitCode": result.returncode,
        "stdoutBytes": len(result.stdout),
        "stdoutSha256": hashlib.sha256(result.stdout).hexdigest(),
        "stderrBytes": len(result.stderr),
        "stderrSha256": hashlib.sha256(result.stderr).hexdigest(),
        "retryPerformed": False,
    }


selection = (
    r'{range .items[*]}{.metadata.name}{"\t"}{.metadata.uid}{"\t"}'
    r'{.metadata.resourceVersion}{"\t"}{.status.phase}{"\t"}'
    r'{.spec.nodeName}{"\t"}{.metadata.creationTimestamp}{"\t"}'
    r'{.metadata.ownerReferences[0].name}{"\t"}'
    r'{.metadata.ownerReferences[0].uid}{"\t"}'
    r'{.status.containerStatuses[0].state.terminated.finishedAt}{"\n"}{end}'
)
result, receipt = call(["get", "pods", "-n", "openbao", "-o", "jsonpath=" + selection])
packet = {
    "schema": "EXT-PARTIES-1.existing-snapshot-log-observations.v1",
    "reason": "Collect previously unread checksum-result logs, including the daily snapshot scheduled since the October 30 metadata observation.",
    "transport": {
        "api": "https://192.168.1.30:6443",
        "context": "jpiquot@local",
        "kubeconfigReference": "/home/administrator/.kube/hexalith-production",
        "tlsVerificationExplicitlyRequired": True,
        "authentication": "Existing kubectl context credentials used by the native client; no credential value read or recorded by this collector.",
        "limit": "Authenticated native-client transport observation; no detached signature or complete filesystem inventory asserted.",
    },
    "podListReceipt": receipt,
    "snapshotPods": [],
    "logObservations": [],
    "copyInventoryComplete": False,
    "nodeFilesystemRead": False,
    "podExecs": 0,
    "writes": 0,
}
if result.returncode == 0:
    keys = ["name", "uid", "resourceVersion", "phase", "node", "createdAt", "jobName", "jobUid", "finishedAt"]
    for line in result.stdout.decode().splitlines():
        values = line.split("\t")
        if values[0].startswith("openbao-raft-snapshot-"):
            packet["snapshotPods"].append(dict(zip(keys, values)))
    candidates = sorted(packet["snapshotPods"], key=lambda p: p["createdAt"], reverse=True)
    for pod in candidates[:2]:
        if pod["phase"] != "Succeeded":
            continue
        output, log_receipt = call([
            "logs", "-n", "openbao", pod["name"], "-c", "snapshot",
            "--timestamps=true", "--tail=100", "--limit-bytes=32768",
        ])
        public_lines = []
        if output.returncode == 0:
            for line in output.stdout.decode(errors="replace").splitlines():
                timestamp, separator, body = line.partition(" ")
                if separator and re.fullmatch(r"openbao-\d{8}T\d{6}Z\.snap: OK", body):
                    public_lines.append({"loggedAt": timestamp, "checksumResult": body})
        packet["logObservations"].append({
            "pod": pod,
            "receipt": log_receipt,
            "publicChecksumResults": public_lines,
            "selection": "Only exact dated snapshot checksum-success lines retained; raw logs and other content are not persisted.",
            "snapshotDigestValueObtained": False,
        })
packet["completedAtUtc"] = now()
(DESTINATION / "snapshot-log-observations.json").write_text(json.dumps(packet, indent=4) + "\n")
print(json.dumps({"podListExitCode": result.returncode, "snapshotPods": packet["snapshotPods"], "logs": [{"exitCode": p["receipt"]["exitCode"], "results": p["publicChecksumResults"]} for p in packet["logObservations"]]}, indent=2))
