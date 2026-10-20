"""Regression coverage for strict BMAD configuration merging and resolution."""

from __future__ import annotations

import copy
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

SCRIPTS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SCRIPTS))

from config_utils import (
    ConfigError,
    load_central_config,
    load_customization,
    merge_layers,
    structural_merge,
)


class StructuralMergeTests(unittest.TestCase):
    def test_rejects_duplicates_within_either_input(self):
        for field in ("code", "id"):
            for duplicate_side in ("base", "override"):
                with self.subTest(field=field, side=duplicate_side):
                    duplicate = [{field: "same", "name": "one"}, {field: "same"}]
                    unique = [{field: "same", "name": "replacement"}]
                    base, override = (
                        (duplicate, unique)
                        if duplicate_side == "base"
                        else (unique, duplicate)
                    )
                    with self.assertRaisesRegex(ConfigError, f"duplicate.*`{field}`"):
                        structural_merge(base, override)

    def test_rejects_duplicates_in_single_layer_and_new_fields(self):
        for field in ("code", "id"):
            for layers in (
                [{"entries": [{field: "same"}, {field: "same"}]}],
                [{"other": True}, {"entries": [{field: "same"}, {field: "same"}]}],
            ):
                with self.subTest(field=field, layers=layers):
                    with self.assertRaises(ConfigError):
                        merge_layers(iter(layers))

    def test_rejects_nested_duplicates_and_type_replacements(self):
        duplicate = [{"id": "same"}, {"id": "same"}]
        for base, override in (
            ({}, {"workflow": {"layers": duplicate}}),
            ({"entries": "old"}, {"entries": duplicate}),
            (None, duplicate),
            ({}, {"entries": [{"code": "outer", "children": duplicate}]}),
            ({}, {"entries": [[{"id": "same"}, {"id": "same"}]]}),
            ({"entries": duplicate}, {"entries": "replacement"}),
        ):
            with self.subTest(base=base, override=override):
                with self.assertRaises(ConfigError):
                    structural_merge(base, override)

    def test_cross_layer_replacement_keeps_position_and_replaces_whole_record(self):
        for field in ("code", "id"):
            with self.subTest(field=field):
                base = [{field: "first", "old": True}, {field: "second"}]
                override = [{field: "first", "new": True}, {field: "third"}]
                self.assertEqual(
                    structural_merge(base, override),
                    [
                        {field: "first", "new": True},
                        {field: "second"},
                        {field: "third"},
                    ],
                )

    def test_code_precedes_secondary_id(self):
        entries = [{"code": "a", "id": "same"}, {"code": "b", "id": "same"}]
        self.assertEqual(structural_merge([], entries), entries)

    def test_common_id_rejects_duplicates_when_only_one_input_has_codes(self):
        duplicate = [{"code": "a", "id": "same"}, {"code": "b", "id": "same"}]
        unique = [{"id": "other"}]
        for base, override in ((duplicate, unique), (unique, duplicate)):
            with self.subTest(base=base, override=override):
                with self.assertRaisesRegex(ConfigError, "duplicate.*`id`"):
                    structural_merge(base, override)

    def test_each_input_rejects_duplicate_codes_even_when_common_identity_is_id(self):
        duplicate = [{"code": "same", "id": "a"}, {"code": "same", "id": "b"}]
        unique = [{"id": "other"}]
        for base, override in ((duplicate, unique), (unique, duplicate)):
            with self.subTest(base=base, override=override):
                with self.assertRaisesRegex(ConfigError, "duplicate.*`code`"):
                    structural_merge(base, override)

    def test_unkeyed_arrays_append_without_deduplicating(self):
        for base, override in (
            (["same"], ["same"]),
            ([{"name": "same"}], [{"name": "same"}]),
            ([{"code": "same"}], ["scalar"]),
            ([{"code": "same"}], [{"id": "same"}]),
            ([], []),
        ):
            with self.subTest(base=base, override=override):
                self.assertEqual(structural_merge(base, override), base + override)

    def test_invalid_selected_identifiers_still_fail(self):
        for field in ("code", "id"):
            for identifier in ("", None, 1, True):
                with self.subTest(field=field, identifier=identifier):
                    with self.assertRaises(ConfigError):
                        structural_merge([], [{field: identifier}])

    def test_merge_does_not_mutate_inputs_on_success_or_failure(self):
        for override in (
            {"workflow": {"entries": [{"id": "a", "new": True}]}},
            {"workflow": {"entries": [{"id": "a"}, {"id": "a"}]}},
        ):
            with self.subTest(override=override):
                base = {"workflow": {"entries": [{"id": "a", "old": True}]}}
                before = copy.deepcopy((base, override))
                try:
                    structural_merge(base, override)
                except ConfigError:
                    pass
                self.assertEqual((base, override), before)


class ConfigResolverTests(unittest.TestCase):
    def setUp(self):
        directory = tempfile.TemporaryDirectory()
        self.addCleanup(directory.cleanup)
        self.root = Path(directory.name)
        self.skill = self.root / "skills" / "example"
        self.central_paths = [
            self.root / "_bmad" / "config.toml",
            self.root / "_bmad" / "config.user.toml",
            self.root / "_bmad" / "custom" / "config.toml",
            self.root / "_bmad" / "custom" / "config.user.toml",
        ]
        self.custom_paths = [
            self.skill / "customize.toml",
            self.root / "_bmad" / "custom" / "example.toml",
            self.root / "_bmad" / "custom" / "example.user.toml",
        ]
        for path in self.central_paths + self.custom_paths:
            self.write(path, "")

    @staticmethod
    def write(path, content):
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(content, encoding="utf-8")

    def test_each_central_layer_rejects_duplicate_codes(self):
        for path in self.central_paths:
            with self.subTest(layer=path.name, directory=path.parent.name):
                self.write(path, '[[members]]\ncode = "same"\n[[members]]\ncode = "same"\n')
                with self.assertRaises(ConfigError):
                    load_central_config(self.root)
                self.write(path, "")

    def test_each_customization_layer_rejects_duplicate_ids(self):
        for path in self.custom_paths:
            with self.subTest(layer=path.name):
                self.write(path, '[[workflow.layers]]\nid = "same"\n[[workflow.layers]]\nid = "same"\n')
                with self.assertRaises(ConfigError):
                    load_customization(self.root, self.skill)
                self.write(path, "")

    def run_resolver(self, name):
        command = [sys.executable, "-B", str(SCRIPTS / name), "--project-root", str(self.root)]
        if name in ("resolve_customization.py", "render_skill.py"):
            command.extend(["--skill", str(self.skill)])
        return subprocess.run(command, capture_output=True, text=True, check=False, timeout=30)

    def test_merge_identity_switch_errors_identify_the_contributing_files(self):
        duplicate = (
            '[[entries]]\ncode = "a"\nid = "same"\n'
            '[[entries]]\ncode = "b"\nid = "same"\n'
        )
        unique = '[[entries]]\nid = "other"\n'
        for paths, loader in (
            (self.central_paths, lambda: load_central_config(self.root)),
            (self.custom_paths, lambda: load_customization(self.root, self.skill)),
        ):
            for side in ("base", "override"):
                with self.subTest(loader=paths[0].name, side=side):
                    self.write(paths[0], duplicate if side == "base" else unique)
                    self.write(paths[-1], unique if side == "base" else duplicate)
                    with self.assertRaises(ConfigError) as raised:
                        loader()
                    message = str(raised.exception)
                    for fragment in (
                        str(paths[0]), str(paths[-1]), "$.entries", f"({side})", "items 0 and 1"
                    ):
                        self.assertIn(fragment, message)
                    self.write(paths[0], "")
                    self.write(paths[-1], "")

    def test_resolvers_report_duplicates_without_json_or_tracebacks(self):
        for name, path in (
            ("resolve_config.py", self.central_paths[-1]),
            ("resolve_customization.py", self.custom_paths[-1]),
        ):
            with self.subTest(resolver=name):
                self.write(path, '[[entries]]\nid = "same"\n[[entries]]\nid = "same"\n')
                result = self.run_resolver(name)
                self.assertEqual(result.returncode, 1, result.stderr)
                self.assertEqual(result.stdout, "")
                self.assertIn("duplicate", result.stderr)
                self.assertIn("`id`", result.stderr)
                self.assertIn(str(path), result.stderr)
                self.assertIn("$.entries", result.stderr)
                self.assertIn("items 0 and 1", result.stderr)
                self.assertNotIn("Traceback", result.stderr)

    def test_renderer_rejects_duplicate_inputs_before_publishing(self):
        self.write(self.skill / "workflow.md", "# Example {workflow.title}\n")
        self.write(self.custom_paths[0], '[workflow]\ntitle = "Example"\n')
        for path in (self.central_paths[-1], self.custom_paths[-1]):
            with self.subTest(layer=path.name):
                self.write(path, '[[entries]]\nid = "same"\n[[entries]]\nid = "same"\n')
                result = self.run_resolver("render_skill.py")
                self.assertEqual(result.returncode, 1, result.stdout)
                self.assertIn("HALT:", result.stdout)
                self.assertIn("duplicate", result.stdout)
                self.assertIn(str(path), result.stdout)
                self.assertEqual(result.stderr, "")
                self.assertFalse((self.root / "_bmad" / "render").exists())
                self.write(path, "")

    def test_renderer_publishes_valid_configuration(self):
        self.write(self.skill / "workflow.md", "# Example {workflow.title}\n")
        self.write(self.custom_paths[0], '[workflow]\ntitle = "Default"\n')
        self.write(self.custom_paths[-1], '[workflow]\ntitle = "Selected"\n')
        result = self.run_resolver("render_skill.py")
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertEqual(result.stderr, "")
        snapshots = list((self.root / "_bmad" / "render").rglob("workflow.md"))
        self.assertEqual(len(snapshots), 1)
        self.assertEqual(snapshots[0].read_text(encoding="utf-8"), "# Example Selected\n")

    def test_resolvers_preserve_valid_layer_precedence(self):
        for name, paths in (
            ("resolve_config.py", self.central_paths),
            ("resolve_customization.py", self.custom_paths),
        ):
            with self.subTest(resolver=name):
                for index, path in enumerate(paths):
                    self.write(path, f'[[entries]]\nid = "same"\nrank = {index}\n')
                result = self.run_resolver(name)
                self.assertEqual(result.returncode, 0, result.stderr)
                self.assertEqual(result.stderr, "")
                self.assertEqual(
                    json.loads(result.stdout),
                    {"entries": [{"id": "same", "rank": len(paths) - 1}]},
                )


if __name__ == "__main__":
    unittest.main()
