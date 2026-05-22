#!/usr/bin/env bash
# Story 9.8 build-gate regression guard.
#
# Fails if any active CI/build script re-introduces the
# `-p:TreatWarningsAsErrors=false` / `-p:WarningsAsErrors=` command-line
# workaround. Story 9.8 (2026-05-22) closed this workaround by ensuring the
# solution-level build is green on clean clone. The Epic 5 retrospective
# (action B13, 2026-05-22) escalated 9.8 to a program-level critical-path
# gate. This script locks the green baseline.
#
# Excluded from scanning (historical / policy documentation):
#   _bmad-output/                       - story records, retros, planning docs
#   docs/                               - human-facing documentation including
#                                         the build-gate policy doc
#   scripts/check-no-warning-override.sh - this script itself (by basename)
#
# Note: scripts/ as a directory is NOT excluded. A future helper script in
# scripts/ that re-introduces the override MUST be detected.
#
# Invoked from .github/workflows/test.yml::lint and from local pre-push hooks.
# See docs/build-gate.md for the gate policy.
set -euo pipefail

cd "$(git rev-parse --show-toplevel)"

matches=$(grep -rIn \
  --include='*.yml' --include='*.yaml' \
  --include='*.ps1' --include='*.sh' \
  --include='*.cmd' --include='*.bat' \
  --exclude-dir='_bmad-output' \
  --exclude-dir='docs' \
  --exclude-dir='node_modules' \
  --exclude-dir='bin' \
  --exclude-dir='obj' \
  --exclude='check-no-warning-override.sh' \
  -e '-p:TreatWarningsAsErrors=false' \
  -e '-p:WarningsAsErrors=' \
  . 2>/dev/null || true)

if [ -n "$matches" ]; then
  echo "::error::Story 9.8 build-gate regression detected. Active CI/build scripts reference the warning-override workaround:"
  echo ""
  echo "$matches"
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
  exit 1
fi

echo "OK: no warning-override regressions detected in active CI/build scripts."
