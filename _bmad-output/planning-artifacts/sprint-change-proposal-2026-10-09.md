---
title: Sprint Change Proposal — EXT-PARTIES-1 Branch B scope classification
date: 2026-10-09
author: Administrator
workflow: bmad-correct-course
mode: batch
status: decision-recorded
scope_classification: minor
decision: spec-only-external-consumer-extension
---

# Sprint Change Proposal — EXT-PARTIES-1 Branch B

## 1. Issue Summary

Sprint planning on 2026-10-09 returned **CONCERNS**: `EXT-PARTIES-1` Branch B
is an in-progress feature spec created on 2026-10-03, with no Parties UI PRD
requirement, epic/story, or sprint-status entry. It changes the public Parties
Contracts API, Party identity history, and host configuration. The existing
plan assigns Epics 7 and 8 maintenance scope only.

A search of `planning-artifacts/sprint-change-proposal-*.md` found no proposal
for `EXT-PARTIES-1`, Branch B authoritative identity, or opaque actor
attribution. This proposal is the first formal scope disposition for that work.
The owner [spec](../implementation-artifacts/spec-ext-parties-1-branch-b-authoritative-identity.md),
[implementation notes](../implementation-artifacts/ext-parties-1-implementation-notes.md),
[binding ADR](adr-consumer-party-id-binding.md), and
[retention policy](actor-history-retention-policy-v1.md) already define its
intent, decisions, implementation evidence, and incomplete live gates.

## 2. Impact Analysis

| Area | Finding and impact |
| --- | --- |
| Epics and stories | No existing Parties epic owns this consumer extension. Epics 7 and 8 retain maintenance-only scope; no story is added or reclassified. Agents Story 5.4 is an external dependency consumer, not a Parties story. |
| PRD | `parties-ui-prd.md` covers the UI MVP and freezes its FR/NFR heading inventory. A new requirement would expand its governed scope. Add a scope note only; keep all requirement IDs and mappings unchanged. |
| Architecture | The binding ADR and actor-history policy already own the new API/history/retention decisions. No UI architecture or Epic 8 spine change is needed. Owner qualification remains open. |
| UX | The extension adds no screen, journey, or interaction. The UX spines need no edit. |
| Technical and deployment | Public Contracts, Party replay/history, gateway authorization, Platform custody, host policy, and live compatibility verification remain governed by the owner spec. Local tests do not prove installed availability. |
| Tracking and readiness | Sprint-status keys are frozen. Keep the tracker unchanged except for the existing Epic 7 documentation action; report extension gates in a separate dated readiness addendum. |

`EpicEightClosureFitnessTests.EpicEightAddsNoPrdFunctionalRequirement` compares
`epics.md` against commit `37f4ec82` with `git diff --name-only`, and compares
the PRD's ordered FR/NFR heading IDs to that baseline. Therefore a new Epic 8
story or canonical PRD requirement would fail the gate. Placing a feature under
Epic 7 would also contradict its completed maintenance classification.

## 3. Recommended Approach

**Decision: option (b), spec-only external-consumer extension outside Parties
sprint tracking.** The owner spec remains the acceptance source and keeps its
`in-progress` status. The ADR and retention policy remain decision sources.
The PRD and readiness notes make the exclusion explicit. No epic, story,
requirement ID, or sprint-status key is created.

Direct backlog adjustment would require a new feature scope container, a PRD
requirement and a separate decision to reopen the PRD/epic freeze; it is not
justified for this Agents-driven dependency. Rollback of already implemented
owner work would not resolve the planning classification. MVP scope and the
Epics 7/8 schedule remain unchanged. Documentation effort is low; technical
and release risk remain material until the spec's production custody, restore,
complete-source and P-01–P-10 live gates pass. No delivery date is inferred.

## 4. Detailed Change Proposals

| Artifact | Before | After and reason |
| --- | --- | --- |
| `parties-ui-prd.md`, Current Implementation Evidence | Epics 6–8 are maintenance only; no EXT-PARTIES-1 classification. | Add an external-consumer scope note, with no new FR/NFR heading or traceability mapping. This keeps the canonical UI inventory stable. |
| `spec-ext-parties-1-branch-b-authoritative-identity.md` | Feature spec is `in-progress`, but does not state its Parties planning classification. | Add an unfrozen planning-classification section: spec-only, outside Parties UI PRD and sprint tracking; live acceptance remains open. |
| Readiness corpus | Existing report predates the extension. | Add `implementation-readiness-scope-addendum-2026-10-09.md` so readiness checks count neither this extension nor Epics 7/8 as MVP feature delivery. |
| Epic 7 retrospective and `sprint-status.yaml` action item | “Keep PRD, epics, and readiness docs explicit that Epics 7 and 8 are maintenance scope with no new PRD functional coverage.” | Keep that maintenance rule and name EXT-PARTIES-1 as separately governed spec-only work. Retain the action as open for ongoing readiness hygiene. |
| `epics.md` and sprint-status development keys | Frozen canonical epic/story inventory. | No change. The Epic 8 fitness test and stable tracker keys remain satisfied. |

## 5. Implementation Handoff

**Scope:** minor planning correction. The Parties documentation owner records
the classification in the PRD, spec, readiness addendum, retrospective, and
existing sprint-status action. The Parties/Platform/EventStore owners continue
the spec's technical and live qualification work. The Agents dependency owner
uses the separate owner acceptance evidence for its Story 5.4 decision.

Success criteria:

1. No new Parties PRD FR/NFR heading, epic/story, or sprint-status key appears.
2. Epic 7/8 maintenance wording and readiness counts stay intact.
3. EXT-PARTIES-1 remains visibly in progress with its own acceptance and live
   gates; local tests are not reported as production availability.
4. The Epic 8 zero-PRD fitness invariant and YAML structure remain valid.

## Change Analysis Checklist

| Checklist | Disposition |
| --- | --- |
| 1.1–1.3 Trigger, problem and evidence | [x] Sprint planning CONCERNS plus spec, ADR, policy and notes. |
| 2.1–2.5 Epic scope, dependencies and sequence | [x] No Parties epic change; Epics 7/8 remain maintenance. |
| 3.1–3.4 PRD, architecture, UX and other artifacts | [x] PRD scope note and readiness addendum; ADR/policy already govern technical decisions; UX N/A. |
| 4.1–4.4 Paths forward | [x] Direct feature tracking requires a separate replan; rollback and MVP change are unwarranted; choose spec-only extension. |
| 5.1–5.5 Proposal and handoff | [x] Recorded above with owners and success criteria. |
| 6.1–6.5 Review, approval and tracker | [x] User directed the decision and action update; no new tracker key; validate the frozen inventory and existing YAML in place. |
