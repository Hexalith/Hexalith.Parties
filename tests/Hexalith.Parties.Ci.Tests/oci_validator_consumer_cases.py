"""Exercise the real Parties image-verification consumer against current shared validator source."""
import importlib.util
import json
import os
import sys
from pathlib import Path
import shutil
from types import SimpleNamespace
import tempfile
import unittest
from unittest import mock
from urllib.parse import unquote, urlsplit

ROOT = Path(__file__).resolve().parents[2]
BUILDS = (Path(sys.argv.pop(1)) if len(sys.argv) > 1 else ROOT / 'references/Hexalith.Builds') / 'Github/publish-containers'


def load(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class ExistingPublicationConsumerTests(unittest.TestCase):
    def test_real_three_argument_consumer_requires_both_canonical_created_labels_across_children(self):
        consumer = load('parties_publication_consumer', ROOT / 'scripts/verify-release-publication.py')
        validator = load('current_shared_validator', BUILDS / 'oci_registry_validator.py')
        fixtures = load('shared_oci_fixtures', BUILDS / 'tests/test_oci_registry_validator.py')
        for mutation in ['none', 'missing-created-label', 'missing-artifact-created-label',
                         'truncated-created-label', 'invalid-created-date', 'different-child-created-pair']:
            with self.subTest(mutation=mutation), tempfile.TemporaryDirectory() as temporary:
                root = Path(temporary)
                capture = root / 'capture'
                capture.mkdir()
                fixtures.build_capture(capture, mutation)
                responses = json.loads((capture / 'responses.json').read_text())

                class CapturedDownloader:
                    def get(self, url, target, purpose, headers, maximum=None):
                        reference = unquote(urlsplit(url).path.rsplit('/', 1)[1])
                        response = responses['tag'] if reference == fixtures.RELEASE_VERSION else responses['objects'][reference]
                        target.parent.mkdir(parents=True, exist_ok=True)
                        shutil.copyfile(capture / response['body'], target)
                        return {**response, 'body': target.name}

                args = SimpleNamespace(registry_url='https://registry.example.test', repository=fixtures.REPOSITORY,
                                       source_sha=fixtures.SOURCE_SHA, version=fixtures.RELEASE_VERSION,
                                       evidence_directory=root / 'evidence')
                evidence = {'images': []}
                with mock.patch.dict(os.environ, {'HEXALITH_ZOT_USERNAME': 'fixture', 'HEXALITH_ZOT_API_KEY': 'fixture'}):
                    if mutation == 'none':
                        consumer.verify_images(args, CapturedDownloader(), validator, evidence)
                        self.assertEqual(len(evidence['images']), 3)
                        for receipt in (root / 'evidence/containers').glob('*/oci-validation.json'):
                            document = json.loads(receipt.read_text())
                            self.assertTrue(all(child['provenance_labels']['org.opencontainers.image.created'] == fixtures.CREATED
                                                and child['provenance_labels']['org.opencontainers.artifact.created'] == fixtures.CREATED
                                                for child in document['children']))
                    else:
                        with self.assertRaises(validator.ValidationError):
                            consumer.verify_images(args, CapturedDownloader(), validator, evidence)


if __name__ == '__main__':
    unittest.main()
