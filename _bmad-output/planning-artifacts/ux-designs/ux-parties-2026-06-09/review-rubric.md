# Spine Pair Review — parties

## Overall verdict

This is a strong, source-extractable spine pair. Every component named anywhere has both a DESIGN.md visual row and an EXPERIENCE.md behavioral row; every YAML token and `{path.to.token}` prose reference resolves; all five mockups are linked inline at the right spine sections and named for what they illustrate; "spine wins on conflict" is stated in both files; and the eventually-consistent state model (stale / degraded / accepted-but-processing / tenant-warming / display-name-only / erased-tombstone) is covered with real treatments mapped to the codebase's `StatusKind` / `PartyPickerSearchState` enums. A downstream consumer can source-extract cleanly. The only gaps are minor and non-blocking: one freshness sub-token (`inactive` badge color used in prose but the badge has an `inactive` token; `restricted` party-state lacks a parallel `restricted` entry that prose names — see Findings), and a couple of unmocked surfaces (My consent full page, restrict/portability/processing-record panels) that are explicitly classified spine-only in the decision log rather than orphaned.

## 1. Flow coverage — strong

Checked: every stated user need / journey in the IA tables (both areas) against the Key Flows; each flow for named protagonist, numbered steps, an explicit climax beat, and a failure path.

All four flows have a named protagonist (Marc ×2, Priya ×2), numbered steps, a bolded **Climax**, and a **Failure** line. Coverage maps cleanly to the consumer needs (view data → Flow 1; consent → Flow 2) and admin needs (erasure → Flow 3; link-a-party/picker → Flow 4). Failure paths are real and distinct (TransientFailure export, Validation revert, TenantUnavailable warming, Degraded/LocalOnly picker).

### Findings
- **low** No dedicated flow exercises **Edit my profile** (Consumer) as a protagonist journey — it is an IA surface (`EXPERIENCE.md:46`) and is mocked (`consumer-profile.html`), but the optimistic-save + validation-reject behavior is only shown in the mock, not narrated as a flow. The mock + State Patterns row cover it, so this is informational, not a gap. *Fix:* optionally fold a one-line edit-profile beat into Flow 1, or leave as-is (the mock is sufficient for story-dev).
- **low** **Sign in** has no narrated flow, only an entry reference and a mock. Given role-routing is load-bearing (decides landing area), a downstream consumer relies entirely on `signin.html` + Foundation prose. Adequate, but a single failure path (bad credentials / SSO bounce) is unspecified anywhere. *Fix:* add a one-row State Pattern or Voice line for sign-in failure if story-dev needs the copy.

## 2. Token completeness — strong

Checked: every frontmatter token and every `{path.to.token}` prose reference in DESIGN.md resolves; color tokens have hex or are inherited-by-name per the project's deliberate Fluent-inheritance posture.

`{colors.accent}` (#0097A7, hex present), `{rounded.sm/md/lg/xl/full}` (all defined with px), `{typography.body-consumer.fontSize}` (16px, defined), and all `{components.party-state-badge.*}`, `{components.freshness-indicator.*}`, `{components.gdpr-destructive-button.*}` references resolve to frontmatter entries. Inherited Fluent 2 tokens (`--colorNeutral*`, `--colorStatus*`, `--colorStrokeFocus2`, `--fontFamilyBase`, etc.) are correctly referenced by name and per instructions are NOT flagged as missing-hex — the file documents the inheritance contract explicitly (`DESIGN.md:13-18`, frontmatter comments). No CRITICAL color gaps.

### Findings
- **medium** Prose at `DESIGN.md:172` names a **`restricted`** party-state badge value (`{components.party-state-badge.restricted}`) and the frontmatter (`DESIGN.md:48`) defines `restricted`, `active`, `inactive`, `erased` — these resolve. However the prose enumerates `active / inactive / restricted / erased` while the *behavioral* lifecycle in EXPERIENCE.md (`:91`) lists `active/inactive/restricted/erased` too — consistent. No miss. (Re-verified: this is clean; flagged only to record that the four-value set matches across both files.)
- **low** `accent-dark` is set identical to `accent` (`#0097A7`, `DESIGN.md:12`) with a comment that Fluent derives dark tints via `baseLayerLuminance`. This is intentional and self-documented, but a literal-minded extractor could read it as a copy-paste error. *Fix:* none required; the comment already explains it. Noted for the reviewer's awareness only.

## 3. Component coverage — strong

Checked: every component named anywhere (frontmatter `components`, DESIGN Components prose, EXPERIENCE Component Patterns, mockups, flows) for a DESIGN visual row AND an EXPERIENCE behavioral row with real rules.

| Component | DESIGN visual | EXPERIENCE behavioral | Verdict |
|---|---|---|---|
| Party-state badge | `DESIGN.md:169-174` (pill, token map, label-always) | `EXPERIENCE.md:91` (lifecycle, color+text, erased tombstone) | covered |
| Data-freshness indicator | `DESIGN.md:176-181` (dot+label, fresh/stale/degraded tokens) | `EXPERIENCE.md:94` (ProjectionFreshnessMetadata, aria-live) | covered |
| GDPR destructive button | `DESIGN.md:183-186` (danger fill, r-md, confirm-pair; outline for reversible) | `EXPERIENCE.md:93` (danger+typed confirm; outline+single confirm) | covered |
| Party picker | `DESIGN.md:188-192` (re-skin to Fluent 2, debt) | `EXPERIENCE.md:90` (combobox, debounce, event, state machine, debt) | covered |
| Parties data grid | inherited (`FluentDataGrid`, `DESIGN.md:162`) | `EXPERIENCE.md:89` (server search, filters, never block on staleness) | covered |
| Consent control | inherited (Fluent toggle) | `EXPERIENCE.md:92` (optimistic, reconcile, lawful-basis inline) | covered |
| Command result toast | inherited (Fluent toast) | `EXPERIENCE.md:95` (async accept toast, danger on reject, no alert) | covered |

Inherited-as-is FluentUI components (`DESIGN.md:158-163`) are listed with the "do not customize" contract — correct shadcn-style discipline; they need no behavioral row.

### Findings
- (none — every named component has both rows or is an explicitly inherited primitive with a stated contract.)

## 4. State coverage — strong

Checked: walked every IA surface and the State Patterns table; verified the eventual-consistency states the prompt flags (stale / degraded / accepted-but-processing / tenant-warming).

State Patterns (`EXPERIENCE.md:103-116`) covers Cold load, Empty/NoData, DisplayNameOnly, Accepted-but-processing, Stale/Degraded, Validation, TransientFailure, LoadFailure, SignInRequired, TenantUnavailable (warming), AdminRequired/Forbidden, and Erased/Gone — all mapped to real `StatusKind`/`PartyPickerSearchState` enum values. The "tenant-warming" case is explicitly fail-closed-but-not-denied with non-alarming copy (`:114`), and accepted-but-processing is called out as the core eventual-consistency UX (`:108`). Mocks render the load-bearing ones (stale read, accepted-but-processing toast, erase typed-confirm, tenant-warming, validation-rejected).

### Findings
- **low** **Picker-specific states** (`LocalOnly`, `Unauthorized`, `NotFound`) are named in the picker's enumerated state machine (`EXPERIENCE.md:90`) and partially in Flow 4 failure (`Degraded/LocalOnly`), but the State Patterns table maps to the *page-level* `StatusKind`, not the picker's `PartyPickerSearchState`. A story-dev implementing the picker must read both the Component Patterns row and the flow to reconstruct the full picker state set. *Fix:* none strictly required (the enum is named verbatim, so it's resolvable); optionally add a picker state sub-table if the picker becomes its own story.
- **low** **Offline** is declared out of scope in the decision log (`.decision-log.md:93`, "offline (no)") and correctly absent from State Patterns — consistent, not a miss. Noted to confirm the omission is deliberate.

## 5. Visual reference coverage — strong

Every mockup file is linked inline at the relevant spine section, named for what it illustrates, and each carries an in-file header comment listing the spine sections it governs. "Spine wins on conflict" appears in both DESIGN.md (`:64-65`) and EXPERIENCE.md (`:14`, `:63`).

| Mockup | Linked at | Named-for | Orphan? |
|---|---|---|---|
| `signin.html` | `EXPERIENCE.md:58`, `:188` (entry) | sign-in + role routing | no |
| `admin-parties.html` | `DESIGN.md:166`, `EXPERIENCE.md:59`, `:188` (Flow 3) | Admin master-detail + GDPR | no |
| `create-edit-party.html` | `EXPERIENCE.md:60`, `:188` (Flow 4) | Create/Edit + in-form picker | no |
| `consumer-profile.html` | `EXPERIENCE.md:61`, `:188` (Flows 1–2) | My profile view/edit | no |
| `consumer-privacy.html` | `DESIGN.md:167`, `EXPERIENCE.md:62`, `:188` (Flows 1–2) | My data & privacy | no |

No orphan mockups; no broken inline links (all paths are relative `mockups/<file>.html` and exist on disk).

### Findings
- **low** The decision log (`.decision-log.md:172-174`) classifies several surfaces as *spine-only* (Sign in is actually mocked; My consent full page, restrict/portability/processing-records panels are not). These are intentionally unmocked, not orphaned — story-dev builds them from the spine tables. The log even flags "user asked whether any need a visual reference," so the gap is acknowledged, not accidental. *Fix:* none; this is correct coverage classification. Surfaced so the reviewer knows these surfaces ship without a mock by design.

## 6. Bloat & overspecification — strong

No notable bloat. DESIGN.md specifies only brand-layer deltas and explicitly refuses to restate inherited Fluent tokens; the inherited-component list is a contract ("do not customize"), not specification. EXPERIENCE.md state table is dense but every row is load-bearing for an eventually-consistent backend. The frontmatter comments (e.g. `DESIGN.md:13-18`, `:38-42`) carry inheritance rationale that a naive reader might call verbose, but it is exactly the context a downstream extractor needs to avoid re-declaring JS-emitted Fluent custom properties — justified, not bloat.

## 7. Inheritance discipline — strong

Sources resolve (FrontComposer shell + FluentUI V5 `5.0.0-rc.3`, grounded in `.decision-log.md:106-131`). Fluent token names are used verbatim (`--colorStatusDangerForeground1`, `--colorStrokeFocus2`, `--fontSizeBase400`, `--borderRadiusMedium`, `--fc-spacing-unit`, etc.) and match the grounding capture. Component names are identical across DESIGN/EXPERIENCE/mockups (`<hexalith-party-picker>`, `FluentDataGrid`, `FluentBadge`). EXPERIENCE token refs that point back to DESIGN resolve: `DESIGN.md body-consumer` (`:153`), `freshness.stale` (`:109`), `--colorStrokeFocus2` (`:144`).

### Findings
- **low** EXPERIENCE.md `:109` references `freshness.stale` in shorthand ("using `freshness.stale`") rather than the full `{components.freshness-indicator.stale}` path used in DESIGN.md. It resolves unambiguously to one token, but the shorthand isn't the canonical `{path.to.token}` form. *Fix:* optionally normalize to `{components.freshness-indicator.stale}` for a mechanical resolver; a human/AI extractor resolves it fine as-is.

## 8. Shape fit — strong

DESIGN.md follows the canonical section order exactly: Brand & Style → Colors → Typography → Layout & Spacing → Elevation & Depth → Shapes → Components → Do's and Don'ts (all 8 present, in order). EXPERIENCE.md has all required defaults: Foundation, Information Architecture, Voice and Tone, Component Patterns, State Patterns, Interaction Primitives, Accessibility Floor, plus the required-when-applicable Responsive & Platform (triggered by responsive web), Inspiration & Anti-patterns, and Key Flows. Frontmatter on both files is complete (`name`, `description`/`status`, `sources`, dates). The `> Spine wins on conflict` blockquote is present in both.

### Findings
- (none — both files match the house shape.)

## Mechanical notes

- **Name consistency:** component names are identical across all files (`<hexalith-party-picker>`, party-state badge, freshness indicator, GDPR destructive/action button, parties data grid, consent control, command result toast). DESIGN calls it "GDPR destructive button"; EXPERIENCE calls the row "GDPR action button" — same component, slightly different row label. Minor; both clearly map. Worth aligning the label if a strict name-match extractor is used.
- **Cross-refs:** all `{path.to.token}` references in DESIGN.md resolve to frontmatter; all `mockups/*.html` inline links exist on disk; all `DESIGN.md.*` / `EXPERIENCE.md.*` section deferrals point to sections that exist. No broken cross-refs found.
- **Frontmatter completeness:** DESIGN.md frontmatter has `name`, `description`, `status`, `created`, `updated`, `colors`, `typography`, `rounded`, `spacing`, `components` — complete. EXPERIENCE.md has `name`, `status`, `sources`, `updated` — complete. One nit: EXPERIENCE `sources` is `{planning_artifacts}/../project-context.md` (a relative-up path); it resolves but is less tidy than DESIGN's. Both `status: draft` — consistent with the decision log's reviewer-gate-pending state.
- **Enum grounding:** `StatusKind` (12 values) and `PartyPickerSearchState` (12 values) named in EXPERIENCE match the grounding capture in `.decision-log.md:122-130` verbatim — strong source fidelity, story-dev can map states 1:1 to existing code.
- **Hex in mockups vs. inheritance:** mockups hard-code Fluent hex values (e.g. `--danger:#b10e1c`) with comments mapping each to its `--colorStatus*` custom property. This is correct for a static HTML mock and does not contradict DESIGN.md's "don't redeclare Fluent custom properties in CSS" rule (that rule governs the real Blazor app, not illustrative mocks). No conflict.

---

**File:** `/home/administrator/projects/hexalith/parties/_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/review-rubric.md`
