#!/usr/bin/env bash
# Supply historical objects to root-specific tests without changing source selections.
set -euo pipefail

root="$(git rev-parse --show-toplevel)"
root="$(cd "$root" && pwd -P)"
selected_root="$(git -C "$root" rev-parse HEAD)"
declarations="$(mktemp "${RUNNER_TEMP:-${TMPDIR:-/tmp}}/parties-ci-history.XXXXXX")"
trap 'rm -f -- "$declarations"' EXIT

git -C "$root" config --blob="$selected_root:.gitmodules" --null --get-regexp '^submodule\..*\.path$' > "$declarations"
paths=()
heads=()
branches=()
statuses=()
while IFS= read -r -d '' declaration; do
    path="${declaration#*$'\n'}"
    if [[ -z "$path" || "$path" = /* || "$path" =~ (^|/)\.\.?(/|$) ]]; then
        printf 'Invalid direct dependency path: %s\n' "$path" >&2
        exit 1
    fi
    checkout="$(realpath -- "$root/$path")"
    if [[ "$checkout" != "$root/"* || ! -e "$checkout/.git" ]]; then
        printf 'Direct dependency is not an already prepared own checkout: %s\n' "$path" >&2
        exit 1
    fi
    if [[ "$(git -C "$checkout" rev-parse --show-toplevel)" != "$checkout" ]]; then
        printf 'Direct dependency resolves to another repository: %s\n' "$path" >&2
        exit 1
    fi
    entry="$(git -C "$root" --literal-pathspecs ls-tree "$selected_root" -- "$path")"
    if [[ "$entry" != '160000 commit '* ]]; then
        printf 'Direct dependency has no recorded gitlink: %s\n' "$path" >&2
        exit 1
    fi
    expected="$(git -C "$root" rev-parse "$selected_root:$path")"
    if [[ "$(git -C "$checkout" rev-parse HEAD)" != "$expected" ]]; then
        printf 'Direct dependency differs from its recorded selection: %s\n' "$path" >&2
        exit 1
    fi
    git -C "$checkout" remote get-url origin > /dev/null
    paths+=("$checkout")
    heads+=("$expected")
    branches+=("$(git -C "$checkout" symbolic-ref --quiet HEAD || true)")
    statuses+=("$(git -C "$checkout" status --porcelain=v1 --untracked-files=all --ignore-submodules=all)")
done < "$declarations"

for index in "${!paths[@]}"; do
    checkout="${paths[$index]}"
    fetch_options=(--tags --no-recurse-submodules)
    if [[ "$(git -C "$checkout" rev-parse --is-shallow-repository)" = true ]]; then
        fetch_options+=(--unshallow)
    fi
    printf 'Preparing historical Git objects: %s\n' "${checkout#"$root/"}"
    GIT_TERMINAL_PROMPT=0 timeout --signal=TERM --kill-after=5s 120s \
        git -c submodule.recurse=false -c fetch.recurseSubmodules=false -C "$checkout" \
        fetch "${fetch_options[@]}" origin
    if [[ "$(git -C "$checkout" rev-parse --is-shallow-repository)" != false \
        || "$(git -C "$checkout" rev-parse HEAD)" != "${heads[$index]}" \
        || "$(git -C "$checkout" symbolic-ref --quiet HEAD || true)" != "${branches[$index]}" \
        || "$(git -C "$checkout" status --porcelain=v1 --untracked-files=all --ignore-submodules=all)" != "${statuses[$index]}" ]]; then
        printf 'History preparation did not preserve the complete selected checkout: %s\n' "$checkout" >&2
        exit 1
    fi
done
if [[ "$(git -C "$root" rev-parse HEAD)" != "$selected_root" ]]; then
    printf 'Root source selection changed during history preparation.\n' >&2
    exit 1
fi
