# Release Checklist

Blocking gates that must pass before a release tag is cut.

## Root gitlink RC gate (blocking — before every release tag)

Every root submodule pointer that has drifted or been bumped must be a conscious
choice: **owner-validated** or **deliberately reset**. Nothing advances into a tag
by accident.

- [ ] `bash scripts/gitlink-rc-gate.sh --worktree` exits **0**.
- [ ] Every drifted (`+`) root gitlink in `git submodule status` is EITHER:
  - **owner-validated** → a `validated-advance` line in `.gitlink-signoff.tsv`
    (the owner confirms the forward move ships) **and** the bump is committed into
    the release branch; OR
  - **deliberately reset** → `git submodule update --checkout <path>` restores the
    recorded commit (no ledger entry required).
- [ ] No `+` / `U` / `-` pointers remain unexplained in `git submodule status`.
- [ ] Ledger `owner` and `date` fields are **real** — no `<OWNER-PENDING>` /
      `<OWNER>` / `<DATE>` placeholders (the gate rejects placeholder owners).
- [ ] CI `Release Candidate Gate` workflow is green on the release candidate
      (runs `scripts/gitlink-rc-gate.sh --diff <base>` — see
      `.github/workflows/rc-gate.yml`).

### How it works

| Surface | Command | Catches |
| --- | --- | --- |
| Local, pre-tag | `scripts/gitlink-rc-gate.sh --worktree` | uncommitted working-tree drift (`+`/`U`), uninitialised (`-`) |
| CI, on RC | `scripts/gitlink-rc-gate.sh --diff <base>` | a gitlink bump committed into the release branch without sign-off |

The ledger (`.gitlink-signoff.tsv`) is the source of truth for release-time
pointers. Nested submodules are never inspected — the ROOT `.gitmodules` only.
