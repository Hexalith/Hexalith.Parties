#!/usr/bin/env python3
"""Validate this bounded receipt without builds, network, credentials or /tmp files."""
import argparse
import collections
import copy
import gzip
import io
import json
from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET

from retain_receipt import digest

HERE = Path(__file__).resolve().parent
MAX_COMPRESSED = 4 * 1024 * 1024
MAX_UNCOMPRESSED = 32 * 1024 * 1024
RESULT_COUNTERS = {'Pass': 'passed', 'Fail': 'failed', 'Skip': 'skipped', 'NotRun': 'not-run'}
REQUIRED_INPUTS = {
    'EXT_PARTIES_GATEWAY_URL', 'EXT_PARTIES_TENANT_A', 'EXT_PARTIES_TENANT_B',
    'EXT_PARTIES_PROVISIONER_CREDENTIAL', 'EXT_PARTIES_IDENTITY_WRITER_CREDENTIAL',
    'EXT_PARTIES_READER_CREDENTIAL', 'EXT_PARTIES_POLICY_ID', 'EXT_PARTIES_RETENTION',
    'EXT_PARTIES_EXPIRY_TRIGGER', 'EXT_PARTIES_CUSTODY_TARGET',
    'EXT_PARTIES_RESTORE_TARGET', 'EXT_PARTIES_FAILURE_INJECTION_TARGET',
}
REST_CLASS = 'Hexalith.Parties.Tests.Search.PartyMemoryRestClientRecoveryTests'
INVENTORY_CLASS = 'Hexalith.Parties.Tests.Search.PartyMemorySourceInventoryTests'
GENERATION_CLASS = 'Hexalith.Parties.Tests.Projections.PartyProcessingGenerationRecoveryTests'
PROJECTION_CLASS = 'Hexalith.Parties.Projections.Tests.Handlers.PartySdkProjectionHandlerTests'
QUERY_CLASS = 'Hexalith.Parties.Tests.Gateway.PartySdkQueryHandlerTests'
UNCONDITIONAL_CONTROL = 'SelectedPackageSendsRefreshRevisionAndAdvertisesItsCompiledCapability'
# This coverage baseline is independent of both editable input matrices.
MANDATORY_CONTROL_METHODS = {
    'published_refresh': {REST_CLASS: (
        'PublishedAcknowledgementAndTokenSurviveRestartWithoutSecondSubmission',
        'LostAcknowledgementRequiresExactExplicitRefreshAdmissionAcrossSerializedRestart',
        'OriginalRefreshBindingSurvivesCanonicalSerializationAndChangedDeliveryContext',
        'SupersededRefreshRedeliveryRegistersAndPublishesStableSuccessor',
        'MissingRevisionFailsBeforeReservationThenCanonicalRedeliverySucceeds',
        'OlderProducerIgnoringExplicitRefreshCannotPublishOrLoseItsUncertainAdmissionAcrossRestart',
    )},
    'inventory_cleanup': {
        REST_CLASS: ('ActualRestRecoveryAcceptsVariablePagesThroughFullInventoryCapacity',),
        INVENTORY_CLASS: (
            'EmptyLocalStateRequiresExplicitCompleteRemoteCoverage',
            'PartialHistoricalPagesAndPendingProducersSurviveRestartUntilTerminalAbsence',
            'UnavailableInventoryNeverSettlesEmptyLocalState',
            'WrongTenantInventoryCannotAuthorizeDeletion',
            'AdmissionAuthorityUsesActualTypedClientAfterConfiguredEndpointChanges',
            'PermanentClosureFromOneProducerDoesNotCertifyAnotherEndpointOrLegacyUnknownAuthority',
            'OutputKnownOrDeletedStillRequiresIndependentWorkflowTerminalEvidence',
            'CanonicalMappingCannotBypassRunningProducerTerminalEvidence',
            'DirectUnitCleanupDoesNotReuseAnotherProducerAbsenceForIdenticalIdentifiers',
        ),
    },
    'processing_history': {
        GENERATION_CLASS: (
            'InterruptedLargeMigrationOrDispatcherRebuildResumesPersistedBucketsBeforeAuthorityCutoverAsync',
            'CompetingAppendOrErasureInvalidatesLargeMigrationOrDispatcherRebuildPromotionAsync',
            'UnqualifiedHistoricalRecoveryRefusesErasureWithEveryPersistedByteUnchangedAsync',
            'RebuildPreservesFailedAuditBeyondCheckpointAndRepairsOnlyWithCompleteUncontendedReplayAsync',
        ),
        PROJECTION_CLASS: ('RetainedProcessingHistoryUsesBoundedBucketsAndPureLargeRebuildAsync',),
        QUERY_CLASS: (
            'ProcessingBucketPagesProtectScopeAndCompatibilityReadsRemainCompleteAsync',
            'InlineProcessingPagesTraverseProtectedFirstContinuationAndTerminalMetadataAsync',
            'ProcessingReadsRejectBrokenCoverageAndMidReadRevisionChangesAsync',
            'InlineProcessingReadsRefuseMisplacedForeignFactsWithoutPayloadAsync',
            'ProcessingFirstPageWithoutRevisionAuthorityReturnsReadModelUnavailableAsync',
        ),
    },
}


class ReceiptError(ValueError):
    """A retained receipt is missing, inconsistent or outside its stated bound."""


def require(condition, message):
    if not condition:
        raise ReceiptError(message)


def load(root):
    names = ['verification.json', 'matrix.json', 'provenance.json',
             'review1-verification.json', 'review1-matrix.json', 'review1-provenance.json']
    documents = {name: json.loads((root / name).read_text()) for name in names}
    receipts = []
    for name in ['verification.json', 'review1-verification.json']:
        receipts.extend(documents[name]['retained_native_xml'].values())
    receipts.extend(documents['review1-verification.json']['retained_build_logs'].values())
    files = {}
    for metadata in receipts:
        name = metadata['retained']
        require(Path(name).name == name and name.endswith('.gz'), 'invalid retained filename')
        with (root / name).open('rb') as stream:
            files[name] = stream.read(MAX_COMPRESSED + 1)
        require(len(files[name]) <= MAX_COMPRESSED, 'compressed bound: ' + name)
    files['custody-live-gate.txt'] = (root / 'custody-live-gate.txt').read_bytes()
    files['retain_receipt.py'] = (root / 'retain_receipt.py').read_bytes()
    return documents, files


def blob(metadata, files, transform):
    name = metadata['retained']
    require(name in files, 'missing retained file: ' + name)
    compressed = files[name]
    require(len(compressed) <= MAX_COMPRESSED, 'compressed bound: ' + name)
    require(digest(compressed) == metadata['sha256'], 'compressed hash: ' + name)
    with gzip.GzipFile(fileobj=io.BytesIO(compressed)) as stream:
        raw = stream.read(MAX_UNCOMPRESSED + 1)
    require(len(raw) <= MAX_UNCOMPRESSED, 'uncompressed bound: ' + name)
    expected = metadata.get('sanitized_sha256', metadata.get('sanitized_xml_sha256'))
    require(digest(raw) == expected, 'uncompressed hash: ' + name)
    require(gzip.compress(raw, mtime=0) == compressed, 'deterministic gzip procedure: ' + name)
    restored = raw.replace(b'${PARTIES_ROOT}', transform['parties_root'].encode())
    restored = restored.replace(b'${SOURCE_WORKSPACE}', transform['source_workspace'].encode())
    original_hash = metadata.get('original_sha256', metadata.get('original_xml_sha256'))
    require(digest(restored) == original_hash, 'path-only transformation: ' + name)
    return raw


def count_xml(element, label):
    tests = element.findall('.//test')
    counts = collections.Counter(test.get('result') for test in tests)
    require(set(counts).issubset(RESULT_COUNTERS), 'unknown test result: ' + label)
    require(int(element.get('total', '-1')) == len(tests), 'declared total: ' + label)
    for result, counter in RESULT_COUNTERS.items():
        require(int(element.get(counter, '-1')) == counts[result], 'declared ' + counter + ': ' + label)
    if 'errors' in element.attrib:
        require(int(element.get('errors')) == len(element.findall('errors/error')), 'declared errors: ' + label)
    return tests, counts


def xml_lane(raw, metadata, declared, command, name):
    tree = ET.fromstring(raw)
    assemblies = tree.findall('assembly')
    require(bool(assemblies), 'empty XML assemblies: ' + name)
    tests = tree.findall('.//test')
    require(bool(tests), 'empty XML cases: ' + name)
    ids = [test.get('id') for test in tests]
    require(all(ids) and len(ids) == len(set(ids)), 'empty or duplicate test IDs: ' + name)
    require(len(tests) == metadata['test_cases'] == declared['executed'], 'manifest total: ' + name)
    for assembly in assemblies:
        count_xml(assembly, name)
        require(int(assembly.get('errors', '-1')) == 0, 'runner errors: ' + name)
        for collection in assembly.findall('collection'):
            count_xml(collection, name)
    counts = dict(collections.Counter(test.get('result') for test in tests))
    require(counts == declared['results'], 'manifest results: ' + name)
    classes = collections.defaultdict(collections.Counter)
    for test in tests:
        require(bool(test.get('type')) and bool(test.get('method')), 'missing method/type: ' + name)
        classes[test.get('type')][test.get('result')] += 1
    require({key: dict(value) for key, value in classes.items()} == declared['classes'], 'manifest classes: ' + name)
    if name == 'source-version-mismatch':
        require(command['exit_code'] == 1 and counts.get('Fail', 0) > 0, 'historical source failure disposition')
    else:
        require(command['exit_code'] == 0, 'successful lane exit: ' + name)
        require(counts == {'Pass': len(tests)}, 'successful lane has failures/skips: ' + name)
    return tests


def validate(documents, files):
    original = documents['verification.json']
    review = documents['review1-verification.json']
    transform = review['transformation']
    require(digest(files['retain_receipt.py']) == transform['procedure_sha256'], 'transformation procedure hash')
    commands = {}
    lanes = {}
    for manifest in [original, review]:
        for command in manifest['commands']:
            require(command['name'] not in commands, 'duplicate command name')
            commands[command['name']] = command
        for filename, metadata in manifest['retained_native_xml'].items():
            name = filename.removesuffix('.xml')
            require(name in manifest['lanes'] and name in commands, 'unbound XML lane: ' + name)
            lanes[name] = xml_lane(blob(metadata, files, transform), metadata, manifest['lanes'][name], commands[name], name)
    for name, metadata in review['retained_build_logs'].items():
        require(name in commands and commands[name]['exit_code'] == 0, 'successful build exit: ' + name)
        text = blob(metadata, files, transform).decode()
        require('Build succeeded.' in text, 'missing build success: ' + name)
        for label in ['warnings', 'errors']:
            pattern = r'(\d+) ' + ('Warning' if label == 'warnings' else 'Error') + r'\(s\)'
            matches = re.findall(pattern, text)
            require(bool(matches), 'missing build counter: ' + name)
            require(int(matches[-1]) == metadata[label] == commands[name][label] == 0, 'nonzero build ' + label + ': ' + name)
    for filename in ['matrix.json', 'review1-matrix.json']:
        matrix = documents[filename]
        require(set(MANDATORY_CONTROL_METHODS) | {'history_expiry'} <= set(matrix), 'missing mandatory matrix row: ' + filename)
        for row_name, classes in MANDATORY_CONTROL_METHODS.items():
            controls = matrix[row_name].get('controls')
            require(isinstance(controls, list) and bool(controls), 'empty mandatory matrix controls: ' + row_name)
            identities = [(control['class'], control['method']) for control in controls]
            required = {(cls, method) for cls, methods in classes.items() for method in methods}
            if filename == 'review1-matrix.json' and row_name == 'published_refresh':
                required.add((REST_CLASS, UNCONDITIONAL_CONTROL))
            require(len(identities) == len(set(identities)) and set(identities) == required,
                    'mandatory matrix control identities: ' + filename + '/' + row_name)
        for row in matrix.values():
            for control in row.get('controls', []):
                require(control['lane'] in lanes, 'unknown matrix lane')
                matches = [test for test in lanes[control['lane']]
                           if test.get('type') == control['class'] and test.get('method') == control['method']]
                require(bool(matches) and len(matches) == control['passed_cases'], 'matrix method count: ' + control['method'])
                require(all(test.get('result') == 'Pass' for test in matches), 'matrix method result: ' + control['method'])
    require('custody-live-gate.txt' in files, 'missing custody refusal')
    gate = files['custody-live-gate.txt'].decode().strip()
    require(gate.startswith('Missing live inputs: '), 'missing custody refusal prefix')
    require(set(re.findall(r'EXT_PARTIES_[A-Z_]+', gate)) == REQUIRED_INPUTS, 'custody missing-input inventory')
    require(commands['custody-live']['exit_code'] == 1 and commands['custody-live']['transport_attempted'] is False,
            'custody must refuse before transport')
    for filename in ['matrix.json', 'review1-matrix.json']:
        expiry = documents[filename]['history_expiry']
        require(expiry['gate'] == gate and expiry['exit_code'] == 1 and expiry['live_qualified'] is False,
                'custody matrix disposition')
    require(review['custody']['live_qualified'] is False and bool(review['custody']['additional_blocker']), 'complete custody lane blocker')
    provenance = documents['provenance.json']
    require(provenance['original_source_failure_hash_limitation']['original_exercised_assembly_hashes_available'] is False,
            'unavailable original source hashes must remain explicit')
    projection = provenance['projection_execution_provenance']
    require(projection['test_assembly']['sha256'] == provenance['projection_test_assembly']['sha256'], 'projection original test identity')
    require(bool(projection['assets']['sha256']) and bool(projection['implementation_outputs']) and bool(projection['sdk_outputs']),
            'missing projection provenance')
    for package in projection['sdk_outputs']:
        require(package['package_assembly']['sha256'] == package['exercised_output']['sha256'], 'projection SDK package/output binding')
    fresh = documents['review1-provenance.json']
    fresh_outputs = {Path(item['path']).name: item['sha256'] for item in fresh['exercised_outputs']}
    for package in provenance['packages']:
        for assembly in package['runtime_assemblies']:
            name = Path(assembly['package_entry']).name
            if name in fresh_outputs:
                require(fresh_outputs[name] == assembly['package_assembly_sha256'], 'fresh SDK package/output binding: ' + name)
    require(fresh['new_control'] == UNCONDITIONAL_CONTROL, 'new unconditional control identity')
    require(any(test.get('type') == REST_CLASS and test.get('method') == UNCONDITIONAL_CONTROL
                and test.get('result') == 'Pass' for test in lanes['review1-rest-focused']), 'new unconditional control missing')
    return {name: len(tests) for name, tests in lanes.items()}


def replace_xml(documents, files, lane, change):
    for manifest in [documents['verification.json'], documents['review1-verification.json']]:
        if lane + '.xml' not in manifest['retained_native_xml']:
            continue
        metadata = manifest['retained_native_xml'][lane + '.xml']
        tree = ET.fromstring(gzip.decompress(files[metadata['retained']]))
        change(tree)
        raw = ET.tostring(tree)
        compressed = gzip.compress(raw, mtime=0)
        files[metadata['retained']] = compressed
        metadata['sha256'] = digest(compressed)
        metadata['sanitized_sha256'] = digest(raw)
        transform = documents['review1-verification.json']['transformation']
        restored = raw.replace(b'${PARTIES_ROOT}', transform['parties_root'].encode())
        restored = restored.replace(b'${SOURCE_WORKSPACE}', transform['source_workspace'].encode())
        metadata['original_sha256'] = digest(restored)
        return
    raise ReceiptError('negative fixture lane missing')


def self_test(documents, files):
    def counter(d, f):
        replace_xml(d, f, 'service-focused', lambda tree: tree.find('assembly').set('total', '1'))
    def duplicate(d, f):
        def change(tree):
            tests = tree.findall('.//test')
            tests[1].set('id', tests[0].get('id'))
        replace_xml(d, f, 'service-focused', change)
    def empty(d, f):
        replace_xml(d, f, 'service-focused', lambda tree: tree.clear())
    def errors(d, f):
        replace_xml(d, f, 'service-focused', lambda tree: tree.find('assembly').set('errors', '1'))
    def exit_code(d, f):
        next(c for c in d['review1-verification.json']['commands'] if c['name'] == 'review1-rest-focused')['exit_code'] = 1
    def matrix(d, f):
        d['review1-matrix.json']['published_refresh']['controls'][-1]['passed_cases'] += 1
    def result(d, f):
        replace_xml(d, f, 'review1-rest-focused', lambda tree: tree.find('.//test').set('result', 'Fail'))
    def matrix_result(d, f):
        metadata = d['verification.json']['retained_native_xml']['source-version-mismatch.xml']
        tests = ET.fromstring(gzip.decompress(f[metadata['retained']])).findall('.//test')
        failed = next(test for test in tests if test.get('result') == 'Fail')
        matches = [test for test in tests if test.get('type') == failed.get('type') and test.get('method') == failed.get('method')]
        d['review1-matrix.json']['published_refresh']['controls'][-1] = {
            'class': failed.get('type'), 'method': failed.get('method'),
            'lane': 'source-version-mismatch', 'passed_cases': len(matches)}
    def build_warning(d, f):
        metadata = d['review1-verification.json']['retained_build_logs']['review1-service-build']
        raw = gzip.decompress(f[metadata['retained']]).replace(b'0 Warning(s)', b'1 Warning(s)')
        compressed = gzip.compress(raw, mtime=0)
        f[metadata['retained']] = compressed
        metadata['sha256'], metadata['sanitized_sha256'] = digest(compressed), digest(raw)
        transform = d['review1-verification.json']['transformation']
        restored = raw.replace(b'${PARTIES_ROOT}', transform['parties_root'].encode())
        restored = restored.replace(b'${SOURCE_WORKSPACE}', transform['source_workspace'].encode())
        metadata['original_sha256'] = digest(restored)
    def hash_failure(d, f):
        name = d['verification.json']['retained_native_xml']['service-focused.xml']['retained']
        f[name] = f[name][:-1] + bytes([f[name][-1] ^ 1])
    def uncompressed_hash(d, f):
        d['review1-verification.json']['retained_native_xml']['review1-rest-focused.xml']['sanitized_sha256'] = '0' * 64
    def refusal(d, f):
        del f['custody-live-gate.txt']
    def missing_matrix_rows(d, f):
        for filename in ['matrix.json', 'review1-matrix.json']:
            for row in MANDATORY_CONTROL_METHODS:
                del d[filename][row]
    def empty_matrix_controls(d, f):
        for filename in ['matrix.json', 'review1-matrix.json']:
            for row in MANDATORY_CONTROL_METHODS:
                d[filename][row]['controls'] = []
    def deleted_matrix_method(d, f):
        method = 'ProcessingReadsRejectBrokenCoverageAndMidReadRevisionChangesAsync'
        for filename in ['matrix.json', 'review1-matrix.json']:
            controls = d[filename]['processing_history']['controls']
            d[filename]['processing_history']['controls'] = [control for control in controls
                if (control['class'], control['method']) != (QUERY_CLASS, method)]
    checks = [('compressed-hash', hash_failure), ('uncompressed-hash', uncompressed_hash),
              ('declared-total-with-rehashed-XML', counter), ('duplicate-ID-with-rehashed-XML', duplicate),
              ('empty-XML-with-rehashed-XML', empty), ('declared-runner-errors', errors),
              ('successful-lane-exit', exit_code), ('exact-matrix-method-count', matrix),
              ('test-result-mutation', result), ('matrix-nonpassing-method', matrix_result),
              ('nonzero-build-warning', build_warning), ('missing-custody-refusal', refusal),
              ('missing-mandatory-matrix-rows', missing_matrix_rows),
              ('empty-mandatory-matrix-controls', empty_matrix_controls),
              ('deleted-processing-method-from-both-matrices', deleted_matrix_method)]
    results = []
    for name, mutate in checks:
        altered_documents, altered_files = copy.deepcopy(documents), dict(files)
        mutate(altered_documents, altered_files)
        try:
            validate(altered_documents, altered_files)
        except (ReceiptError, ET.ParseError, OSError, ValueError) as error:
            results.append({'mutation': name, 'result': 'rejected', 'reason': str(error)})
        else:
            raise ReceiptError('negative mutation was accepted: ' + name)
    return results


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--self-test', action='store_true')
    args = parser.parse_args()
    try:
        documents, files = load(HERE)
        result = {'status': 'pass', 'verified_case_counts': validate(documents, files)}
        if args.self_test:
            result['negative_mutations'] = self_test(documents, files)
        print(json.dumps(result, indent=2))
        return 0
    except (ReceiptError, ET.ParseError, OSError, ValueError, KeyError) as error:
        print('Receipt validation failed: ' + str(error), file=sys.stderr)
        return 1


if __name__ == '__main__':
    sys.exit(main())
