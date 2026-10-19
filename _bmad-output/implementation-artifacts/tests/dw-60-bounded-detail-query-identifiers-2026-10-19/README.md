# DW-60 validation — 2026-10-19

SDK detail and privacy queries now use the same bounded semantic identifier policy as the typed query clients and command validators. All six handlers reject malformed identifiers before read-model or erasure-record access and preserve supported ULID, semantic and legacy GUID forms, optional EntityId fallback and ordinal envelope agreement.

The initial 150-row rejection regression reproduced 102 failures before the fix. The final focused handler suite passes 685/685; the complete affected service assembly passes 1898/1898, with no errors, failures, skips or not-run tests. The Debug source-reference build has zero warnings and errors. `validation.json` records commands, outcomes and content hashes; original logs and XML are in `/tmp/dw-60-*` for this session.

The existing-build Aspire baseline reports Parties and Security healthy. EventStore and Tenants are Finished; this does not qualify the full integrated topology. The owned AppHost was stopped before edits and test builds.

Commitlint evidence is retained in `commitlint.txt`. The independent review outcome is recorded in the linked implementation spec.

Independent Blind Hunter review raised four test gaps. All were patched and independently verified: blank EntityId fallback, ordinal mismatches, malformed legacy GUID forms and exact processing-record routing/response assertions. No new deferred work.
