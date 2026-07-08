#!/usr/bin/env python3
"""File List reconciliation gate.

Compares the set of files changed since a story's frontmatter ``baseline_commit``
against the story's declared ``### File List`` section. This is the mechanical
replacement for the previously self-attested "File List includes every changed
file" check used by bmad-dev-story (completion gate) and bmad-code-review
(pre-review gate).

Ground truth for "changed" = committed (base..HEAD) U working-tree (base)
U untracked (git ls-files --others --exclude-standard).

Exit codes:
  0  reconciled - no undeclared files (phantom entries only warn)
  1  FAIL       - one or more changed files are missing from the File List,
                  or --require-file-list was given and no File List section exists
  2  usage/error - bad args, story unreadable, no baseline_commit, git failure

Undeclared files (changed on disk, absent from the File List) are the review
blind spot and always fail. Phantom entries (listed but not changed) only warn.
Paths matching EXCLUDE never fail or warn in either direction; the story file
is likewise never faulted for omitting itself.
"""
import argparse
import fnmatch
import os
import re
import subprocess
import sys

# Paths that legitimately drift but are NOT story deliverables. A changed file
# matching one of these is never counted as UNDECLARED; a listed file matching
# one of these is never counted as phantom.
EXCLUDE = [
    "references/*", "references/**",          # submodule gitlink drift
    "**/sprint-status.yaml",                  # workflow-owned sprint tracking
    "_bmad-output/story-automator/**",        # transient automator session state
    "_bmad-output/**/orchestration-*.md",     # transient orchestration logs
]


def sh(args):
    return subprocess.run(args, capture_output=True, text=True)


def norm(p):
    return p.replace("\\", "/").strip().strip("`")


def changed_since(base):
    """Union of committed, working-tree, and untracked changes since ``base``."""
    files = set()
    for cmd in (
        ["git", "diff", "--name-only", f"{base}..HEAD"],       # committed
        ["git", "diff", "--name-only", base],                  # working tree
        ["git", "ls-files", "--others", "--exclude-standard"],  # untracked
    ):
        r = sh(cmd)
        if r.returncode != 0:
            sys.stderr.write(r.stderr)
            return None
        files.update(line for line in (x.strip() for x in r.stdout.splitlines()) if line)
    return {norm(p) for p in files}


def read_baseline(text):
    m = re.search(r'^baseline_commit:\s*[\'"]?([0-9a-fA-F]{7,40})', text, re.MULTILINE)
    return m.group(1) if m else None


def read_file_list(text):
    """Return (present: bool, paths: set[str]) parsed from the File List section."""
    present = capturing = False
    out = set()
    for line in text.splitlines():
        if re.match(r'^#{2,3}\s+File List\s*$', line, re.IGNORECASE):
            present = capturing = True
            continue
        if capturing and re.match(r'^#{1,6}\s+\S', line):  # next heading ends the block
            break
        if capturing:
            m = re.match(r'^\s*[-*]\s+`?([^`\s].*?)`?\s*$', line)
            if m:
                out.add(norm(m.group(1)))
    return present, out


def main():
    ap = argparse.ArgumentParser(description="File List reconciliation gate")
    ap.add_argument("--story", required=True, help="path to the story / spec markdown file")
    ap.add_argument("--base", help="override the frontmatter baseline_commit")
    ap.add_argument("--require-file-list", action="store_true",
                    help="FAIL (exit 1) when the story has no File List section")
    a = ap.parse_args()

    try:
        with open(a.story, encoding="utf-8") as f:
            text = f.read()
    except OSError as e:
        print(f"ERROR: cannot read story '{a.story}': {e}")
        return 2

    base = a.base or read_baseline(text)
    if not base:
        print("ERROR: no baseline_commit in frontmatter and no --base given")
        return 2
    if sh(["git", "rev-parse", "--verify", f"{base}^{{commit}}"]).returncode != 0:
        print(f"ERROR: baseline commit '{base}' not found in this repo")
        return 2

    changed = changed_since(base)
    if changed is None:
        print("ERROR: git diff failed")
        return 2

    present, declared = read_file_list(text)
    story_rel = norm(os.path.relpath(a.story))

    def keep(p):
        return not any(fnmatch.fnmatch(p, pat) for pat in EXCLUDE) and p != story_rel

    print(f"File List gate - baseline {base[:12]} "
          f"({len(changed)} changed, {len(declared)} declared)")

    # A story with no File List section cannot be reconciled. When the caller
    # requires one that is a FAIL; otherwise it is a non-blocking warning and
    # there is nothing further to compare.
    if not present:
        msg = "no `### File List` section found in story"
        if a.require_file_list:
            print(f"FAIL: {msg}")
            return 1
        print(f"WARN: {msg} - cannot reconcile against git diff")
        return 0

    undeclared = sorted(p for p in (changed - declared) if keep(p))
    phantom = sorted(p for p in (declared - changed) if keep(p))
    for p in undeclared:
        print(f"  UNDECLARED (changed, not in File List): {p}")
    for p in phantom:
        print(f"  phantom    (in File List, not changed): {p}")

    if undeclared:
        print(f"FAIL: {len(undeclared)} changed file(s) missing from File List")
        return 1
    if phantom:
        print(f"OK - {len(phantom)} phantom warning(s)")
        return 0
    print("OK: File List matches git diff")
    return 0


if __name__ == "__main__":
    sys.exit(main())
