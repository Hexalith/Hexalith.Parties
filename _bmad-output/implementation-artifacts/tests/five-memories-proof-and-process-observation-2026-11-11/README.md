# Memories proof and process observation verification (2026-11-11)

This packet belongs to `spec-five-memories-proof-and-process-observation-deferred-items.md`.
All implementation and live matrix gates pass. `handoff-verification.json` maps the five
requirements to exact passing tests and evidence paths; `changed-files.json` lists the
complete implementation file set. `implementation-verification.json` retains source identities,
hashes and historical individual results. Counts from repeated or overlapping lanes are not
summed. Independent review, owner adoption and Git completion remain parent workflow gates;
the spec stays in-progress and the five ledger entries remain open pending those gates.

The source Debug integration lane passes71 cases with zero skips:43 producer,12 inventory,
8 refresh,7 semantic and1 real backup/restore. The separate adapter/unit lane passes50 cases.
The producer regression removes per-unit semantic inventory attestation. Before the fix, two
failed-producer rows return409 while the terminated-work refusal passes. New admission now
retains fresh producer eligibility; first candidate retention atomically initializes exact-key
provenance before extraction. Pre-upgrade admission, retained candidate redelivery, failed
retries, existing NL artifacts and tombstones cannot manufacture fresh coverage. Tracked
content writes retain their exact keys without certifying unknown legacy history. Fresh
annotation admission and tracked-write regressions pass. Untracked restore behavior remains
unchanged. Authenticated virgin synthetic source setup remains the separate source prerequisite.

The final package-host Debug model lane passes41 controls:12 authoritative persistence,
10 process identity,18 process capture and1 real namespace lifetime control. Earlier aggregate40
and process-only19 results overlap these controls and are historical reruns. The original
observer replay fails7 of19 controls. The final XML records an actual reused numeric PID with
different kernel start identities. A real procfs iterator opened before parent exit returns an
empty task set while the owned child survives; cleanup waits for that child's termination.
The isolated namespace reproducer and forced cleanup have separate bounded waits. The observer
only reads processes. Existing EventStore controller/actor/prefix safeguards pass149 source
Debug controls, preserving native-proof, range, legacy and drift refusals.

Both final owned package-host Debug wrappers pass, one focused refresh and one full recovery.
The full authenticated child passes its real failed-extraction cleanup and survivor matrix.
Both wrappers verify tenant and case streams plus both canonical output streams against native
reads and first/final-prefix replay. Both retain complete disposal receipts: owned topology
disposed, session directory removed, no primary failure and no cleanup failure. Typed actor
diagnostics retain the original gateway503 despite matching available proof, three scoped native
events and stable later proof. The refusal precedes the controller: the qualification client
omitted the catalog generation boundary. The repaired caller reads the actual modeled authority
component/key through the SDK and computes current activation stamps through the SDK codec.
Live missing and stale generation controls return503; the current generation succeeds. No
EventStore controller or actor behavior changes. Replay uses the exact owned tenant grant;
the same otherwise-authorized operator token receives403 for another tenant.

The final ordinary live lane passes both tests. Complete capture has zero capture failures,
no surviving owned workers and quiesces in9574.6ms within30s. Deliberately incomplete capture
retains15 capture failures, refuses qualification after owned cleanup, has no surviving owned
workers and quiesces in9967.5ms. All five launchers exit143 in each run. Unknown timing in the
incomplete observation remains unknown. The actual timing JSON files are retained.

Runtime preparation exposed two harness prerequisites. The qualification wrapper's native
Admin UI reference now binds the separately verified private-host receipt, exact reference
identity, source artifact and matching bytes, including readback drift refusal. All29 validator
controls pass. The owned wrapper defers explicit app-channel custody until composition reuses
the generated SDK binding; all6 model controls pass. Final acceptance uses the completed
canonical `bash scripts/prepare-aspire-runtime-hosts.sh --configuration Debug
--include-memory-qualification true` preparation with13 matching projects and official archive
validation. Retained final receipts bind the exact executed cohort. Intermediate failed
preparations and narrow reseal attempts supply no final acceptance.

`credential-scan.json` records a clean scan of public evidence, including decompressed archives,
against10 generated credentials from5 private run configurations: zero exact credential matches
and zero private-key-pattern matches. Configurations and the credential set remain outside the
repository. Completed processes no longer retain their ephemeral proof keys, so those keys
cannot be exact-matched; retained text and decoded JSON additionally receive private-key-pattern
screening. No secret values are reported.

Both owner and root whitespace gates pass with `git -c
core.whitespace=blank-at-eol,blank-at-eof,space-before-tab,cr-at-eol diff --check`, honoring tracked
CRLF policy while checking actual whitespace errors. Raw `git diff --check` interprets required
CRLF as trailing whitespace in this root; C# files were not normalized to LF.

Source/native startup does not enroll this private package catalog composition and remains a
separate diagnostic limitation. The canceled first diagnostic overlapped an owner test build;
it supplies no acceptance and has no final wrapper ownership receipt. Later owned runs retain
complete cleanup receipts. No packages are published, versions changed or production data
migrated. Owner source Debug and public package Release/deployment acceptance remain distinct.

Final parent review, patched-tree verification and source adoption are recorded in
`../five-memories-proof-and-process-observation-final-2026-11-11/verification.json`.
The pending-gate statements above describe the original implementation handoff.
