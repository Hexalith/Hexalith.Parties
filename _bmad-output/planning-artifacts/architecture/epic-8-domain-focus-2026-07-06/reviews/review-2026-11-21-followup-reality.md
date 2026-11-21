# Follow-up reality check · November 21, 2026

**Pass for the documentation update.** The revised spine, team overview and CAP-1 companion address the source discrepancies in the November 21 validation. The custody, retirement and named-owner decisions remain open in their recorded scopes.

| Checked against source | Result |
| --- | --- |
| Live delivery, rebuild and query admission | I9 matches `PartyDetailSdkProjectionHandler.ProjectAsync`, its rebuild candidate and the sequence checks in `PartySdkProjectionFold`. I10 and the overview reflect the canonical-read failure branches in `PartySdkQueryService` and the cross-instance erasure cases. Denied personal data cannot be served from a previous response. |
| Operational storage and authority | I19 identifies the SDK interface used by `PartyMemoryUnitMappingStore` and the distinct non-rebuildable ledger behavior. DW-81 records the transport change. The Story 8.3 I19 and §2 rows still require owner decisions. Neither the spine nor the companion authorizes a migration from that historical evidence. |
| Create, erasure and historical attribution | I11 matches the two create paths in `PartyAggregate` and the grammar in `PartyIdentifier`. I7/I8 distinguish profile erasure from purpose-specific past attribution and preserve the approved binding-effective 365-day expiry and independent custody gate. The overview matches the consumer UI states and domain-host adapter registration. |
| Source selection and evidence | Parties `42b27f2e` pins Platform `a74abe17`, Builds `dc225a69` and EventStore `7435254c`. Platform pins the same Builds revision. Release/package MSBuild evaluation against that catalog returned `4.2.0` for the Parties and gateway test hosts and `5.4.0` for the attached suite. The Parties host also honored an explicit `5.4.0` override. I4/§4 preserve the distinction between committed pins, worktree tips and the versions exercised by forward and rollback runs. |
| Closure and runtime evidence | CAP-1 and §7 distinguish the August baseline, the November 2 closure receipts and the November 16 retirement of story-status tests. The current gateway test explicitly skips outside its configured profile and requires availability when enabled. The isolated caller suffix, namespace and trust domain match `DaprMtlsBootstrap`. |

All local file links and heading fragments in the spine and team overview resolved. This review checked Git objects, source, tests, package evaluation and recorded approvals. Runtime, hosted CI and production custody qualification require their own evidence.
