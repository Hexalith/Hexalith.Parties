"""Exercise ACL parsing, deterministic rendering, stale input and source selection."""
import copy
import importlib.util
import json
import subprocess
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

SCRIPT = Path(__file__).with_name("render-platform-declaration.py")
SPEC = importlib.util.spec_from_file_location("render_platform_declaration", SCRIPT)
renderer = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(renderer)


class PlatformDeclarationTests(unittest.TestCase):
    def setUp(self):
        self.acl = renderer.ACL.read_text()

    def test_owner_acl_is_the_exact_declaration_authority(self):
        actual = renderer.declaration(self.acl)
        self.assertEqual(actual, json.loads(renderer.MANIFEST.read_text()))
        module = actual["modules"][0]
        self.assertEqual(module["identity"]["dependencies"], ["eventstore", "tenants", "memories"])
        rules = module["surfaces"]["interfaces"][0]["invocationRules"]
        self.assertEqual(len(rules), 13)
        self.assertTrue(all(rule["caller"] == "eventstore" and rule["verbs"] == ["POST"] for rule in rules))
        self.assertEqual(module["integration"]["topics"][0]["subscription"], {
            "server": "domain", "route": "/tenants/events", "topic": "system.tenants.events"})

    def test_unsafe_malformed_and_duplicate_acl_inputs_fail_closed(self):
        for label, content in {
            "empty": "", "truncated": self.acl[:self.acl.index("    policies:")],
            "default allow": self.acl.replace("    defaultAction: deny", "    defaultAction: allow", 1),
            "wildcard": self.acl.replace("name: /process", "name: /**"),
            "wrong caller": self.acl.replace("appId: eventstore", "appId: parties"),
            "wrong verb": self.acl.replace("['POST']", "['GET']", 1),
            "duplicate operation": self.acl.replace("name: /query", "name: /process"),
            "duplicate field": self.acl.replace("    defaultAction: deny", "    defaultAction: deny\n    defaultAction: allow", 1),
            "duplicate spec": self.acl + "\nspec:\n  accessControl:\n    defaultAction: allow\n",
            "unknown section": self.acl + "\n  unknown: value\n",
            "tag": self.acl.replace("appId: eventstore", "appId: !unsafe eventstore"),
            "missing route": self.acl[:self.acl.index("          - name: /project/rebuild/verify/v1")],
        }.items():
            with self.subTest(label=label), self.assertRaises((AssertionError, ValueError, SyntaxError)):
                renderer.declaration(content)

    def test_check_rejects_missing_stale_and_unsafe_inputs_without_rewriting(self):
        with tempfile.TemporaryDirectory() as temporary:
            acl = Path(temporary) / "accesscontrol.yaml"
            manifest = Path(temporary) / "declaration.json"
            acl.write_text(self.acl)
            with patch.object(renderer, "ACL", acl), patch.object(renderer, "MANIFEST", manifest):
                with patch("sys.argv", [str(SCRIPT), "--check"]):
                    self.assertEqual(renderer.main(), 1)
                with patch("sys.argv", [str(SCRIPT)]):
                    self.assertEqual(renderer.main(), 0)
                rendered = manifest.read_bytes()
                with patch("sys.argv", [str(SCRIPT), "--check"]):
                    self.assertEqual(renderer.main(), 0)
                manifest.write_text("{}")
                with patch("sys.argv", [str(SCRIPT), "--check"]):
                    self.assertEqual(renderer.main(), 1)
                self.assertEqual(manifest.read_text(), "{}")
                manifest.write_bytes(rendered)
                acl.write_text(self.acl.replace("name: /query", "name: /query/v2"))
                with patch("sys.argv", [str(SCRIPT), "--check"]):
                    self.assertEqual(renderer.main(), 1)
                self.assertEqual(manifest.read_bytes(), rendered)
                acl.write_text(self.acl.replace("['POST']", "['GET']", 1))
                with patch("sys.argv", [str(SCRIPT)]):
                    self.assertEqual(renderer.main(), 1)
                self.assertEqual(manifest.read_bytes(), rendered)

    def test_build_files_leave_all_source_selection_to_the_tool(self):
        root = SCRIPT.parents[1]
        props = (root / "Directory.Build.props").read_text()
        packages = (root / "Directory.Packages.props").read_text()
        for forbidden in ("FromSource", "UseNuGetDeps", "UseHexalithProjectReferences", "Exists("):
            self.assertNotIn(forbidden, props)
        self.assertNotIn("Exists(", packages)
        self.assertIn("'$(HexalithBuildsRoot)' == ''", packages)
        for name in ("Contracts", "Client"):
            self.assertNotIn("<HexalithEventStoreRoot", (root / f"src/Hexalith.Parties.{name}/Hexalith.Parties.{name}.csproj").read_text())
        host = (root / "src/Hexalith.Parties/Hexalith.Parties.csproj").read_text()
        self.assertNotIn("AdditionalProperties=", host)
        self.assertNotIn("GlobalPropertiesToRemove=", host)


if __name__ == "__main__":
    unittest.main()
