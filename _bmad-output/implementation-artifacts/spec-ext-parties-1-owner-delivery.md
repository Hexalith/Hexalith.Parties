---
title: 'EXT-PARTIES-1 owner delivery'
type: 'feature'
created: '2026-10-25'
status: 'draft'
route: 'dispatch'
review_loop_iteration: 1
baseline_commit: 'e523b06551ebdc69657ee78c045123dac19de6c8'
context:
  - '_bmad-output/planning-artifacts/actor-history-retention-policy-v1.md'
  - '_bmad-output/implementation-artifacts/spec-ext-parties-1-branch-b-authoritative-identity.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Agents Story 5.4 still cannot accept a replacement for `EXT-PARTIES-1`. Live always refuses, and the authenticated persisted P-01–P-10 proof, including ambiguity, cross-tenant denial, destruction, successor continuity, and recovery, is not an executed owner lane.

**Approach (user-directed resume, 2026-10-25):** Finish the Parties-owned delivery and replace refusal-only Live with complete authenticated persisted P-01–P-10 qualification. Bind real actor authority, retained-history custody, all-copy expiry, nonrollback restore and successor continuity. Return reviewed canonical source containing the delivery, a coordinated dependency target set, explicit owner integration date, exact executable command and actual installed hashes. Local test custody cannot substitute for those bindings. Execution requires acceptance of the exact target first.

**Decisions (owner answers, 2026-10-25):**
1. Installation: none — stop before Live.
2. Integration date: unresolved — do not invent one.

Both answers are absent. Stop before Live. Do not correct the reviewed local-proof limitations. Keep Live pending until the accepted target, installed hashes, and a new owner date exist. The local P-01–P-10 lane and the historical Local exit 1 are not Available.

## Boundaries & Constraints

**Always:** Preserve Branch B: the provisioned Party stays Organization-typed and is verified by immutable tenant and Party id. Retain human-binding history for 365 fixed days from original `binding-effective-at` under `party-actor-retention-v1` / `party-actor-history-v1`, with exclusive expiry (`now < ExpiresAt`). Reuse the existing processor, query, fold, and custody seams. Preserve historical failures and all recovery evidence. Distinguish observed source commits, reviewed delivery commits, accepted dependency targets and actual installed hashes.

**Never:** Change the Agents register, invent an owner integration date, trust anchor, credential, or qualified production custody backend, or register a test provider in production DI. Do not qualify an unaccepted target or execute a consuming Agents path. Do not treat Local or persisted evidence as Available. Do not alter Consumer onboarding, GDPR erasure, or the unrelated staged Parties files and dirty EventStore and Memories submodule worktrees.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Recovery | Lost acknowledgement, then restart | One original Party and binding survive | A second create or today's binding is rejected |
| Ambiguity | Gap, overlap, or conflicting revision | Typed fail-closed history result | No current-binding fallback |
| Cross-tenant denial | Other tenant, forged admission, or foreign id | Denial before protected lookup or effect | Foreign persisted state is unchanged |
| Destruction | Exclusive expiry reached | Reads deny; decryption capability is destroyed for every copy | Unknown acknowledgement stays pending |
| Successor continuity | Predecessor expired; approved actor-free successor | Later interval resolves only the successor | Expired predecessor cannot be relabeled or revived |
| Live qualification | Accepted exact source/dependency targets and complete authenticated installed bindings | Execute every P-01–P-10 persisted, restart, restore and fault obligation | Missing, mismatched or unaccepted bindings deny effects; incomplete/skipped probes fail |

</frozen-after-approval>

## Current custody direction — 2026-10-25

Following the user's instruction to apply the [feasibility recommendation](../../../platform/docs/implementation/ext-parties-1-custody-direction-2026-10-25.md), stop the custom Luna/FM path and further production backend implementation under the unchanged contract. Preserve existing source and local evidence; retain the host-supplied refusal behavior and pending Live work. OpenBao is a conditional candidate after an explicit guarantee amendment, not a replacement production provider. Q1–Q7, D01–D04, F1 and B01–B15 retain their obligations and unresolved bindings. No contract, target, operator, resource or delivery date is accepted here; the frozen intent and Agents register remain unchanged.

## Code Map

- `src/Hexalith.Parties/Domain/PartyDomainProcessor.cs` — reuse custody admission. Do not add a provider.
- `src/Hexalith.Parties/Queries/PartyIdentityQueryService.cs` and `Queries/RetainedHumanActorHistoryFold.cs` — reuse typed ambiguity, denial, and successor behavior. `PartyDetail` is not authority.
- `src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs` — custody and the retained-history reader stay host-supplied.
- `eng/verify-ext-parties-1.ps1` — replace the unconditional Live refusal only with the complete accepted installed protocol; bind exact clean source targets and installed input hashes before effects. Preserve the original Local lane and reject failed/skipped/not-run classes.
- `tests/Hexalith.Parties.IntegrationTests/Identity/` — new persisted lane. Do not use the Aspire E2E profile or the dirty `references/Hexalith.EventStore` worktree.
- Platform `IdentityHistoryCleanup` and EventStore `IIdentityHistoryCustody` — no production implementation found. Q1–Q7, D01–D04, F1 and B01–B15 remain unresolved. Platform's October 25 interaction-custody foundation explicitly excludes actor-history custody; it is not a substitute.

## Tasks & Acceptance

**Prior local work (retained; does not satisfy the resumed delivery):**
- [x] `src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs` — bind retained-history reads and custody only through host-supplied registrations; missing registrations keep authoritative history and binding writes fail-closed.
- [x] `tests/Hexalith.Parties.IntegrationTests/Identity/PartyIdentityPersistedVerifierTests.cs` — authenticate and persist P-01–P-10, including the six matrix rows, across a restart of a disposable durable store. Use a test-only custody double that is not production DI.
- [x] `eng/verify-ext-parties-1.ps1` — execute that class in the owner lane and reject skips. Live still exits 1 and writes every missing installation input; environment presence is not qualification.
- [x] `_bmad-output/implementation-artifacts/tests/ext-parties-1-owner-delivery-2026-10-25/replacement-acceptance-packet.json` — write the nine register fields, the coordinated full commit ids, the executable Live command, and the remaining inputs. Date stays an unresolved owner field. Status stays proposed and not accepted.

**Acceptance Criteria:**
- Given complete accepted installed bindings and exact source/dependency targets, when Live executes, then all original P-01–P-10 obligations pass through real authenticated authority, persisted history, all-copy expiry, nonrollback restore and approved successor continuity, with independent end-state evidence and no fixture substitution.
- Given missing or unaccepted target/bindings, when qualification is requested, then no target effect occurs and the precise missing inputs are retained. Environment presence alone grants no authority.
- Given reviewed canonical source containing the delivery, when the replacement packet is submitted, then it names its coordinated immutable target set, new owner integration date, exact executable command and actual installed hashes. Local evidence does not change the accepted register or Available status.

**Remaining execution:**
- [ ] Stopped. Both owner answers are absent: installation is `none — stop before Live`, and the integration date is `unresolved — do not invent one`. Do not execute Live, do not edit `tests/Hexalith.Parties.IntegrationTests/Identity/`, and do not present the local P-01–P-10 lane or the historical Local exit 1 as Available.
- [ ] Platform/EventStore owner source and both actual owning hosts — remain blocked until an owner-approved B01–B15 packet path exists. Do not invent a custody backend, trust anchor, or credential.
- [ ] `eng/verify-ext-parties-1.ps1` and approved installed owner executors — Live stays pending until the accepted target, installed hashes, and a new owner date exist.
- [ ] `tests/Hexalith.Parties.IntegrationTests/Identity/` — local-proof corrections stay unstarted on this stop. Test custody stays out of production DI and is not installed qualification.
- [ ] Replacement acceptance packet — keep the date unresolved. Do not invent a commitment, and do not change the Agents register.
- [x] Original Local prerequisite lane — rerun unchanged against the latest EventStore fixture correction; retain its historical failure, current recovery and next blocker separately. The historical Local exit 1 is not Available.

## Implementation Notes

- `AddParties` resolves `IIdentityHistoryCustody` and `IRetainedIdentityHistoryReader` only through host `GetService`. Missing registrations leave custody and retained-history reads null, so binding writes return `CustodyUnavailable` and history reads return `HumanActorBindingOutcome.Unavailable`. No production or test provider is registered in `AddParties`.
- The persisted lane is a disposable directory plus the real processor, query, authority, and fold. `TestOnlyIdentityHistoryCustody` is registered only by the test host. It is not production DI. Persisted event bytes use EventStore replay options so command replay can read them.
- The owner script lane is `*PartyIdentityPersistedVerifierTests` and rejects failed, skipped, and not-run results. The Live refusal at lines 28–39 is unchanged.
- The replacement packet is proposed coordination. `TargetVersionOrCommit` stays `56cb4401c4179d431385ad3d931fefa402e4fc5a`. `TargetIntegrationDate` is null. `AcceptedStatus` is `proposed`. The Agents register was not edited.
- The full Local script exited 1 at `Hexalith.EventStore.Server.Tests` before the Parties lanes. That sibling failure was not bypassed. The persisted class was then executed with the script's resolved flags.

## Spec Change Log

- No change to the approved intent. Status stays `in-progress`. Live is not Available.
- 2026-10-25 resume: the user explicitly replaced the local-only scope with complete real installed P-01–P-10 delivery. Review found the local proof limitations above. Updated approach, command map and acceptance prevent presenting local test custody, source HEAD observations or historical refusal commands as that delivery. KEEP Branch B, immutable identity, original 365-day exclusive expiry, host-supplied seam refusal and every prior evidence artifact. No code changes from this resume needed reverting; pre-existing user work was preserved. Open real installation/date inputs prevent ready-for-dev; status returns to draft. Full review evidence is retained in `tests/ext-parties-1-owner-delivery-2026-10-25/resume-recovery/review-triage.json`.
- 2026-10-25 owner answers: Installation is `none — stop before Live`. Integration date is `unresolved — do not invent one`. Open Questions removed. No Identity test edits and no Live run. Branch B, immutable tenant/Party identity, and 365 fixed days from `binding-effective-at` with exclusive expiry stay in force. The Agents register, production DI, and the test custody double were not changed. The local P-01–P-10 lane and the historical Local exit 1 stay unavailable. Status stays `draft`.

## Review Triage Log

Independent layers completed against the retained baseline-wide diff. Each finding is recorded below before grouping. GDPR rows concern concurrent work outside this owner delivery.

| Finding | Verdict | Route | Verified evidence |
| --- | --- | --- | --- |
| BH-01 | medium | bad_spec | UnprotectEventAsync/Open decrypts while key.bin exists without checking clock or persisted lifecycle; CanReadAsync denies at expiry only later. This invalidates an inference of cryptographic expiry from query denial. Test-only, not production custody. |
| BH-02 | medium | bad_spec | The test custody ignores AggregateIdentity and Seal uses no owner/event-type authenticated data. It cannot verify tenant-bound custody or reject foreign evidence at that seam. |
| BH-03 | medium | bad_spec | AdmitAsync creates key.bin when absent and writes Pending=false/Destroyed=false even for a retired unit. The new random key does not decrypt old ciphertext, but retirement and identity cannot be claimed irreversible. |
| BH-04 | medium | bad_spec | DestroyExpiredAsync wipes keys/copy files before writing Pending. The retained source accepts !IsPending plus unreadable markers, so a crash before status persistence can mint a terminal certificate without acknowledged destruction. No production crash guarantee is shown. |
| BH-05 | medium | bad_spec | P09 uses a second Established with expected version zero at start+30 days while observation remains start. Future-time/duplicate-establishment rejection prevents this fixture isolating overlap handling. |
| BH-06 | medium | bad_spec | Lane.Dispatch calls Source.CommandState before processor admission; that disk read is absent from authoritative/retained counters. P04 does not prove denial before every protected source lookup. |
| BH-07 | medium | bad_spec | Lane.Build replaces IEventPayloadProtectionService with a substitute and Commit calls custody directly. The local lane does not exercise the production payload adapter-to-custody route. |
| BH-08 | medium | bad_spec | ProtectSnapshotAsync returns its input and CommandState supplies a snapshot-free tail. Named copy files do not test actual snapshot, cache or backup replay/destruction. |
| BH-09 | medium | bad_spec | Retained ReadAsync ignores ReadFault and Restamp refreshes observation/certificate times. Current-source fault tests cannot establish historical stale/future/gap/outage rejection. |
| BH-10 | medium | bad_spec | The original packet pins HEAD while the test/DI implementation is uncommitted. This resume now retains selected source snapshots, diff, XML/build receipts and 95 local DLL hashes; these improve local attribution but do not supply a complete canonical or installed target. |
| EC-01 | medium | bad_spec | Duplicate root cause of BH-01: at exclusive expiry a remaining key still permits Open to decrypt. Both findings are retained individually; no installed cryptographic-expiry claim is accepted. |
| VG-01 | medium | defer | Pre-verified concurrent GDPR gap: ErasureRetiresSdkPrivateCanonicalManifestAfterCrashBeforeFirstParticipantAsync constructs DiscoverableRecovery=true itself; no erasure case covers a handler-generated pre-marker manifest. Preserve those unrelated changes; return to their owner. |
| VG-02 | medium | defer | Pre-verified concurrent GDPR gap: the processing-page HTTP malformed-envelope cases have matching tenants/parties, so removing the foreign-record guards would leave them passing. Preserve those unrelated changes; return to their owner. |

## Verification

**Commands:**
- `pwsh -NoProfile -File eng/verify-ext-parties-1.ps1 -Mode Local -EventStoreRoot /home/administrator/projects/hexalith/eventstore -PlatformRoot /home/administrator/projects/hexalith/platform -EvidenceDirectory /tmp/ext-parties-1-owner-delivery-20261025` — exit 1. Stopped at `Hexalith.EventStore.Server.Tests` (`Total: 450, Errors: 0, Failed: 4, Skipped: 0, Not Run: 0`). Four failures are `StreamsControllerTests.NativeHeadGatewayCrossesRealWorkloadMiddlewareWithJwtBindingsOrExactSignedProof`: observation was null. The script did not reach the persisted class. The failure was not bypassed.
- Persisted class, same flags (`HexalithEventStoreVersion=3.127.0`, sibling EventStore root, evidence artifacts path): `dotnet /tmp/ext-parties-1-owner-delivery-20261025/artifacts/bin/Hexalith.Parties.IntegrationTests/debug/Hexalith.Parties.IntegrationTests.dll -class '*PartyIdentityPersistedVerifierTests'`. Exit 0. `Total: 11, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0, Time: 1.112s`. XML: 11 Pass. This is not Available.
- `pwsh -NoProfile -File eng/verify-ext-parties-1.ps1 -Mode Live -EvidenceDirectory /tmp/ext-parties-1-owner-delivery-20261025-live` — exit 1. Missing all twelve `EXT_PARTIES_*` inputs. Not qualification.
- Same Live entry point with all twelve variables set — exit 1. Evidence `/tmp/ext-parties-1-owner-delivery-20261025-live-present/live-gate.txt`: qualification remains blocked. Environment presence is not qualification.


### Resume verification — 2026-10-25

- Original Local command rerun unchanged: `pwsh -NoProfile -File eng/verify-ext-parties-1.ps1 -Mode Local -EventStoreRoot /home/administrator/projects/hexalith/eventstore -PlatformRoot /home/administrator/projects/hexalith/platform -EvidenceDirectory /tmp/ext-parties-1-owner-recovery-20261025-r1`. Exit **1**, now at `ContractsPublicApiSnapshotTests.PublicContractSurface_MatchesCheckedInSnapshot`. Six prior lanes passed **1,079/1,079**; the failing Contracts lane passed 28 and failed 1. The checked-in snapshot omits existing `ProcessingActivityPage` and `GetProcessingRecords`; no snapshot or unrelated GDPR source was modified.
- EventStore source `7012502bc094484eb2d4687badb98cb3b5e52b73` includes fixture correction `03ef9f156c5590a81c5409fde601952ebfb58c21`. Its Server lane passes **450/450**, including all four formerly failing `NativeHeadGatewayCrossesRealWorkloadMiddlewareWithJwtBindingsOrExactSignedProof` cases. The original **446 pass / 4 fail** XML remains retained, with original-file hash checks; this recovery does not erase it.
- Platform source observed at `90aef9ac3e813d9cae5b16d68ca5e326ce0fb599`; its Identity and Custody lanes passed 5/5 and 171/171. These are source observations, not accepted dependency targets. Parties remains `9c116910f9d712b82ae27ceb1946605d76c8a451` with uncommitted delivery work.
- Focused fallback: build `tests/Hexalith.Parties.IntegrationTests/Hexalith.Parties.IntegrationTests.csproj` using the Local graph/Debug flags, then `dotnet /tmp/ext-parties-1-owner-recovery-20261025-r1/artifacts/bin/Hexalith.Parties.IntegrationTests/debug/Hexalith.Parties.IntegrationTests.dll -class '*PartyIdentityPersistedVerifierTests' -result-xml /tmp/ext-parties-1-owner-recovery-20261025-r1/focused-persisted-tests.xml`. Build/test exit **0**; **11 pass**, zero failed/skipped/not-run. This separate test-only lane has the reviewed limitations above and does not complete Live or the failed Local command.
- Fresh read-only workload metadata: 172 workload objects, no Parties-name match. All twelve Live environment inputs are absent; `/home/administrator/hexalith-parties-evidence` does not exist. Other names/contexts are not excluded. No Live, LiveReadiness, consumer, credential retrieval, installation or Git mutation ran.
- Retained packet: `tests/ext-parties-1-owner-delivery-2026-10-25/resume-recovery/resume-result.json`. It records exact commands, selected source snapshots and local artifact/receipt hashes. Installed hashes, replacement canonical delivery target, qualifying command and integration date remain null because no actual accepted bindings exist. Neither the historical replacement packet nor the Agents register was changed.
