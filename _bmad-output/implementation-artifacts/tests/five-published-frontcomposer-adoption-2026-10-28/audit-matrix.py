"""Bind every approved matrix row to executed passing cases and retained hashes."""
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import xml.etree.ElementTree as ET

HERE = Path(__file__).resolve().parent
subprocess.run([sys.executable, str(HERE / 'verify-evidence.py')], check=True)
inputs = ['ui-full.xml', 'admin-full.xml', 'consumer-full.xml', 'browser-cases.json']
cases = {}
for filename in inputs[:-1]:
    for case in ET.parse(HERE / filename).getroot().findall('.//test'):
        cases[case.attrib['name']] = case.attrib['result'] == 'Pass'
for case in json.loads((HERE / inputs[-1]).read_text())['cases']:
    cases[case['title']] = case['ok'] and case['results'] == [dict(case['results'][0], status='passed', retry=0)]


def covered(fragment, count=1):
    matches = [name for name, passed in cases.items() if fragment in name and passed]
    if len(matches) != count:
        raise RuntimeError(f'{fragment}: expected {count} passing cases, found {len(matches)}')
    return matches


rows = {
    'Route focus': covered('route-heading announcement leaves skip links forward reachable')
        + covered('client-side route focus restores forward skip access'),
    'Compact layout': covered('compact and resized layouts expose only mounted skip targets'),
    'Freshness': covered('DataFreshnessIndicatorTests.Every_freshness_status_', 6)
        + covered('DataFreshnessIndicatorTests.SelectedDependencyModeUsesOnlyAnAvailableRegisteredCapability')
        + covered('DataFreshnessIndicatorTests.MissingSharedRegistrationRetainsLocalFreshnessMarkup')
        + covered('StatusLiveRegionTests.RegisteredCapabilityRendersTheProducerLiveRegion')
        + covered('StatusLiveRegionTests.MissingSharedRegistrationRetainsLocalLiveRegionSemantics', 8)
        + covered('StatusLiveRegionTests.AssertiveKinds_render_', 4)
        + covered('StatusLiveRegionTests.PoliteKinds_render_', 4)
        + covered('StatusLiveRegionTests.SignInRequired_renders_no_live_region'),
    'Export': covered('SharedDownloadCandidatePreservesAdminBytesAndSafeCallerFilename')
        + covered('SharedDownloadCandidatePreservesConsumerBytesFilenameAndRepeatedDownload')
        + covered('RegisteredSharedDownloadCancellationDrainsInteropWithoutLateFeedback')
        + covered('MyPrivacyPage_JsFailure_MapsToTransientAlertAndKeepsDownloadVisible')
        + covered('direct GDPR route for an erased party keeps export and records available without PII')
        + covered('bound Consumer exports own data through the self-scoped path')
        + covered('bound Consumer navigation cancels an active shared download without late feedback or browser resources')
        + covered('bound Consumer sees retry guidance when the JSON download helper fails'),
    'Typed confirmation': covered('SharedTypedConfirmationCandidateRetainsOrdinalDomainGuardAndOneSubmit')
        + covered('PartiesAdminPortal_RequestErasure_RollbackCancellationRestoresTrigger', 2)
        + covered('direct GDPR route opens operations destination without browser-visible command/query calls or overflow'),
    'Reconciliation': covered('RegisteredFeedbackWaitsForReadinessAndAcceptanceWithEarlyConfirmation', 2),
}
record = {
    'result': 'pass',
    'rows': rows,
    'inputs_sha256': {name: hashlib.sha256((HERE / name).read_bytes()).hexdigest() for name in inputs},
    'limitations': ['Package qualification only; fixed shared-reconciler adoption and source launch remain open.'],
}
(HERE / 'primary-matrix-audit.json').write_text(json.dumps(record, indent=2) + '\n')
print(f'All {len(rows)} approved matrix rows have executed passing coverage.')
