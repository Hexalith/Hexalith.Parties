# Historical implementation proposal — superseded during review

**Review rejected the source-wide Memories prerequisite:** it breaks ordinary production cleanup and fails the independently reproduced cross-source identity case. The source proposal and added baseline-fixture attestations have been removed. Counts below describe that historical proposal, not the current tree or publication readiness. See `review-cross-source/` and `rejected-memories-proposal.diff.gz`. The final safe implementation and the pending human scope decision require a separate final receipt.

Historical implementation controls were verified locally. Independent review, pinned commitlint,
final owning commit identities, signoff/source selection, divergence checks and
owner/main pushes remain parent-owned. No owning repository was staged or committed,
no remote Git operation ran, and no dependency or package version changed.

The approved [spec](../../spec-five-snapshot-history-and-publish-deferrals.md)
is the scope authority. [verification.json](verification.json) binds the exact
working source/binary hashes and normalized per-test XML;
[commands.json](commands.json) retains full argument lists and exact outcomes.
`${PARTIES_ROOT}` and `${PRIVATE_ROOT}` replace private checkout/scratch prefixes.
Raw XML hashes are retained separately from the normalized XML hashes. Full raw
runtime output remains private because it can include synthetic payloads.

| Boundary | Actual executed evidence | Result and limit |
| --- | --- | --- |
| Snapshot deadline | Owned regular-file lease held for 3 s against 750 ms; capture/recheck, copied worker output, unavailable runtime, inventory/seal controls | 11/11; caller returned 787 ms in each lease case and measurement worker exited. Reversed source 9/11, exactly two lease regressions. Linux fixture; no production filesystem completeness claim. |
| Purged workflow identity | Real historical chunk activity and historical NL activity, completed Dapr workflow purge, identity reuse, actual cleanup | Unknown source gains no completeness marker; cleanup refuses and retains artifact/survivor bytes. Included in final 102/102. |
| Concurrent historical producer | Real absent lookup pauses while another actual Dapr producer completes, then modern admission/scheduling proceeds | Unknown coverage stays incomplete and cleanup refuses. Included in final 102/102; proves no older-writer exclusion or drain after an independent attestation. |
| DW-58 workload restart | Owned Redis 7.4.7/Dapr 1.19.1; ten exiting workload children across absent/existing variants | 9/9 provider controls; immutable histories/tenant/operation survive seed/rebuild/stage/commit/verify, with persisted detail/processing/index, erasure and nine survivor bytes. Supported aggregate one-shot/shared staged protocols only; deployed cutover gate stays open. |
| Sidecar publication | Three actual selected SDK CLI manifests for docker/k8s/aca plus 42 publish/OIDC/run-model controls | 3/3 conformance/refusal and 42/42 model controls. App secret refs exist; sidecar has only `dapr`/`type`, no environment. Positive delivery remains blocked upstream. |

The final Memories lane includes SourceInventoryIntegrationTests,
RefreshPublicationIntegrationTests, TenantIngestionLifecycleIntegrationTests,
RetryWorkflowErasureIntegrationTests and SourceInventoryProducerIntegrationTests:
102/102 pass with zero skips. The unchanged Server determinism guards pass 25/25.
The isolated reversed inventory implementation discovers 102 and passes 98, with
exactly the four new purge/concurrent controls failing; all baseline controls pass.
Counts from overlapping or repeated runs are not added together.

Positive fixture prerequisites remain explicit. Virgin sources use the existing
fenced reconciliation before any producer. The legacy refresh fixture keeps
source coverage unknown and independently attests only actual indexed output keys
before their cleanup. Default/unknown-source controls continue requiring refusal.
No database scan or automatic source attestation was introduced.

The retained `historicalIntermittent.xml.gz` records one unexpected content call
in the unchanged worker-upgrade case during a run overlapping the reversal suite.
No cause is established. Earlier serialized core controls passed; the final
serialized 102-control run and reversed baseline also pass that existing case.
This observed intermittent limitation is retained rather than hidden.

Platform's ordinary nested-root build first refused missing uninitialized nested
Parties paths (MSB4025); selecting the actual parent root then exposed enclosing
Parties analyzer debt. The isolated owner checkout builds with exact existing
source roots and no rule suppression. Its worker was also tested from a directory
containing only Composition.dll, its deps file and runtimeconfig. Source Debug
qualification remains separate from package Release provider/publish evidence.

The selected publisher cohort remains Aspire.Hosting 13.6.4,
CommunityToolkit.Aspire.Hosting.Dapr 13.6.2-beta.959 and EventStore.Aspire 4.2.0.
[SDK emission receipt](sdk-sidecar-emission.json) records actual artifact hashes.
Mutated copies qualify only the pairing checker; they provide no publisher fix.
Raw publication artifacts and synthetic credentials are removed after each run.

The reusable owned driver is `run-owned-backfill.py`; invoke it with `--assembly`
and a fresh `--output` directory. `verify-sidecar-publication.py` accepts the
selected `--apphost` and a fresh `--receipt` path. Both use exclusively owned
resources. `provider-cleanup.json` records exited processes and absent backing
container; `memories-cleanup.json` independently checks every container created
by these recorded Memories runs. Owner rollback checkouts and raw logs remain
under the implementation's private scratch prefix for parent review; none are
initialized nested submodules. No cloud or shared service was deployed.
