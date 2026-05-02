# Sprint Change Proposal: Epic 3 Search Scope Alignment

Project: Hexalith.Parties  
Date: 2026-05-01  
Trigger: Epic 3 retrospective significant discovery  
Mode: Batch, implemented directly from project lead request

## 1. Issue Summary

Epic 3 implementation aligns with Architecture D2: v1.0 search is display-name-only, backed by `PartyIndexEntry` and match metadata that emits `displayName`. Planning artifacts still implied email and identifier search in the MVP.

Evidence:

- Epic 3 retrospective records the mismatch as a significant discovery.
- Story 3.3 and Story 3.4 implementation records state that email and identifier search are deferred.
- The v1.0 index projection tracks contact and identifier events for freshness, but does not store searchable email or identifier fields.

Core problem:

- Issue type: misunderstanding/drift between original requirements wording and implemented architecture.
- The implementation is not wrong; the planning artifacts overstated delivered v1.0 search behavior.

## 2. Impact Analysis

Epic impact:

- Epic 3 remains valid after wording correction.
- Epic 4 composite command work is not invalidated.
- Epic 5 MCP identity-resolution language needed clarification because `find_parties` cannot promise email or identifier matching in v1.0.
- Epic 8 already contains projection lifecycle scope through Story 8.3; the retro action now explicitly ties D14/D15 to operational readiness.
- Epic 10 admin search language now distinguishes baseline display-name search from future email/identifier search.

Story impact:

- Story 3.2: contact/identifier events update index freshness metadata only.
- Story 3.3: search endpoint is display-name-only.
- Story 3.4: tests verify display-name match metadata and negative email-query behavior, not email/identifier indexing.
- Epic 5 find/search story: AI agents must retrieve candidate details when email or identifier evidence is needed.

Artifact conflicts resolved:

- PRD MVP scope and FR15/FR17/FR20 now state display-name-only v1.0 search.
- Epics and story acceptance criteria now match the implemented projection model.
- Product brief now separates default projection search from future dedicated search capabilities.

Technical impact:

- No code rollback or production code change required.
- Future email/identifier search requires a deliberate search model change, not a controller-only query tweak.
- Privacy surface remains constrained because email and identifier values are not copied into the v1.0 index.

## 3. Recommended Approach

Recommended path: Direct Adjustment.

Rationale:

- The implementation already follows the accepted architecture.
- The fix is primarily planning and backlog alignment.
- Rollback would add risk without simplifying the problem.
- MVP scope remains achievable because display-name search, list, filtering, and detail retrieval are already coherent.

Effort estimate: Low.  
Risk level: Low.  
Timeline impact: None for current implementation; future search capability remains explicit roadmap work.

## 4. Detailed Change Proposals

### PRD

Section: MVP Included Scope / Functional Requirements

Old:

```text
Read projection: paginated list, search by name/email/identifier with match metadata...
FR15: Consumer can search parties by name, email, or identifier
FR20: AI agent can search and resolve parties by name, email, or identifier...
```

New:

```text
Read projection: paginated list, display-name search with match metadata...
FR15: Consumer can search parties by display name in MVP. Email and identifier search are deferred...
FR20: AI agent can search and resolve parties by display name via a dedicated AI-optimized interface in MVP...
```

Rationale: Aligns product scope with Architecture D2 and the delivered `PartyIndexEntry` model.

### Epics

Section: Epic 3 / Stories 3.2-3.4

Old:

```text
search by name/email/identifier
PartyIndexProjectionHandlerTests ... email/identifier indexing
match metadata indicates the email field was matched
```

New:

```text
display-name search with match metadata
contact/identifier event freshness updates
email and identifier match metadata are reserved for the future dedicated search capability
```

Rationale: Prevents future stories from treating email/identifier search as already delivered.

### Product Brief

Section: Query side / MVP scope / REST and MCP surfaces

Old:

```text
default projection with search by name, email, identifier, and semantic search included in v1
```

New:

```text
default projection with paginated list, filtering, and display-name search included in v1
```

Rationale: Keeps early product framing consistent with final architecture and implementation.

### Carry-Forward Backlog

Added an explicit Post-Epic 3 Corrective Backlog to `epics.md`:

- no-PII logging regression coverage for command and query paths
- composite-to-projection regression coverage
- actor runtime readiness checklist for future projection stories
- projection lifecycle work remains explicit through D14/D15 and Story 8.3

## 5. Implementation Handoff

Scope classification: Moderate.

Reason:

- Planning artifacts were corrected directly.
- Follow-up work spans quality, projection regression testing, and process readiness.
- No architecture replan is required.

Handoff:

- Product Owner: verify stakeholder-facing wording now matches display-name-only v1.0 search.
- Developer/Senior Dev: add no-PII logging regression coverage when the next quality-hardening story is opened.
- QA: add composite-to-projection regression coverage if not already covered by current test inventory.
- Architect: keep D14/D15 projection lifecycle behavior visible in operational readiness validation.

Success criteria:

- Planning artifacts no longer imply email/identifier search as v1.0 delivered behavior.
- Future work treats email, identifier, and semantic search as dedicated search capability scope.
- Automated tests eventually cover no-PII logging and composite-to-projection replay.
- Projection readiness checks include actor interfaces, DI, options, actor IDs, state keys, flush behavior, and query consistency.

## Checklist Results

- [x] 1.1 Triggering story/epic identified: Epic 3 retrospective after Stories 3.2-3.4.
- [x] 1.2 Core problem defined: planning drift, not implementation defect.
- [x] 1.3 Evidence gathered from retrospective, story records, PRD, epics, product brief, and architecture.
- [x] 2.1 Epic 3 remains completable as implemented.
- [x] 2.2 Required epic changes applied as wording/scope corrections.
- [x] 2.3 Future epics reviewed for MCP/admin search wording.
- [x] 2.4 No epic invalidated; carry-forward quality work documented.
- [x] 2.5 No resequencing required.
- [x] 3.1 PRD conflicts corrected.
- [x] 3.2 Architecture conflict reviewed; architecture already aligned with display-name-only v1.0 search.
- [N/A] 3.3 UI/UX specs: no separate UX artifact found.
- [x] 3.4 Secondary artifacts reviewed; product brief and epics updated.
- [x] 4.1 Direct Adjustment selected.
- [x] 4.2 Rollback evaluated and rejected.
- [x] 4.3 MVP review evaluated; no MVP reduction beyond corrected wording.
- [x] 4.4 Recommended path selected.
- [x] 5.1 Issue summary created.
- [x] 5.2 Epic and artifact impacts documented.
- [x] 5.3 Recommended path documented.
- [x] 5.4 MVP impact and action plan documented.
- [x] 5.5 Handoff plan documented.
- [x] 6.1 Checklist completion reviewed.
- [x] 6.2 Proposal accuracy reviewed against edited artifacts.
- [x] 6.3 Approval basis: implemented from project lead request to fix the retro issue.
- [N/A] 6.4 Sprint status update: no formal story IDs added or removed.
- [x] 6.5 Next steps and handoff plan documented.
