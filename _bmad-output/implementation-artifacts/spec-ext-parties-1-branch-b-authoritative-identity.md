---
title: 'EXT-PARTIES-1 Branch B authoritative identity'
type: 'feature'
created: '2026-10-03'
status: 'draft'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '_bmad-output/implementation-artifacts/ext-parties-1-implementation-notes.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Agents cannot prove its immutable Party or a human approver's current and historical identity. Generic creation retries ignore divergent intent, client scope is ambient, and identity reads lack authoritative evidence.

**Approach:** Deliver the complete Parties-owned Branch B provisioning, identity and actor-attribution contract through EventStore, with explicit client scope, pure durable history, fail-closed authorization and an executable compatibility verifier.

## Boundaries & Constraints

**Always:** Preserve Organization Party IDs and Consumer onboarding. Keep issuer/subject mappings outside Party streams; store only opaque actor attribution/version/interval/provenance. Verify authenticated tenant, operation and exact target before protected reads or mutation. Use EventStore SDK persistence and shared verified-source reads; reject unsupported authority/currentness. Preserve exact provisioning identity and first result on retry; inactive/restricted/erasing/erased Parties cannot become eligible. Person is the positive Human classification; Organization flags do not confer it.

**Never:** Create replacement identities, infer legacy ownership, trust JWT roles/unsigned extensions/projection age as current authority, use PartyDetail as an identity result, persist tokens/subjects/PII, add direct Parties APIs/actors/state stores, change unrelated GDPR behavior, or promote EXT-PARTIES-1 from fixtures. Consumers and launch gates retain their existing dependency requirements.

## I/O & Edge-Case Matrix

| Scenario | Input/state | Result | Failure |
| --- | --- | --- | --- |
| Provision | Verified service; exact tenant/Agent/Party intent | One immutable creation/marker; retry preserves result | Changed intent/occupied unmarked ID conflicts |
| Current identity | Exact scope; complete current source and actor evidence | Safe classification/liveness/binding basis | Missing/gapped/stale/ambiguous/unavailable blocks |
| History | Binding X before T, Y from T | Before T resolves X; at T resolves Y after replay | Gaps/overlaps/version or actor mismatch block |
| Isolation | Wrong service/tenant/target; concurrent scoped calls | Deny before protected lookup/effect | No foreign identity/existence/PII disclosure |

</frozen-after-approval>

## Open Questions

1. Stable actor authority: use a new Platform-owned issuer/continuity contract, or supply an existing accepted service contract? Neither subject claims nor current code prove alias/login-rotation continuity.
2. Binding administration: restrict establish/revoke/rebind to the Platform identity service, or permit tenant operators under an explicit delegation/verification policy? Consumer TenantOwner authority alone cannot mint global actors.
3. History policy: retain approved opaque attribution after erasure for a specified horizon, or require an approved external protected history authority? Supply duration, retained fields and erasure/restore rules; no indefinite retention is assumed.

## Code Map

- `src/Hexalith.Parties/Domain/PartyAggregate.cs` and `src/Hexalith.Parties.Contracts/State/PartyState.cs` — reuse pure folds; non-null rejection state is not creation; Apply's wall clock is not history.
- `src/Hexalith.Parties/Domain/PartyDomainProcessor.cs` — add verified identity admission before unprotection; preserve gateway RBAC ownership and erasure processing.
- `src/Hexalith.Parties/Queries/PartySdkQueryService.cs` — preserve ordinary stale-cache reads; separate authority queries from timestamp-based freshness.
- `src/Hexalith.Parties.Client/HttpPartiesCommandClient.cs` — preserve existing clients; add a narrow identity client with explicit immutable per-call tenant and retry identity.

## Tasks & Acceptance

**Execution:**
- [ ] `references/Hexalith.EventStore/src/Hexalith.EventStore.Client/Streams/IAuthoritativeEventStreamReader.cs` — add the missing shared scoped complete-prefix/head evidence seam and its owner implementation/tests; do not reimplement it in Parties.
- [ ] `src/Hexalith.Parties.Contracts/Commands/ProvisionAgentParty.cs`, `Events/AgentPartyProvisioned.cs`, and `Models/PartyIdentityEvidence.cs` — add closed safe provision/current/history results, binding commands/events and versioned ID mapping; one type per file. Companion supplies the full file map.
- [ ] `src/Hexalith.Parties.Contracts/State/PartyState.cs` and `src/Hexalith.Parties/Domain/PartyAggregate.cs` — fold creation provenance and immutable intervals; enforce exact retry/conflict, rejection-only recovery and generic/composite bypass denial.
- [ ] `src/Hexalith.Parties/Domain/PartyDomainProcessor.cs` and `Authorization/IPartyIdentityAuthority.cs` — enforce verified source/actor/target admission; missing authority/profile denies; add validators and composition.
- [ ] `src/Hexalith.Parties/Queries/PartyIdentityQueryHandler.cs` and `HumanActorBindingAtQueryHandler.cs` — require complete source evidence, current human/liveness and exact historical basis; expose no PII or stale fallback.
- [ ] `src/Hexalith.Parties.Client/Abstractions/IPartiesIdentityClient.cs` and `HttpPartiesIdentityClient.cs` — add explicit-scope gateway methods and stable logical retry IDs; validate returned scope/identity.
- [ ] `_bmad-output/planning-artifacts/adr-consumer-party-id-binding.md` and `tests/Hexalith.Parties.UI.Tests/IdentityBindingBoundaryTests.cs` — document opaque attribution extension while preserving private login mapping and existing Consumer behavior.
- [ ] `eng/verify-ext-parties-1.ps1` and the companion's owning test files — cover P-01–P-10, persisted recovery, authorization, isolation and wire compatibility; qualification fails on missing authority, targets or skipped required lanes.

**Acceptance Criteria:**
- Given lost acknowledgement/concurrent retries, when provisioning recovers, then one original Party/result survives and no alternate generic create can replace it.
- Given revoked, non-human or uncertain evidence, when current identity is evaluated, then eligibility blocks before effects while ordinary Consumer/UI behavior stays intact.
- Given binding transitions and restart/restore, when action-time identity is resolved, then the unique immutable actor/version/interval is reproduced without current-binding substitution.
- Given tenant collisions and forged admission, when every identity surface runs, then foreign persisted state and public existence/count/error/log outputs remain unchanged.
- Given complete installed owner targets, when the live verifier runs, then P-01–P-10 and persisted end states pass; mocks or partial runs never establish record availability.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Design Notes

One goal spans Contracts/domain/SDK/client/tests. No deployment, production mutation or irreversible migration is proposed. New wire names are implementation proposals; launch acceptance remains separate. Legacy unmarked IDs fail closed pending an approved same-ID provenance procedure.

## Verification

- Planned `pwsh -NoProfile -File eng/verify-ext-parties-1.ps1` — individual Debug/source builds/suites; distinct live qualification with persisted-state evidence.
- `git diff --check` — document/source whitespace. This run performs planning validation only.
