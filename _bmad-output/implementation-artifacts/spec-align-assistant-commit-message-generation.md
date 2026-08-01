---
title: 'Align assistant commit message generation with commitlint'
type: 'bugfix'
created: '2026-08-01'
status: 'done'
review_loop_iteration: 0
baseline_commit: '549dac1f1c218ce9b02235d9f20f52961ba06aff'
context:
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-git-instructions.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Claude, Codex, GitHub Copilot, and Visual Studio receive only a generic Conventional Commits reminder, so generated messages can still violate the repository's commitlint contract or Hexalith's stricter Git policy.

**Approach:** Add one concise, self-contained commit-generation contract to the three synchronized assistant entry points, clarify the same rule in persistent BMAD project context, and guard the contract with focused CI tests. The Copilot entry point also governs current Visual Studio AI commit generation.

## Boundaries & Constraints

**Always:** Keep `AGENTS.md`, `CLAUDE.md`, and `.github/copilot-instructions.md` byte-equivalent; require generators to inspect the active repository config, use `<type>[optional scope][!]: <description>`, honor configured limits plus stricter Hexalith rules, reject ignored/default-shaped subjects, and validate the exact candidate with repository-pinned commitlint when tooling is available. Preserve the existing `references/Hexalith.Memories` drift and concurrent AppHost, host, and fitness-test edits without including them.

**Ask First:** Any change to `commitlint.config.mjs`, semantic-release behavior, Git hooks, user/IDE settings, or shared instructions inside a `references/` submodule.

**Never:** Add a separate Visual Studio policy file, permit `chore`, weaken commitlint, claim validation ran when it did not, bypass hooks, stage or commit changes, or modify unrelated working-tree files.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Valid generation | Documentation-only change | A concise message such as `docs: align commit guidance` passes pinned commitlint and Hexalith policy | Revise until validation passes |
| Invalid conventional shape | Plain-English `Update commit guidance` or uppercase/trailing-period subject | Generator replaces it with a valid typed, lowercase-imperative subject | Never present the invalid candidate as compliant |
| Commitlint-ignored default | Merge, fixup, squash, version-only, or Git-generated revert subject | Generator rejects the default and emits a real Conventional Commit header; intentional reverts use `revert: ...` | Do not treat an ignored exit-success result as policy compliance |
| Tooling unavailable | Repository-pinned commitlint cannot run | Generator applies the documented rules and explicitly reports validation as not run | Never claim tool-verified compliance |

</frozen-after-approval>

## Code Map

- `AGENTS.md` -- Codex repository instructions and normalized baseline source.
- `CLAUDE.md` -- Claude entry point; must remain byte-equivalent to `AGENTS.md`.
- `.github/copilot-instructions.md` -- GitHub Copilot and Visual Studio repository instructions; must remain byte-equivalent.
- `_bmad-output/project-context.md` -- persistent BMAD facts currently containing stale `chore` guidance.
- `tests/Hexalith.Parties.Ci.Tests/AssistantCommitMessageInstructionsTests.cs` -- focused synchronization and policy regression guard.
- `commitlint.config.mjs` -- authoritative active lint configuration; inspection-only for this change.

## Tasks & Acceptance

**Execution:**
- [x] `AGENTS.md`, `CLAUDE.md`, `.github/copilot-instructions.md` -- add identical commit-message generation rules covering syntax, type selection, casing, lengths, breaking changes, ignored defaults, and exact-message validation.
- [x] `_bmad-output/project-context.md` -- replace the stale `chore` example with commitlint-aware Hexalith guidance so quick-dev planning cannot reintroduce the conflict.
- [x] `tests/Hexalith.Parties.Ci.Tests/AssistantCommitMessageInstructionsTests.cs` -- assert all three entry points are identical and contain the essential generation/validation contract.

**Acceptance Criteria:**
- Given any of the four supported assistant surfaces, when it generates or suggests a commit message, then the loaded repository instructions require a valid Conventional Commit that satisfies commitlint and stricter Hexalith policy.
- Given Visual Studio AI commit generation, when repository instructions are loaded, then `.github/copilot-instructions.md` supplies the same contract without a separate VS-only file.
- Given future edits to one assistant entry point or removal of a critical rule, when the focused CI tests run, then they fail with a clear synchronization or policy assertion.
- Given the existing dirty tree, when implementation completes, then only the approved guidance, context, spec, and focused test files appear in this task's diff.

## Spec Change Log

## Design Notes

Stock `@commitlint/config-conventional` accepts `chore`, headers up to 100 characters, and several ignored default subjects. Hexalith deliberately narrows that contract: never use `chore`, prefer subjects near 50 characters and body lines near 72, and reject ignored defaults even if commitlint exits successfully. Generated guidance must require both layers, not describe stock commitlint as the whole policy.

## Verification

**Commands:**
- `cmp -s AGENTS.md CLAUDE.md && cmp -s AGENTS.md .github/copilot-instructions.md` -- expected: all synchronized entry points are byte-equivalent.
- `printf '%s\n' 'docs: align commit guidance' | npx --no-install commitlint --verbose` -- expected: candidate passes the repository-pinned configuration.
- `dotnet test tests/Hexalith.Parties.Ci.Tests/Hexalith.Parties.Ci.Tests.csproj --configuration Release` -- expected: focused CI test project passes with zero warnings or errors.
- `git -c core.whitespace=cr-at-eol diff --check -- AGENTS.md CLAUDE.md .github/copilot-instructions.md _bmad-output/project-context.md tests/Hexalith.Parties.Ci.Tests/AssistantCommitMessageInstructionsTests.cs` -- expected: no whitespace errors while honoring the repository-required CRLF endings.

## Suggested Review Order

**Generation contract**

- Start with the canonical syntax, policy precedence, and validation behavior.
  [`AGENTS.md:59`](../../AGENTS.md#L59)

- Confirm Visual Studio and GitHub Copilot receive the identical contract.
  [`copilot-instructions.md:59`](../../.github/copilot-instructions.md#L59)

- Confirm Claude receives the same normalized baseline without divergence.
  [`CLAUDE.md:59`](../../CLAUDE.md#L59)

**Persistent workflow context**

- Verify BMAD planning retains the stricter commit-generation policy.
  [`project-context.md:222`](../project-context.md#L222)

**Regression coverage**

- Review byte-equivalence and complete contract assertions first.
  [`AssistantCommitMessageInstructionsTests.cs:8`](../../tests/Hexalith.Parties.Ci.Tests/AssistantCommitMessageInstructionsTests.cs#L8)

- Trace valid, invalid, ignored-default, and unavailable-tooling matrix coverage.
  [`AssistantCommitMessageInstructionsTests.cs:43`](../../tests/Hexalith.Parties.Ci.Tests/AssistantCommitMessageInstructionsTests.cs#L43)

- Confirm persistent BMAD context receives equally strong regression protection.
  [`AssistantCommitMessageInstructionsTests.cs:94`](../../tests/Hexalith.Parties.Ci.Tests/AssistantCommitMessageInstructionsTests.cs#L94)

**Review follow-ups**

- Keep unrelated ACL and branch-policy findings outside this patch.
  [`deferred-work.md:58`](deferred-work.md#L58)
