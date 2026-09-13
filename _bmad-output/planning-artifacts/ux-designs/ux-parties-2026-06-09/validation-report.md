# Validation Report — parties

- **DESIGN.md:** `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/DESIGN.md`
- **EXPERIENCE.md:** `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md`
- **Run at:** 2026-09-08T14:48:17+02:00
- **Lenses:** rubric walker · implementation drift · accessibility (WCAG 2.2 AA) · regulated language (GDPR) · Hexalith UX compliance (FrontComposer + Fluent UI Blazor V5)
- **Baseline:** spines `updated: 2026-08-18`; prior validation 2026-08-18 (0 critical / 7 high / 22 medium / 24 low)

## Overall verdict

The 2026-08-18 update pass landed: every one of the thirteen prior rubric findings is resolved or superseded on disk, and the pair remains a disciplined, source-extractable delta contract with canonical shape on both files. What the rubric finds now is **internal contradiction rather than omission** — the three-state freshness contract is muddled at the State Patterns row (the `Degraded` read binds "last known" copy to the *stale/Warning* token while DESIGN says that copy is *degraded/Info*), Flow 3 and the admin mock paint the erase *trigger* danger-filled while both component tables say Outline, and Interaction Primitives still mandates typed confirmation for all destructive actions after the consumer path dropped it. The requirements chain also moved under the spine: the PRD (2026-09-08) and the frozen `epics.md` UX-DRs are undeclared sources with no `FR-*`/`UX-DR*` id cited anywhere, and `UX-DR6` still carries the danger binding the spine deliberately superseded.

The four extra lenses shift the picture from "reconcile the contradictions" to **"two token-layer facts are wrong, and the reciprocal code queue never shipped."** Accessibility computed the contrast of the GDPR destructive pair the August update adopted *from code* — `--colorStatusDangerBackground3` + `--colorStatusDangerForegroundInverted` — at **1.74:1 light / 1.17:1 dark**: the label on the irreversible "Erase party" Confirm is near-invisible in both themes, and spine, code and memlog all agree on the wrong value. This is the only critical this round, and two other lenses recommended *propagating* that pair to the admin panel — that recommendation is withdrawn here. The same lens found that `--colorStatusInfoForeground1`, the token the freshness `degraded` state and "processing/informational" bind to, **does not exist in Fluent 2** (status family is success/warning/danger only; the shell's `--fc-color-info` alias is equally empty) — which is why three lenses tripped on freshness independently. Hexalith UX compliance adds that **per-area density** (Admin comfortable / Consumer roomy via `--fc-spacing-unit`) is not a FrontComposer capability — density is one global, user-owned posture — so the code fix queued in August would build a rule violation; and that the spines transmit the FAST ban too narrowly (`--*-rest` only), omit the `FluentAccordion` page-sections rule, and mandate no conformance guard.

Implementation drift confirms **0 of the 12 code fixes queued on 2026-08-18 landed** (one unqueued a11y item did: product-wide forced-colors/reduced-motion in `MainLayout.razor.css`). Every prior High persists — consumer surfaces unreachable by navigation, both portals on FAST tokens (63 hits across 9 files, none scanned by the style guard), a fake `role=dialog` erasure confirm, a dead Object button — and the August spine additions (consent paused during erasure, verbatim rejection copy, request reference, Restricted banner, Help & contact) have widened the gap. Regulated language finds the consumer register plain, default-Off and dark-pattern-free, but over-promising at the rights' edges: "verified it's gone" is false under Art. 17(3) retention, the Admin *Lift restriction* dialog can breach Art. 18(3) silently, and an erased consumer who re-signs in a month later lands on the bug-shaped `/no-party-binding` page.

## Category verdicts

- Flow coverage — strong
- Token completeness — adequate *(rubric: strong — every `{path}` resolves; downgraded here because two bindings fail at the Fluent 2 layer: the destructive pair is not AA and the Info token does not exist)*
- Component coverage — adequate
- State coverage — adequate
- Visual reference coverage — strong
- Bloat & overspecification — strong
- Inheritance discipline — adequate
- Shape fit — strong

## Cross-lens clusters

Findings that several lenses hit from different angles; resolve as one decision each.

1. **Freshness contract** — rubric high (stale/degraded row collision) + accessibility high (Info token missing) + drift medium (code is two-state, no as-of). Decide the Info colour first; then split the State Patterns row; then the code fix.
2. **GDPR destructive pair** — accessibility critical + drift medium (admin literal hex) + compliance low (hover/pressed tokens). Rebind foreground to `--colorNeutralForegroundOnBrand`, then consume one component everywhere.
3. **Density** — compliance high (not a shell capability) vs drift medium (unwired). Spine/platform decision; pause the queued code fix.
4. **Erased self** — regulated high (`/no-party-binding` copy) + rubric low (Gone row lacks Consumer surface) + rubric medium (verification states). One "Erased / Gone — Consumer" row plus a backend tombstone decision.
5. **Enforcement** — compliance high (no guard mandated) + drift medium (roots/axe scope) + accessibility medium ×2 (ring enforcement, `wcag22aa` tag). One conformance test mirroring FrontComposer's `FluentConformanceTests`.
6. **Sign-in mock** — rubric low + accessibility medium: the mock renders an app-owned credential form the spine says the IdP owns.
7. **Export copy** — rubric medium + regulated low: two wordings of the "Preparing your export" string; PRD `UX-DR15` quotes the Flow 1 variant.

## Findings by severity

### Critical (1)

**[Accessibility · Contrast]** — GDPR destructive Confirm's "matched AA pair" is not a pair (DESIGN frontmatter L55–57, §Components L204–208; shipped in `GdprDestructiveButton.razor.css` L12–14 · SC 1.4.3)
`--colorStatusDangerForegroundInverted` is Fluent 2's danger *text* colour for dark neutral surfaces (`cranberry.tint30` light / `shade10` dark); on `--colorStatusDangerBackground3` (`cranberry.primary`) it computes to **1.74:1 light, 1.17:1 dark**. The label of the single most consequential control in the product is near-invisible in both themes; the admin mock hides it because `.btn.danger` hand-codes `#b10e1c` + white (7.12:1). The 2026-08-18 memlog decision "spine adopts code" locked in the wrong value.
Fix: bind foreground to `--colorNeutralForegroundOnBrand` (white in both themes → 6.07:1 on `Background3`, 7.12:1 on `Background3Hover`); strike "AA-designed together" for this pair; add the rule "`*ForegroundInverted` is text-on-dark-neutral, never text-on-`Background3`"; add the pair to the dark-mode acceptance table. Do **not** propagate the current pair to `PartyGdprOperationsPanel.razor.css` as the drift/compliance lenses suggested.

### High (17)

**[Rubric · State coverage]** — Three-state freshness contract is internally inconsistent (EXPERIENCE §State Patterns L129, §Voice and Tone L99 vs DESIGN L196–202, EXPERIENCE §Component Patterns L114)
"Stale read (`Degraded`)" binds the *last-known* copy to `{components.freshness-indicator.stale}` (Warning) while DESIGN defines that copy as `degraded`/Info and forbids Warning for it; `StatusKind.Degraded` — what code emits on a last-known-cache fallback — has no unambiguous token.
Fix: split L129 into **Stale** (age known → `stale` Warning + "as of HH:MM") and **Degraded** (`StatusKind.Degraded` / last-known fallback → `degraded` + "Showing what we last knew — refreshing"); relabel Voice and Tone L99 "(degraded read)". Depends on the Info-token decision below.

**[Accessibility · Contrast]** — DESIGN binds two states to a token Fluent 2 does not emit (DESIGN L18, L54, L110 · SC 1.4.3, 1.4.11)
`--colorStatusInfoForeground1` has no definition (status family is success/warning/danger); `var()` without fallback is invalid at computed-value time — the silent-disappearance failure the spine itself bans for FAST tokens. The shell alias `--fc-color-info` is equally empty.
Fix: bind Info to an existing pair and say so (e.g. `--colorPaletteBlueForeground2` on `--colorPaletteBlueBackground2`, or neutral `--colorNeutralForeground2` on `--colorNeutralBackground3`), or route Info through components (`FluentMessageBar Intent.Info`, `FluentBadge Color.Informative`) and forbid any `--colorStatusInfo*`; file the shell alias as a FrontComposer defect.

**[Accessibility · Contrast]** — `brand-fill ≈ #00767f` is an estimate no one has read back from the running theme (DESIGN L13, §Colors L93–97 · SC 1.4.3)
V5 derives the brand ramp from `ThemeSettings(AccentColor, …, isExact:false)`; `--colorBrandBackground` = `brand[80]` light / `brand[70]` dark, where the key colour tends to land. If it resolves near `#0097A7`, every filled primary and every link is ~3.5:1, and all five mocks hard-code `#00767f` so nobody would notice.
Fix: record the *computed* `--colorBrandBackground` for light and dark with its ratio as the acceptance value; if it misses 4.5:1, change the accent seed to an AA-safe teal, which also collapses the raw-accent/brand-fill split.

**[Accessibility · Live regions]** — Live regions are never required to exist before their content changes (EXPERIENCE §Component Patterns L115, §Accessibility Floor L177–181; `StatusLiveRegion.razor` L12–15 · SC 4.1.3)
The shipped primitive renders the `role=status` node and its first message together; most AT/browser pairs do not reliably announce polite content that arrives with the node. "Saved — updating…", "Preparing your export…", consent "Saving…" are the messages most likely to go unheard.
Fix: Accessibility Floor rule — "each surface mounts its `role=status` and `role=alert` regions empty at render; only text content changes; regions are never conditionally rendered"; bUnit `StatusLiveRegionTests` asserts the empty mount.

**[Regulated · Erasure]** — "We've deleted your data and verified it's gone" over-promises against Art. 17(3) retention and the product's own residue (EXPERIENCE §State Patterns erasure row (b), Flow 5 step 5; `consumer-privacy.html` State B · Art. 17(3)(b), 30, 12(1))
The codebase has a `LegalObligation` basis and a "Legal records" consent title; `PrivacyProcessingErasedMessage` admits "bounded processing records may remain"; the event store retains ciphertext and rejection events. Telling the subject *all* data is gone is a false statement about a legal right.
Fix: completed copy — "We've deleted your personal data and verified it's gone. We keep only what the law makes us keep — {legal records category} — and a record that this deletion happened."; "What's being deleted" gets a per-datum "Kept — legal requirement" state shown *before* Confirm; backend/PRD: erasure status exposes retained categories and why.

**[Regulated · Rights]** — Admin *Lift restriction* has no inform-the-subject step (EXPERIENCE §State Patterns Restricted row; `AdminPortalLabels.cs:260-263` · Art. 18(3))
Art. 18(3) requires the controller to inform the data subject *before* lifting; nothing in the spine or dialog stops a DPO lifting silently.
Fix: Lift dialog — "Before lifting, the person must be told. Confirm you have informed them." with a required attestation checkbox (reversible action: no typed name); `LiftRestriction` carries `SubjectInformedAt`/channel for audit (route to architecture).

**[Regulated · Erasure]** — The realistic completion-confirmation path routes an erased self to `/no-party-binding` (EXPERIENCE §State Patterns "No party binding", "Erased / Gone"; Flow 5 step 5 · Art. 12(1), 17)
Deletion "usually within 30 days" outlives any session; Marc re-signs in, his `party_id` claim does not resolve, and the spine sends him to "Your account isn't linked to a data record yet — contact us and we'll fix it" — bug-shaped copy that contradicts the deletion and invites re-binding. PRD `FR-Consumer-1` already requires a PII-free tombstone; code has `MyProfileDeletedTitle`.
Fix: extend "Erased / Gone" to the Consumer area — "Your data was deleted on {date}. Reference {ref}. Nothing else is held." — never No-party-binding; backend distinguishes "claim → erased party" from "claim → no party" (tombstone keyed by hashed claim; route to architecture); reword Flow 5 step 5 to the re-sign-in case.

**[Hexalith UX · Inheritance]** — Per-area density is specified as inherited shell behaviour that FrontComposer does not expose (DESIGN §Layout & Spacing + frontmatter `spacing.density-*`; EXPERIENCE §Foundation L27–28)
FrontComposer density is one global posture (`DensityPrecedence.Resolve`: viewport → user preference → `FcShellOptions.DefaultDensity` → factory **Compact**), applied once on `<body data-fc-density>`. The only per-element hook is a locked `data-fc-density` that would silently override the user's Ctrl+, choice — contradicting the spine's own "follows the OS until the user chooses" ethic. The memlog has queued "wire `--fc-spacing-unit` density per area" — the violation is about to be built.
Fix: state that density is user-owned via the shell; set `FcShellOptions.DefaultDensity = Comfortable`; drop "Consumer roomy" or record it as a FrontComposer request (area/route-scoped `DensitySurface`); Don't row: "never set `--fc-spacing-unit` or `data-fc-density` in module CSS/markup"; withdraw the queued code fix.

**[Hexalith UX · Forbidden tokens]** — FAST ban transmitted too narrowly (DESIGN §Do's and Don'ts L235)
The Don't row bans "legacy FAST `--*-rest` tokens"; the rule bans the families `--type-ramp-*`, `--neutral-*`, `--accent-*`, `--neutral-fill-*`, `--palette-*`. Shipped `--type-ramp-*-font-size` and `--neutral-foreground-hint` pass a `--*-rest` reading.
Fix: quote the families verbatim; mandate FrontComposer's `LegacyFluentToken` + `LegacyErrorToken` regexes (`FluentConformanceTests.cs` L41–55) as the guard.

**[Hexalith UX · Enforceability]** — No conformance guard is mandated, and the one that exists scans one of four UI roots (DESIGN §Do's and Don'ts; `AccessibilityStyleGuardTests.cs` L11–14)
Raw-colour + focus-suppression checks over `src/Hexalith.Parties.UI/Components` only; no token regex, no raw-control regex, no `Heading=` (v4) check, no shrinking allowlist. AdminPortal, ConsumerPortal and Picker — where the ≈70 legacy-token hits live — are unscanned.
Fix: DESIGN "Enforcement" paragraph mandating a Fluent-conformance test across all four UI projects mirroring FrontComposer's `FluentConformanceTests` (`RawInteractiveControl`, `LegacyFluentToken`, `LegacyErrorToken`, `V4AccordionHeadingAttribute`, shrinking allowlists), seeded with the eight current offenders; add `wcag22aa` to the axe tags and run axe on real routes.

**[Hexalith UX · Enforceability]** — `FluentAccordion` page-sections rule absent from both spines and from code (DESIGN §Components; EXPERIENCE §IA / §Component Patterns)
Page-like surfaces with ≥2 sibling titled sections must use one `FluentAccordion`, one `FluentAccordionItem` per section, primary item expanded, titles/toolbars/primary region outside. Qualifying: Admin GDPR operations (five sections), Party detail (Details + GDPR), Consumer "My data & privacy". Mocks build `<section><h4>` stacks.
Fix: add the rule verbatim with a per-surface map (in / outside / default-expanded); note V5 uses `Header=` (v4 `Heading=` is silently dropped).

**[Implementation drift · DESIGN]** — Both portals still style state, type and strokes against the legacy FAST family (`PartiesAdminPortal.razor.css:44-46,52,63-64,69-85,103,107,124`; `PartyGdprOperationsPanel.razor.css:18-30`; `CreateEditPartyPage.razor.css`; `MyProfilePage.razor.css`; `MyPrivacyPage.razor.css`; `MyConsentPage.razor.css:36`; `EditMyProfilePage.razor.css`; `ConsumerRouteShell.razor.css` — 63 hits / 9 files)
Where a fallback exists it is a system colour or rem literal (theming silently lost); where none exists the declaration is invalid. Owner: **code**.
Fix: map to `--colorNeutralForeground1..4`, `--colorNeutralStroke1..2`, `--colorNeutralBackground1..2`, `--colorStatus*Foreground1/Background1`, `--fontSizeBase*` + `--lineHeightBase*` per DESIGN L14–19, L25–28.

**[Implementation drift · DESIGN]** — Admin selected-row indicator is `outline: 2px solid var(--accent-fill-rest)` with no fallback (`PartiesAdminPortal.razor.css:50-54`; forced-colors block `:148-152` only sets `border-color`)
No visible selection indicator in either mode nor under forced-colors, contrary to DESIGN L236 and EXPERIENCE L197–199. Owner: **code**.
Fix: `outline: var(--strokeWidthThick) solid var(--colorStrokeFocus2)` (or `--colorBrandStroke1`); `outline-color: Highlight` under forced-colors.

**[Implementation drift · EXPERIENCE]** — IA nav-reachability rule unmet (`PartiesUiFrontComposerRegistration.cs:42-53`; `MyProfilePage.razor`)
Only "Parties" and "My space" registered; no inbound link anywhere to `/me/edit` or `/me/privacy`; no Edit action on My profile. Marc cannot reach Flow 1/2/5 surfaces without typing URLs. Owner: **code**.
Fix: `FrontComposerNavEntry`s "My consent" → `/me/consent`, "My data & privacy" → `/me/privacy` under `ConsumerPolicy`; Edit link on My profile → `/me/edit`; align "My space" with the IA's "My profile".

**[Implementation drift · EXPERIENCE]** — Consent is not paused during erasure (`MyConsentPage.razor:52,223,436`)
No erasure-state input; toggles stay live during `ErasurePending`/`KeyDestroyed`, backend rejects, row shows generic `ConsentSaveFailure` — the exact "'We couldn't change this' on a rejection that can never succeed" the spine bans (EXPERIENCE L96, L112, L131; Flow 5 step 3). No "paused" string exists. Owner: **code**.
Fix: inject `IConsumerPrivacyErasureClient`; disable rows + "Consent changes are paused while your data is being deleted." when state ∉ {Active}.

**[Implementation drift · EXPERIENCE]** — Object (Art. 21) rendered as a permanently disabled button (`MyConsentPage.razor:105-110`; `ConsumerPortalResources.resx:372-373`)
Banned three times in the spine (EXPERIENCE L94, L112, L243–247). Owner: **code**.
Fix: replace with "You can object to this use — contact us and we'll review it." linked to Help & contact.

**[Implementation drift · EXPERIENCE]** — Consumer erasure confirm is a fake dialog (`MyPrivacyPage.razor:119-135`)
Inline `<div role="dialog" aria-modal="true">` with no trap, no `Esc`, no restore; Confirm is Outline + colour class rather than the danger-filled in-dialog Confirm (DESIGN L209–210). Owner: **code**.
Fix: `FluentDialog Modal="true"` (pattern at `PartyGdprOperationsPanel.razor:30-73`) with a danger Confirm (after the critical above is fixed) and "Keep my data" secondary.

### Medium (54)

**[Rubric]** — Export "preparing" string in two wordings (Voice L97 vs Flow 1 L263–264; PRD `UX-DR15` quotes Flow 1). Fix: one canonical string. · Flow 3 step 4 "Erase party (danger fill)" contradicts Outline-trigger rule in both tables; `admin-parties.html` L169 also fills it. Fix: "(outline trigger; danger fill on dialog Confirm)". · Interaction Primitives L150–152 + Inspiration L241–242 mandate typed confirm for *all* destructive actions vs consumer no-transcription. Fix: scope to Admin irreversible. · Person/Organization chooser has no Component Patterns row; `FluentRadioGroup` absent from inventory (PRD `UX-DR9` names it). Fix: row + inventory; create-only, no default, read-only on edit. · Tablet detail sheet has a focus contract but no rendering component. Fix: name `FluentDrawer` (or equivalent) in DESIGN. · `FR-Shell` no-area and dual-role states absent (Foundation L21–22/L38–39). Fix: "No area" state row + dual-role precedence sentence. · Erasure verification states (pending / verified / failed → Retry) absent though Flow 3's climax depends on them. Fix: add row. · `sources` omits `parties-ui-prd.md` and `epics.md`; no `FR-*`/`UX-DR*` id cited. Fix: declare both; tag flows/rows with ids. · Spine silently supersedes frozen `UX-DR6` (danger binding) and `UX-DR5` (name). Fix: state the supersession in DESIGN Components.

**[Implementation drift]** — Admin danger fill is literal hex (`PartyGdprOperationsPanel.razor.css:18-30`). Fix: one shared component, *after* the critical rebind. · Freshness two-state Warning everywhere; `AsOf` unconsumed (`DataFreshnessIndicator.razor.css:19-21`, `FreshnessStatus.razor:12-15`, `PartiesAdminPortal.razor:1618-1625`). Fix: after the Info decision, add `stale` + as-of and remap `degraded`. · Link text on `--accent-foreground-rest` (`MyPrivacyPage.razor.css:131-135`). Fix: `FluentLink` / `--colorBrandForegroundLink`. · Bespoke admin badge; consumer plain text; shared `PartyStateBadge` used only by the specimen and `Rounded` not `Circular`. Fix: share via RCL; `BadgeShape.Circular`. · Per-area density unwired — **superseded by the compliance high; do not wire**. · Cancel-too-late rejection hedged ("…or cannot be changed right now") and not reason-specific; `ConsumerPrivacyErasureResult` has no reason field. Fix: thread the backend reason. · Verified-deletion wording absent for terminal `Erased` (renders the "permanent" warning). Fix: new resx string (see regulated high for wording). · PII-free request reference not shown; result has no reference field. Fix: code, or mark row blocked-on-backend. · Restricted consumer treatment absent (banner, consent-still-editable copy, request path). · Help & contact persistent affordance does not exist; every "support path" dangles. · TenantUnavailable copy still "Tenant context is unavailable" (`AdminPortalLabels.cs:153,155`). · Style-guard roots + axe scope narrower than the Floor (`AccessibilityStyleGuardTests.cs:11-14`; `parties-accessibility.spec.ts:5`). · Stale auto-refresh / "announces when fresh" unconsumed (SignalR stack services-only). · Reverse drift: product-wide forced-colors/reduced-motion now exists (`MainLayout.razor.css:8-32`); spine still says "today only the picker honors them" (EXPERIENCE L195–197). Fix: **spine** — update the parenthetical.

**[Accessibility]** — Dark-mode gate names no method, tool or owner; computed dark Danger `Fg1/Bg1` = 4.65:1 (thin). Fix: "computed pairs" table + axe `colorScheme:'dark'` run. · Dialog initial focus unspecified; must never be the destructive Confirm (SC 2.4.3, 3.3.4). · "Ring never suppressed" enforced on one of four roots; admin outline is on a non-resolving token. · Three polite regions describe one transition (status region + freshness + banner). Fix: one-voice rule per view. · Rejection reasons not mandated human/localized/field-scoped; consumer gets no reason (SC 3.3.1, 3.3.3). · Typed-name confirm has no matching rules (`StringComparison.Ordinal`); a trailing space blocks a DPO. Fix: trim + case-insensitive + NFKC; show expected string; paste allowed. · 3.3.8 has no verification artifact. Fix: deployment-checklist line item. · Consent switch target under-specified; mock label not associated (22px target). Fix: label is the switch's `<label>`; row ≥44px touch. · 2.5.8 not gated: axe tags lack `wcag22aa`. · Selection indicators die in forced-colors in mocks + picker (`PartyPicker.razor.css:102-105,147-154`). · Document titles unspecified and unimplemented (no `PageTitle`) (SC 2.4.2). · Language of page unspecified; `App.razor` hard-codes `lang="en"` while the product is localized (SC 3.1.1). · `signin.html` renders an app-owned email/password card the spine says the IdP owns. · Form labels unassociated in `create-edit-party.html` and `consumer-profile.html`.

**[Regulated]** — No Art. 12(3) response deadline on any "contact us" rights path. Fix: "We'll reply within a month…" · Retention period/criteria absent from "Things we keep" (Art. 13(2)(a), 15(1)(d)). · No surface names controller, DPO or links the privacy notice (Art. 13(1)). · Per-row purpose description not mandated on consent rows (code has `ConsentMarketingEmailsDescription`; spine and mock are behind). · "Cancel until deletion begins" never says when that is. Fix: decide the grace period; state it. · Consumer request reference and Admin `#SR-4471` not tied to one format. · Stale/degraded reads not framed for an Art. 15/20 response; export from a degraded projection. Fix: export carries freshness; disable Export when `Degraded`. · Export scope, download validity, file retention unstated (Art. 20(1)). · Restriction reason is free text — special-category leak into immutable events (Art. 18(1), 9). Fix: bounded picklist of the four grounds. · Admin-recorded consent has no evidence-of-consent capture (Art. 7(1)).

**[Hexalith UX]** — `typography.body-consumer` declares a literal 16px / 1.5 ramp with no allowed mechanism (1.5 ≠ `--lineHeightBase400`). Fix: `FluentText Size="TextSize.Size400"`. · Freshness indicator specified as a hand-rolled "dot + label" with token colours. Fix: `FluentBadge Tint/Circular` + `BadgeColor`, or `FluentIcon` + `FluentText`. · `<hexalith-party-picker>` blessed as-is with no written carve-out vs `FluentAutocomplete`/`FluentCombobox`. · "Bind link text to brand-fill" reads as CSS `color:` on `<a>`. Fix: `FluentLink`. · Party-state badge bound to `--colorStatus*Foreground1` tokens instead of `BadgeColor` parameters (V5 enum is `BadgeAppearance.Tint`). · State Patterns re-specifies routing the shell already implements (`FcAggregateDetailPage`, `FcProjectionLoadingSkeleton`, `FcProjectionEmptyPlaceholder`, `FcPageLayout Mode=Constrained`). Fix: map each row to the shell primitive. · Neither spine states the **no-port** rule for mocks; mock headers invite porting (`.b-*` tints, `.dot`, `.sw`, `.dialog`, `.toast`, `220px` nav). Fix: caption every mock; one line in DESIGN Components.

### Low (52)

**[Rubric]** — Dark brand fill uncommitted pending acceptance check. · Banner / Help & contact have no named component (`FluentMessageBar` / footer `FluentAnchor`). · Gone row lacks Consumer profile surface (see regulated high). · Edit forms over a stale/degraded read untreated. · `signin.html` shows an app-owned form (see a11y medium). · `admin-parties.html` L169 fills the erase trigger with hard-coded `#b10e1c`. · IdP/3.3.8 prose duplicated (Foundation + Floor); KMS launch gate restated. · Create/Edit IA row lacks `/admin/parties/new`, `/admin/parties/{id}/edit`.

**[Implementation drift]** — `FluentSkeleton`/`FluentMessageBar` inventoried, zero uses (hand-rolled loading lines, `<p role="status">`). · Picker focus ring on `--hx-picker-accent` (`--colorBrandStroke1` + foreign `#0067b8` fallback) not `--colorStrokeFocus2`. · No `scroll-margin-top` anywhere (2.4.11). · Consent Empty copy unreachable (purposes hard-coded). · Skip links "first tab stops" over-promises after route focus (deferred to FrontComposer). · Picker Component Pattern otherwise met. · Spine records no shell version the Floor is verified against (code pins 4.4.0). · Still unspecified: admin pagination/search-mode buttons, EventStore-admin links, DPO summary panel, `/admin` redirect, "No area assigned" landing, specimen routes.

**[Accessibility]** — Mock input borders mislead on 1.4.11. · Focus destination when the focused row disappears. · Shell keyboard claims (two skip links, palette semantics) asserted not contracted. · Grid type-ahead 2.1.4 scope. · Picker announcements: debounce + noun phrasing. · Auto-refresh announces on change only. · Validation row lacks `aria-invalid` + alert-links-to-fields. · Picker clear "✕" in mock under 24px. · Reduced-motion inventory empty (shimmer, switch, drawer, scroll). · Heading hierarchy unstated; mocks skip levels. · "as of HH:MM" unqualified by zone/locale. · Decorative glyphs unhidden in mocks. · Help & contact in no mock. · `admin-parties.html` interactive `<div>`s, Confirm enabled before typing, two `role=status` for one read. · `consumer-privacy.html` State B lacks request reference; `consumer-profile.html` nav `<button>`.

**[Regulated]** — Art. 19 recipient notification unaddressed (PRD). · Object sentence as 13px sub-line vs Art. 21(4) "clearly and separately". · Art. 22 neither claimed nor excluded. · Consent rejection copy drifts spine/code. · Consumer stale mock lacks "as of". · Export copy two variants (see rubric). · Admin erase body "across all records" over-promises. · TenantUnavailable mock body "your access is fine" can be false. · `#SR-4471` must never encode the ground. · "Your data is safe" is an implied security guarantee. Fix: "Nothing about your data has changed — try again." · DESIGN lacks a symmetry rule for consent/erasure control pairs. · Verbatim-parity drift inventory recorded for `bmad-build` (two items not yet in the memlog queue: hedged `PrivacyErasureRejectedMessage`; `ConsentObjectAction`).

**[Hexalith UX]** — Nav width "220px / 48px" restated; shell rail is 72px / 48px. · `rounded.*` px literals; `full` 9999 vs 10000. · V4 theme vocabulary ("design-token API", `baseLayerLuminance`, `IThemeService`); the shell owns the theme call. · Focus ring re-drawn for Fluent components too; scope to non-Fluent focusables. · Danger fill not labelled "allowed fallback"; hover/pressed tokens omitted (pair itself is the critical above). · Behavioural rows lack a "V5 component" column (`FluentSwitch CheckedMessage`, `FluentField Message`, `FluentRadioGroup`, `FluentTextInput`). · Status region: say `FluentMessageBar Intent`/`AriaLive` mapping. · `PreventDismissOnEscape` must stay false for the erase dialog. · Existing guard asserts the ring recreation; narrow it with the spine.

## Queued code fixes (2026-08-18) — status

| Fix | Status |
|---|---|
| Consumer nav reachability (`/me/consent`, `/me/privacy`, Edit on My profile) | not landed |
| Portals off FAST `--*-rest` tokens onto Fluent 2 | not landed |
| Consumer erasure confirm as real `FluentDialog` | not landed |
| Admin danger-fill hex → token pair | not landed — **and the target pair is wrong (critical)** |
| Freshness 3-state + "as of HH:MM" on real surfaces | not landed — **blocked on the Info-token decision** |
| `--fc-spacing-unit` density per area | not landed — **withdraw (compliance high)** |
| Shared `PartyStateBadge` (FluentBadge, Circular) in both portals | not landed |
| TenantUnavailable warming copy | not landed |
| Remove dead Object button, add contact path | not landed |
| a11y style-guard roots → all four UI projects + axe on real routes | not landed (file touched 3×, roots unchanged) |
| Stale auto-refresh via SignalR | not landed |
| *(unqueued)* product-wide forced-colors + reduced-motion | landed (`MainLayout.razor.css`) — spine needs the annotation |

## Prior findings (2026-08-18) — disposition

- Rubric: 13 → 12 resolved, 1 superseded (source drift now points at the undeclared PRD/epics chain).
- Implementation drift: all 3 Highs persist verbatim; 7 mediums persist; 5 lows superseded by spine adoption.
- Accessibility: 16 → 14 resolved, 2 resolved-in-spine but persisting in mocks/picker code or untestable as written.
- Regulated language: 8 → 7 resolved, 1 superseded (Art. 18(3) inform-before-lift re-raised as high).

## Reviewer files

- `review-rubric.md`
- `review-implementation-drift.md`
- `review-accessibility.md`
- `review-regulated-language.md`
- `review-hexalith-ux-compliance.md`
