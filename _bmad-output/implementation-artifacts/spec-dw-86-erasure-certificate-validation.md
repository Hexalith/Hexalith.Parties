---
title: 'DW-86: Validate erasure certificates before store cleanup'
type: 'bugfix'
created: '2026-10-20'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
baseline_commit: '7334c703585329ac7ac9fae40c08dd40b8a207c0'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** DW-86 records that store-cleanup verification ignores the supplied key-destruction certificate. A certificate for another tenant or party, or with incomplete key destruction, can certify erasure.

**Approach:** Require a nonblank requested scope and a matching, ordinally equal certificate with Verified key destruction and well-formed destroyed-version evidence before invoking any cleanup. Invalid certificate evidence returns a bounded Failed report without invoking stores, even when no stores are registered. Null arguments use standard argument guards. Preserve verified idempotent deletion with an empty version list and existing valid cleanup, cancellation and clock behavior. Preserve the certificate's key-verification status across cleanup retries; cleanup outcomes remain in the existing report/status records. Cover invalid evidence and incomplete-then-successful retries, document legacy certificate recovery limits, close DW-86, commit and push to main.

</frozen-after-approval>

## Implementation Notes

- Freeform deferred work, no sprint key. Clean main and canonical baseline recorded above. Continue the bounded local queue after DW-85; DW-81 through DW-84 retain their separate upstream persistence/inventory/recovery prerequisites.
- No intent gaps, irreversible operations or new API. Small footprint: ErasureVerificationService, the existing retry certificate update in PartyDomainProcessor, their focused tests, one production-registration fixture and operator documentation. No dependency, gitlink, schema or deployment changes.
- Reuse the existing Failed report and per-store result shape with a fixed key-destruction category and fixed error text. Never include certificate identifiers, versions or provider details in diagnostics. Do not certify store cleanup from unchecked certificate fields.
- Positive key versions are valid; an empty list is valid after authoritative idempotent deletion. Null lists and nonpositive versions are malformed. No new timestamp freshness/expiry policy: the contract defines none.
- PartyKeyManagementService.DeleteKeyAsync marks Verified only after read-back confirms no remaining keys. PartyDomainProcessor currently overwrites that status with cleanup Pending/Failed. Preserve key status and versions while retaining its existing timestamp refresh. Do not auto-promote legacy Pending/Failed certificates, which may represent real key-destruction failures; trusted owner reissuance is required.
- Baseline Aspire isolated start using existing assets succeeded; parties wait succeeded. Capture resource states and stop this exact AppHost before builds. This is a startup observation, not authenticated topology qualification.
- Verify Debug/source mode using the existing pinned module tooling where available; focused xUnit v3 classes, then affected complete test assemblies, warning/submodule guard, whitespace and pinned commitlint. Keep exact command/results and any independent broad-gate blocker in tests/dw-86-erasure-certificate-validation-2026-10-20/validation.json.

- Implementation refinement: removing the retry certificate write entirely also prevents a mismatched certificate from being saved under another tenant/party's key. Preserve the original timestamp and key status together; the report/status already carries the cleanup clock. The earlier note about retaining timestamp refresh is superseded by this concrete safety finding.
- Regression baseline: all 49 security-verification cases ran; 25 failed against the original implementation. After the certificate gate, all 49 pass. Pinned source launcher exits 2/HXR011 because HEXALITH_DAPR_HOME is not configured; Debug builds against the existing selected package dependencies provide focused fallback evidence without changing source selections.

- Independent blind review returned six findings. Patch the two exposed AdminPortal consumers: require affirmative cleanup status before saying projections are verified, and display status UpdatedAt as the report timestamp instead of the immutable key-certificate timestamp. Preserve the existing component/label structure and add bUnit coverage. This is the same erasure-evidence goal, without new UI APIs or styling.
- Extend persisted retry coverage to Partial; add pre-canceled valid/invalid evidence with/without stores; document the public method's failure/argument semantics. Legacy operator recovery lacks a supported command and remains owner work; document this explicit limit and append its tracking entry rather than weakening the guard or claiming an available procedure.

- Final validation: all three affected Debug test projects build with zero warnings/errors. Full Security.Tests passes 280/280 and AdminPortal.Tests 201/201. Focused service classes pass 143/143 and the complete service assembly passes 2108/2109; its sole failure is the unchanged, previously recorded Builds receipt mismatch (expected 5d03f1db9c38db34ecac7fe6048937478ffb82cc; committed fff4a212180d8cd1c5c0759655a9bc4fb6963c45). No errors/skips/not-run. The fitness source blob is identical to baseline. No full-suite green claim.
- Pinned commitlint 21.2.2 validates the exact commit file with zero problems/warnings. Warning/submodule guard and whitespace check pass. Evidence: tests/dw-86-erasure-certificate-validation-2026-10-20/validation.json. Temporary filesystem exhaustion recovered without removing any user data; only this task's scratch logs were removed.

## Review Triage Log

- Medium, patch: preserved Verified key evidence exposed a consumer that treated missing cleanup status as projection verification. Require an affirmative Verified/Complete/Erased status; bUnit checks missing, blank, incomplete and completed states.
- Medium, patch: the Admin report timestamp still came from the now-immutable key certificate. Display cleanup status UpdatedAt, omitting report timestamp when there is no report status; verify distinct key and cleanup times.
- Low, patch: extend persisted retry coverage to mixed Cleaned/Failed stores yielding Partial, followed by successful cleanup with unchanged certificate bytes.
- Medium, patch: previous cancellation coverage relied on a cooperative delegate. Add pre-canceled calls with valid/invalid evidence and zero/configured stores, checking cancellation token and no cleanup.
- Low, patch: document ordinal scope matching, Verified status, empty idempotent version lists, returned Failed evidence and argument/cancellation exceptions on the public method.
- Medium, defer: no supported operator reissuance/persistence procedure exists for ambiguous legacy Pending/Failed certificates. Explicitly document the owner blocker and append the recovery item to deferred-work.md; do not manually promote failed evidence or broaden this guard into key-management operations.
- Independent follow-up confirms all six findings addressed, with legacy recovery explicitly tracked. No remaining concrete introduced issues; reviewer did not rerun tests.
