"""Verify exact executed acceptance cases in each lane and immutable evidence inputs."""

from collections import Counter
import hashlib
import json
from pathlib import Path
import xml.etree.ElementTree as ET


HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
UI = "Hexalith.Parties.UI.Tests.OptimisticReconcileTests."
PRODUCER = "Hexalith.FrontComposer.Shell.Tests.Services.FcOptimisticReconcilerTests."
BACKFILL = "Hexalith.Parties.Tests.Projections.PartyLegacyReadModelBackfillTests."


def variants(prefix, method, parameter):
    return {prefix + method + f"({parameter}: {value})" for value in ("False", "True")}


UI_ROWS = {
    "Early confirmation": {
        UI + "DelayedRegistration_RecoversOnlyEarlyConfirmation_AndDrainsTeardown",
        UI + "ConnectedOldCurrentWithoutConfirmation_DoesNotReconcileQueuedCommand",
        *variants(UI, "EarlyConfirmationCannotReconcileRejectedOrCancelledCommand", "cancel"),
        *variants(UI, "SubscriptionAcquisitionFailure_RevertsOnceOrSilentlyAbandonsBeforeDispatch", "cancel"),
    },
    "Repeated disconnect": {
        UI + "RepeatedDisconnects_ResumePolling_AndCompletionReleasesAllWork",
        UI + "CancellationDuringDelayedRegistration_DrainsLateRegistrationWithoutFeedback",
        UI + "ConcurrentConfirmations_SerializeReads_AndIgnoreLateCurrentAfterCancellation",
        UI + "DuplicateOrLateConfirm_RunsReconcileExactlyOnce",
        UI + "ReconcileReadThrowsTransiently_IsRetriedOnTheNextConfirm_NoRevert",
        UI + "CleanupCancellationFailureStillAwaitsUnsubscription",
        *variants(UI, "ExecuteWaitsForDelayedUnsubscription", "cancel"),
        *variants(UI, "OfflineConnectionFailure_RetainsAcceptedCommandPollingFallback", "transportTimeout"),
        "Hexalith.Parties.UI.Tests.ProjectionFreshnessFallbackTests.PollsWhileDisconnected_ObservesReconnectAndResumesAfterAnotherDisconnect",
    },
}
REQUIRED_BY_LANE = {
    "final-retained-ui-tests": UI_ROWS,
    "final-mapped-ui-tests": UI_ROWS,
    "final-producer-tests": {
        "Early confirmation": {
            PRODUCER + "ConfirmationDuringCommandIsRecoveredByInitialRead",
            PRODUCER + "DelayedTransportReadinessRecoversWithoutAnotherConfirmation",
            PRODUCER + "ConnectedDirectPollFallbackCannotReadOldCurrent",
            *variants(PRODUCER, "BufferedConfirmationCannotReconcileRejectedOrCancelledCommand", "cancel"),
            *variants(PRODUCER, "SubscriptionAcquisitionFailureRevertsOnceOrSilentlyAbandonsBeforeDispatch", "cancel"),
        },
        "Repeated disconnect": {
            PRODUCER + "FallbackObservesRepeatedDisconnectionsAndIsDrainedAtCompletion",
            PRODUCER + "CancellationDuringReadSuppressesLateFeedbackAndDisposes",
            PRODUCER + "SimultaneousAndLateConfirmationsCannotApplyTwice",
            PRODUCER + "ThrowingDisposalStillDrainsFallbackAndActiveRead",
            PRODUCER + "CancellationCallbackFailureStillAwaitsRemovalAndFallback",
            *variants(PRODUCER, "AsynchronousSubscriptionDisposalIsAwaitedExactlyOnce", "cancel"),
        },
    },
    "final-backfill-recovery-tests": {
        "Backfill": {
            BACKFILL + "ActorOnlyHistoryBackfillSurvivesRestartAndPreservesRollbackAndForeignTenantBytes",
            BACKFILL + "ExistingIndexConditionalPolicySurvivesCommitAndFreshVerification",
        },
        "Unsafe backfill": {
            *variants(BACKFILL, "IncompleteOrUnreadableHistoryRefusesPublicationAndPreservesActorAuthority", "unreadable"),
            *variants(BACKFILL, "CompetingCanonicalWriterAfterPreparationRefusesBackfillWithoutOverwritingAuthority", "shared"),
        },
    },
}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def checked_file(root, name, expected):
    path = root / name
    require(path.is_file(), f"Missing required input: {name}")
    payload = path.read_bytes()
    require(hashlib.sha256(payload).hexdigest() == expected, f"SHA-256 mismatch: {name}")
    return payload


def audit(here=HERE, root=ROOT):
    receipt = json.loads((here / "implementation-verification.json").read_text())
    lanes = {}
    command_names = set()
    for command in receipt["commands"]:
        name = command["name"]
        require(name not in command_names, f"Duplicate command: {name}")
        command_names.add(name)
        require(command["exit_code"] == 0, f"Failed command: {name}")
        checked_file(here, command["log"], command["log_sha256"])
        if "xml" in command:
            payload = checked_file(here, command["xml"], command["xml_sha256"])
            tests = list(ET.fromstring(payload).iter("test"))
            cases = {item.attrib["name"]: item.attrib["result"] for item in tests}
            recorded = {item["name"]: item["result"] for item in command["test_results"]}
            require(len(tests) == len(cases) == len(command["test_results"]), f"Duplicate cases: {name}")
            require(cases == recorded, f"Receipt/XML cases differ: {name}")
            lanes[name] = cases
    for name, expected in receipt["source_sha256"].items():
        checked_file(root, name, expected)

    matrix = {}
    for lane, rows in REQUIRED_BY_LANE.items():
        require(lane in lanes, f"Missing required lane: {lane}")
        for row, names in rows.items():
            for name in sorted(names):
                require(lanes[lane].get(name) == "Pass", f"Missing passing variant in {lane}: {name}")
                entry = matrix.setdefault(row, {"result": "Pass", "executed_cases": []})
                entry["executed_cases"].append({"lane": lane, "test": name})

    dispositions = json.loads((here / "disposition-verification.json").read_text())
    require(dispositions["checks"] and all(result == "Pass" for result in dispositions["checks"].values()),
            "Disposition checks did not all pass")
    require(bool(dispositions["input_sha256"]), "Missing disposition input hashes")
    catalog = "references/Hexalith.Builds/Props/Directory.Packages.props"
    historical = dispositions["historical_inputs"]
    require(set(historical) == {catalog}, "Only the historical selected Builds catalog may use a snapshot")
    require(catalog in dispositions["input_sha256"], "Missing historical catalog input hash")
    for name, expected in dispositions["input_sha256"].items():
        if name == catalog:
            snapshot = historical[name]
            require(snapshot["sha256"] == expected, "Historical catalog receipt hashes disagree")
            require(snapshot["snapshot"] == "historical-inputs/" + expected + ".catalog",
                    "Historical catalog must use its hash-named archive")
            checked_file(here, snapshot["snapshot"], expected)
        else:
            checked_file(root, name, expected)
    helper = json.loads((here / "helper-verification.json").read_text())
    require(len(helper) == 2 and all(command["exit_code"] == 0 for command in helper), "Helper commands did not pass")
    for command in helper:
        checked_file(here, command["log"], command["sha256"])
    helper_xml = checked_file(here, helper[1]["xml"], helper[1]["xml_sha256"])
    tests = list(ET.fromstring(helper_xml).iter("test"))
    require(len(tests) == 11 and all(test.attrib["result"] == "Pass" for test in tests), "Missing passing helper cases")
    blocker = receipt["source_resolver_blocker"]
    require(blocker["exit_code"] != 0, "Resolver blocker must retain its failed outcome")
    checked_file(here, blocker["log"], blocker["sha256"])
    generation = json.loads(checked_file(here, "retained-generation-receipt.json",
                                        receipt["mapping"]["generation_receipt_sha256"]))
    for item in generation["archived_inputs"].values():
        checked_file(here, item["path"], item["sha256"])
    require(generation["archived_inputs"]["generator"]["sha256"] == generation["generator_sha256"],
            "Archived materializer differs from the executed generator")
    for name, expected in generation["generated_sha256"].items():
        checked_file(here, name.replace("source-mapping/", "retained-source-mapping/", 1), expected)
    matrix["Stale reports"] = {"result": "Pass", "checks": dispositions["checks"], "helper_cases": len(tests),
                               "historical_catalog": historical[catalog], "qualification_limits": dispositions["limits"]}
    return {"matrix": matrix, "lane_results": {name: dict(Counter(cases.values())) for name, cases in lanes.items()},
            "source_and_receipt_hashes": "Pass",
            "limits": "Executed focused source/package/fallback controls. The named legacy-SDK skip "
                      "does not cover a required row. No deployed migration or source-resolver acceptance."}


if __name__ == "__main__":
    print(json.dumps(audit(), indent=2))
