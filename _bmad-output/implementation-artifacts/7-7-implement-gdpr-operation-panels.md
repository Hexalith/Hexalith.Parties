# Story 7.7: Implement GDPR Operation Panels

Status: blocked

## Story

As a Parties administrator or DPO,
I want compact GDPR operation panels for a selected party,
so that I can trigger and monitor erasure, restriction, consent, portability, and processing-record workflows.

## Acceptance Criteria

1. Given an administrator opens the GDPR panel for a selected party, when the panel renders, then it shows operation sections for erasure, erasure status/certificate, verification retry, restrict/lift restriction, consent add/revoke/history, portability export, and processing records where supported, and each section uses the accepted Parties client/query/command contract.
2. Given an erasure request is submitted, when confirmation is required, then the confirmation displays party id only, and it returns command accepted outcome before refreshing authoritative erasure status.
3. Given erasure certificate or verification status is requested, when the operation completes, then the panel shows safe verification results, and generated filenames use party id plus timestamp only.
4. Given restriction is applied or lifted, when the administrator submits a bounded reason where required, then the operation returns command accepted outcome, and the panel refreshes current status before enabling follow-on actions.
5. Given consent is added, revoked, or viewed, when the administrator uses consent controls, then consent is scoped per channel and per purpose, and no party-wide or tenant-wide consent shortcut is offered.
6. Given portability export or processing records are requested, when the operation completes, then outputs use safe filenames, content types, bounded summaries, and safe correlation links, and exported payloads or raw processing details are not logged.
7. Given GDPR panel tests run, when they cover all supported flows, disabled unsupported flows, safe filenames, bounded reasons, stale state, redaction, and tenant switch, then GDPR operations remain usable and privacy-safe.

## Blocker

Story 7.7 is blocked by `_bmad-output/planning-artifacts/dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md`.

That dependency is still `status: Required` and says Story 7.7 must not be scheduled until the accepted EventStore-fronted Parties client/gateway contract is updated to `Satisfied` or `Risk Accepted` and linked from sprint planning.

Story 7.6 added the fail-closed capability gate for this exact condition. Implementing the 7.7 operation panels now would bypass the planning gate and would require guessing the accepted typed command/query contract.

## Required To Unblock

- Update `_bmad-output/planning-artifacts/dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md` to `Satisfied` or `Risk Accepted`.
- Link the accepted contract reference from sprint planning or story metadata.
- Confirm the accepted contract includes typed GDPR command methods, query/status/certificate methods, capability detection semantics, and bounded failure behavior for unauthorized, forbidden, gone/erased, degraded, timeout, malformed, and contract-unavailable states.

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-05-22 | 0.1 | Created blocked story artifact because the accepted EventStore-fronted Parties client/gateway contract remains unsatisfied. | Codex |

## Dev Agent Record

### Agent Model Used

Codex GPT-5

### Debug Log References

- Loaded Story 7.7 source from `_bmad-output/planning-artifacts/epics.md`.
- Loaded dependency gate `_bmad-output/planning-artifacts/dependency-eventstore-fronted-parties-client-gateway-2026-05-17.md`; status is still `Required`.
- Confirmed readiness follow-up explicitly requires the dependency to be `Satisfied` or `Risk Accepted` before scheduling Story 7.6 or Story 7.7.

### Completion Notes List

- No production implementation started because the story is blocked by a required planning dependency.
- Story 7.6 already provides fail-closed UI behavior until the accepted contract exists.

### File List

- `_bmad-output/implementation-artifacts/7-7-implement-gdpr-operation-panels.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/story-automator/orchestration-1-20260521-062818.md`
