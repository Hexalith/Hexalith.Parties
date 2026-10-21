"""Reproduce the DW-181/DW-186 installed-consumer observations without running skills."""

import json
import subprocess
import sys
from pathlib import Path

root = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(root / "_bmad/scripts"))
from config_utils import load_central_config
from render_skill import _resolve_replacements

step_paths = [
    ".agents/skills/bmad-loop-sweep/SKILL.md",
    ".claude/skills/bmad-loop-sweep/SKILL.md",
]
steps = []
for relative in step_paths:
    source = (root / relative).read_text(encoding="utf-8")
    step = source.split("### Step 1: Locate the ledger\n", 1)[1].split("### Step 2:", 1)[0]
    assert "BMAD_LOOP_LEDGER" in step
    assert "never re-derive it" in step
    assert "missing-ledger" in step
    assert "config.yaml" not in step and "config.toml" not in step
    steps.append(step)
assert steps[0] == steps[1]

central = load_central_config(root)
replacements, inputs = _resolve_replacements(
    {"workflow.md": "{{config.modules.cis.visual_tools}}"}, central, {}, None, root
)
value = replacements["{{config.modules.cis.visual_tools}}"]
assert isinstance(value, str)
decoded = json.loads(value)
assert isinstance(decoded, list) and all(isinstance(tool, str) for tool in decoded)
matches = subprocess.run(
    ["git", "grep", "-n", "visual_tools", "--", ".",
     ":(exclude)references/**", ":(exclude)_bmad-output/**", ":(exclude)_bmad/render/**"],
    cwd=root, text=True, capture_output=True, timeout=15, check=True,
).stdout.splitlines()
print(json.dumps({
    "ledger_steps": step_paths,
    "ledger_disposition": "orchestrator path required; missing path escalates; no config fallback",
    "resolved_visual_tools_type": type(value).__name__,
    "resolved_visual_tools": value,
    "decoded_visual_tools": json.loads(value),
    "render_inputs": inputs,
    "tracked_visual_tools_references": matches,
    "consumer_scope": "installed tracked non-reference files; tests and this trace do not imply a CIS runtime consumer",
}, indent=2))
