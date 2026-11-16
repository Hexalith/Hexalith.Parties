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
      and on each ordinary `main` push. Main pushes run
      `scripts/gitlink-rc-gate.sh --push-range <event.before>`; RC, tag and manual
      runs retain `--diff <base>` (see `.github/workflows/rc-gate.yml`).

### How it works

| Surface | Command | Catches |
| --- | --- | --- |
| Local, pre-tag | `scripts/gitlink-rc-gate.sh --worktree` | uncommitted working-tree drift (`+`/`U`), uninitialised (`-`) |
| CI, main push | `scripts/gitlink-rc-gate.sh --push-range <event.before>` | every root pointer change across the complete push range, including intermediate unsigned bumps |
| CI, on RC | `scripts/gitlink-rc-gate.sh --diff <base>` | a gitlink bump committed into the release branch without sign-off |

The ledger (`.gitlink-signoff.tsv`) is the source of truth for release-time
pointers. Nested submodules are never inspected — the ROOT `.gitmodules` only.

Main push bases must be exact, nonzero, available ancestor commits. The workflow
fails closed when `event.before` cannot be proved; it never substitutes the new
`origin/main` tip. Every advanced SHA in the range needs its matching validated
owner signoff, even when a later commit replaces it. A stale SHA, placeholder
owner or audit-only `reset` entry cannot authorize an advance.
Main source SHAs use separate concurrency groups. Declarations added later in a
push still cover earlier gitlink advances, and unchanged first-parent pointers
are preserved when merging branches.

## Exact-source publication admission

- [ ] Dispatch Release from the exact current `main` tip only after both `ci.yml`
      and `rc-gate.yml` have completed successful `main` push runs at that SHA.
      The caller validates their independent workflow IDs and each run's source,
      branch, event, completed status and success verdict. Manual, PR, tag, stale,
      pending, canceled or failed range proof does not authorize publication.
- [ ] Retain the existing release-floor root-gitlink signoff gate before login
      and publication; the main-push range proof is an additional prerequisite.
- [ ] Both `release` and `verify-publication` declare the protected `production`
      environment. Configure its reviewers and `main` restriction in GitHub;
      local declaration tests do not exercise those external settings.

## Published artifact proof

After the protected manual release completes:

- [ ] `verify-publication` resolves all unique non-draft canonical stable
      `vMAJOR.MINOR.PATCH` tags and finds exactly one distinct version at the
      dispatched SHA. No leading zeros, prerelease or build metadata is eligible.
      Non-version tags are ignored; duplicate entries are deduplicated; ambiguity
      fails in either list order. Missing refs (HTTP 404) may be skipped, while
      other API or object-validation errors fail. The sole matched tag supplies
      the artifact version.
- [ ] All nine unique packages in `tools/release-packages.json` download and pass
      archive/boundary checks, with exact nuspec ID, version, repository URL,
      repository type `git` and source commit. Duplicate or malformed archive
      metadata, missing license files, incorrect required dependencies/versions
      and incomplete inventories fail verification against the dispatched catalog.
- [ ] `parties`, `parties-mcp` and `parties-ui` pass the validator from immutable
      Builds `397c94a4e246c90b21cf408790fa0d55bf32d795`: index/child/config hashes,
      exact `linux/amd64` and `linux/arm64` platforms and release provenance.
- [ ] Inspect the always-uploaded
      `publication-verification-RUN_ID-RUN_ATTEMPT` evidence, including download
      attempts, raw bytes and `publication-verification.json`. A missing artifact,
      authentication failure, metadata mismatch or exhausted propagation retry
      fails the job; inspect its retained diagnostics before retrying verification.

Verification makes read requests only and never publishes test artifacts. Each
response is byte-bounded; requests time out after thirty seconds, missing or
transient responses get at most four attempts, and the total budget is ten
minutes for downloads. Connection setup, headers, bodies and redirects share the
request deadline. At most four abandoned connection workers retain capacity until
they settle; late sockets are closed. Bounded partial captures are retained with
attempt receipts. Evidence is retained for 30 days. Package hashes describe the downloaded
registry bytes; NuGet signatures can change those bytes, so they do not establish
equality with unsigned build archives. Container proof covers the shared
validator's manifest/config graph and provenance, including both architectures.
