"""Pure assembly-role controls; no CI identity, runtime or qualification claim."""
import copy
import importlib.util
from pathlib import Path
import unittest

_spec = importlib.util.spec_from_file_location("public_reader_role_controls", Path(__file__).with_name("public_runtime_observation_contract.py"))
public = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(public)


class PublicReaderRoleTests(unittest.TestCase):
    def setUp(self):
        self.source = "c" * 40
        self.output = Path("/product/Release/native-tests")
        self.contracts = "Hexalith.Parties.Contracts"
        self.extensions = "Hexalith.Parties.EventStoreExtensions"
        directory = str(self.output) + "/"
        dto = dict(identity=self.contracts + ", Version=1.0.0.0, Culture=neutral, PublicKeyToken=null", sha256="4" * 64,
                   informationalVersion="1.0.0+" + self.source, loadedPath=directory + self.contracts + ".dll")
        test = dict(identity="Hexalith.Parties.Platform.IntegrationTests, Version=1.0.0.0", informationalVersion=dto["informationalVersion"],
                    loadedPath=directory + "Hexalith.Parties.Platform.IntegrationTests.dll")
        # The composition's consumed Contracts is measured, not pinned to a published package body.
        composition = dict(identity=self.contracts + ", Version=1.0.0.0, Culture=neutral, PublicKeyToken=null", sha256="6" * 64,
                           informationalVersion="1.0.0+" + "9" * 40)
        self.observed = dict(loaded=[dto, test], copied=[composition])

    def check(self):
        return public.public_reader_roles(self.observed, source=self.source, native_output=self.output)

    def dto(self):
        return self.observed["loaded"][0]

    def test_caller_source_dto_and_composition_contracts_keep_separate_roles(self):
        self.assertEqual(self.check(), dict(loadedContracts="caller-source-test-dto", copiedContracts="composition-output"))

    def test_absent_or_present_unconsumed_extensions_are_accepted(self):
        self.check()
        for field in self.observed:
            self.observed[field].append(dict(identity=self.extensions + ", Version=1.0.0.0", sha256="e" * 64))
        self.check()

    def test_missing_or_duplicate_contracts_are_refused_in_each_role(self):
        for field, message in (("loaded", "caller-source DTO inventory"), ("copied", "composition Contracts inventory")):
            for mutation in ("missing", "duplicate"):
                with self.subTest(field=field, mutation=mutation):
                    original = copy.deepcopy(self.observed)
                    item = next(entry for entry in self.observed[field] if entry["identity"].startswith(self.contracts + ","))
                    if mutation == "missing": self.observed[field].remove(item)
                    else: self.observed[field].append(copy.deepcopy(item))
                    with self.assertRaisesRegex(ValueError, message): self.check()
                    self.observed = original

    def test_composition_contracts_require_a_measured_body(self):
        for digest in ("", "g" * 64, "a" * 65, None):
            with self.subTest(digest=digest):
                self.observed["copied"][0]["sha256"] = digest
                with self.assertRaisesRegex(ValueError, "composition Contracts inventory"): self.check()

    def test_source_dto_requires_current_native_test_revision(self):
        self.dto()["informationalVersion"] = "1.0.0+" + "d" * 40
        with self.assertRaisesRegex(ValueError, "provenance"): self.check()

    def test_source_dto_requires_native_test_sibling(self):
        self.observed["loaded"][1]["loadedPath"] = "/foreign/test.dll"
        with self.assertRaisesRegex(ValueError, "source/output"): self.check()

    def test_source_dto_requires_native_test_source_match(self):
        self.observed["loaded"][1]["informationalVersion"] = "1.0.0+" + "d" * 40
        with self.assertRaisesRegex(ValueError, "source/output"): self.check()

    def test_source_dto_requires_this_run_native_release_output(self):
        item = self.dto()
        original = item["loadedPath"]
        for path in (original.replace("/product/", "/foreign/"), original.replace("/Release/", "/Debug/"), "/foreign/Hexalith.Parties.Contracts.dll", original.replace("/product/", "/product/../")):
            with self.subTest(path=path):
                item["loadedPath"] = path
                with self.assertRaisesRegex(ValueError, "path"): self.check()
        item["loadedPath"] = original

    def test_source_dto_requires_bounded_hash_and_actual_assembly_identity(self):
        item = self.dto()
        for digest in ("", "g" * 64, "a" * 65):
            with self.subTest(digest=digest):
                item["sha256"] = digest
                with self.assertRaisesRegex(ValueError, "provenance"): self.check()
        item["sha256"] = "4" * 64
        item["identity"] = self.contracts + ", Version=3.1.0.0, Culture=neutral, PublicKeyToken=null"
        with self.assertRaisesRegex(ValueError, "provenance"): self.check()


if __name__ == "__main__":
    unittest.main()
