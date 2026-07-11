# Story 8.6 AC1 LLM Instructions

Use these instructions to unblock Story 8.6 AC1. The work is split into two
separate LLM assignments because the first belongs to the EventStore owner
surface and the second belongs to the Parties evidence matrix.

AC1 is not fixed by editing status text alone. It is fixed only when the Story
8.3 matrix row `EventStore projection/query SDK` records owner-approved proof
and no longer has status `needs-additive-api`.

## Instruction 1: EventStore Owner Proof Agent

Copy this prompt into an LLM session working in the Hexalith.EventStore owner
repository or in an explicitly approved EventStore owner worktree.

```text
You are the Hexalith.EventStore owner proof agent for Hexalith.Parties Story
8.6 AC1.

Goal:
Produce owner-approved additive API or already-available proof that the
EventStore projection/query SDK can safely replace Parties-local projection and
query mechanics.

Repository boundary:
- Work in Hexalith.EventStore, not in Hexalith.Parties production source.
- If you are launched from a Parties checkout, do not edit
  references/Hexalith.EventStore unless the user explicitly approved submodule
  owner work in that path.
- Do not run recursive submodule commands.
- Do not make breaking API changes.

Required proof items:
1. G3 read-model erasure hooks.
2. G10 index batching or an approved SDK equivalent.
3. G6 Parties freshness mapping for Current, Stale, Rebuilding, Degraded,
   Unavailable, and LocalOnly semantics.
4. Duplicate and out-of-order replay behavior.
5. Full rebuild verification against aggregate replay.
6. Cursor scope compatibility through IQueryCursorCodec and QueryCursorScope.
7. Current EventStore commit SHA intended for Parties consumption.

Workflow:
1. Read the repository instructions and EventStore project context before
   changing code.
2. Inspect these SDK surfaces and their tests:
   - IDomainProjectionHandler
   - IDomainQueryHandler
   - IReadModelStore
   - ReadModelWritePolicy
   - IQueryCursorCodec
   - QueryCursorScope
   - any domain-service projection/query registration APIs
3. For each required proof item, classify the result as:
   - already available
   - additive API/test added
   - blocked
4. If any required proof item is blocked, stop and report the exact missing API
   or behavior. Do not claim Story 8.6 AC1 is unblocked.
5. If additive code is needed, keep it generic in EventStore. Do not add
   Parties-specific domain logic to EventStore.
6. Add or identify tests proving the required behavior. Prefer focused unit or
   integration tests that can be cited directly by class and method name.
7. Run the focused validation commands first, then the broadest practical
   EventStore validation lane. If an environment blocks a broad lane, record the
   exact blocker and the focused evidence that did run.
8. Produce an owner proof packet.

Required proof packet format:
- EventStore commit SHA:
- Owner approval source:
  - PR:
  - reviewer:
  - approval date:
- Evidence by requirement:
  - G3 read-model erasure hooks:
    - source paths:
    - test paths:
    - validation command:
    - result:
  - G10 index batching or approved equivalent:
    - source paths:
    - test paths:
    - validation command:
    - result:
  - G6 freshness mapping:
    - source paths:
    - test paths:
    - validation command:
    - result:
  - duplicate/out-of-order replay:
    - source paths:
    - test paths:
    - validation command:
    - result:
  - full rebuild verification:
    - source paths:
    - test paths:
    - validation command:
    - result:
  - cursor scope compatibility:
    - source paths:
    - test paths:
    - validation command:
    - result:
- Rollback note:
- Known limitations:
- Final decision: available or still blocked

Acceptance gate:
Return `available` only if all required proof items are satisfied by reviewed
source and validation evidence. Otherwise return `still blocked`.
```

## Instruction 2: Parties Matrix Recorder Agent

Copy this prompt into an LLM session working in the Hexalith.Parties repository
after the EventStore proof packet exists.

```text
You are the Hexalith.Parties evidence recorder for Story 8.6 AC1.

Goal:
Update the Story 8.3 platform API prerequisite matrix so Story 8.6 may start
source migration only after the EventStore projection/query SDK proof is
complete and owner-approved.

Repository boundary:
- Work in Hexalith.Parties.
- Do not edit production source.
- Do not edit EventStore submodule contents.
- Do not initialize nested submodules and do not run recursive submodule
  commands.
- Preserve unrelated working-tree changes.

Preconditions:
- An EventStore owner proof packet exists.
- The proof packet decision is `available`, not `still blocked`.
- The checked-out references/Hexalith.EventStore pin matches the approved
  EventStore commit, or the user explicitly instructs you how to update the
  root-declared submodule pointer.

Workflow:
1. Read repository instructions, then load:
   - _bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md
   - _bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md
   - the EventStore owner proof packet
2. Run:
   - git -C references/Hexalith.EventStore rev-parse HEAD
   - rg -n -F "EventStore projection/query SDK" _bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md
3. Compare the checked-out EventStore SHA to the proof packet SHA.
4. If the SHA does not match and the user did not explicitly authorize a root
   submodule pointer update, stop. Do not edit the matrix row to `available`.
5. Verify the proof packet covers every AC1 item:
   - G3 read-model erasure hooks
   - G10 index batching or approved equivalent
   - G6 freshness mapping
   - duplicate/out-of-order replay
   - full rebuild verification
   - cursor scope compatibility
   - current EventStore pin
6. If any proof item is missing, stop and keep the row as `needs-additive-api`.
7. If every proof item is present, update only the
   `EventStore projection/query SDK` row in
   story-8-3-platform-api-prerequisite-matrix.md:
   - change Status from `needs-additive-api` to `available`
   - add the approved EventStore commit SHA
   - add concise evidence paths and test paths for each proof item
   - add validation commands and results
   - retain rollback language that local Parties projection/query mechanics
     stay until Story 8.6 parity harness and rebuild evidence are green
8. Do not mark Story 8.6 complete. At most, record in its Dev Agent Record that
   AC1 prerequisite evidence is now available and that the dev-story workflow may
   be resumed.
9. Run:
   - git diff --check
   - rg -n -F "EventStore projection/query SDK" _bmad-output/implementation-artifacts/story-8-3-platform-api-prerequisite-matrix.md
10. Report the exact files changed and the final row status.

Matrix row content template:

Status:
available

Proof required before migration:
Story 8.6 AC1 proof recorded on YYYY-MM-DD against
references/Hexalith.EventStore pin <sha>. Owner-approved evidence covers G3
read-model erasure hooks, G10 index batching or approved equivalent, G6 Parties
freshness mapping, duplicate/out-of-order replay, full rebuild verification, and
cursor scope compatibility. Evidence: <paths/tests/PR>. Story 8.6 must still
build its Parties parity harness before deleting local rollback paths.

Validation command or inspection used:
`git -C references/Hexalith.EventStore rev-parse HEAD` -> `<sha>`;
`<EventStore validation command>` -> PASS;
`<focused test command>` -> PASS;
`git diff --check` -> PASS.

Acceptance gate:
The row may become `available` only when every AC1 proof item is backed by the
owner proof packet and the checked-out EventStore SHA matches the recorded pin.
```

## Resume Condition For Story 8.6

Story 8.6 can resume only when:

- the matrix row `EventStore projection/query SDK` is `available`;
- the row records the approved EventStore pin;
- every AC1 proof item is mapped to evidence;
- rollback language remains intact; and
- no Parties production source was changed before the gate opened.

After that, run the normal `bmad-dev-story 8.6` workflow and start with the
parity harness before any production deletion.
