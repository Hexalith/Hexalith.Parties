#!/usr/bin/env bash
# Release-candidate gate for root gitlinks.
#
# Invariant: before a release tag, every root submodule pointer that has drifted
# (working tree) or been bumped (vs a base ref) must be EITHER owner-validated in
# the sign-off ledger OR deliberately reset to the recorded commit.
#
# Modes:
#   (default / --worktree)  Fail if `git submodule status` shows any root
#                           submodule whose checked-out commit differs from the
#                           index (+ / U) — or is uninitialised (-) — and lacks a
#                           matching validated-advance ledger entry.
#                           "Reset" a drift by restoring the recorded commit
#                           (git submodule update --checkout <path>) — that clears
#                           the drift and needs no ledger entry.
#   --diff <base-ref>       CI mode. Fail if any root gitlink SHA recorded at HEAD
#                           differs from <base-ref> and the HEAD SHA has no
#                           matching validated-advance ledger entry.
#
# Ledger (.gitlink-signoff.tsv), pipe-delimited, one line per authorised pointer:
#   <path>|<sha>|<disposition>|<ref>|<owner>|<date>
#   disposition in { validated-advance, reset }.  A validated-advance entry is
#   honoured only when <owner> is a real handle (not empty, not a <PLACEHOLDER>).
#
# Never recurses into nested submodules — reads the ROOT .gitmodules only.
# No external dependencies beyond git / awk.
#
# Exit 0 = gate passes (safe to tag).  Exit 1 = unvalidated drift/bump.  Exit 2 = usage.
set -euo pipefail

REPO_ROOT="$(git rev-parse --show-toplevel)"
cd "$REPO_ROOT"
LEDGER="${GITLINK_LEDGER:-.gitlink-signoff.tsv}"

mode="worktree"
base_ref=""
case "${1:-}" in
  --diff)          mode="diff"; base_ref="${2:-}" ;;
  --worktree|"")   mode="worktree" ;;
  *) echo "usage: $0 [--worktree | --diff <base-ref>]" >&2; exit 2 ;;
esac

# Root-declared submodule paths (root .gitmodules only — never nested).
mapfile -t SUBMODULES < <(git config -f .gitmodules --get-regexp '^submodule\..*\.path$' | awk '{print $2}' | sort)

# ledger_has <path> <sha> -> 0 if a validated-advance entry with a real owner exists.
ledger_has() {
  local path="$1" sha="$2"
  [ -f "$LEDGER" ] || return 1
  awk -F'|' -v p="$path" -v s="$sha" '
    /^[[:space:]]*#/ { next }
    NF < 3 { next }
    { for (i = 1; i <= NF; i++) gsub(/^[ \t]+|[ \t]+$/, "", $i) }
    $1 == p && $2 == s && $3 == "validated-advance" && $5 != "" && $5 !~ /^</ { found = 1 }
    END { exit(found ? 0 : 1) }
  ' "$LEDGER"
}

fail=0
report() { printf '  %-45s %s\n' "$1" "$2"; }

if [ "$mode" = "worktree" ]; then
  echo "gitlink-rc-gate: working-tree drift check"
  while IFS= read -r line; do
    [ -n "$line" ] || continue
    flag="${line:0:1}"
    rest="${line:1}"
    sha="${rest%% *}"
    path="$(echo "$rest" | awk '{print $2}')"
    case "$flag" in
      '+'|'U')
        if ledger_has "$path" "$sha"; then
          report "$path" "DRIFT ok — validated-advance @ ${sha:0:12}"
        else
          report "$path" "DRIFT UNVALIDATED @ ${sha:0:12} — validate (ledger) or reset"
          fail=1
        fi
        ;;
      '-')
        report "$path" "NOT INITIALIZED — run git submodule update --init"
        fail=1
        ;;
    esac
  done < <(git submodule status -- "${SUBMODULES[@]}")
else
  [ -n "$base_ref" ] || { echo "--diff requires <base-ref>" >&2; exit 2; }
  echo "gitlink-rc-gate: recorded-pointer diff vs $base_ref"
  for path in "${SUBMODULES[@]}"; do
    head_sha="$(git ls-tree HEAD "$path" | awk '{print $3}')"
    base_sha="$(git ls-tree "$base_ref" "$path" 2>/dev/null | awk '{print $3}')"
    [ -n "$head_sha" ] || continue
    if [ "$head_sha" != "$base_sha" ]; then
      if ledger_has "$path" "$head_sha"; then
        report "$path" "BUMP ok — validated-advance @ ${head_sha:0:12}"
      else
        report "$path" "BUMP UNVALIDATED ${base_sha:0:12}->${head_sha:0:12} — add ledger entry"
        fail=1
      fi
    fi
  done
fi

if [ "$fail" -ne 0 ]; then
  echo ""
  echo "gitlink-rc-gate: FAIL — resolve each pointer above before tagging." >&2
  exit 1
fi
echo "gitlink-rc-gate: PASS — all root gitlinks validated or clean."
