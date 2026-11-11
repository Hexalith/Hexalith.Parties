# Snapshot, historical identity and publish qualification — final

The reviewed safe changes are verified and the owner commits are pushed. The
Parties commit records their exact source selections without changing package
versions or granting historical/deployed acceptance. Final current-tree evidence
is [final-verification.json](final-verification.json); the earlier
[proposal](historical-proposal-README.md) was superseded during review.

| Deferred boundary | Final result |
| --- | --- |
| Snapshot hashing deadline | 14/14 controls pass, including both actual hash workers, copied-child failures and retained ownership after termination failure. Original source fails exactly two controls. |
| Purged historical identity | Real workflow purge/reuse is reproduced; full safety remains open because source coverage is not unit authority. The narrow legacy-NL collision guard is fixed and 99 ordinary/related Memories controls pass without added production prerequisites. |
| Concurrent historical producer | The actual interleaving and independent cross-source false-success case are retained. The proposed blanket prerequisite was rejected and removed; per-unit authority/old-writer exclusion remains an external gate. |
| DW-58 workload restart | 9/9 provider controls pass, including ten exiting workload processes, durable detail/processing/index, erasure and nine survivors. Deployed history, drain and cutover acceptance remain open. |
| Sidecar token publication | 42 model controls and three actual target conformance checks pass; selected upstream sidecars still omit environment bindings. Positive publication is unqualified. |

The final selected checks total 204: Platform 14, Memories 99, determinism 25,
provider 9, publish models 42, Python evidence/cleanup guards 12 and actual target
conformance 3. Current source, binary and exact per-test identities are retained.
Final reversals pass 12/14 Platform baseline controls and 98/99 Memories baseline
controls, failing only the two intended deadline cases and legacy-NL collision.
No proposal-era counts are added to the final total.

Review triaged all 16 findings. The invalid source-wide Memories prerequisite and
its artificial baseline fixture attestations were removed. The independent
[false-success counterexample](review-cross-source/result.json) proves why full
historical safety cannot be inferred from empty source B after source-A purge.
The [rejected patch](rejected-memories-proposal.diff.gz) is evidence only.
An earlier unchanged worker-upgrade intermittent failure is retained separately;
the final serialized 99-control baseline passes it.

The ordinary Platform restore reports NU1903 for unchanged pinned dependencies.
Its documented NuGetAudit=false diagnostic build and the merged owner's 14 focused
controls pass; tracked gates and package pins remain intact. The Parties Release
selection build and the Memories Debug build pass with ordinary audit settings.

Reusable controls are run-owned-backfill.py, verify-sidecar-publication.py and
12 focused cases in test-evidence-guards.py. retain-evidence.py now contains strict
receipt checks only; its obsolete proposal collector is archived. Source Debug,
package Release, unsupported publisher output and external deployment gates remain
separate. All selected owner commits are available on origin/main; protected-main
pushes reported PR/status-rule bypasses under the user's explicit main-push request.

Committed-source adoption at `e522f6fe35f5d061fcf57b3c27523e7e64387353`
passes 196 source-selection/closure checks, 31 reader tests and the two-owner
push-range gitlink gate. Combined with the 204 implementation controls, 431
distinct selected checks pass. The initial 196-case run had one unchanged fixture
MSBuild query failure (exit 1, blank details); that method passed alone and the
same complete selection passed serialized without changing assertions. No cause
is established. All attempts and exact case identities are retained as
`postcommit-*` evidence; repeated attempts do not inflate the total.
The final evidence commit records this verified code revision before the
user-authorized parent `main` push.
