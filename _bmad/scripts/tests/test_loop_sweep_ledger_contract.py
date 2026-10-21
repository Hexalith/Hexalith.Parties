"""Guard the installed sweep's orchestrator-owned ledger path instructions."""

import unittest
from pathlib import Path


class LoopSweepLedgerContractTests(unittest.TestCase):
    def test_both_installed_entry_points_require_the_orchestrator_ledger(self):
        root = Path(__file__).resolve().parents[3]
        steps = []
        for owner in (".agents", ".claude"):
            with self.subTest(owner=owner):
                source = (root / owner / "skills/bmad-loop-sweep/SKILL.md").read_text(encoding="utf-8")
                step = source.split("### Step 1: Locate the ledger\n", 1)[1].split("### Step 2:", 1)[0]
                self.assertIn("BMAD_LOOP_LEDGER", step)
                self.assertIn("never re-derive it", step)
                self.assertIn("missing-ledger", step)
                self.assertIn("If the output is empty, or", step)
                self.assertIn("the ledger is missing or unreadable", step)
                self.assertNotIn("config.yaml", step)
                self.assertNotIn("config.toml", step)
                steps.append(step)
        self.assertEqual(steps[0], steps[1])
