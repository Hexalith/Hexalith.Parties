# Validation Report — parties

- **DESIGN.md:** `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/DESIGN.md`
- **EXPERIENCE.md:** `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md`
- **Run at:** 2026-08-18T20:26:57+02:00
- **Lenses:** rubric walker · implementation drift · accessibility (WCAG 2.2 AA) · regulated language (GDPR)

## Overall verdict

A clean, genuinely source-extractable delta-spine pair: canonical shape on both files, committed tokens with load-bearing contrast caveats spelled out, a State Patterns section that carries the product's hardest problem (eventual consistency), and all five mocks linked with spine-wins stated. The one structural risk is **source drift**: `project-context.md` was revised 2026-06-21 — twelve days *after* finalize — and now carries consumer-binding (`/no-party-binding`) and Art. 18 restriction rules the spine pair does not reflect.

The three extra lenses shift the picture from "polish" to "reconcile." The **behavioral spine is implemented with high fidelity** (status-kind vocabulary, live-region politeness split, consent semantics, and regulated microcopy in code near-verbatim and test-pinned; the picker re-skin debt is resolved in code and its spine annotations can be retired). But **DESIGN.md's token layer drifts materially in the build**: both portals style domain states against banned legacy FAST `--*-rest` tokens (one selected-row outline silently vanishes), the density contract was never wired, `/me/privacy` and `/me/edit` are unreachable by navigation, and the consumer erasure confirm is a fake modal.

All 18 June finalize resolutions (6 accessibility + 12 regulated-language) verifiably landed in the spine text — **no criticals anywhere this round**. The remaining exposure is **under-specified rights**: the Object (Art. 21) button promises a right the backend has no command for (flagged independently by two lenses; it ships as a permanently disabled control), restriction (Art. 18) is invisible to the data subject, sign-in lacks a WCAG 2.2 §3.3.8 mandate, and the sign-in mock still paints link text in the raw 3.51:1 accent.

## Category verdicts

- Flow coverage — adequate
- Token completeness — strong
- Component coverage — adequate
- State coverage — strong
- Visual reference coverage — strong
- Bloat & overspecification — strong
- Inheritance discipline — adequate
- Shape fit — strong

## Findings by severity

### Critical (0)

None.

### High (7)

**[Rubric · Flow coverage]** — `/no-party-binding` unbound-consumer path absent everywhere (EXPERIENCE §IA, §State Patterns)
The source mandates zero/multiple `party_id` claims → redirect `/no-party-binding`; the surface/state appears in no IA row, state row, or flow. The drift review found the code already built it (`NoPartyBinding.razor`).
Fix: back-port the shipped surface — a `No party binding` state row (fail-closed, reassuring copy, support path) + a `/no-party-binding` IA entry.

**[Implementation drift]** — Consumer surfaces `/me/privacy` and `/me/edit` are unreachable (`PartiesUiFrontComposerRegistration.cs:42-53`)
Only two nav entries registered; no Edit link on My profile; no inbound link anywhere to `/me/privacy` or `/me/edit`. The pages exist and work, but Flow 1/2 surfaces need a typed URL.
Fix: fix code — nav entries for Consent/Privacy under the Consumer policy + an Edit action on My profile.

**[Implementation drift]** — Both portals style domain states against banned FAST V4 tokens (`PartiesAdminPortal.razor.css:52,68-86,102-108` + consumer CSS)
Badges, freshness, danger fills, and type sit on non-resolving `--*-rest` / `--type-ramp-*` vars; the selected-row outline has no fallback and silently disappears. The decision log confined this debt to the picker; the portals re-introduced it at scale.
Fix: fix code — map to `--colorNeutral*`, `--colorStatus*Foreground1/Background1` pairs, `--fontSizeBase*`, per DESIGN.

**[Implementation drift]** — Consumer erasure confirm is a fake modal (`MyPrivacyPage.razor:119-136`)
Inline `<div role="dialog" aria-modal="true">` with no focus trap/restore — semantics claimed to AT that the page doesn't honor. The Admin side does it correctly with `FluentDialog Modal="true"`.
Fix: fix code — FluentDialog, or drop the false dialog semantics.

**[Accessibility]** — 1.4.3: sign-in mock still paints link text in the raw accent (`mockups/signin.html .alt a`)
`#0097A7` on white = 3.51:1 on the surface every user must pass — the June critical's exact failure mode, surviving in the mock the fix claimed to have repointed.
Fix: repoint `.alt a` to `--brand-fill` (5.39:1); extend the DESIGN Don't column to cover links.

**[Accessibility]** — 3.3.8 Accessible Authentication unaddressed on a claimed WCAG 2.2 AA floor (EXPERIENCE §IA Sign in, §Accessibility Floor)
No paste/autofill guarantee, no `autocomplete` tokens, no ban on cognitive-function tests. A consumer who cannot clear sign-in can exercise *no* GDPR right.
Fix: add the 3.3.8 floor; if sign-in is delegated to the IdP (it is — `Routes.razor:15-18`), say so and make 3.3.8 a requirement on its configuration.

**[Regulated language]** — "Object to this use" (Art. 21) promises a right the backend does not deliver (EXPERIENCE §Voice and Tone, §Component Patterns, Flow 1)
No Object command/event/workflow in `Contracts/Commands/` (24 commands verified); ships today as a permanently disabled button (`MyConsentPage.razor:107-110`). Copy also implies objection stops the use; Art. 21(1) outcome is an assessment, not a toggle.
Fix: spec Object as a request-raising flow with honest review copy + backend command/DPO queue, or replace the button with a contact path and mark the spine row blocked-on-backend.

### Medium (22)

**[Rubric · Flow]** — No consumer self-erasure Key Flow (EXPERIENCE §Key Flows). Fix: fifth flow (request → cancellable state → optional cancel) or extend Flow 1.
**[Rubric · Token]** — `gdpr-destructive-button` binds a Foreground token as a fill; dark-mode AA risk, text color unstated (DESIGN frontmatter + §Components). Fix: adopt the `DangerBackground3`/`ForegroundInverted` pair the shipped host component already uses.
**[Rubric · Component]** — Consent control has no visual home; no `FluentSwitch` in the as-is inventory (DESIGN §Components). Fix: add it.
**[Rubric · State]** — No `Restricted` state row: what restriction does to each surface, incl. the Art. 18(3) consent-still-editable guard (EXPERIENCE §State Patterns). Fix: add the row.
**[Rubric · Inheritance]** — Source drift: source rewritten 2026-06-21, after finalize; spines never re-validated. Fix: spine refresh pass; bump `updated`.
**[Drift]** — Admin danger fill is literal hex (`PartyGdprOperationsPanel.razor.css:18-30`); host `GdprDestructiveButton` ships a better matched pair than the spine declares. Fix: fix admin hex; update spine token.
**[Drift]** — Freshness collapsed to 2 states (degraded→Warning, not Info); "as of HH:MM" rendered on no real surface. Fix: decide — 3-state + as-of in code, or simplify the spine.
**[Drift]** — Per-area density (`--fc-spacing-unit` 4/6px) never wired; zero references in `src/`. Fix: decide — wire it or rewrite DESIGN §Layout & Spacing.
**[Drift]** — Bespoke admin badge vs the FluentBadge mandate; compliant shared component used only by the specimen; consumer shows plain text, no badge. Fix: share the component via an RCL or annotate the spine.
**[Drift]** — `TenantUnavailable` copy is operator jargon ("Tenant context is unavailable") vs the mandated warming-up copy (`AdminPortalLabels.cs:153`). Fix: fix copy or bless the terse register.
**[Drift]** — Object button rendered permanently `Disabled="true"` with no handler — a dead control against the spine's honesty stance (`MyConsentPage.razor:107-110`). Fix: see the Art. 21 high.
**[Drift]** — A11y gates pin a narrower floor than the spine: style guard scans only the UI host (never portal CSS, where the hex lives); axe runs only on the synthetic specimen route (`AccessibilityStyleGuardTests.cs:9-12`, `parties-accessibility.spec.ts:5`). Fix: extend guard roots to all four UI projects; axe on real routes.
**[Accessibility]** — Restricted badge mock hand-mixes the forbidden 4.44:1 tint (`admin-parties.html .b-restricted`). Fix: repoint to token-pair values or annotate.
**[Accessibility]** — 2.4.11 Focus Not Obscured uncovered: sticky grid header, injected banners, fixed app header. Fix: `scroll-margin-top` mandate; banners never overlay focus.
**[Accessibility]** — 4.1.3 gaps: picker result-state transitions and the export-ready moment have no announcement path. Fix: `role=status` in the picker; announce + focusable download control on ready.
**[Accessibility]** — Forced-colors strips all selected/active indicators (tint + inset box-shadow only). Fix: forced-colors-surviving border/outline + `aria-current`/`aria-selected`.
**[Accessibility]** — Tablet/phone detail sheet has no focus contract in spine text (behavior is built and e2e-pinned; spec it to match). Fix: extend the Focus management bullet.
**[Accessibility]** — Dark-mode contrast asserted, never gated: derived ramp from custom accent seed isn't automatically AA. Fix: dark-mode ≥4.5:1 acceptance check; pin the dark fill if derived misses.
**[Accessibility]** — 3.2.6 Consistent Help: no help/contact surface exists anywhere; Load-failure "support path" dangles (also an Art. 12 concern). Fix: persistent help affordance in both areas.
**[Regulated]** — Consent-during-erasure unspecced; generic "please try again" copy would be dishonest (backend rejects consent commands during erasure). Fix: disabled toggles + paused copy + erasure-specific rejection reason.
**[Regulated]** — Restriction (Art. 18) invisible to the data subject; the consent-still-editable-while-restricted guard unspecced and breakable by a helpful dev. Fix: consumer-facing restricted treatment + request path + Component Patterns pin.
**[Regulated]** — Erasure confirmation promise ("We'll confirm when it's done") has no specced channel/artifact for a person whose contact data is being shredded. Fix: PII-free request reference; in-session status confirmation; verified-deletion wording.

### Low (24)

**[Rubric]** — Restriction and Edit-my-profile have no flow (table-covered; note it). · `FluentDialog`/toast/skeleton absent from the as-is inventory. · Component name drift across spines ("Data-freshness indicator"/"Freshness indicator"; "GDPR destructive"/"GDPR action"). · Export preparing/ready states flow-only. · My-consent empty state unspecified. · `freshness.stale` unresolvable token shorthand (use `components.freshness-indicator.stale`). · Consumer IA rows lack verbatim `/me/*` routes.

**[Drift]** — Badge shape `Rounded` vs the spine's pill; the two implementations also disagree with each other (`PartyStateBadge.razor:5`). · Erase trigger is Outline, not danger fill (arguably safer; bless or fill). · Consumer erasure request has no typed confirm; spine ambiguous — code reads it as reversible-until-begins (update spine). · "Toast" vs shipped inline status regions (contract honored; update wording). · Sign-in delegated to the IdP's hosted page (update spine; ties to 3.3.8). · Export prepared synchronously (copy matches verbatim; update spine or leave).

**[Accessibility]** — Validation-rejected mock lacks `role=alert`/`aria-describedby`/`aria-invalid`. · Picker clear control is an `aria-hidden` interactive span. · Picker mock suppresses the focus outline (spec the shadow-root ring). · Consent `aria-describedby` mandated, never demonstrated. · Freshness indicator in mocks isn't a live region. · Consumer typed confirmation underspecified (cognitive burden — allow type-DELETE or two-step). · Listbox options lack per-option `id`s.

**[Regulated]** — Consent Off sub-label loss-framing in the mock ("no offers unless…"). · "erase" leaks into consumer copy (use "delete"). · Cancel-too-late race copy unspecced (backend's honest rejection string is ready to adopt). · No spine note on the README production-KMS launch gate for the consumer portal.

## Reviewer files

- `review-rubric.md`
- `review-implementation-drift.md`
- `review-accessibility.md`
- `review-regulated-language.md`
