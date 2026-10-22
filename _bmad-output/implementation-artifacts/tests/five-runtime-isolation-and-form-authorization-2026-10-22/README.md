Five runtime isolation and form authorization items, 2026-10-22.

Final post-review qualification: AdminPortal 322/322, Release CI bootstrap/cleanup 30/30, fixture/model/Redis/cleanup 33/33, complete-mTLS gateway/ACL/concurrent isolation 5/5, and mapped Debug CI bootstrap/cleanup 30/30. Every counted test passed with zero skips. Exact commands, source/assembly hashes and cleanup are in `final-verification.json`; `matrix-audit.json` maps all five frozen rows to named passing tests.

`validation.json` records exact commands, exit codes, test names, selected Release dependencies and assembly hashes. Original pre-review tests passed without skips: full AdminPortal 315, Release CI bootstrap/cleanup 26, fixture/model/Redis/cleanup 27, complete original-identity gateway/ACL 3 and dual-session isolation 2. Mapped Debug CI bootstrap/cleanup passed 26.

The live gateway queries the authenticated projection and independently reads exact Redis actor metadata and persisted envelopes. The original ACL run retains the production identity tuples, verifies eight sidecars and allowed controls, and proves six Dapr policy denials. The dual-session run exercises private stored markers, same-party distinct persisted payloads, cross-session rejection and survivor work after peer disposal.

The pinned source tool reached seven healthy actual Debug source launchers from `${SOURCE_VALIDATION_ROOT}`; exact revisions, loaded assembly hashes and mapping are recorded. Its exact run ID was stopped successfully. Focused mapped Debug AdminPortal and Integration test builds remain blocked by the source mapping task's missing AssetsFile argument in FrontComposer outer multi-target contexts. This is separate from successful Release/package runtime acceptance. No selector bypass or upstream/dependency change was used.

DW-116 and Memories persistence remain open. Independent review and commit/push are subsequent parent workflow steps.


Concurrent routing qualification covers the exercised Parties gateway and Tenants flows. Reference admin/sample UI application clients still hardcode upstream target app IDs and are not concurrently qualified; their source remains unchanged.

Published receipts replace local prefixes with `${PARTIES_ROOT}` (owning checkout), `${SOURCE_VALIDATION_ROOT}` (contained Git root named Hexalith.Parties), `${ISOLATED_DAPR_HOME}` (prepared tool runtime), `${HEXALITH_BUILDS_STATE}` (pinned tool state), and `${PRIVATE_RECEIPTS}` (temporary output directory). Set those variables to the corresponding private locations before replaying commands. Relative paths, command arguments, exact revisions, hashes and run IDs are preserved; the mapping hash is the tool's observed original mapping hash. Raw receipts and placeholder values remain outside the repository.

The original acceptance results precede review amendments. Focused post-review checks pass: form class 129/129, bootstrap/model and fixture classes 21/21, and CI bootstrap/cleanup classes 30/30, all without skips. Full post-review verification is recorded in `final-verification.json`.

Final Debug refresh reported all seven servers healthy at launch; focused Debug CI passed 30/30. Debug AdminPortal/Integration builds again stopped at MSB4044, missing AssetsFile. The reported launcher PIDs were absent at post-build inspection, so no new loaded-assembly claim is made. Exact-run cleanup completed successfully.
