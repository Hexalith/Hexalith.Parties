"""Exercise visual-tool configuration through layered loading and token rendering."""

import json
import sys
import tempfile
import unittest
from pathlib import Path

SCRIPTS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SCRIPTS))

from config_utils import load_central_config
from render_skill import RenderError, _render_sources, _resolve_replacements


class VisualToolResolutionTests(unittest.TestCase):
    def test_json_string_survives_each_layer_and_renders_without_array_coercion(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            paths = [
                root / "_bmad/config.toml",
                root / "_bmad/config.user.toml",
                root / "_bmad/custom/config.toml",
                root / "_bmad/custom/config.user.toml",
            ]
            for index, path in enumerate(paths):
                with self.subTest(layer=index):
                    expected = ["mermaid", f"layer-{index}"]
                    scalar = json.dumps(expected)
                    path.parent.mkdir(parents=True, exist_ok=True)
                    # A TOML literal string contains the JSON text unchanged.
                    path.write_text(f"[modules.cis]\nvisual_tools = '{scalar}'\n", encoding="utf-8")
                    replacements, inputs = _resolve_replacements(
                        {"workflow.md": "Tools: {{config.modules.cis.visual_tools}}"},
                        load_central_config(root), {}, None, root,
                    )
                    rendered = replacements["{{config.modules.cis.visual_tools}}"]
                    self.assertEqual(rendered, scalar)
                    self.assertEqual(json.loads(rendered), expected)
                    self.assertEqual(inputs["config.modules.cis.visual_tools"], scalar)
                    rendered_sources = _render_sources(
                        {"workflow.md": "Tools: {{config.modules.cis.visual_tools}}"},
                        replacements, root / "rendered",
                    )
                    self.assertEqual(rendered_sources["workflow.md"], "Tools: " + scalar)

    def test_array_is_not_a_supported_scalar_template_input(self):
        with tempfile.TemporaryDirectory() as temporary:
            with self.assertRaisesRegex(RenderError, "must be a string, got list"):
                _resolve_replacements(
                    {"workflow.md": "{{config.modules.cis.visual_tools}}"},
                    {"modules": {"cis": {"visual_tools": ["mermaid"]}}},
                    {}, None, Path(temporary),
                )
