#!/usr/bin/env python3
"""Validate exact executed native selections and actual sidecar publication receipts.

The earlier collector is archived as historical-collector.py.gz because its
rejected Memories proposal and private input layout are not current qualification.
"""

import re
import xml.etree.ElementTree as ET


def validate_test_evidence(raw, expected_cases, expected_failures=()):
    expected = set(expected_cases)
    failures = set(expected_failures)
    if not expected or len(expected) != len(expected_cases) or not failures <= expected:
        raise ValueError("Invalid admitted test-case inventory")
    document = ET.fromstring(raw)
    assemblies = document.findall("assembly")
    if len(assemblies) != 1:
        raise ValueError("Exactly one executed assembly is required")
    assembly = assemblies[0]
    cases = assembly.findall(".//test")
    names = [case.attrib["name"] for case in cases]
    if len(names) != len(expected) or len(set(names)) != len(names) or set(names) != expected:
        raise ValueError("Executed case identities/counts differ from admitted selection")
    if any(case.attrib.get("result") not in ("Pass", "Fail") for case in cases):
        raise ValueError("Skipped or unexecuted targeted case")
    for case in cases:
        identity = case.attrib.get("type", "") + "." + case.attrib.get("method", "")
        if case.attrib["name"] != identity and not case.attrib["name"].startswith(identity + "("):
            raise ValueError("Case identity does not match executed type/method")
    actual_failures = {case.attrib["name"] for case in cases if case.attrib["result"] == "Fail"}
    if actual_failures != failures:
        raise ValueError("Unexpected failing or missing regression cases")
    counts = {"total": len(cases), "passed": len(cases) - len(failures), "failed": len(failures), "skipped": 0}
    if any(int(assembly.attrib.get(key, "-1")) != value for key, value in counts.items()):
        raise ValueError("Assembly totals do not match executed cases")
    if int(assembly.attrib.get("errors", "0")) or int(assembly.attrib.get("not-run", "0")) or assembly.findall(".//error"):
        raise ValueError("Assembly reports errors or unexecuted tests")
    return {**{key: str(value) for key, value in counts.items()}, "time": assembly.attrib["time"], "failures": sorted(actual_failures)}


def validate_sidecar_receipt(receipt):
    if type(receipt.get("executed")) is not int or receipt["executed"] != 3 or receipt.get("passed") is not True:
        raise ValueError("Three successful publisher conformance executions are required")
    records = receipt.get("records", [])
    if len(records) != 3 or {row.get("target") for row in records} != {"docker", "k8s", "aca"}:
        raise ValueError("Three distinct target records are required")
    if not re.fullmatch(r"[0-9a-f]{64}", receipt.get("apphostSha256", "")):
        raise ValueError("Selected publisher hash is required")
    if set(receipt.get("effectivePackages", [])) != {"Aspire.Hosting/13.6.4", "CommunityToolkit.Aspire.Hosting.Dapr/13.6.2-beta.959", "Hexalith.EventStore.Aspire/4.2.0"}:
        raise ValueError("Publisher cohort differs from selected packages")
    expected = {"APP_API_TOKEN": "{memories-app-api-token.value}", "DAPR_API_TOKEN": "{memories-dapr-api-token.value}"}
    for row in records:
        if type(row.get("publisherExitCode")) is not int or row["publisherExitCode"] != 0 or row.get("conformance") != "passed":
            raise ValueError("Publisher execution/conformance failed")
        if not re.fullmatch(r"[0-9a-f]{64}", row.get("actualManifestSha256", "")) or row.get("privateRawArtifactsRemoved") is not True:
            raise ValueError("Actual artifact hash and cleanup evidence are required")
        if row.get("appSecretReferences") != expected:
            raise ValueError("App secret references are invalid")
        if row.get("pairedSecretEmissionQualified") is False:
            if row.get("blocker") != "sidecar-environment-not-emitted" or row.get("sidecarType") != "dapr.v0" or sorted(row.get("sidecarKeys", [])) != ["dapr", "type"] or row.get("sidecarSecretReferences") not in (None, dict.fromkeys(expected)):
                raise ValueError("Unsupported output does not match the observed refusal")
        elif row.get("pairedSecretEmissionQualified") is True:
            if row.get("blocker") is not None or row.get("sidecarSecretReferences") != expected or "env" not in row.get("sidecarKeys", []):
                raise ValueError("Positive paired sidecar emission lacks evidence")
        else:
            raise ValueError("Missing sidecar qualification disposition")
    return all(row["pairedSecretEmissionQualified"] for row in records)


if __name__ == "__main__":
    raise SystemExit("Import these receipt checks; the obsolete proposal collector is archived separately.")
