#!/usr/bin/env python3
"""Execute the selected SDK publisher and refuse unsupported app/sidecar secret pairing."""

import argparse
import copy
import hashlib
import json
import os
from pathlib import Path
import secrets
import signal
import subprocess
import tempfile
import unittest


def token_references(resource):
    environment = resource.get("env", {})
    return {key: environment.get(key) for key in ("APP_API_TOKEN", "DAPR_API_TOKEN")}


def require_pairing(resources):
    expected = {"APP_API_TOKEN": "{memories-app-api-token.value}", "DAPR_API_TOKEN": "{memories-dapr-api-token.value}"}
    for variable, expression in expected.items():
        if resources["memories"]["env"].get(variable) != expression:
            raise ValueError("app-secret-reference-mismatch")
        parameter = expression[1:-7]
        if resources[parameter]["inputs"]["value"].get("secret") is not True:
            raise ValueError("nonsecret-parameter")
    sidecar = resources["memories-dapr"]
    if "env" not in sidecar:
        raise ValueError("sidecar-environment-not-emitted")
    for variable, expression in expected.items():
        if sidecar["env"].get(variable) != expression:
            raise ValueError("sidecar-secret-reference-mismatch")


class PublicationConformance(unittest.TestCase):
    records = []

    def qualify(self, target):
        with tempfile.TemporaryDirectory(prefix="parties-sidecar-publish-") as temporary:
            private = Path(temporary)
            os.chmod(private, 0o700)
            artifact = private / "manifest.json"
            environment = dict(os.environ, PartiesUi__OidcClientSecret=secrets.token_hex(24))
            args = ["dotnet", str(OPTIONS.apphost.resolve()), "--operation=publish", "--publisher=manifest",
                    "--output-path=" + str(artifact), "--PUBLISH_TARGET=" + target]
            process = subprocess.Popen(args, stdout=subprocess.PIPE, stderr=subprocess.PIPE, env=environment, start_new_session=True)
            try:
                process.communicate(timeout=90)
                self.assertEqual(process.returncode, 0, "Selected SDK publication failed; no credential-bearing output retained")
            finally:
                if process.poll() is None:
                    os.killpg(process.pid, signal.SIGKILL)
                    process.communicate(timeout=10)
            raw = artifact.read_bytes()
            resources = json.loads(raw)["resources"]
            sidecar = resources["memories-dapr"]
            supported = True
            blocker = None
            try:
                require_pairing(resources)
            except ValueError as refusal:
                # Precisely recognize the selected publisher's actual omission; every other
                # failure remains a failing control rather than an assumed upstream blocker.
                self.assertEqual(str(refusal), "sidecar-environment-not-emitted")
                self.assertEqual(sidecar["type"], "dapr.v0")
                self.assertNotIn("env", sidecar)
                supported = False
                blocker = str(refusal)
            # These copies qualify only the checker; they never qualify publisher delivery.
            paired = copy.deepcopy(resources)
            paired["memories-dapr"]["env"] = {key: resources["memories"]["env"][key] for key in ("APP_API_TOKEN", "DAPR_API_TOKEN")}
            require_pairing(paired)
            for key in ("APP_API_TOKEN", "DAPR_API_TOKEN"):
                mutated = copy.deepcopy(paired)
                mutated["memories-dapr"]["env"][key] = "literal-refusal-control"
                with self.assertRaisesRegex(ValueError, "sidecar-secret-reference-mismatch"):
                    require_pairing(mutated)
            self.records.append({"target": target, "publisherExitCode": process.returncode,
                                 "actualManifestSha256": hashlib.sha256(raw).hexdigest(),
                                 "appSecretReferences": token_references(resources["memories"]),
                                 "sidecarSecretReferences": token_references(sidecar),
                                 "sidecarType": sidecar["type"], "sidecarKeys": sorted(sidecar),
                                 "pairedSecretEmissionQualified": supported, "blocker": blocker,
                                 "conformance": "passed", "privateRawArtifactsRemoved": True})

    def test_docker(self):
        self.qualify("docker")

    def test_kubernetes(self):
        self.qualify("k8s")

    def test_aca(self):
        self.qualify("aca")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--apphost", type=Path, required=True)
    parser.add_argument("--receipt", type=Path, required=True)
    OPTIONS = parser.parse_args()
    result = unittest.TextTestRunner(verbosity=2).run(unittest.defaultTestLoader.loadTestsFromTestCase(PublicationConformance))
    dependencies = json.loads(OPTIONS.apphost.with_suffix(".deps.json").read_text())["libraries"]
    receipt = {"executed": result.testsRun, "passed": result.wasSuccessful(),
               "apphostSha256": hashlib.sha256(OPTIONS.apphost.read_bytes()).hexdigest(),
               "effectivePackages": [name for name in dependencies if name.startswith(("Aspire.Hosting/", "CommunityToolkit.Aspire.Hosting.Dapr/", "Hexalith.EventStore.Aspire/"))],
               "scope": "Actual selected SDK manifest emission; mutated copies qualify checker only; no deployment or delivery claim",
               "records": PublicationConformance.records}
    OPTIONS.receipt.write_text(json.dumps(receipt, indent=2) + "\n")
    raise SystemExit(0 if result.wasSuccessful() and result.testsRun == 3 else 1)
