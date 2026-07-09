#!/usr/bin/env bash
# Story 9.8 build-gate regression guard.
#
# Fails if any active CI/build script re-introduces either:
#   1. The `-p:TreatWarningsAsErrors=false` / `-p:WarningsAsErrors=` (empty)
#      command-line workaround. MSBuild-equivalent prefixes (`/p:`, `-property:`,
#      `--property`) and case-insensitive variants are also caught. The
#      narrowing form `-p:WarningsAsErrors=<RuleId>` (adding a single ID to the
#      warnings-as-errors set) is intentionally allowed.
#   2. Nested-submodule initialization (`git submodule update ... --recursive`
#      or `submodules: recursive` in GitHub Actions checkout), per AC5.
#
# Story 9.8 (2026-05-22) closed both workarounds by ensuring the solution-level
# build is green on a clean clone with root-level submodules only. The Epic 5
# retrospective (action B13, 2026-05-22) escalated 9.8 to a program-level
# critical-path gate. This script locks the green baseline.
#
# Excluded from scanning (historical / policy documentation):
#   _bmad-output/                       - story records, retros, planning docs
#   docs/                               - human-facing documentation including
#                                         the build-gate policy doc
#   scripts/check-no-warning-override.sh - this script itself (by basename)
#
# Note: scripts/ as a directory is NOT excluded. A future helper script in
# scripts/ that re-introduces the override or nested-submodule init MUST be
# detected.
#
# Invoked from the shared CI workflow and from local pre-push hooks.
# See docs/build-gate.md for the gate policy.
set -euo pipefail

repo_root=$(git rev-parse --show-toplevel 2>/dev/null || true)
if [ -z "$repo_root" ]; then
  echo "::error::scripts/check-no-warning-override.sh must run inside a git repository (run from a fresh clone or container with .git present)." >&2
  exit 2
fi
cd "$repo_root"

run_grep() {
  # Returns matches on stdout. Caller must capture the exit code separately.
  # Exits 0 on match, 1 on no-match, >=2 on real error (unreadable file,
  # broken pipe, invalid regex, etc.).
  grep -rIEni \
    --include='*.yml' --include='*.yaml' \
    --include='*.ps1' --include='*.sh' \
    --include='*.cmd' --include='*.bat' \
    --exclude-dir='_bmad-output' \
    --exclude-dir='docs' \
    --exclude-dir='node_modules' \
    --exclude-dir='bin' \
    --exclude-dir='obj' \
    --exclude='check-no-warning-override.sh' \
    "$@" \
    .
}

# Pattern set 1: warnings-as-errors override at the command line.
# Covers -p:, /p:, -property:, --property: (with `:`, `=`, or space separator).
# For WarningsAsErrors=, only the empty-value form is matched; the narrowing
# typed form `=CA2007` is intentionally allowed per docs/build-gate.md.
set +e
override_matches=$(run_grep \
  -e '[-/]p[: =]TreatWarningsAsErrors=false' \
  -e '[-/]-?property[: =]TreatWarningsAsErrors=false' \
  -e '[-/]p[: =]WarningsAsErrors=([[:space:]"'\'']|$)' \
  -e '[-/]-?property[: =]WarningsAsErrors=([[:space:]"'\'']|$)')
override_rc=$?
set -e
if [ "$override_rc" -ge 2 ]; then
  echo "::error::scripts/check-no-warning-override.sh: grep failed with exit code $override_rc while scanning for warning-override patterns." >&2
  exit 2
fi

# Pattern set 2: nested-submodule initialization (AC5).
set +e
nested_matches=$(run_grep \
  -e 'submodule update[[:space:]].*--recursive' \
  -e 'submodules:[[:space:]]*['\''"]?recursive['\''"]?')
nested_rc=$?
set -e
if [ "$nested_rc" -ge 2 ]; then
  echo "::error::scripts/check-no-warning-override.sh: grep failed with exit code $nested_rc while scanning for nested-submodule patterns." >&2
  exit 2
fi

fail=0

if [ -n "$override_matches" ]; then
  fail=1
  echo "::error::Story 9.8 build-gate regression detected. Active CI/build scripts reference a warnings-as-errors override workaround:"
  echo ""
  echo "$override_matches"
  echo ""
  echo "Background:"
  echo "  Story 9.8 (2026-05-22) closed this workaround by ensuring the solution-level"
  echo "  build is green on a clean clone. Re-adding the override is a regression."
  echo ""
  echo "Fix:"
  echo "  Address the underlying warning rather than re-adding the override at the"
  echo "  command line. If a submodule update genuinely re-introduces a warning, add"
  echo "  a narrow <NoWarn>RuleId</NoWarn> in the consuming project (with an issue"
  echo "  link), or bump the submodule pointer to a fixed upstream commit."
  echo ""
  echo "See docs/build-gate.md for the gate policy."
  echo ""
fi

if [ -n "$nested_matches" ]; then
  fail=1
  echo "::error::Story 9.8 build-gate regression detected. Active CI/build scripts re-introduce nested-submodule initialization:"
  echo ""
  echo "$nested_matches"
  echo ""
  echo "Background:"
  echo "  Story 9.8 acceptance criterion AC5 forbids regressions that require nested-"
  echo "  submodule initialization. Story 3.6 established root-level submodules only"
  echo "  as the default; opt-in projects must use the EnableMemoriesSearch=true pattern."
  echo ""
  echo "Fix:"
  echo "  Remove --recursive from 'git submodule update' invocations, and set the"
  echo "  GitHub Actions checkout 'submodules:' option to 'true' (root-level only),"
  echo "  not 'recursive'. If a project genuinely needs nested submodules, make it"
  echo "  opt-in following the EnableMemoriesSearch pattern."
  echo ""
  echo "See docs/build-gate.md for the gate policy."
  echo ""
fi

if [ "$fail" -ne 0 ]; then
  exit 1
fi

echo "OK: no warning-override or nested-submodule regressions detected in active CI/build scripts."
