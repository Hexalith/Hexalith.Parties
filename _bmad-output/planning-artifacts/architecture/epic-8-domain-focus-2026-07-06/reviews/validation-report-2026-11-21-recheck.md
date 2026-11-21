# Epic 8 architecture revalidation

21 November 2026 · Hexalith.Parties · Post-amendment working copy

## Verdict

**Changes required before the affected migrations can use these documents for sign-off.** All eleven findings in the earlier report were addressed in the amended wording. This independent recheck identified two high-priority issues for successor-host and UI parity, seven medium findings and two low findings. The November 2 closure still applies to its recorded revision.

The first corrections belong to the host team (readiness behavior) and the admin UI team (what an empty degraded response permits the grid to display). Stories 8.7–8.9 remain blocked on their recorded prerequisites.

## Findings

### Successor-host readiness

**High.** The overview (§9) lists the Dapr sidecar and state store as readiness gates. The current host also tags the Tenants integration as ready. A disabled integration reports healthy, while a failed enabled probe makes the host unready. Pub/sub, Memories and projection-store degradation contribute to health separately. Section 2 of the spine leaves the projection rule unclear.

**Action.** State which failures take the host out of rotation. Qualify the successor against enabled and disabled Tenants and against projection-only degradation. [Epic 8 spine](../ARCHITECTURE-SPINE.md) · [Team overview](../../../../../docs/architecture.md) · [Readiness registrations](https://github.com/Hexalith/Hexalith.Parties/blob/42b27f2e9375c6d8db270524029c3e875d830a8e/src/Hexalith.Parties/HealthChecks/PartiesHealthCheckExtensions.cs#L30) · [Tenants probe](https://github.com/Hexalith/Hexalith.Parties/blob/42b27f2e9375c6d8db270524029c3e875d830a8e/src/Hexalith.Parties/HealthChecks/TenantsIntegrationHealthCheck.cs#L62)

### Empty degraded responses and previously displayed names

**High for UI parity.** I10/I14 now cover denied reads, and the admin grid clears its rows on a failure. On a successful empty response the component can retain prior rows when metadata signals degradation. A component test expects an earlier name to remain visible after a new search returns no results and a degradation reason. The typed client supplies empty portal metadata for successful list and search calls. The finding concerns the component contract and other adapters. The review established no production disclosure.

**Action.** Distinguish a validated unchanged result for the same query from a new, admitted empty result. Require the latter to clear rows and counts. Cover both results and explicit denial across list and search before accepting UI parity. [Epic 8 spine](../ARCHITECTURE-SPINE.md) · [Grid result handling](https://github.com/Hexalith/Hexalith.Parties/blob/42b27f2e9375c6d8db270524029c3e875d830a8e/src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor#L995) · [Failure clears the grid](https://github.com/Hexalith/Hexalith.Parties/blob/42b27f2e9375c6d8db270524029c3e875d830a8e/src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor#L1528) · [Two component expectations](https://github.com/Hexalith/Hexalith.Parties/blob/42b27f2e9375c6d8db270524029c3e875d830a8e/tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs#L1122) · [Typed and fallback adapters](https://github.com/Hexalith/Hexalith.Parties/blob/42b27f2e9375c6d8db270524029c3e875d830a8e/src/Hexalith.Parties.AdminPortal/Services/PartiesAdminPortalApiClient.cs#L93)

### Subscriber acknowledgement

**Medium.** The overview (§7) tells subscribers to always return HTTP 200. A success acknowledgement before a transient processing failure is durably handed off can consume the broker’s opportunity to retry, including work needed for erasure cleanup. The publishing guide provides retry and dead-letter handling.

**Action.** Document the acknowledgement point for successful work, duplicates and durable retry handoffs. Require retryable failure or an explicit retry outcome when the subscriber cannot accept responsibility. [Team overview](../../../../../docs/architecture.md) · [Subscriber examples](https://github.com/Hexalith/Hexalith.Parties/blob/42b27f2e9375c6d8db270524029c3e875d830a8e/docs/event-handler-patterns.md#L34) · [Delivery failure policy](https://github.com/Hexalith/Hexalith.Parties/blob/42b27f2e9375c6d8db270524029c3e875d830a8e/docs/event-publishing.md#L82) · [Erasure retry ownership](https://github.com/Hexalith/Hexalith.Parties/blob/42b27f2e9375c6d8db270524029c3e875d830a8e/_bmad-output/planning-artifacts/sprint-change-proposal-2026-11-20.md#L38)

### Receiver authentication at host cutover

**Medium.** I1/I1a specify sidecar ACLs, isolated identities and general security parity. The current host also verifies its application channel on Dapr callbacks and requires a workload assertion scoped to the receiving service and gateway operation. The successor acceptance map does not name these two checks.

**Action.** Require receiver-side acceptance and rejection results for the intended channel, service and operation as part of the host migration evidence. [Epic 8 spine](../ARCHITECTURE-SPINE.md) · [Host endpoint registration](https://github.com/Hexalith/Hexalith.Parties/blob/42b27f2e9375c6d8db270524029c3e875d830a8e/src/Hexalith.Parties/Program.cs#L71) · [Existing verifier tests](https://github.com/Hexalith/Hexalith.Parties/blob/42b27f2e9375c6d8db270524029c3e875d830a8e/tests/Hexalith.Parties.IntegrationTests/Topology/PrivateCatalogWorkloadAuthenticationTests.cs#L32)

### Operation paths in deployment ACL evidence

**Medium.** I1 lists thirteen paths and requires external owners to attest identical grants. Its prescribed tuple names app ID, verb, policy and action, leaving the operation path implicit. Different allowed paths can produce the same written receipt.

**Action.** Include the operation name/path for every grant and preserve the caller identity, namespace, trust domain, verb, action and deny defaults. Compare this receipt against the canonical declaration at the selected revision. [Epic 8 spine](../ARCHITECTURE-SPINE.md) · [Canonical grant](https://github.com/Hexalith/Hexalith.Parties/blob/42b27f2e9375c6d8db270524029c3e875d830a8e/src/Hexalith.Parties.AppHost/DaprComponents/accesscontrol.parties.yaml#L26) · [Declaration check](https://github.com/Hexalith/Hexalith.Parties/blob/42b27f2e9375c6d8db270524029c3e875d830a8e/scripts/render-platform-declaration.py#L506)

### Scheduling work identified only by a touched file

**Medium.** Section 5 binds a task when it touches a protected file. It assigns slots for named parents and special cases, but a task linked solely by its touched file can still lack a unique slot. Two teams can agree that the task is gated and disagree about which predecessor must finish.

**Action.** Specify how to derive the owning slot from the affected rollback or acceptance evidence, and who resolves overlaps before a task starts. [Epic 8 spine](../ARCHITECTURE-SPINE.md) · [Epic 8 SPEC](../../../../specs/spec-epic-8-domain-focus/SPEC.md)

### Classifying durable key custody

**Medium.** I19 assigns each persisted store to a rebuildable read model or an operational side-effect ledger. The per-party key backend manages authoritative secrets and wrapping metadata. I8 and GATE-KMS cover provider qualification, but a team cannot tell which I19 approval or tests also apply to that backend.

**Action.** Have the custody and storage owners record how I19 applies to the provider and which evidence is required alongside I8 before a provider migration. [Epic 8 spine](../ARCHITECTURE-SPINE.md) · [Provider operations](https://github.com/Hexalith/Hexalith.Parties/blob/42b27f2e9375c6d8db270524029c3e875d830a8e/src/Hexalith.Parties.Contracts/Security/IKeyStorageBackend.cs#L3) · [Deployment gate](https://github.com/Hexalith/Hexalith.Parties/blob/42b27f2e9375c6d8db270524029c3e875d830a8e/_bmad-output/planning-artifacts/parties-ui-prd.md#L454)

### Replay method selection

**Medium.** The overview (§13) says reordering Apply overloads would silently corrupt replay. A repository fitness test enforces declaration order. The selected resolver gives priority to exact names, then boundary-anchored suffixes, and reports ambiguity. The local ordering convention and SDK dispatch behavior have different evidence.

**Action.** Describe the repository ordering test as a compatibility guard and document the selected resolver’s behavior separately. [Team overview](../../../../../docs/architecture.md) · [Local ordering guard](https://github.com/Hexalith/Hexalith.Parties/blob/42b27f2e9375c6d8db270524029c3e875d830a8e/tests/Hexalith.Parties.Tests/FitnessTests/PartyStateApplyOrderingFitnessTests.cs#L17) · [Selected EventStore resolver](https://github.com/Hexalith/Hexalith.EventStore/blob/7435254cc1a148031224eebe1aafd53a8659b2cd/src/Hexalith.EventStore.Client/Aggregates/ApplyMethodResolver.cs#L145)

### Pact job status

**Medium.** Section 13 describes a Pact job reporting a skip accepted by the quality gate. The current CI workflow still accepts the contract-verification dispatch event. Neither its jobs nor the pinned reusable workflow defines that Pact job.

**Action.** Date the historical Pact description. Point the current qualification section to the jobs and evidence consumers can actually run. [Team overview](../../../../../docs/architecture.md) · [Current CI](https://github.com/Hexalith/Hexalith.Parties/blob/42b27f2e9375c6d8db270524029c3e875d830a8e/.github/workflows/ci.yml#L10) · [Pinned reusable workflow](https://github.com/Hexalith/Hexalith.Builds/blob/fe68718b6c074a6b500a7191e3554a1d39e17688/.github/workflows/domain-ci-v2.yml) · [Historical Pact notes](https://github.com/Hexalith/Hexalith.Parties/blob/42b27f2e9375c6d8db270524029c3e875d830a8e/docs/ci-legacy-reference.md#L511)

### Test project inventory

**Low.** The overview (§§1 and 11) counts 18 runnable test projects. The repository also defines three Platform-run suites, giving 21 xUnit projects. The legacy attached-lifecycle project forwards builds to the qualification project.

**Action.** Distinguish the script’s 18 projects from the three suites run by Platform. Keep the forwarding project in the support inventory. [Team overview](../../../../../docs/architecture.md) · [Runner and support inventories](https://github.com/Hexalith/Hexalith.Parties/blob/42b27f2e9375c6d8db270524029c3e875d830a8e/scripts/test.ps1#L100)

### GDPR notice in the README

**Low.** Section 13 lists a discrepancy that the README already resolved. Both documents describe default-enabled crypto-shredding and the development-only in-memory key store.

**Action.** Remove the stale discrepancy and keep GATE-KMS visible as the open production decision. [Team overview](../../../../../docs/architecture.md) · [Current GDPR notice](https://github.com/Hexalith/Hexalith.Parties/blob/42b27f2e9375c6d8db270524029c3e875d830a8e/README.md#L5)

## Earlier findings

| Earlier finding | Result on the amended copy |
| --- | --- |
| Outage reads | I10/I14 specify which cached results may be used and require clients to clear denied data. The new UI finding concerns a successful empty response. |
| Memories cleanup ledger | I19 preserves mapping keys across rebuilds and records the conditional SDK interface. Owner approval of the interface and tests is outstanding. |
| Actor-history custody | I7/I8 bind independently authorized attribution to exclusive expiry 365 fixed days from binding effectiveness, including on restore. |
| Retired CAP-1 tests | CAP-1 distinguishes the August baseline from November closure and requires approved successors before affected deletions. |
| Projection recovery | I9 specifies incremental live folds, a rebuild from empty state and preservation of erasure checkpoints. |
| Tenant-claims and McpCli routing | Section 2 separates selected destinations from migration authority. |
| Create retries | I11 records existing aggregate evidence and the no-event outcomes for both create commands. |
| Rollback identities | Section 4 records forward and restored dependency identities and the result at each. |
| Isolated Dapr identity | I1 records the canonical and exercised identities and the session namespace. |
| Protection adapter host | The team overview identifies the correct host and service-registration order. |
| Gateway test deferral | Section 9 removes the superseded reminder. Section 7 retains the recorded DW-96 scope. |

## Evidence and limits

The reviewed working copy is based on Parties `42b27f2e9375c6d8db270524029c3e875d830a8e`. Its SHA-256 prefixes are `8a5a3a85be25` for the spine, `998f196c8d35` for the SPEC and `373bf1efaf1a` for the overview. [Epic 8 spine](../ARCHITECTURE-SPINE.md) · [Epic 8 SPEC](../../../../specs/spec-epic-8-domain-focus/SPEC.md) · [Team overview](../../../../../docs/architecture.md)

| Check | Result |
| --- | --- |
| Structure and references | Spine lint returned zero findings. All 22 Epic 8 invariant IDs, six inherited decisions, 16 closure-map rows and six capability IDs match the committed order. The 75 distinct-per-document relative links and their heading targets resolved. |
| Closure and tracker | Git records the two original test classes at `2b63ab9`, their retirement at `0f261ccd` and six passing Round 10 lanes at `c7dd607b`. The tracker records Epic 8 and Story 8.10 as done and 8.7–8.9 as blocked. |
| Source and packages | Checked 11 direct gitlinks and the matching Platform v2.2.4 / Builds v6.4.3 selection. Builds, CiCd and Platform working checkouts differ from the parent pins. MSBuild using the pinned catalog evaluated EventStore `4.2.0` for the Parties host and gateway test host, `5.4.0` for the attached qualification project and `5.4.0` for an explicit host override. All target `net11.0`. |
| Declaration | The generated Platform declaration matched. All seven declaration unit tests passed. |

Production custody, completed erasure after restart, cross-replica revocation, I18 successor tests and I19 storage approvals still require their own evidence. This validation used source and test inspection, Git history, package evaluation and the declaration checks. Hosted CI, the UI component suite and live erasure were not executed for this report.

## Review records

[Earlier validation](validation-report-2026-11-21.md) · [Rubric review](validate-2026-11-21-recheck-rubric.md) · [Reality review](validate-2026-11-21-recheck-reality.md) · [Adversarial review](validate-2026-11-21-recheck-adversarial.md)
