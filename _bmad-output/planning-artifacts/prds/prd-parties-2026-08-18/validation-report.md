# Validation Report — Parties UI PRD

- **PRD:** `_bmad-output/planning-artifacts/parties-ui-prd.md`
- **Rubric:** `.agents/skills/bmad-prd/assets/prd-validation-checklist.md`
- **Run at:** 2026-09-08T11:30:57+02:00
- **Grade:** Fair

## Overall verdict

The v1.2.0 correction closed the Fair-grade identity holes: NFR rows exist, UX-DR1–16 are enumerated against `epics.md`, change control is real, the role model and the worst untestable phrases were tightened, and KMS is a named deployment gate. What is at risk is the new readiness surface itself. The NFR "Verified By" column cites a five-job CI topology this repository does not run, so a tool that extracts coverage gates from the Traceability Matrix will chase jobs that are not there. Residual scope cracks — the party picker missing from FR-Admin-3, NFR1 scoped to "Consumer-facing" while UX-DRs claim product-wide AA, and UX-DRs plus KMS sitting off the matrix the contract names — matter because this file is the chain-top index.

The adversarial and source-fidelity reviewers shift the picture on two verified facts and one remaining structural attack. Story 8.9 is stated as `backlog` and is `blocked` as of `sprint-status.yaml` 2026-09-07; the Epic 8 PRD freeze is now inventory-only (currency edits do not break CI unless `### FR-*` / `### NFR*` headings change). The adversarial reviewer re-rated three findings critical (identity-only canonicity, matrix still hiding UX-DRs/KMS, false CI topology); this synthesis consolidates them to high — the evidence-based reviewers corroborate the CI and 8.9 facts at high, and the identity split is a documented decision the rubric still calls adequate. Together with the remaining extraction gaps they keep the grade at Fair rather than Good.

## Dimension verdicts
- Decision-readiness — adequate
- Substance over theater — strong
- Strategic coherence — adequate
- Done-ness clarity — adequate
- Scope honesty — adequate
- Downstream usability — thin
- Shape fit — strong

## Findings by severity

Severities below are the consolidated (synthesis) ratings. Where a reviewer's original rating differed, it is noted. Duplicated findings across reviewers are merged, with corroborating reviewers named.

### Critical (0)

None after consolidation. The adversarial reviewer's three critical-rated findings appear under High with their original rating noted; their full adversarial statements are preserved in `review-adversarial-general.md`.

### High (7)

**[Rubric + Adversarial + Source-fidelity]** — NFR "Verified By" cites a CI topology this repository does not run (§Traceability Matrix NFR1 / NFR9 vs `.github/workflows/ci.yml`) *(adversarial rated critical)*
The extractable gate names (`lint`, `test`, `ui-a11y`, `contract-test`, `report`) are not workflow jobs. Current CI is a reusable `Hexalith.Builds` `domain-ci.yml@main` caller whose jobs are `build-and-test`, `aspire-tests`, and `performance-tests`. Playwright a11y still lives in `tests/e2e`; Pact is unscaffolded at the root. A tool that trusts this column will look for gates that do not exist and can still report green.
Fix: Name the jobs and test projects that actually run (`ci.yml` / `domain-ci.yml` tiers, plus the local Playwright lane), and mark NFR1 Playwright as out-of-CI until a workflow exists.

**[Adversarial + Source-fidelity]** — Story 8.9 is stated as `backlog` but is `blocked` today (§Current Implementation Evidence vs `sprint-status.yaml`) *(rubric rated low)*
The as-of 2026-08-18 roster still matches every Epic 8 row except 8.9. `sprint-status.yaml` (`last_updated: 2026-09-07`) marks `8-9-ui-frontcomposer-and-fluent-consolidation: blocked`. The 2026-09-06 key-freeze SCP ratified that status and chose "No PRD FR/NFR edits." FR identity is unaffected; a readiness run that trusts the roster without reconciling sprint-status would treat 8.9 as unstarted.
Fix: Restamp the as-of date and change “8.9 `backlog`” to “8.9 `blocked`”, or drop the story-level roster and point at `sprint-status.yaml` as the only status source. Currency-only; does not break the inventory freeze.

**[Adversarial]** — Identity-only canonicity is a dodge, not a fix (§Purpose vs §Source Artifacts vs §Document Control) *(adversarial rated critical; rubric did not file)*
The file still opens as the canonical readiness source, then confines canonicity to identity and scope and tells tooling that story records win on completed-work evidence. Sources remain unpinned. The rubric treats this split as a usable, stated decision; the adversarial reviewer says a readiness source that cannot produce a red verdict on substance is still false.
Fix: Either make this file win — pin sources, put ACs and NFR thresholds here — or rename it a requirements index and stop telling readiness tooling this is the source.

**[Rubric + Adversarial]** — Readiness contract extracts from a matrix that still omits UX-DRs and the KMS gate (§Document Control, §Traceability Matrix, §UX Requirements, §Deployment Gates) *(adversarial rated critical; rubric rated medium)*
Document Control tells tooling to extract identity and epic/surface mapping from the Traceability Matrix. That matrix still has nine FR rows and nine NFR rows. UX-DR1–16 and the production-KMS go-live gate have no matrix row. Enumerating UX-DRs in a prose list closes the old "unrecoverable IDs" finding for a human; it does not put them on the surface the new contract blesses.
Fix: Add UX-DR and GATE-KMS (or NFR) rows to the same matrix, or change the contract to name every extractable section.

**[Adversarial]** — Still no acceptance criteria, no NFR thresholds, and no waived-category list (§Functional Requirements, §Non-Functional Requirements)
Every FR is still capability prose. NFR2 still calls freshness "first-class" with no bound. There is still no performance, latency, availability, or browser-support NFR, and no sentence that those categories are waived. `.memlog.md` records this as an explicit deferral ("revisit if PRD becomes chain-top again").
Fix: Attach 2–5 observable ACs per FR; quantify each NFR or list waived categories with a reason — or keep the deferral and stop claiming this file is the readiness source for substance.

**[Rubric + Adversarial]** — Party picker is a delivered MVP surface with no FR row (§FR-Admin-3, §Traceability Matrix vs `epics.md`) *(rubric rated medium)*
Epics FR-Admin-3 includes the in-form `<hexalith-party-picker>`; Story 2.5 is `done`. This PRD's FR-Admin-3 names a radiogroup and omits the picker. UX-DR7 exists only in the prose list the readiness contract does not extract.
Fix: Restore the picker to FR-Admin-3 (or add FR-Admin-5) and map it on the matrix to the create/edit routes plus the picker host.

**[Adversarial]** — KMS is still a paragraph, not a fail-closed gate (§Deployment Gates, §Out of MVP Scope, `docs/architecture.md`) *(rubric rated medium)*
There is no requirement ID, no matrix row, and the item remains listed as out of MVP. Verification is "read the GDPR notice in `docs/index.md`." Brownfield architecture still warns that `LocalDevKeyStorageBackend` is the only registered store and that there is no production-environment rejection guard.
Fix: Give it a stable ID (`GATE-KMS` or NFR), put it on the matrix, keep it out of Out of MVP Scope, and name the fail-closed check if one exists — or state that the gate is documentary-only.

### Medium (8)

**[Rubric + Adversarial]** — NFR1 scopes AA to Consumer-facing while UX-DRs and Admin GDPR are product-wide (§NFR1, §UX-DR9, §UX-DR12)
NFR1 opens "Consumer-facing surfaces target WCAG 2.2 AA." UX-DR12 is product-wide. Typed-name erasure confirmation, the party-picker combobox, and admin sheets are Admin surfaces. A tool that honors the "Consumer-facing" sentence can skip Admin a11y and still pass NFR1.
Fix: State the AA target as product-wide, or list excluded Admin surfaces, and point verification at tests that actually cover Admin GDPR and the picker.

**[Rubric]** — NFR7's owned UX-DR range disagrees with its matrix cell (§NFR7 vs §Traceability Matrix NFR7)
Body: "UX-DR1 through UX-DR7." Matrix: "UX-DR1–UX-DR3." An extractor cannot tell whether brand discipline owns three token DRs or seven domain deltas.
Fix: Make the body and the matrix cell name the same UX-DR set.

**[Adversarial + Source-fidelity]** — UX-DR one-liners drop criteria `epics.md` still gates (§UX Requirements vs `epics.md`) *(adversarial rated high; source-fidelity rated low)*
v1.2.0 printed sixteen labels. Material omissions include UX-DR8's assertive half, UX-DR5 degraded/"showing last known", UX-DR12's live gap, and UX-DR6's danger-token / typed-name split. The PRD's precedence rule already sends detailed semantics to `epics.md`.
Fix: Paste the `epics.md` one-sentence MUST per UX-DR, including current-gap clauses, or stop listing UX-DRs here and extract them from `epics.md` with a pinned revision.

**[Adversarial]** — The Epic 8 inventory freeze cannot see the sections added to close the last review (`EpicEightClosureFitnessTests.ExtractRequirementIds`)
The regex sees heading-shaped `FR-*` / `NFR*` IDs only. UX-DR bullets, Deployment Gates, and the NFR table are invisible. The freeze would still pass if every UX-DR vanished. Source-fidelity confirms this is now inventory-only, not byte-for-byte, and that currency edits are allowed.
Fix: Extract UX-DR and GATE IDs too, or stop claiming the freeze protects readiness extractability.

**[Adversarial]** — NFR "Verified By" cells are unexecutable except the ones that are wrong (§Traceability Matrix NFR2–NFR8)
Cells such as "freshness contract tests" and "GDPR and consent surface copy" name no test class, job, or command.
Fix: One named job or test class per NFR, or an explicit `unverified` token.

**[Adversarial]** — FR rows still have no verification column (§Traceability Matrix FR table)
NFRs got a (false) gate column; the nine product FRs still map only to an epic and a path.
Fix: Add the same Verified-By column to the FR table with story IDs and test names.

**[Adversarial]** — Live freshness (SignalR) is a done Epic 1 requirement absent from this PRD (§NFR2 vs `epics.md` AR-D6 / Story 1.7)
NFR2 mentions last-known rendering and optimistic echo, not SignalR subscribe + degraded-poll fallback.
Fix: Add the SignalR subscribe + degraded-poll fallback as a normative NFR2 clause, or an FR-Shell/NFR2 AC.

**[Source-fidelity]** — Post-2026-08-18 planning left the PRD’s implementation snapshot frozen on purpose (§Current Implementation Evidence; 2026-09-06 SCP)
The 2026-09-06 SCP correctly said the PRD has no FR/NFR impact (I15) and then left a dated story-status table that readiness tooling is told to extract.
Fix: Keep the zero-new-FR rule, but treat the evidence snapshot as a living pointer to `sprint-status.yaml`.

### Low (3)

**[Rubric + Adversarial]** — Residual adverbial copy requirements (§FR-Consumer-3, §FR-Consumer-4)
"Grant and withdraw consent honestly" and "Copy must be plain, honest" remain adjectives. UX-DR13–16 own the observable copy rules.
Fix: Drop the adverbs or replace them with the already-stated observables.

**[Source-fidelity]** — UX-DR one-liners drop definitional halves that `epics.md` still requires (§UX Requirements)
Covered in more detail under Medium. Residual compression on UX-DR5/9/10/13/16.
Fix: Add the assertive/degraded/typed-name clauses if the one-liners are the extractable UX inventory.

**[Source-fidelity]** — Admin route templates use `{id}` while `@page` parameters are `{RoutePartyId}` (§Traceability Matrix)
Paths match. `epics.md` also writes `{id}`. Parameter-name drift, not a missing surface.
Fix: Optional note that the Blazor parameter is `RoutePartyId`.

## Mechanical notes
- Closed since 2026-08-18 (not re-filed): frontmatter `last_updated` / `version` / changelog; NFR identity table; UX-DR1–16 enumerated with `epics.md` as ID authority; FR-Shell role/landing rules; NFR3 now-vs-deferred seam; Epic 6 in the maintenance invariant; D7 / named GDPR seams; cancellation event; NFR8 `Hexalith.Commons.ServiceDefaults`; KMS promoted into §Deployment Gates.
- All six §Source Artifacts paths present; KMS verification paths `docs/index.md` and `docs/getting-started.md` exist; `docs/deployment-security-checklist.md` remains retired.
- Sprint-status roundtrip: Epics 1–7 `done`, Epic 8 `in-progress`. Story-level drift since 2026-08-18: 8.9 `backlog` → `blocked`; 8.7 / 8.8 remain `blocked`; 8.10 remains `review`.
- ID continuity: FR-Shell + FR-Admin-1..4 + FR-Consumer-1..4 unique; NFR1–NFR9 contiguous; UX-DR1–UX-DR16 contiguous. Document Control declares which classes count as functional requirements.
- Assumptions Index: vacuously consistent — no `[ASSUMPTION]` or `[NOTE FOR PM]` tags.
- No UJs — shape-appropriate. Glossary still absent; `party_id`, "Bound Consumers", "last-known", "tombstone" remain consistent.
- Epic 8 PRD freeze is inventory-only (`ExtractRequirementIds` on `### FR-*` / `### NFR*` headings). `epics.md` is still a git-diff freeze. Current and baseline inventories are the same 18 IDs. Currency edits do not break the gate unless those headings are added, removed, or reordered.

## Reviewer files
- `review-rubric.md`
- `review-adversarial-general.md`
- `review-source-fidelity.md`
