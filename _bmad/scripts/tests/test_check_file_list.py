import subprocess
import tempfile
import unittest
from pathlib import Path


SCRIPT = Path(__file__).resolve().parents[1] / "check_file_list.py"


class CheckFileListTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.repo = Path(self.tmp.name)
        self.run_cmd("git", "-c", "init.defaultBranch=main", "init")
        self.run_cmd("git", "config", "user.email", "test@example.com")
        self.run_cmd("git", "config", "user.name", "Test User")
        self.write("README.md", "initial\n")
        self.run_cmd("git", "add", "README.md")
        self.run_cmd("git", "commit", "-m", "initial")
        self.base = self.run_cmd("git", "rev-parse", "HEAD").stdout.strip()

    def tearDown(self):
        self.tmp.cleanup()

    def run_cmd(self, *args):
        return subprocess.run(
            args,
            cwd=self.repo,
            check=True,
            capture_output=True,
            text=True,
        )

    def run_gate(self, *args):
        return subprocess.run(
            ["python3", str(SCRIPT), *args],
            cwd=self.repo,
            capture_output=True,
            text=True,
        )

    def write(self, relative_path, text):
        path = self.repo / relative_path
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding="utf-8")

    def write_story(self, body):
        self.write(
            "story.md",
            f"---\nbaseline_commit: {self.base}\n---\n\n{body}",
        )

    def test_passes_when_changed_file_is_declared(self):
        self.write("src/changed.txt", "changed\n")
        self.write_story("### File List\n\n- `src/changed.txt`\n")

        result = self.run_gate("--story", "story.md", "--require-file-list")

        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertIn("OK: File List matches git diff", result.stdout)

    def test_fails_when_changed_file_is_undeclared(self):
        self.write("src/changed.txt", "changed\n")
        self.write_story("### File List\n\n")

        result = self.run_gate("--story", "story.md", "--require-file-list")

        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
        self.assertIn("UNDECLARED (changed, not in File List): src/changed.txt", result.stdout)

    def test_requires_file_list_when_flag_is_set(self):
        self.write("src/changed.txt", "changed\n")
        self.write_story("## Notes\n\nNo file list here.\n")

        result = self.run_gate("--story", "story.md", "--require-file-list")

        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
        self.assertIn("FAIL: no `### File List` section found in story", result.stdout)

    def test_missing_file_list_can_warn_for_non_gate_callers(self):
        self.write("src/changed.txt", "changed\n")
        self.write_story("## Notes\n\nNo file list here.\n")

        result = self.run_gate("--story", "story.md")

        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertIn("WARN: no `### File List` section found in story", result.stdout)


if __name__ == "__main__":
    unittest.main()
