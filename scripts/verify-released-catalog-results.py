#!/usr/bin/env python3
"""Validate the retained package identities and xUnit results of the released-catalog runner."""

import argparse
from collections import Counter
import json
from pathlib import Path
import sys
import tempfile
from xml.etree import ElementTree as ET


def theory(method, parameter, values):
    return [f"{method}({parameter}: {value})" for value in values]


# Names relative to each test assembly, as reported by xUnit v3.
REQUIRED = {
    "core": {
        "DW-50": [
            *theory("Gateway.PartySdkQueryHandlerTests.ProcessingBucketPagesProtectScopeAndCompatibilityReadsRemainCompleteAsync",
                    "scenario", [f'"{value}"' for value in ("continue", "long-user", "tenant", "party", "user", "page-size", "revision")]),
            *theory("Projections.PartyProcessingGenerationRecoveryTests.InterruptedLargeMigrationOrDispatcherRebuildResumesPersistedBucketsBeforeAuthorityCutoverAsync",
                    "rebuild", ("False", "True")),
        ],
        "DW-58": [
            "Projections.PartyLegacyReadModelBackfillTests.ActorOnlyHistoryBackfillSurvivesRestartAndPreservesRollbackAndForeignTenantBytes",
            "Projections.PartyLegacyReadModelBackfillTests.ExistingIndexConditionalPolicySurvivesCommitAndFreshVerification",
            *theory("Projections.PartyLegacyReadModelBackfillTests.IncompleteOrUnreadableHistoryRefusesPublicationAndPreservesActorAuthority",
                    "unreadable", ("False", "True")),
            *theory("Projections.PartyLegacyReadModelBackfillTests.CompetingCanonicalWriterAfterPreparationRefusesBackfillWithoutOverwritingAuthority",
                    "shared", ("False", "True")),
        ],
        "DW-82": [
            *theory("Search.PartyMemorySourceInventoryTests.EmptyLocalStateRequiresExplicitCompleteRemoteCoverage",
                    "complete", ("False", "True")),
            "Search.PartyMemorySourceInventoryTests.WrongTenantInventoryCannotAuthorizeDeletion",
            "Search.PartyMemoryRestClientRecoveryTests.ActualRestRecoveryAcceptsVariablePagesThroughFullInventoryCapacity",
        ],
        "DW-168": [
            "Search.PartyMemoryRecoveryTests.AcceptedDeleteRetainsObligationsUntilUnitIsObservablyAbsent",
            "Search.PartyMemorySourceInventoryTests.PartialHistoricalPagesAndPendingProducersSurviveRestartUntilTerminalAbsence",
            "Search.PartyMemoryRecoveryRegressionTests.ConditionalMappingRemovalFailureCannotReportCleanupSuccess",
        ],
    },
    "projections": {
        "DW-50": ["Handlers.PartySdkProjectionHandlerTests.RetainedProcessingHistoryUsesBoundedBucketsAndPureLargeRebuildAsync"],
    },
    "ui": {
        "DW-100": [
            "DataFreshnessIndicatorTests.SelectedDependencyModeUsesOnlyAnAvailableRegisteredCapability",
            "DataFreshnessIndicatorTests.MissingSharedRegistrationRetainsLocalFreshnessMarkup",
            "StatusLiveRegionTests.RegisteredCapabilityRendersTheProducerLiveRegion",
            *theory("OptimisticReconcileTests.RegisteredReconciliationActuallyExecutesTheSelectedSharedAssembly",
                    "shared", ("False", "True")),
            "ProjectionFreshnessCompositionTests.Graph_BuildsUnderValidateScopes_AndResolvesInertWithinScope",
            "ProjectionFreshnessFallbackTests.PollsWhileDisconnected_ObservesReconnectAndResumesAfterAnotherDisconnect",
            "ProjectionTransportPackageTests.SelectedTransportReconnectsRejoinsAndDeliversThroughTheProductionAdapter",
        ],
    },
}

ASSEMBLIES = {
    "core": "Hexalith.Parties.Tests",
    "projections": "Hexalith.Parties.Projections.Tests",
    "ui": "Hexalith.Parties.UI.Tests",
}
PACKAGES = {
    "core": {
        "Hexalith.EventStore.DomainService": "HexalithEventStoreVersion",
        "Hexalith.Memories.Client.Rest": "HexalithMemoriesVersion",
        "Hexalith.Memories.Contracts": "HexalithMemoriesVersion",
    },
    "projections": {"Hexalith.EventStore.DomainService": "HexalithEventStoreVersion"},
    "ui": {
        "Hexalith.FrontComposer.Shell": "HexalithFrontComposerVersion",
        "Hexalith.FrontComposer.Contracts.UI": "HexalithFrontComposerVersion",
        "Hexalith.EventStore.SignalR": "HexalithEventStoreVersion",
    },
}


def verify_packages(directory, lane, catalog):
    deps = json.loads((directory / f"{lane}-dependencies.json").read_text())
    target = deps["targets"][deps["runtimeTarget"]["name"]]
    libraries = deps["libraries"]
    project = ASSEMBLIES[lane]
    project_keys = [key for key in target if key.partition("/")[0] == project]
    if (len(project_keys) != 1 or libraries.get(project_keys[0], {}).get("type") != "project"
            or f"{project}.dll" not in target[project_keys[0]].get("runtime", {})):
        raise ValueError(f"{lane}: expected the runtime manifest for {project}")
    packages = {}
    for name, property_name in PACKAGES[lane].items():
        version = catalog[property_name]
        identity = f"{name}/{version}"
        runtime = target.get(identity, {}).get("runtime", {})
        if (not version or libraries.get(identity, {}).get("type") != "package"
                or not any(Path(asset).name == f"{name}.dll" for asset in runtime)):
            raise ValueError(f"{lane}: expected runtime package {identity}")
        packages[name] = version
    return packages


def verify_counts(node, count, lane):
    for field in ("failed", "skipped", "not-run", "total", "passed"):
        expected = count if field in ("total", "passed") else 0
        if node.get(field) != str(expected):
            raise ValueError(f"{lane}: inconsistent xUnit {node.tag} {field} count")


def verify_report(directory, lane):
    project = ASSEMBLIES[lane]
    assemblies = ET.parse(directory / f"{lane}.xml").getroot().findall("assembly")
    if len(assemblies) != 1 or Path(assemblies[0].get("name", "")).name != f"{project}.dll":
        raise ValueError(f"{lane}: expected one {project} assembly")
    assembly = assemblies[0]
    tests = assembly.findall("collection/test")
    if assembly.get("errors") != "0" or assembly.find("errors/error") is not None:
        raise ValueError(f"{lane}: xUnit reported assembly errors")
    verify_counts(assembly, len(tests), lane)
    for collection in assembly.findall("collection"):
        verify_counts(collection, len(collection.findall("test")), lane)

    classes = Counter()
    cases = Counter()
    for test in tests:
        if test.get("result") != "Pass":
            raise ValueError(f"{lane}: test did not pass: {test.get('name')}")
        classes[test.attrib["type"]] += 1
        # The xUnit v3 XML writer escapes quotes in displayed string arguments.
        display_name = test.attrib["name"].replace(r'\"', '"')
        cases[(test.attrib["type"], test.attrib["method"], display_name)] += 1

    checked = {}
    for item, required in REQUIRED[lane].items():
        for case in required:
            name = f"{project}.{case}"
            class_name, method = name.partition("(")[0].rsplit(".", 1)
            if cases[class_name, method, name] != 1:
                raise ValueError(f"{item} ({lane}): expected exactly one passing result for {name}")
        checked[item] = required
    return {"passed": len(tests), "classes": dict(classes), "deferredItems": checked}


def summarize(directory):
    catalog = json.loads((directory / "catalog.json").read_text())["Properties"]
    summary = {}
    for lane in ASSEMBLIES:
        summary[lane] = verify_report(directory, lane)
        summary[lane]["packages"] = verify_packages(directory, lane, catalog)
    return summary


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path)
    directory = parser.parse_args().directory
    output = directory / "summary.json"
    try:
        output.unlink(missing_ok=True)
        summary = summarize(directory)
        with tempfile.TemporaryDirectory(dir=directory) as scratch:
            candidate = Path(scratch) / output.name
            candidate.write_text(json.dumps(summary, indent=2, sort_keys=True) + "\n", encoding="utf-8")
            candidate.replace(output)
    except (KeyError, OSError, ValueError, ET.ParseError) as error:
        sys.exit(f"Released-catalog verification failed: {error}")
    for lane, result in summary.items():
        print(f"{lane}: {result['passed']} passed ({', '.join(result['deferredItems'])})")


if __name__ == "__main__":
    main()
