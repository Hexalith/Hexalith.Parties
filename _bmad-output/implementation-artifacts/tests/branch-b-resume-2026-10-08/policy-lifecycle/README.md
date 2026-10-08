# Branch B policy/lifecycle verification — 2026-10-08

The [canonical selected manifest](verification-manifest.json) records **1,205 required passing cases and 10 supplemental cases** across eleven owner XMLs. All 41 required classes executed. These are selected local results; the latest complete Local command exited **1**.

The verifier now requires `IdentityHistoryCustodyPolicyTests` in Platform custody coverage: 56 cases check the approved fixed deadline, exact scope, lifecycle and copy/receipt invariants. Three additional Parties wire cases deny expired establishment/rebind/revocation certificates after a retained binding. The externally added expired-prefix continuation implementation and its tests were preserved; the three suffix cases are passing ordering coverage, not a reproduced or repaired continuation defect.

A narrow Platform test repair specifies the return types on two throwing NSubstitute callbacks. The normal custody build and required/supplemental filters passed **181/181**: 68 prerequisite, 47 cleanup, 56 policy and 10 revocation-subscriber cases. This preserves the original lost-acknowledgement behavior.

[Independent root audit](root-independent-audit.json) checks XML/artifact digests, all required class source hashes, five selected production files, the four frozen matrix rows and the three suffix cases. Its timestamped source-byte recheck found no additional changes in those selected files; policy-aware continuation changes were already captured, rather than confirmed post-audit drift. It makes no Portable-PDB document, whole-worktree, final-current compiled-source or installed-availability claim.

## Gate results and source limits

- The original complete Local run passed **1,195/1,195**; [its manifest](initial-verification-manifest.json) and original XMLs remain separate. Subsequent external source movement prevents reusing it as complete current-source qualification.
- A fresh Local attempt failed on `DeletionCapabilityRevocationSubscriberTests.cs:28/:31` with CS0121. All six remaining Parties lanes were then normally built and executed. The scoped callback repair subsequently passed, with the supplemental subscriber class also executed.
- The latest cached Local attempt failed on external `Fr34ProtectionGate.cs:37`: CS0117, `EventStorePayloadProtectionMetadata` has no `SchemaVersion`. That source and a new FR-34 test file moved afterward. Their hashes are retained; no further broad rerun or unrelated source repair was performed.
- The [normal pinned-package Debug host build](pinned-package-receipt.json) exited **1**, with three CS0246/CS1061 errors at `RetainedHumanActorHistoryFold.cs:29/:30/:31`. Installed EventStore **3.117.1** lacks `ExpiredIdentityHistoryCertificate` and `ExpiredEvents` used by the new external continuation source. The pin and external implementation were preserved.
- Live missing-input, non-authoritative sentinel capability and LiveReadiness missing-input checks each exited **1** before endpoint access. Isolated Parties reached Healthy and stopped; the isolated default Platform host exposed no opt-in resources and stopped. Neither observation qualifies identity availability.

[Run receipts](run-receipts.json) and the canonical manifest retain exact commands, raw logs/XML and digests. [File mapping](retained-file-manifest.json) maps repository copies to their original execution paths. [Before](source-before.json) and [after](source-after.json) fingerprints disclose owner movement; selected counts do not double-count earlier runs or the required custody cases.

## Remaining owner requirements

The [Platform inspection](platform-inspection.json) records the approved `party-actor-retention-v1` policy: 365 fixed days from `binding-effective-at`. Neither inspected Platform checkout registers a production `IIdentityHistoryCustody` backend or expired-certificate custody implementation. Its contract work explicitly leaves production qualification open. Qualified independent custody, irreversible all-copy receipts, nonrollback restore, successor continuation qualification and complete installed P-01–P-10 persisted-state/restart/restore/failure-injection targets remain unavailable.

Both original production tasks and the spec's `in-progress` status remain open. This continuation performed no dependency update, staging, commit, push, deployment or production identity mutation. External source/Git movement is retained separately.
