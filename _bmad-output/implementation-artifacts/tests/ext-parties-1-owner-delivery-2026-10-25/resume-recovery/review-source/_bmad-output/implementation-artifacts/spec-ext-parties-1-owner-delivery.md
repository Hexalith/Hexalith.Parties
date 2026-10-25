---
title: 'EXT-PARTIES-1 owner delivery'
type: 'feature'
created: '2026-10-25'
status: 'in-review'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'e523b06551ebdc69657ee78c045123dac19de6c8'
context:
  - '_bmad-output/planning-artifacts/actor-history-retention-policy-v1.md'
  - '_bmad-output/implementation-artifacts/spec-ext-parties-1-branch-b-authoritative-identity.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Agents Story 5.4 still cannot accept a replacement for `EXT-PARTIES-1`. Live always refuses, and the authenticated persisted P-01–P-10 proof, including ambiguity, cross-tenant denial, destruction, successor continuity, and recovery, is not an executed owner lane.

**Approach:** Finish the Parties-owned authority, history, and custody bindings and run that persisted verifier locally. Return local evidence, the exact remaining installation inputs, and a complete replacement packet coordinated to the Platform and EventStore checkouts used for that run. Leave Live qualification pending acceptance and those prerequisites.

## Boundaries & Constraints

**Always:** Preserve Branch B: the provisioned Party stays Organization-typed and is verified by immutable tenant and Party id. Retain human-binding history for 365 fixed days from `binding-effective-at` under `party-actor-retention-v1` / `party-actor-history-v1`, with exclusive expiry (`now < ExpiresAt`). Reuse the existing processor, query, fold, and custody seams. The packet records full commit ids from the Local run's Parties, EventStore, and Platform roots and labels them proposed coordination, not acceptance.

**Never:** Change the Agents register, invent an integration date, trust anchor, credential, or production custody backend, or register a test provider in production DI. Do not run Live, OQ-5.4-1, or a consuming Agents path. Do not treat Local or persisted evidence as Available. Do not alter Consumer onboarding, GDPR erasure, or the unrelated staged Parties files and dirty EventStore and Memories submodule worktrees.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Recovery | Lost acknowledgement, then restart | One original Party and binding survive | A second create or today's binding is rejected |
| Ambiguity | Gap, overlap, or conflicting revision | Typed fail-closed history result | No current-binding fallback |
| Cross-tenant denial | Other tenant, forged admission, or foreign id | Denial before protected lookup or effect | Foreign persisted state is unchanged |
| Destruction | Exclusive expiry reached | Reads deny; decryption capability is destroyed for every copy | Unknown acknowledgement stays pending |
| Successor continuity | Predecessor expired; approved actor-free successor | Later interval resolves only the successor | Expired predecessor cannot be relabeled or revived |
| Live gate | Any missing prerequisite or unaccepted target | Exit 1 and the precise missing-input list | Populated environment variables do not open Live |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Parties/Domain/PartyDomainProcessor.cs` — reuse custody admission. Do not add a provider.
- `src/Hexalith.Parties/Queries/PartyIdentityQueryService.cs` and `Domain/RetainedHumanActorHistoryFold.cs` — reuse typed ambiguity, denial, and successor behavior. `PartyDetail` is not authority.
- `src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs` — custody and the retained-history reader stay host-supplied.
- `eng/verify-ext-parties-1.ps1` — keep the Live refusal at lines 28–39. Add the persisted class to the owner lane.
- `tests/Hexalith.Parties.IntegrationTests/Identity/` — new persisted lane. Do not use the Aspire E2E profile or the dirty `references/Hexalith.EventStore` worktree.
- Platform `IdentityHistoryCleanup` and EventStore `IIdentityHistoryCustody` — no production implementation. Q1–Q7, D01–D04, and F1 remain installation inputs.

## Tasks & Acceptance

**Execution:**
- [x] `src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs` — bind retained-history reads and custody only through host-supplied registrations; missing registrations keep authoritative history and binding writes fail-closed.
- [x] `tests/Hexalith.Parties.IntegrationTests/Identity/PartyIdentityPersistedVerifierTests.cs` — authenticate and persist P-01–P-10, including the six matrix rows, across a restart of a disposable durable store. Use a test-only custody double that is not production DI.
- [x] `eng/verify-ext-parties-1.ps1` — execute that class in the owner lane and reject skips. Live still exits 1 and writes every missing installation input; environment presence is not qualification.
- [x] `_bmad-output/implementation-artifacts/tests/ext-parties-1-owner-delivery-2026-10-25/replacement-acceptance-packet.json` — write the nine register fields, the coordinated full commit ids, the executable Live command, and the remaining inputs. Date stays an unresolved owner field. Status stays proposed and not accepted.

**Acceptance Criteria:**
- Given a restarted durable store, when the persisted verifier runs, then recovery, ambiguity, cross-tenant denial, destruction, successor continuity, and the remaining P-01–P-10 vectors pass without a production provider.
- Given the Live entry point, when it runs with or without the twelve variables, then it exits 1 and qualification remains pending.
- Given the packet, when its pins are read, then they match the checkouts used for local evidence and do not replace `56cb4401c4179d431385ad3d931fefa402e4fc5a`.

## Implementation Notes

- `AddParties` resolves `IIdentityHistoryCustody` and `IRetainedIdentityHistoryReader` only through host `GetService`. Missing registrations leave custody and retained-history reads null, so binding writes return `CustodyUnavailable` and history reads return `HumanActorBindingOutcome.Unavailable`. No production or test provider is registered in `AddParties`.
- The persisted lane is a disposable directory plus the real processor, query, authority, and fold. `TestOnlyIdentityHistoryCustody` is registered only by the test host. It is not production DI. Persisted event bytes use EventStore replay options so command replay can read them.
- The owner script lane is `*PartyIdentityPersistedVerifierTests` and rejects failed, skipped, and not-run results. The Live refusal at lines 28–39 is unchanged.
- The replacement packet is proposed coordination. `TargetVersionOrCommit` stays `56cb4401c4179d431385ad3d931fefa402e4fc5a`. `TargetIntegrationDate` is null. `AcceptedStatus` is `proposed`. The Agents register was not edited.
- The full Local script exited 1 at `Hexalith.EventStore.Server.Tests` before the Parties lanes. That sibling failure was not bypassed. The persisted class was then executed with the script's resolved flags.

## Spec Change Log

- No change to the approved intent. Status stays `in-progress`. Live is not Available.

## Review Triage Log

- None.

## Verification

**Commands:**
- `pwsh -NoProfile -File eng/verify-ext-parties-1.ps1 -Mode Local -EventStoreRoot /home/administrator/projects/hexalith/eventstore -PlatformRoot /home/administrator/projects/hexalith/platform -EvidenceDirectory /tmp/ext-parties-1-owner-delivery-20261025` — exit 1. Stopped at `Hexalith.EventStore.Server.Tests` (`Total: 450, Errors: 0, Failed: 4, Skipped: 0, Not Run: 0`). Four failures are `StreamsControllerTests.NativeHeadGatewayCrossesRealWorkloadMiddlewareWithJwtBindingsOrExactSignedProof`: observation was null. The script did not reach the persisted class. The failure was not bypassed.
- Persisted class, same flags (`HexalithEventStoreVersion=3.127.0`, sibling EventStore root, evidence artifacts path): `dotnet /tmp/ext-parties-1-owner-delivery-20261025/artifacts/bin/Hexalith.Parties.IntegrationTests/debug/Hexalith.Parties.IntegrationTests.dll -class '*PartyIdentityPersistedVerifierTests'`. Exit 0. `Total: 11, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0, Time: 1.112s`. XML: 11 Pass. This is not Available.
- `pwsh -NoProfile -File eng/verify-ext-parties-1.ps1 -Mode Live -EvidenceDirectory /tmp/ext-parties-1-owner-delivery-20261025-live` — exit 1. Missing all twelve `EXT_PARTIES_*` inputs. Not qualification.
- Same Live entry point with all twelve variables set — exit 1. Evidence `/tmp/ext-parties-1-owner-delivery-20261025-live-present/live-gate.txt`: qualification remains blocked. Environment presence is not qualification.
