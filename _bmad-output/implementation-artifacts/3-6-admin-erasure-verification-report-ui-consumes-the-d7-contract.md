---
baseline_commit: 4a8b58c
---

# Story 3.6: Admin erasure-verification report UI consumes the D7 contract

Status: done

<!-- Note: Ultimate context engine analysis completed - comprehensive developer guide created. -->

## Story

As a DPO,
I want a verification report proving a party was shredded across projections,
so that I can prove the right to erasure was honored, not merely assert it.

## Acceptance Criteria

1. Given the D7 contract from Story 3.5 is in place, when I open the erasure-verification report for an erased party, then the Admin report shows the record confirmed shredded across projections, using `GetErasureCertificateAsync` results plus erasure status and mapping certificate/retry outcomes through the existing GDPR outcome live-region pattern with the correct politeness.
2. Given the D7 contract has not landed or capability reports certificate/retry unavailable, when the report surface is reached, then it degrades to a clear "verification not yet available" state with no fault, no raw backend detail, no PII, and the rest of the GDPR page remains fully usable.
3. Given an erased party, when the report renders, then it shows only stable bounded erased/verification state: verification status, erasure/report timestamp, verified/complete state, and at most bounded non-secret metadata derived from the certificate. It must not expose destroyed-key or cryptographic exception text, stale display names, contact values, identifiers, raw event payloads, state keys, ProblemDetails, tenant secrets, or cleanup internals.
4. Given verification is partial or failed and `CanRetryVerification` is true, when the DPO retries verification, then the existing `RetryErasureVerificationAsync` flow is used, routine accepted/completed outcomes announce politely, assertive failures use the existing alert behavior, and a successful retry refreshes erasure status and certificate without stale-party or stale-tenant state writes.
5. Given capability or tenant context changes while report requests are in flight, when any previous request completes, then `PartyGdprOperationsPanel` preserves its current stale-result guards (`TenantContextSignature`, party id, cancellation token, `_capabilityVersion`, and `IsCurrentOperation`) and does not mutate the new party's report state.
6. Given the AdminPortal RCL cannot reference the UI host just to consume shared host components, when implementing StatusKind/freshness/report presentation, then either reuse the existing AdminPortal GDPR outcome mapping already present in `PartyGdprOperationsPanel` or make an explicit shared-package/composition decision; do not add an RCL reference to `Hexalith.Parties.UI`.
7. Given the report panel renders in desktop and phone layouts, when tested with bUnit and Playwright, then controls and report content remain keyboard reachable, state is not color-only, status/failure announcements use the required `role=status`/`role=alert` split, forced-colors styles remain usable, and no text overflows or overlaps at narrow widths.

## Tasks / Subtasks

- [x] Expand the bounded report panel UI (AC: 1-3, 7)
  - [x] Update `src/Hexalith.Parties.AdminPortal/Components/ErasureVerificationReportPanel.razor` from the current two-field display into a scannable DPO report surface.
  - [x] Render localized labels through `AdminPortalLabels`; do not inline new user-facing strings in Razor.
  - [x] Show a clear unavailable/fallback state when `Certificate` is null or certificate capability is disabled.
  - [x] For a verified certificate, render bounded evidence only: status, timestamp, complete/verified copy, and optional key-version count if useful. Do not list raw key-version values unless a product/security decision explicitly approves that disclosure.
  - [x] Use semantic HTML (`section`, heading, `dl`/list/table as appropriate); avoid interactive `div`s and color-only state.

- [x] Preserve the existing GDPR operation orchestration (AC: 1, 4, 5)
  - [x] Keep report refresh inside `PartyGdprOperationsPanel` and its existing `RefreshErasureStatusAsync` / `RetryVerificationAsync` flow.
  - [x] Do not call lower-level EventStore, DAPR, erasure record store, or Parties actor-host services from the UI.
  - [x] Do not add a new browser-visible endpoint, a new public actor-host endpoint, or a second certificate/report state machine.
  - [x] Preserve `CanRetryVerification` gating: retry button appears only when capability allows retry and erasure status is partial/failed (`VerificationPartial`, `VerificationFailed`, `Partial`, or `Failed` today).
  - [x] After retry completes, refresh status and certificate through the existing client seams and stale-operation guards.

- [x] Improve fallback and failure states without regressing other GDPR operations (AC: 2, 4)
  - [x] Ensure `AdminPortalGdprCapability.ProvisionalBridge()` keeps certificate/retry disabled and does not call `GetErasureCertificateAsync` when refreshing an erased party.
  - [x] Keep the rest of GDPR actions usable under provisional support: erase status, restrict/lift, consent, export, and processing records.
  - [x] Map `ContractUnavailable` to bounded "verification not yet available" copy for the report, not a generic crash or raw 501 display.
  - [x] Keep routine completed/accepted outcomes polite and blocking/transient failures assertive through the existing `data-gdpr-operation-announcement` region.

- [x] Add or update tests at existing seams (AC: 1-7)
  - [x] Extend `tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs` for verified report rendering, unavailable fallback, no certificate call under provisional bridge, retry success refresh, retry/certificate failure mapping, and stale-party/tenant guards.
  - [x] Extend `tests/Hexalith.Parties.AdminPortal.Tests/Services/RecordingAdminPortalApiClient.cs` only as needed to queue retry outcomes or certificate failures; keep it a test double, not production logic.
  - [x] Extend `tests/e2e/specs/admin-gdpr-erasure-verification.spec.ts` to assert the final report content, no browser-visible EventStore gateway calls, no PII/raw-error strings, and retry button disappearance after completion.
  - [x] Add markup/privacy assertions for forbidden strings: `destroyed-key`, `key-material`, `stateKey`, `ProblemDetails`, raw contact/name/identifier seeds, raw payload, actor/state keys, and backend exception text.
  - [x] If CSS changes are needed, cover forced-colors and no raw teal/hex regressions with existing style guard patterns.

- [x] Validate with focused lanes (AC: 1-7)
  - [x] Run `dotnet build tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj -c Release --no-restore -m:1 -v:minimal`.
  - [x] Run focused AdminPortal bUnit tests for the GDPR component paths.
  - [x] Run `cd tests/e2e && npm run typecheck`.
  - [x] Run `cd tests/e2e && npx playwright test specs/admin-gdpr-erasure-verification.spec.ts --project=chromium --list`; run the test itself when the local server/socket environment permits.
  - [x] Run `bash scripts/check-no-warning-override.sh` and `git diff --check`.

## Dev Notes

### Current Implementation State

- Story 3.5 is done at commit `4a8b58c`. It replaced the client-side D7 stubs with a real EventStore gateway query/command path and deliberately left the full Admin erasure-verification report UI for this story. [Source: _bmad-output/implementation-artifacts/3-5-eventstore-erasure-verification-contract-backend-cross-submodule-approval-gated.md]
- Story 3.5 chose the existing EventStore projection-query actor route for `GetErasureCertificate`; it did not change the EventStore submodule, `/query` endpoint, `Program.cs`, or DAPR ACLs. Do not reopen that routing decision for this UI story. [Source: _bmad-output/implementation-artifacts/3-5-eventstore-erasure-verification-contract-backend-cross-submodule-approval-gated.md#Debug-Log-References]
- `IAdminPortalGdprClient` and `IPartiesAdminPortalApiClient` already expose `GetErasureCertificateAsync` and `RetryErasureVerificationAsync`. Reuse them. [Source: src/Hexalith.Parties.Client/AdminPortal/IAdminPortalGdprClient.cs] [Source: src/Hexalith.Parties.AdminPortal/Services/IPartiesAdminPortalApiClient.cs]
- `HttpAdminPortalGdprClient.GetErasureCertificateAsync` posts the `GetErasureCertificate` projection query through `/api/v1/queries`; `RetryErasureVerificationAsync` posts `RetryErasureVerification` through `/api/v1/commands`. The browser must not call either gateway route directly. [Source: src/Hexalith.Parties.Client/AdminPortal/HttpAdminPortalGdprClient.cs]
- `AdminPortalGdprCapability.Available()` enables certificate and retry; `ProvisionalBridge()` intentionally keeps them disabled with `ContractUnavailableReason`. Preserve that distinction. [Source: src/Hexalith.Parties.AdminPortal/Services/AdminPortalGdprCapability.cs]
- `PartyGdprOperationsPanel` already fetches certificates only when capability allows it and status is `Verified`, `Complete`, or `Erased`; otherwise `_erasureCertificate` stays null and the report panel renders the safe fallback. [Source: src/Hexalith.Parties.AdminPortal/Components/PartyGdprOperationsPanel.razor]
- `ErasureVerificationReportPanel` currently renders only `Certificate.VerificationStatus` and `Certificate.Timestamp`, or `Labels.GdprCertificateUnavailable` when null. This is the primary UI surface to expand. [Source: src/Hexalith.Parties.AdminPortal/Components/ErasureVerificationReportPanel.razor]
- `ErasureStatusPanel` nests `ErasureVerificationReportPanel` under the "Erasure certificate" heading and passes `Status`, `Certificate`, labels, and instance id context. Keep that composition unless a narrow change is required. [Source: src/Hexalith.Parties.AdminPortal/Components/ErasureStatusPanel.razor]

### Current Files Being Modified - Required Reading

- `src/Hexalith.Parties.AdminPortal/Components/ErasureVerificationReportPanel.razor`
  - Current state: display-only component with null fallback and a `dl` for certificate status/timestamp.
  - What this story changes: expands it into the bounded DPO proof/report presentation.
  - Preserve: encoded Razor output, localized labels, no service calls, no raw backend detail.
- `src/Hexalith.Parties.AdminPortal/Components/ErasureStatusPanel.razor`
  - Current state: wraps erasure status and nested certificate panel under stable headings.
  - What this story changes: possibly passes extra presentation flags or status context to the report panel.
  - Preserve: heading hierarchy, status fallback, instance-id based labels.
- `src/Hexalith.Parties.AdminPortal/Components/PartyGdprOperationsPanel.razor`
  - Current state: owns capability probing, erasure status refresh, certificate fetch, retry command, operation announcement politeness, busy state, cancellation, and stale-party/tenant guards.
  - What this story changes: only as needed to feed richer report state or labels.
  - Preserve: `IsCurrentOperation`, `IsCurrentCapabilityTarget`, cancellation-token behavior, `_capabilityVersion`, `TenantContextSignature`, `ClearSensitiveOperationState`, provisional-bridge no-certificate-call behavior, and the single operation announcement region.
- `src/Hexalith.Parties.AdminPortal/Services/AdminPortalLabels.cs`
  - Current state: label record for AdminPortal strings, including GDPR certificate, unavailable, retry, updated-at, and operation outcome copy.
  - What this story changes: add report-specific localized labels.
  - Preserve: no inline regulated copy in components.
- `tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs`
  - Current state: already covers provisional bridge fallback, retry button gating, GDPR outcome live-region politeness, privacy, and no raw details.
  - What this story changes: add final report assertions and regression tests around fallback/retry/certificate.
  - Preserve: xUnit v3, Shouldly, bUnit, existing helper conventions.
- `tests/e2e/specs/admin-gdpr-erasure-verification.spec.ts`
  - Current state: Story 3.5 E2E fixture proves real contract mode reads a bounded certificate and retries without browser-visible gateway calls.
  - What this story changes: assert the final report UI, not just raw `Complete` / `Verified` text.
  - Preserve: fixture reset/cookie setup, request capture assertions, no PII/raw-error checks.

### Privacy and Security Guardrails

- The UI report must not expose personal data. Do not render display names, contact values, identifiers, stale names, raw payloads, free-text restriction reasons, actor ids, state keys, raw ProblemDetails, stack traces, destroyed-key text, cryptographic exception text, cleanup exception details, tokens, claims, or tenant secrets. [Source: _bmad-output/project-context.md#Critical-Dont-Miss-Rules-anti-patterns--gotchas]
- `ErasureCertificate` contains `PartyId`, `TenantId`, `Timestamp`, `KeyVersionsDestroyed`, and `VerificationStatus`. Party and tenant ids are identifiers, so avoid showing them in visible copy unless an existing AdminPortal pattern already exposes the selected party id in a bounded way; the report does not need them to prove state to the current operator. [Source: src/Hexalith.Parties.Contracts/Security/ErasureCertificate.cs]
- `ErasureVerificationReport` and `ErasureVerificationStoreResult` exist in contracts, but Story 3.5 did not expose a UI client method to read report store results. Do not introduce a new report query for Story 3.6 unless the user explicitly scopes a backend/API story; build the UI from status + certificate + retry result. [Source: src/Hexalith.Parties.Contracts/Security/ErasureVerificationReport.cs] [Source: src/Hexalith.Parties.Contracts/Security/ErasureVerificationStoreResult.cs]
- If `KeyVersionsDestroyed` is displayed, prefer a bounded count such as "Key versions destroyed: 1" rather than values like `1, 2, 3`. The exact values are non-secret metadata per Story 3.5 only if approved, but the report does not need them for the DPO proof surface. [Source: _bmad-output/implementation-artifacts/3-5-eventstore-erasure-verification-contract-backend-cross-submodule-approval-gated.md#Acceptance-Criteria]

### Architecture and UI Guardrails

- AdminPortal is an RCL embedded by `Hexalith.Parties.UI`. The host-owned `StatusKind`, freshness, and shared domain components currently live in `Hexalith.Parties.UI`; do not make the AdminPortal RCL reference the UI host to reach them. [Source: _bmad-output/planning-artifacts/architecture.md#Format-Patterns]
- The existing AdminPortal GDPR mapping already turns `AdminPortalGdprOutcome.Accepted` and `Completed` into polite `role=status`, and validation/forbidden/missing tenant/transient/auth/not found/unknown into assertive `role=alert`. Use that unless the shared boundary is deliberately changed. [Source: src/Hexalith.Parties.AdminPortal/Components/PartyGdprOperationsPanel.razor]
- Follow the canonical politeness split: status/freshness/accepted-processing are polite; validation/transient/load failures are assertive. Never blanket-polite all outcomes. [Source: _bmad-output/planning-artifacts/architecture.md#Communication-Patterns]
- Keep the report in the existing GDPR page. Do not create a new route unless the current page cannot satisfy the story; the epic says `/admin/parties/{id}/gdpr` wraps erasure status, processing records, consent, portability, and verification. [Source: _bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md#Information-Architecture]
- Use FluentUI inherited components/tokens. Do not hard-code hex colors, do not use raw teal `#0097A7` for text-bearing controls, and keep forced-colors support if adding CSS. [Source: _bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/DESIGN.md#Colors]

### Previous Story Intelligence

- Story 3.5 left UI components unchanged except fixture/test support; the report panel can now receive a real certificate but still only shows a minimal status/timestamp view. This story owns the full report UX. [Source: _bmad-output/implementation-artifacts/3-5-eventstore-erasure-verification-contract-backend-cross-submodule-approval-gated.md#Completion-Notes-List]
- Story 3.5 validation passed focused builds and direct xUnit assemblies; `dotnet test --no-build` was locally blocked by IPC/socket permissions, and Playwright runtime was blocked by Kestrel socket binding permission. Expect the same local limitation and record it if it recurs. [Source: _bmad-output/implementation-artifacts/3-5-eventstore-erasure-verification-contract-backend-cross-submodule-approval-gated.md#Senior-Developer-Review-AI]
- Recent commits for Epic 3 are sequential GDPR work: 3.1 page, 3.2 erasure, 3.3 restriction/consent, 3.4 export/records, 3.5 D7 backend. Keep Story 3.6 focused on the Admin verification report and avoid broad AdminPortal restructuring. [Source: git log -5]

### Testing Standards

- Use xUnit v3, Shouldly, NSubstitute, and bUnit. Do not introduce Moq, FluentAssertions, or raw `Assert.*`. [Source: _bmad-output/project-context.md#Testing-Rules]
- Build with `Hexalith.Parties.slnx` or focused project builds in Release. Do not add `Version=` attributes to `.csproj`; Central Package Management owns versions. [Source: _bmad-output/project-context.md#Technology-Stack--Versions]
- Keep solution warnings as errors. Do not disable `TreatWarningsAsErrors`; run `bash scripts/check-no-warning-override.sh`. [Source: _bmad-output/project-context.md#Critical-Implementation-Rules]
- Existing relevant tests:
  - `PartiesAdminPortal_GdprOperations_ProvisionalBridge_EnablesSupportedOperationsAndBlocksContractUnavailableOnes`
  - `PartiesAdminPortal_GdprOperations_ProvisionalBridge_RefreshErasedPartySkipsCertificateAndAvoidsFailure`
  - `PartiesAdminPortal_GdprOperations_RetryVerificationRefreshesStatus`
  - GDPR outcome live-region tests for accepted/completed/assertive failures
  - `tests/e2e/specs/admin-gdpr-erasure-verification.spec.ts`

### Files Likely to Change

- `src/Hexalith.Parties.AdminPortal/Components/ErasureVerificationReportPanel.razor`
- `src/Hexalith.Parties.AdminPortal/Components/ErasureStatusPanel.razor`
- `src/Hexalith.Parties.AdminPortal/Components/PartyGdprOperationsPanel.razor`
- `src/Hexalith.Parties.AdminPortal/Components/PartyGdprOperationsPanel.razor.css` only if report layout needs CSS
- `src/Hexalith.Parties.AdminPortal/Services/AdminPortalLabels.cs`
- `tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs`
- `tests/Hexalith.Parties.AdminPortal.Tests/Services/RecordingAdminPortalApiClient.cs`
- `src/Hexalith.Parties.UI/Services/PartiesAdminPortalE2eFixture.cs`
- `tests/e2e/specs/admin-gdpr-erasure-verification.spec.ts`

### Out of Scope

- Do not modify `Hexalith.EventStore` or DAPR ACLs for this story.
- Do not add or change `GetErasureCertificate` / `RetryErasureVerification` wire contracts.
- Do not add `GetVerificationReport` UI client/API work in this story.
- Do not change export, processing-records, restriction, consent, or typed-erasure behavior except to preserve usability while the report is unavailable.
- Do not move shared UI host components into a package unless that is strictly required and documented as a small boundary decision.

### Latest Technical Information

- No external dependency or package upgrade is required for this story. The implementation should stay on the pinned local stack: .NET 10, FluentUI Blazor `5.0.0-rc.3`, xUnit v3, Shouldly, NSubstitute, bUnit, and existing Playwright E2E. [Source: _bmad-output/project-context.md#Technology-Stack--Versions]
- The relevant technical specifics are local contracts and existing code paths delivered by Story 3.5; no new browser API, EventStore route, or FluentUI package is needed.

### Project Structure Notes

- Keep AdminPortal adopter-facing and infrastructure-free from lower-level EventStore/DAPR dependencies.
- Keep all user-facing report text in AdminPortal labels/resources.
- Keep Razor output encoded; do not use `MarkupString`, `AddMarkupContent`, or raw HTML rendering for backend-provided values.
- Keep report UI bounded and terse for the Admin/DPO register.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story-3.6-Admin-erasure-verification-report-UI-consumes-the-D7-contract]
- [Source: _bmad-output/planning-artifacts/architecture.md#Communication-Patterns]
- [Source: _bmad-output/planning-artifacts/architecture.md#Format-Patterns]
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md#Flow-3-Priya-fulfills-an-erasure-request]
- [Source: _bmad-output/implementation-artifacts/3-5-eventstore-erasure-verification-contract-backend-cross-submodule-approval-gated.md]
- [Source: src/Hexalith.Parties.AdminPortal/Components/ErasureVerificationReportPanel.razor]
- [Source: src/Hexalith.Parties.AdminPortal/Components/PartyGdprOperationsPanel.razor]
- [Source: src/Hexalith.Parties.AdminPortal/Services/AdminPortalGdprCapability.cs]
- [Source: src/Hexalith.Parties.Client/AdminPortal/HttpAdminPortalGdprClient.cs]
- [Source: tests/e2e/specs/admin-gdpr-erasure-verification.spec.ts]

## Validation Summary

- Source discovery loaded project context, sprint status, planning epics, architecture, UX design and experience spine, accessibility and regulated-language reviews, previous Story 3.5, current AdminPortal GDPR components/services/tests, D7 client methods, certificate/report contracts, and recent git history.
- Checklist fixes applied before finalizing: kept Story 3.6 UI-only, pinned reuse of Story 3.5 client seams, documented the AdminPortal RCL vs UI-host boundary, required no-PII/raw-error report rendering, preserved provisional bridge fallback, and tied testing to existing bUnit/E2E seams.
- Latest-technology review found no new dependency requirement; implementation should use the pinned local stack and existing Story 3.5 contracts.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-06-10: Resolved bmad-dev-story customization; loaded project context and checklist.
- 2026-06-10: Preserved existing `baseline_commit: 4a8b58c`; moved sprint story status to in-progress before implementation.
- 2026-06-10: `dotnet test --no-build` was blocked by local named-pipe/socket permission (`SocketException (13): Permission denied`); used the built xUnit v3 in-process runner for bUnit execution.
- 2026-06-10: `npx playwright test specs/admin-gdpr-erasure-verification.spec.ts --project=chromium` was blocked by local Kestrel socket binding permission (`SocketException (13): Permission denied`); Playwright `--list` and TypeScript typecheck passed.
- 2026-06-10: `pwsh scripts/test.ps1 -Lane unit` printed repeated `Build failed with exit code: 1` lines from its `dotnet test --project` invocations without usable diagnostics; focused project build and in-process AdminPortal test assembly passed.
- 2026-06-10: `dotnet build Hexalith.Parties.slnx -c Release --no-restore -m:1 -v:minimal` reached Parties projects but failed on unrelated `Hexalith.PolymorphicSerializations` package warnings-as-errors (`NU5118`, `NU5128`).
- 2026-06-10: Senior review auto-fixed stale certificate fallback and non-verified certificate announcement regressions; `dotnet test --no-build` remained blocked by local named-pipe/socket permission, so the xUnit v3 in-process runner was used for focused bUnit tests.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Expanded the AdminPortal erasure verification report into a bounded DPO proof surface with localized copy, semantic `dl` evidence, polite report status, verified/completed state, timestamp, and key-version count only.
- Preserved the existing GDPR orchestration: status refresh and retry still flow through `PartyGdprOperationsPanel`, existing client seams, capability gates, and stale-operation guards; no new backend/browser-visible endpoint or state machine was added.
- Improved the unavailable/capability-disabled report state to show bounded "verification not yet available" copy without raw backend details while leaving other GDPR operations usable.
- Added bUnit coverage for verified report rendering, bounded fallback, provisional no-certificate-call behavior, retry refresh with certificate, certificate contract-unavailable mapping, and stale party guard behavior.
- Updated the Playwright erasure-verification spec to assert final report content, retry disappearance, no browser-visible gateway calls, and no PII/raw-error strings.
- Senior review fixed stale certificate clearing before certificate fetch failures and changed non-verified certificates to announce attention-needed copy instead of confirmed-copy.

### File List

- _bmad-output/implementation-artifacts/3-6-admin-erasure-verification-report-ui-consumes-the-d7-contract.md
- _bmad-output/implementation-artifacts/sprint-status.yaml
- src/Hexalith.Parties.AdminPortal/Components/PartyGdprOperationsPanel.razor
- src/Hexalith.Parties.AdminPortal/Components/ErasureStatusPanel.razor
- src/Hexalith.Parties.AdminPortal/Components/ErasureVerificationReportPanel.razor
- src/Hexalith.Parties.AdminPortal/Components/ErasureVerificationReportPanel.razor.css
- src/Hexalith.Parties.AdminPortal/Services/AdminPortalLabels.cs
- tests/Hexalith.Parties.AdminPortal.Tests/Components/PartiesAdminPortalComponentTests.cs
- tests/Hexalith.Parties.AdminPortal.Tests/Services/RecordingAdminPortalApiClient.cs
- tests/e2e/specs/admin-gdpr-erasure-verification.spec.ts

### Senior Developer Review (AI)

Reviewer: GPT-5 Codex on 2026-06-10

Outcome: Approved after auto-fixes. No critical issues remain.

Findings fixed:

- HIGH: Certificate refresh failures could leave a previously loaded certificate visible because `_erasureCertificate` was only cleared on non-fetch paths. Fixed by clearing report certificate state before attempting a certificate fetch, so failures render the bounded unavailable fallback.
- MEDIUM: `ErasureVerificationReportPanel` announced "Verification confirmed across projections" for any non-null certificate, including `Failed` or `Pending` certificate states. Fixed by deriving the live-region copy from the verified/complete state and adding attention-needed bounded copy for non-verified certificates.

Validation:

- `dotnet build tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj -c Release --no-restore -m:1 -v:minimal` - passed, 0 warnings.
- `dotnet test tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~GdprOperations|FullyQualifiedName~RetryVerification" -v:minimal` - blocked by local named-pipe/socket permission (`SocketException (13): Permission denied`).
- `dotnet tests/Hexalith.Parties.AdminPortal.Tests/bin/Release/net10.0/Hexalith.Parties.AdminPortal.Tests.dll -noLogo -parallel none -method ...` - passed 6 focused GDPR/report tests.
- `cd tests/e2e && npm run typecheck` - passed.
- `cd tests/e2e && npx playwright test specs/admin-gdpr-erasure-verification.spec.ts --project=chromium --list` - listed 2 tests.
- `bash scripts/check-no-warning-override.sh` - passed.
- `git diff --check` - passed.

### Change Log

- 2026-06-10: Implemented bounded Admin erasure-verification report UI, preserved existing GDPR orchestration, added bUnit/E2E coverage, and moved story to review.
- 2026-06-10: Senior review auto-fixed stale certificate fallback and non-verified certificate announcement handling; moved story to done.
