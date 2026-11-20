---
title: Sprint change proposal — production key custody, erasure and release qualification
date: 2026-11-20
workflow: bmad-correct-course
mode: batch
scope_classification: moderate
status: applied
audited_commit: f907ff97de4f129ad8897976e6b1fa0c361baf68
---

# Sprint change proposal — 20 November 2026

## Issue and decision

The technical audit found that the default Parties host keeps encryption keys in process memory. An accepted erasure reaches a pending state, but the audit found no owner for the first destruction step in the inspected sources. A destruction certificate also leaves open whether a second replica can still serve a cached key. On the same revision, CI stopped before package integration because two quick starts named an obsolete tool version. [Key registration](../../src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs) · [Erasure command processing](../../src/Hexalith.Parties/Domain/PartyDomainProcessor.cs) · [Key cache](../../src/Hexalith.Parties.Security/CachedPartyKeyManagementService.cs) · [Hosted CI](https://github.com/Hexalith/Hexalith.Parties/actions/runs/44709603237/job/136197645223)

The user selected “Apply the sprint changes and fix the documented tool version.” The product owner and maintainers can use the eight actions below to schedule implementation. Production key custody and initial-worker ownership remain open questions for deployment owners.

## Planning changes applied

| Artifact | Before | Applied change and reason |
|---|---|---|
| [PRD deployment gates](parties-ui-prd.md#deployment-gates), Story 1.10 | Either a startup guard or provider registration could qualify GATE-KMS. | Keep the gate unverified until the host refuses ephemeral keys outside development **and** the selected provider passes restart and replica replay. Either check alone leaves a data-loss scenario untested. |
| [Sprint tracker](../implementation-artifacts/sprint-status.yaml) | The open October erasure action covered failed event append and retry. It began after the initial request. The other audit items had no common qualification plan. | Extend the same action upstream to the first request and downstream to redacted reads. Add seven actions with owners and completion conditions. Preserve the historical evidence for completed stories and the three blocked story statuses. |
| [README](../../README.md#quick-start) and [getting-started guide](../../docs/getting-started.md) | Both identified module tool 6.3.2. The manifest and the test expected 6.3.4. | Both guides now match the manifest. Upstream commit `f1b4c075` updated the manifest and both guides to 6.4.4 before this sprint change was published. Future module-tool upgrades should update both guides in the same change. The existing test reads the manifest. |
| [Architecture](architecture.md) and [UX flows](ux-designs/ux-parties-2026-06-09/EXPERIENCE.md) | The architecture assigns the gateway and server-side sign-in responsibilities. Consumer privacy distinguishes pending, cancellable, and completed erasure. | Carry those existing decisions into the relay and erasure acceptance checks. The UI can continue to show pending after request acceptance. Terminal evidence belongs to the committed lifecycle and verification record. |

This is a maintenance correction. The canonical epic inventory, MVP requirements and UX journeys continue to apply. Story 8.7 retains its EventStore G5 prerequisite and local crypto rollback path. Stories 8.8 and 8.9 retain their owner prerequisites. Rolling back the gateway migration would reopen completed work and would still leave key custody unresolved. Reducing the MVP would remove privacy behavior already promised. [Epic inventory](epics.md) · [Crypto retention decision](sprint-change-proposal-2026-07-07-crypto-key-management-retention.md)

## Implementation handoff

The action IDs resolve in the sprint tracker. Owners below are the roles responsible for obtaining evidence. Record the tested revision, provider or workflow identity, and redacted results when closing an action.

| Order and tracker action | Owner and completion evidence |
|---|---|
| 1. `audit-package-integration` | **Parties and release.** The pin correction passes locally. Close after hosted CI on the corrected revision restores and builds packages, runs the real create/query test, verifies persisted events and completes cleanup. Use the [existing integration test](../../tests/Hexalith.Parties.Platform.IntegrationTests/PartyEnvironmentTests.cs). |
| 2. `audit-key-custody` | **Parties and deployment/Platform.** Inventory the backend actually selected in each intended environment. Make startup refuse ephemeral custody outside development. With a durable provider, create a synthetic protected record and prove read and replay after restart and from a second process. Inventory affected deployments before rolling out the guard. |
| 3. `epic-8-retro-erasure-status-after-commit` | **Parties domain, architect and test owner, with the deployment owner.** Identify the deployed subscriber, subscription and retry owner. If it is absent, assign one durable, idempotent consumer. One gateway request must progress through key destruction, persisted certification, cleanup, terminal events and redacted reads. Cover failed append, restart, redelivery and cancellation before destruction. Monitor the age of pending requests against the owner’s operating threshold. [Original action](../implementation-artifacts/epic-8-retro-2026-10-09.md#action-items) |
| 4. `audit-replica-key-revocation` | **Parties Security and key-provider owner.** Verify that after certified destruction, both replicas refuse retired key versions, including a lookup already in flight. Measure the cost of bypassing the raw-key cache. Retain caching only with an authoritative revocation check that also covers overlapping cache fills. |
| 5. `audit-standalone-token-relay` | **Parties UI and identity platform.** Reuse the [existing per-client authorization registration](../../src/Hexalith.Parties.UI/Extensions/PartiesUiServiceCollectionExtensions.cs). Qualify an own-record read and update through the standalone OIDC host and real gateway. Cover session expiry and isolation between two users. Limit changes to continuity of the frozen host. |
| 6. `audit-gdpr-actor-attribution` | **Parties domain and privacy owner.** Use an approved opaque identifier from authenticated metadata for new erasure and consent records. Compare event payload and processing history for two distinct users. Replay historical events with missing or generic actor values without inventing an identity. |
| 7. `audit-tenant-search-capacity` | **Parties queries/projections and performance test owner.** Agree representative tenant sizes and workload with operators. Measure end-to-end p95, allocations, serialized index size and concurrent write retries. The 100k-entry local fuzzy search took 227–250 ms in three warmed runs and 1,103 ms in an earlier shared-host run. Optimize only if the measured service target warrants it, then recheck ordering and erasure. [Benchmark](../../tests/Hexalith.Parties.Tests/Search/LocalFuzzySearchPerformanceBenchmarkTests.cs) |
| 8. `audit-dependency-review-pin` | **Hexalith.CiCd and Parties release.** Have the workflow owner select a reviewed immutable revision for the reusable dependency-review job and deliver it through the owned template. Verify reporting on a test PR with the intended permissions. [Delivered workflow](../../.github/workflows/dependency-review.yml) |

## Sequence, effort and open dependencies

- **First 1–2 engineer-days.** Restore release feedback, identify the actual key provider and erasure subscriber, and develop the startup guard. The release owner can request the workflow pin in parallel. Hosted results depend on publishing the corrected revision.
- **Next 3–5 engineer-days** if the provider and worker already exist. Exercise custody, erasure and revocation in the deployed topology with synthetic records. Allow **1–3 days each** for relay and attribution with the identity and privacy owners.
- **Conditional work.** Allow **1–3 weeks** to integrate a missing durable provider or worker, depending on external ownership and infrastructure. Allow **2–3 days** to profile search. A larger indexing change needs measurements and a separate estimate.

## Review record

The audit supplied the trigger and baseline. PRD, epic, architecture and UX impacts are recorded above. The user authorized the planning amendments and pin correction. The tracker assigns each remaining acceptance check. Release qualification, regulated-data readiness and the external workflow change stay open with their respective owners.
