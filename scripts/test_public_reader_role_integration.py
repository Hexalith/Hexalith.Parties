"""Pure assembly-role controls; no CI identity, runtime or qualification claim."""
import copy
import importlib.util
from pathlib import Path
import unittest

_spec = importlib.util.spec_from_file_location("public_reader_role_controls", Path(__file__).with_name("verify-public-package-integration.py"))
public = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(public)


class PublicReaderRoleTests(unittest.TestCase):
    def setUp(self):
        self.source = "c" * 40
        self.run = "0" * 32
        self.contracts = "Hexalith.Parties.Contracts"
        self.extensions = "Hexalith.Parties.EventStoreExtensions"
        package = dict(identity=self.contracts + ", Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
                       informationalVersion="1.0.0+50904cc4ba53c9b0b2c082d5872a0d276be8f5c3",
                       sha256=public.PACKAGE_READERS[self.contracts])
        self.observed = dict(loaded=[copy.deepcopy(package)], copied=[copy.deepcopy(package)])

    def check(self):
        return public.public_reader_roles(self.observed, source=self.source, run_id=self.run)

    def source_dto(self):
        item = self.observed["loaded"][0]
        directory = "/unit/g4-workspaces/" + self.run + "/test-builds/bin/Hexalith.Parties.Platform.IntegrationTests/release/"
        item.update(sha256="4" * 64, informationalVersion="1.0.0+" + self.source,
                    loadedPath=directory + self.contracts + ".dll")
        self.observed["loaded"].append(dict(identity="Hexalith.Parties.Platform.IntegrationTests, Version=1.0.0.0",
            informationalVersion=item["informationalVersion"], loadedPath=directory + "Hexalith.Parties.Platform.IntegrationTests.dll"))
        return item

    def test_genuine_public_consumed_reader_with_actual_clr_version_is_accepted(self):
        self.assertEqual(self.check(), dict(loadedContracts="public-nuget", copiedContracts="public-nuget"))

    def test_absent_unconsumed_extensions_are_accepted(self):
        self.assertEqual(len(self.observed["copied"]), 1)
        self.assertEqual(self.check()["copiedContracts"], "public-nuget")

    def test_present_genuine_extensions_are_accepted(self):
        for field in self.observed:
            self.observed[field].append(dict(identity=self.extensions + ", Version=1.0.0.0", sha256=public.PACKAGE_READERS[self.extensions]))
        self.check()

    def test_wrong_extensions_are_refused_in_each_role(self):
        for field in self.observed:
            with self.subTest(field=field):
                item = dict(identity=self.extensions + ", Version=1.0.0.0", sha256="e" * 64)
                self.observed[field].append(item)
                with self.assertRaises(ValueError): self.check()
                self.observed[field].remove(item)

    def test_source_dto_remains_explicitly_separate_from_public_reader(self):
        self.source_dto()
        self.assertEqual(self.check(), dict(loadedContracts="caller-source-test-dto", copiedContracts="public-nuget"))

    def test_source_dto_cannot_replace_consumed_public_contracts(self):
        self.observed["copied"][0] = copy.deepcopy(self.source_dto())
        with self.assertRaisesRegex(ValueError, "public NuGet reader"): self.check()

    def test_image_cannot_replace_public_or_source_dto(self):
        for field in self.observed:
            with self.subTest(field=field):
                previous = copy.deepcopy(self.observed)
                self.observed[field][0]["sha256"] = public.IMAGE_BODIES[self.contracts]
                with self.assertRaises(ValueError): self.check()
                self.observed = previous

    def test_unknown_public_body_is_refused_in_each_role(self):
        for field in self.observed:
            with self.subTest(field=field):
                self.observed[field][0]["sha256"] = "e" * 64
                with self.assertRaises(ValueError): self.check()
                self.observed[field][0]["sha256"] = public.PACKAGE_READERS[self.contracts]

    def test_package_version_is_not_a_clr_version(self):
        self.observed["copied"][0]["identity"] = self.contracts + ", Version=3.1.0.0, Culture=neutral, PublicKeyToken=null"
        with self.assertRaisesRegex(ValueError, "metadata"): self.check()

    def test_package_hash_does_not_excuse_foreign_metadata(self):
        self.observed["copied"][0]["informationalVersion"] = "1.0.0+" + self.source
        with self.assertRaisesRegex(ValueError, "metadata"): self.check()

    def test_missing_contracts_is_refused_in_each_role(self):
        for field in self.observed:
            with self.subTest(field=field):
                item = self.observed[field].pop()
                with self.assertRaisesRegex(ValueError, "inventory"): self.check()
                self.observed[field].append(item)

    def test_duplicate_contracts_and_extensions_are_refused(self):
        for name in (self.contracts, self.extensions):
            with self.subTest(name=name):
                items = [dict(identity=name, sha256=public.PACKAGE_READERS[name])] * 2
                original = self.observed["copied"]
                self.observed["copied"] = items if name == self.contracts else original + items
                with self.assertRaises(ValueError): self.check()
                self.observed["copied"] = original

    def test_source_dto_requires_current_native_test_revision(self):
        self.source_dto()["informationalVersion"] = "1.0.0+" + "d" * 40
        with self.assertRaisesRegex(ValueError, "provenance"): self.check()

    def test_source_dto_requires_native_test_sibling(self):
        self.source_dto()
        self.observed["loaded"][1]["loadedPath"] = "/foreign/test.dll"
        with self.assertRaisesRegex(ValueError, "source/output"): self.check()

    def test_source_dto_requires_native_test_source_match(self):
        self.source_dto()
        self.observed["loaded"][1]["informationalVersion"] = "1.0.0+" + "d" * 40
        with self.assertRaisesRegex(ValueError, "source/output"): self.check()

    def test_source_dto_requires_this_run_native_release_output(self):
        item = self.source_dto()
        original = item["loadedPath"]
        for path in (original.replace(self.run, "1" * 32), original.replace("/release/", "/debug/"), "/foreign/Hexalith.Parties.Contracts.dll", original.replace("/unit/", "/unit/../")):
            with self.subTest(path=path):
                item["loadedPath"] = path
                with self.assertRaisesRegex(ValueError, "path"): self.check()
        item["loadedPath"] = original

    def test_source_dto_requires_bounded_hash_and_actual_assembly_identity(self):
        item = self.source_dto()
        for digest in ("", "g" * 64, "a" * 65):
            with self.subTest(digest=digest):
                item["sha256"] = digest
                with self.assertRaisesRegex(ValueError, "provenance"): self.check()
        item["sha256"] = "4" * 64
        item["identity"] = self.contracts + ", Version=3.1.0.0, Culture=neutral, PublicKeyToken=null"
        with self.assertRaisesRegex(ValueError, "provenance"): self.check()


if __name__ == "__main__":
    unittest.main()
