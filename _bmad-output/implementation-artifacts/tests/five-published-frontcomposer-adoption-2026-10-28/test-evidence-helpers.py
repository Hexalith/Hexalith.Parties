"""Focused positive/negative controls for evidence capture and verification."""
import copy
import importlib.util
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch
import xml.etree.ElementTree as ET

HERE = Path(__file__).resolve().parent


def load(name, filename):
    spec = importlib.util.spec_from_file_location(name, HERE / filename)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


verify = load('adoption_verify', 'verify-evidence.py')
collect = load('adoption_collect', 'collect-evidence.py')


class EvidenceHelperTests(unittest.TestCase):
    def test_retained_positive_results_have_actual_cases(self):
        commands = json.loads((HERE / 'implementation-verification.json').read_text())['commands']
        verify.validate_final_commands(commands)
        verify.validate_xunit(HERE / 'ui-full.xml', 445)
        verify.validate_browser_xml(HERE / 'browser-ui.xml', json.loads((HERE / 'browser-cases.json').read_text()))

    def test_truncated_theory_names_use_unique_record_ids(self):
        verify.validate_xunit(HERE / 'primary-client-tests.xml', 345)

    def test_duplicate_record_id_is_rejected(self):
        tree = ET.parse(HERE / 'ui-full.xml')
        cases = tree.getroot().findall('.//test')
        cases[1].set('id', cases[0].get('id'))
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'duplicate-id.xml'
            tree.write(path)
            with self.assertRaisesRegex(RuntimeError, 'duplicate case identities'):
                verify.validate_xunit(path, 445)

    def test_nonzero_final_test_or_client_build_exit_is_rejected(self):
        original = json.loads((HERE / 'implementation-verification.json').read_text())['commands']
        for name in ['ui-full', 'admin-full', 'consumer-full', 'primary-client-tests', 'primary-client-build']:
            with self.subTest(name=name):
                commands = copy.deepcopy(original)
                commands[name + '.json']['exit_code'] = 1
                with self.assertRaisesRegex(RuntimeError, 'command failed'):
                    verify.validate_final_commands(commands)

    def test_missing_fresh_xml_capture_is_rejected(self):
        commands = copy.deepcopy(json.loads((HERE / 'implementation-verification.json').read_text())['commands'])
        del commands['ui-full.json']['xunit_xml']
        with self.assertRaisesRegex(RuntimeError, 'fresh result XML'):
            verify.validate_final_commands(commands)

    def test_claimed_xunit_totals_without_actual_cases_are_rejected(self):
        tree = ET.parse(HERE / 'ui-full.xml')
        assembly = tree.getroot().find('assembly')
        for collection in assembly.findall('collection'):
            assembly.remove(collection)
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'empty.xml'
            tree.write(path)
            with self.assertRaisesRegex(RuntimeError, 'missing actual cases'):
                verify.validate_xunit(path, 445)

    def test_xunit_runner_error_is_rejected(self):
        tree = ET.parse(HERE / 'ui-full.xml')
        ET.SubElement(tree.getroot(), 'error').text = 'runner failed after executing cases'
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'errors.xml'
            tree.write(path)
            with self.assertRaisesRegex(RuntimeError, 'runner errors'):
                verify.validate_xunit(path, 445)

    def test_native_browser_empty_identity_mismatch_or_error_is_rejected(self):
        browser = json.loads((HERE / 'browser-cases.json').read_text())
        for mutation in ['empty', 'identity', 'error']:
            with self.subTest(mutation=mutation), tempfile.TemporaryDirectory() as directory:
                tree = ET.parse(HERE / 'browser-ui.xml')
                if mutation == 'empty':
                    for suite in tree.getroot().findall('testsuite'):
                        tree.getroot().remove(suite)
                elif mutation == 'identity':
                    tree.getroot().find('.//testcase').set('name', 'unexecuted replacement case')
                else:
                    tree.getroot().set('errors', '1')
                path = Path(directory) / 'native.xml'
                tree.write(path)
                with self.assertRaises(RuntimeError):
                    verify.validate_browser_xml(path, browser)

    def test_input_inventory_includes_head_diff_and_new_helpers(self):
        with patch.object(collect, 'git', side_effect=['tests/staged.cs\ntests/unstaged.cs', 'tests/new.cs']) as git:
            names = collect.implementation_input_names()
        git.assert_any_call('diff', '--name-only', 'HEAD')
        self.assertTrue({'tests/staged.cs', 'tests/unstaged.cs', 'tests/new.cs',
                         'src/Hexalith.Parties.AdminPortal/Components/PartyGdprOperationsPanel.razor',
                         'tests/e2e/helpers/consumer-download-cancellation.ts'}.issubset(names))

    def test_runner_archives_prior_xml_and_refuses_missing_fresh_output(self):
        self.run_capture_control(produce=False)

    def test_runner_attaches_only_newly_written_xml(self):
        self.run_capture_control(produce=True)

    def run_capture_control(self, produce):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            here = root / '_bmad-output/implementation-artifacts/tests/evidence-control'
            here.mkdir(parents=True)
            shutil.copyfile(HERE / 'run-check.py', here / 'run-check.py')
            artifact = here / 'result.xml'
            artifact.write_text('<old-result />')
            code = "from pathlib import Path; import sys; Path(sys.argv[-1]).write_text('<new-result />')" if produce else 'pass'
            result = subprocess.run([sys.executable, str(here / 'run-check.py'), 'fixture', '--', sys.executable,
                                     '-c', code, '-result-xml', str(artifact)], cwd=root, capture_output=True, text=True)
            self.assertEqual(result.returncode, 0 if produce else 1, result.stderr)
            record = json.loads((here / 'fixture.json').read_text())
            archived = list((here / 'historical-results').glob('*/result.xml'))
            self.assertEqual(len(archived), 1)
            self.assertEqual(archived[0].read_text(), '<old-result />')
            if produce:
                self.assertEqual(artifact.read_text(), '<new-result />')
                self.assertEqual(record['xunit_xml']['path'], 'result.xml')
            else:
                self.assertFalse(artifact.exists())
                self.assertNotIn('xunit_xml', record)
                self.assertIn('missing_result_xml', record)


if __name__ == '__main__':
    unittest.main()
