# Spine Pair Review — parties

_Rubric-walker pass, 2026-08-18. Validates `DESIGN.md` + `EXPERIENCE.md` (both `status: final`, 2026-06-09) as the source-extraction contract for downstream consumers. Prior run (2026-06-09) and its documented residuals in `.decision-log.md` were honored — documented residuals are marked "known residual", not re-flagged._

## Overall verdict

A clean, genuinely source-extractable delta-spine pair: canonical shape on both files, committed tokens with load-bearing contrast caveats spelled out, a State Patterns section that actually carries the product's hardest problem (eventual consistency), and all five mocks linked with spine-wins stated. The one structural risk is **source drift**: the `sources` file (`project-context.md`) was revised 2026-06-21 — twelve days *after* finalize — and now carries consumer-binding (`/no-party-binding`) and Art. 18 restriction rules the spine pair does not reflect. Everything else found is medium-or-below polish on an otherwise strong contract.

## 1. Flow coverage — adequate

Extracted user-facing capabilities from `project-context.md` (Consumer portal / consent / GDPR rules + MCP/UI sections): admin party CRUD (create/find/view/update/erase), GDPR consent grant/withdraw, restriction/lift (Art. 18), erasure (admin + consumer front doors, cancel, verification), export (Art. 20), processing records (Art. 30), consumer self-service view/edit, sign-in/role routing, consumer→party claim binding. Checked each against EXPERIENCE.md IA + Key Flows. All four Key Flows have a named protagonist, numbered steps, a bold **Climax** beat, and an explicit Failure path — the mechanical shape is flawless. Export (Flow 1), consent withdrawal (Flow 2), admin erasure incl. verification (Flow 3), and create/link via picker (Flow 4) are covered end-to-end.

### Findings
- **high** The unbound-consumer path is absent everywhere: `project-context.md` (Consumer portal rules, "Consumer→Party binding is claim-based and fail-closed") mandates zero/multiple `party_id` claims → redirect `/no-party-binding`, yet that surface/state appears in neither the IA table, State Patterns, nor any flow (EXPERIENCE.md §Information Architecture, §State Patterns). Every mis-provisioned consumer hits this dead end with no specified copy or recovery. Root cause is source drift (see §7), but the downstream impact lands here. *Fix:* add a `No party binding` state row (fail-closed, reassuring copy, support path) and a `/no-party-binding` IA entry.
- **medium** Consumer self-service erasure — the emotionally central GDPR journey (`RequestMyErasureAsync` / `CancelMyErasureAsync` in the source) — has no Key Flow. Its copy (Voice and Tone rows 1–2) and state row ("Erasure requested / in progress") exist, but the request→cancel-window→permanent narrative, including where the **Cancel** control lives, is never walked (EXPERIENCE.md §Key Flows). *Fix:* add a fifth flow (Marc requests deletion, sees the cancellable state, optionally cancels) or extend Flow 1.
- **low** Restriction (Art. 18 restrict/lift) and Edit-my-profile have surfaces and component rules but no flow; both are simple enough that the tables carry them (EXPERIENCE.md §Key Flows). *Fix:* optional — a one-line note that these are table-covered by design.

## 2. Token completeness — strong

Extracted every frontmatter token and all 26 `{path.to.token}` prose references in DESIGN.md; **every reference resolves** against the YAML. Owned colors carry hex (`accent` #0097A7); the delta-only inheritance strategy is executed cleanly — every inherited value is a *named* Fluent 2 custom property (`var(--colorStatus…)`, `--fontSizeBase300/400`, `--borderRadius*`), unambiguous for a consumer with the Fluent 2 catalog. Contrast targets are stated exactly where load-bearing: raw accent 3.51:1 non-text-only, `brand-fill` → `--colorBrandBackground` (≈#00767f, AA), status *token pairs*, the 4.44:1 warning-tint caveat. Verified all 5 mocks bind `--brand-fill:#00767f` as the decision log claims.

### Findings
- **medium** `components.gdpr-destructive-button.background` binds a **Foreground** token (`--colorStatusDangerForeground1`) as a button *fill* (DESIGN.md frontmatter + §Components "GDPR destructive button"). In light theme that red carries white text acceptably, but in dark theme Fluent flips `*Foreground1` to a light tint designed for text-on-dark — as a fill under white text it will fail AA, and the button's own text color is never specified. The spine's own rule ("use matched token *pairs*… do not hand-mix a status foreground") argues against this binding. *Fix:* bind the fill to the danger *background* ramp (e.g. `--colorStatusDangerBackground3` with its paired foreground) and state the text color.

## 3. Component coverage — adequate

Extracted every component named in either spine. The four domain components (party-state badge, freshness indicator, GDPR button, party picker) each have a real DESIGN.md visual row *and* a real EXPERIENCE.md behavioral row — genuinely two-sided specs, with the picker's row carrying the full ARIA combobox contract and its real `PartyPickerSearchState` machine. Inherited components are governed by the explicit "as-is, unchanged" inventory. Picker FAST-token re-skin: known residual (design debt, logged).

### Findings
- **medium** The **consent control** — a first-class, GDPR-load-bearing component with a rich behavioral row (EXPERIENCE.md §Component Patterns) — has no visual home: no DESIGN.md Components row, and no switch component (`FluentSwitch` or equivalent) in DESIGN.md's as-is inventory (DESIGN.md §Components). A story-dev must guess what renders it. *Fix:* add `FluentSwitch` to the as-is list (or a brief DESIGN row if the visual deviates).
- **low** `FluentDialog` (used 3× in EXPERIENCE.md — banned-natives rule, typed-confirm, Flow 3), the command-result toast, and the cold-load skeleton are behaviorally specified but their rendering components are absent from DESIGN.md's inherited inventory (DESIGN.md §Components). *Fix:* extend the as-is list with `FluentDialog`, the toast/message-bar component, and the skeleton component.
- **low** Name drift across spines: "Data-freshness indicator" (DESIGN) vs "Freshness indicator" (EXPERIENCE); "GDPR destructive button" (DESIGN) vs "GDPR action button" (EXPERIENCE — deliberately wider, covering reversible outline actions too, but the widening is implicit). *Fix:* align names or note the widening explicitly.

## 4. State coverage — strong

Walked all nine IA surfaces against the State Patterns table. Coverage is the pair's best work: 14 state rows mapped to the *real* `StatusKind` / `PartyPickerSearchState` enums, including the states most specs forget — `DisplayNameOnly` partial projections, accepted-but-processing optimistic echo, `TenantUnavailable` warming copy, tombstoned `Gone`. Offline is a documented "no" (decision log concern scan). Focus lives correctly in Interaction Primitives. Erasure gets the honest two-state treatment (cancellable vs permanent).

### Findings
- **medium** No state row for **restricted** as a *surface treatment*: `restricted` exists as a badge value, but nothing says what restriction does to the UI — which admin actions gray out, what the consumer sees, and the source's subtle Art. 18(3) rule that **consent edits stay allowed while restricted** (but reject during erasure) (`project-context.md` "Restriction (Art.18) guards are subtle"; EXPERIENCE.md §State Patterns). Source-drift-related (rule added 2026-06-21). *Fix:* add a `Restricted` state row: badge + which controls stay live (consent toggles) vs disabled, with the erasure-in-progress exception.
- **low** Export has flow-only coverage: "preparing" and "ready for download" states exist in Flow 1 narrative but not as State Patterns rows, so they're extractable only from prose (EXPERIENCE.md §Key Flows Flow 1 vs §State Patterns). *Fix:* one `Export preparing / ready` row.
- **low** My-consent empty state (consumer with zero defined consent purposes) unspecified; the `Empty (NoData)` row is admin-list-shaped (EXPERIENCE.md §State Patterns). *Fix:* extend the Empty row's surface column.

## 5. Visual reference coverage — strong

mockups/ holds exactly 5 files (`signin`, `admin-parties`, `create-edit-party`, `consumer-profile`, `consumer-privacy`); no imports/ or wireframes/ exist. All 5 are linked inline from EXPERIENCE.md §IA with a parenthetical naming what each illustrates, re-mapped per-flow at §Key Flows; DESIGN.md additionally links the two component-bearing mocks at §Components. Zero orphans (the 5 `.working/key-*.html` files are pre-promotion drafts of the same five — expected). "Spine wins on conflict" is stated in both spines. Phone-reflow mock for the Admin master-detail: known residual (deferred, documented). No findings.

## 6. Bloat & overspecification — strong

Both spines are disciplined delta documents: tables where tables work, no source restatement (the source's build/test rules are correctly ignored), no pixel specs where an inherited token covers it, no decorative narrative outside the sanctioned climax beats. DESIGN.md's editorial voice ("enterprise records tool wearing a calm face") earns its place; EXPERIENCE.md prose stays behavioral. The dense picker row (full ARIA wiring + enum dump) is at the edge but every clause is load-bearing against real code. No findings.

## 7. Inheritance discipline — adequate

`sources` resolves: `{planning_artifacts}/../project-context.md` → `/home/administrator/projects/hexalith/parties/_bmad-output/project-context.md` (exists). Domain names are verbatim from source/code: `ProjectionFreshnessMetadata`, `PartyCommandValidationRejected`, `StatusKind`, `PartyPickerSearchState`, `party-selected {partyId, partyType, status}`, routes `/admin/parties*`, roles `Admin`/`TenantOwner`/`Consumer`. Cross-spine deferrals (`DESIGN.md.Brand & Style`, `EXPERIENCE.md.State Patterns`, `DESIGN.md` `body-consumer`) all resolve. Consent-vs-lawful-basis and Object-not-toggle honesty rules mirror the source faithfully. No glossary exists in any of the three files (nothing to diverge). Export-as-async framing vs source's export-as-read: known residual (deliberate regulated-language resolution #8).

### Findings
- **medium** **Source drift:** the frontmatter source was rewritten 2026-06-21 (Epics 4–5 consumer/GDPR rules) — after both spines went `final` on 2026-06-09 — and the spines were never re-validated against it. The concrete casualties are §1's `/no-party-binding` gap and §4's restriction row (EXPERIENCE.md frontmatter `updated: 2026-06-09` vs source `Last Updated: 2026-06-21`). *Fix:* run a spine refresh pass against the current source; bump `updated`.
- **low** The token path `freshness.stale` in the Stale-read state row doesn't match any DESIGN.md path — the real token is `components.freshness-indicator.stale` (EXPERIENCE.md §State Patterns). Human-resolvable, machine-unresolvable. *Fix:* use the full path.
- **low** Admin surfaces carry verbatim routes but Consumer surfaces don't, though the source names the `/me/*` scope (EXPERIENCE.md §Information Architecture; `project-context.md` "ConsumerPortal (self-scoped `/me/*`)"). *Fix:* add `/me/...` routes to the four consumer IA rows.

## 8. Shape fit — strong

DESIGN.md: all 8 canonical sections present, exactly in order (Brand & Style → Colors → Typography → Layout & Spacing → Elevation & Depth → Shapes → Components → Do's and Don'ts); frontmatter has `name`, `description`, `status`, dates, and all five token families. EXPERIENCE.md: all 8 required defaults present and ordered per the reference examples, plus Responsive & Platform (correctly triggered — two form-factor postures) and Inspiration & Anti-patterns (earns its place: the AdminPortal-pattern lift and the consent-dark-pattern rejections are real decisions). No dropped defaults, no invented sections. Frontmatter complete (`name`, `status`, `sources`, `updated`). No findings.

## Mechanical notes

- **Name inconsistencies:** "Data-freshness indicator" (DESIGN §Components) ↔ "Freshness indicator" (EXPERIENCE §Component Patterns); "GDPR destructive button" (DESIGN) ↔ "GDPR action button" (EXPERIENCE). Token keys (`freshness-indicator`, `gdpr-destructive-button`) are consistent.
- **Broken/loose cross-refs:** `freshness.stale` (EXPERIENCE §State Patterns) is not a resolvable DESIGN token path (should be `components.freshness-indicator.stale`). All 26 `{…}` references inside DESIGN.md resolve. All 7 mockup links in EXPERIENCE and 2 in DESIGN point at files that exist.
- **Frontmatter:** complete on both spines; EXPERIENCE `sources` resolves. Spine `updated: 2026-06-09` now trails the source's `Last Updated: 2026-06-21` — the pair's only structural exposure.
- **Component inventory gaps:** `FluentDialog` (used 3× in EXPERIENCE), the toast, the skeleton, and a switch for the consent control are absent from DESIGN's as-is list.
- **Mermaid:** none used in either spine — nothing to validate.
- **Mock parity spot-check:** all 5 mocks define `--brand-fill:#00767f` (decision-log resolution #1 verified on disk).
- **Known residuals honored (not re-flagged):** picker FAST→Fluent 2 re-skin debt; deferred phone-reflow mock; illustrative sub-24px/12px sizing in mocks; export async framing; typed-name PII kept in-memory (implementation note).
