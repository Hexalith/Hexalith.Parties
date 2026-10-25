# EXT-PARTIES-1 resumed owner delivery — 2026-10-25

**Incomplete: no complete canonical replacement target or installed qualification is delivered.** Branch B, immutable tenant/Party identity, and the accepted 365-day exclusive expiry remain unchanged. No Live, LiveReadiness, Agents consumer, installation, credential retrieval or Git mutation was performed.

The [result](resume-result.json) keeps acceptance, availability and Live qualification false. Actual installed hashes, new owner integration date, qualifying Live command and canonical commit containing the complete delivery remain unresolved. The existing proposed packet and Agents register were preserved. Observed checkout HEADs below cannot replace their accepted targets.

| Checkout | Observed full HEAD | Meaning |
| --- | --- | --- |
| Parties | `9c116910f9d712b82ae27ceb1946605d76c8a451` | Existing HEAD; delivery source is uncommitted |
| EventStore | `7012502bc094484eb2d4687badb98cb3b5e52b73` | Includes fixture correction `03ef9f156c5590a81c5409fde601952ebfb58c21` |
| Platform | `90aef9ac3e813d9cae5b16d68ca5e326ce0fb599` | Local source tested; history custody backend still absent |

## Original Local lane and recovery

From `/home/administrator/projects/hexalith/parties`:

```bash
pwsh -NoProfile -File eng/verify-ext-parties-1.ps1 -Mode Local -EventStoreRoot /home/administrator/projects/hexalith/eventstore -PlatformRoot /home/administrator/projects/hexalith/platform -EvidenceDirectory /tmp/ext-parties-1-owner-recovery-20261025-r1
```

Actual exit **1**. Six completed lanes pass **1,079/1,079**: EventStore Contracts 37, Client 381, Server 450, PayloadProtection 35; Platform Identity 5, Custody 171. The original Server failure has recovered: all four `NativeHeadGatewayCrossesRealWorkloadMiddlewareWithJwtBindingsOrExactSignedProof` cases now pass. The [historical preservation record](historical-preservation.json) binds the retained original 446-pass/4-fail XML and both original Live refusals; their source files were not overwritten.

The rerun stopped at `ContractsPublicApiSnapshotTests.PublicContractSurface_MatchesCheckedInSnapshot`: 28 pass, 1 fail. Its [exact delta](recovery-local/public-api-mismatch.diff) shows existing `ProcessingActivityPage` and `GetProcessingRecords` omitted from the snapshot. Those concurrent GDPR/API changes were preserved and no snapshot gate was weakened. [Raw current logs, XML and manifest](recovery-local/local-result.json) remain separate from the historical failure.

The required focused fallback separately built and ran the existing persisted class: **11/11 pass**, zero skips/not-run, with a clean Debug build. [Exact build/test commands](resume-result.json) and [raw output](recovery-local/focused-persisted-tests.log) are retained. This does not turn the full Local exit into success or qualify installed P-01–P-10.

An initial PowerShell reflection diagnostic failed with `PSInvalidOperationException: There is no Runspace available to run scripts in this thread`. A C# assembly resolver hosted by PowerShell then read the existing private snapshot generator successfully; the [successful diagnostic script](recovery-local/inspect-public-api.ps1) and generated API text are retained. Neither diagnostic updated the snapshot.

## Review and remaining prerequisites

All three build review layers completed. [Thirteen individually triaged findings](review-triage.json) identify test-only expiry/lifecycle/tenant-binding weaknesses, a crash-to-certificate acknowledgement gap, inadequate overlap/fault/lookup assertions, bypassed production protection/snapshot routes and local source attribution. Two findings belong to concurrent GDPR work; its staged/unstaged ledger and source were preserved. The resumed spec records every finding and keeps the full installed delivery open. No review approval or defect-free delivery is claimed.

Selected reviewed source snapshots, the reviewed diff and **95 local DLL hashes** support local provenance only. [Local source/artifact hashes](local-source-and-artifact-hashes.json) are explicitly distinct from installed configuration, plan, trust and target hashes.

The [prerequisite observation](recovery-local/source-and-prerequisite-observation.json) retains all **661** decision null paths and **165** command-binding null paths. Every B01–B15 executor still lacks approved exact argv/authority/target/receipt binding. The newer Platform interaction-custody source dispositions explicitly exclude actor-history custody. No registered production `IIdentityHistoryCustody` implementation was found. Original-unit/revocation mapping, independently current actor/lifecycle/time authority, C01–C16 all-copy/key-backup destruction, real isolated restore, fault/lost-ack targets and approved actor-free successor proofs remain required.

A fresh read-only cluster metadata query returned 172 workload objects with no Parties-name matches. All twelve Live inputs are absent and `/home/administrator/hexalith-parties-evidence` is absent. Other names or contexts are not excluded. This observation supplies no installed trust or qualification.

The accountable owner must supply the newer real installation/command-binding packet and new integration date, or the missing owning services/backend must first be delivered and qualified. An exact complete replacement target set and command can be accepted only after they exist. No date, identity, credential, installed hash or physical custody guarantee was invented.
