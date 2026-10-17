# DW-24 validation — 2026-10-17

The missing-CaseId warning tracker now retains at most 1,024 exact tenant/party pairs per singleton adapter. Retained duplicates do not grow either tracking structure or refresh FIFO order. Evicted pairs may warn again.

The corrected regression failed against the original dictionary with 3,072 retained entries. Final Debug/source builds have zero warnings/errors. The indexer focus passes 21/21 and the full service assembly passes 1,080/1,080 without failures, errors or skips. Coverage includes mixed concurrent churn, both bounded structures and matching identities, constant structured diagnostics without exceptions, immutable configuration reload, and persisted-case cleanup end-state. All five independent review findings were addressed and follow-up found no concrete issue.

`validation.json` records commands, result summaries, hashes, review and commitlint evidence. Raw test/build logs and XML remain local under `/tmp/dw-24-*`; only sanitized evidence is retained here.

The separate baseline AppHost started, but `aspire wait parties --status healthy --timeout 20 --apphost src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj --non-interactive` exited 17 after 20 seconds. AppHost shutdown succeeded before source builds. This observation does not diagnose or qualify the complete topology. No dependency, gitlink, persistence or public API change is included.
