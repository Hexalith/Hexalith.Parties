# Validation Report — parties

- **DESIGN.md:** `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/DESIGN.md`
- **EXPERIENCE.md:** `_bmad-output/planning-artifacts/ux-designs/ux-parties-2026-06-09/EXPERIENCE.md`
- **Run at:** 2026-06-09
- **Lenses:** rubric walker · accessibility (WCAG 2.2 AA) · consumer-trust & regulated-language
- **Totals:** 3 critical · 7 high · 7 medium · 9 low — **all critical + high resolved in this run.**

## Overall verdict

The rubric found a **strong, source-extractable** spine pair — all eight categories
*strong*, 0 critical/high. The **accessibility** and **regulated-language** lenses
shifted the picture: accessibility surfaced one screen-wide critical (the teal
accent at 3.51:1 on every primary action) plus five highs; regulated-language
surfaced two criticals (a hard 30-day erasure SLA, and never stating completed
erasure is permanent) and a lawful-basis honesty gap. **Every critical and high was
resolved in this run** — DESIGN.md rebinds filled buttons to the AA-safe
`--colorBrandBackground` and drops the false "ratios hold" claim; EXPERIENCE.md
splits live-region politeness, mandates the full combobox/switch/radiogroup
semantics, refines the focus contract, separates "things you control" from "things
we keep," and corrects the erasure + export copy; all five mocks were patched to
comply. Remaining items are medium/low residuals, mostly mock-fidelity.

## Category verdicts

- Flow coverage — **strong**
- Token completeness — **strong**
- Component coverage — **strong**
- State coverage — **strong**
- Visual reference coverage — **strong**
- Bloat & overspecification — **strong**
- Inheritance discipline — **strong**
- Shape fit — **strong**

## Findings by severity

### Critical (3) — all resolved

**[Accessibility · 1.4.3]** Teal accent `#0097A7` + white text = 3.51:1 on the primary action of every screen (§ DESIGN.md.Colors; all mocks)
The product's one-primary-action-per-view fails AA; the spine even claimed the accent "holds ratios."
Fix (applied): raw accent reserved for non-text use; filled buttons bind to `--colorBrandBackground` (AA-safe ≈#00767f); false claim replaced with an explicit AA gate; mocks repointed to `--brand-fill:#00767f`.

**[Regulated · GDPR Art. 12(3)]** Hard 30-day erasure SLA committed in user copy (§ EXPERIENCE.md.Voice and Tone; consumer-privacy.html)
A fixed completion window can slip for a crypto-shred across projections.
Fix (applied): copy commits to the *start* — "We've started deleting your data… usually within 30 days… we'll confirm when it's done."

**[Regulated]** Completed-erasure irreversibility never stated; only cancellability shown (§ EXPERIENCE.md.State Patterns; consumer-privacy.html)
A user reading "any time within 30 days" could try to cancel after the key is already shredded.
Fix (applied): both halves stated — cancel until deletion begins; once done, permanent. New two-state "Erasure requested / in progress" pattern; 30-day no longer reads as the cancel window.

### High (7) — all resolved

**[Accessibility · 4.1.3]** Live-region politeness uniformly "polite", including errors (§ EXPERIENCE.md.Accessibility Floor / Component Patterns / State Patterns)
Fix (applied): split — status/freshness/processing = `polite`; validation-rejected/failure = `role=alert` (assertive).

**[Accessibility · 4.1.2]** Party picker doesn't implement the WAI-ARIA combobox pattern it claims (§ EXPERIENCE.md.Component Patterns; create-edit-party.html)
Fix (applied): spine mandates the full pattern (folded into the re-skin debt); mock gets role=combobox + aria-controls + aria-activedescendant; options role=option + aria-selected.

**[Accessibility · 4.1.2 / 2.1.1]** Interactive controls were non-semantic divs — consent toggle, segmented control, typed-confirm (§ EXPERIENCE.md.Accessibility Floor; consumer-privacy.html / create-edit-party.html / admin-parties.html)
Fix (applied): spine forbids interactive divs; mocks use role=switch+aria-checked, role=radiogroup/radio, and a real labeled confirm `<input>`.

**[Accessibility · 2.4.x]** Focus management asserted but not specified for the Consumer area (§ EXPERIENCE.md.Interaction Primitives)
Fix (applied): per-surface contract — trap/restore on dialogs, move-to-alert on blocking errors, announce-via-aria-live (no focus steal) on optimistic saves.

**[Accessibility · 3.3.2 / 4.1.2]** Typed-confirmation erase field was an unlabeled div (§ admin-parties.html; EXPERIENCE.md)
Fix (applied): real labeled `<input>`; spine requires aria-describedby to the irreversibility warning, Erase disabled until the name matches.

**[Regulated]** Lawful-basis list implied control that doesn't exist for contract / legitimate interest (§ consumer-privacy.html; EXPERIENCE.md.Voice and Tone / Component Patterns)
Fix (applied): split "Things you control" (consent) from "Things we keep to run your account"; legitimate interest gets an *Object* (Art. 21) action, not a withdraw toggle.

**[Regulated]** Export latency over-promised ("ready in a moment / under a minute") (§ EXPERIENCE.md.Voice and Tone / Flow 1; consumer-privacy.html)
Fix (applied): time promise dropped; machine-readable (JSON) expectation set; "we'll show it here the moment it's ready."

### Medium (7)

- **[Accessibility · 1.4.3]** Warning-on-tint badge 4.44:1 (Restricted) — *resolved*: spine mandates Fluent matched `--colorStatus*Foreground1`-on-`Background1` pairs.
- **[Accessibility · 2.5.8]** 44px target claim contradicted — *resolved in spine*: ≥24px AA floor + ≥44px touch target stated; small decorative mock controls residual.
- **[Accessibility · 1.4.11]** Accent as UI boundary 3.51:1 / active cue color-only — *addressed*: active/selected affordances must carry a non-color cue; dot-plus-word rule kept inviolable.
- **[Accessibility · 1.4.10]** Admin master-detail phone reflow described but not mocked — *residual*: sheet + focus contract specified in prose; phone mock deferred.
- **[Regulated · Art. 7]** Marketing consent defaulted On — *resolved*: consent defaults Off, never pre-checked; "no consent dark patterns" anti-pattern added.
- **[Regulated]** "Erasure" legalese + success-green toast on a deletion — *resolved*: "Delete my data" leads; erasure ack uses neutral/info tone, never success-green.
- **[Regulated]** Conflicting "Saved" + "Saving" on one screen — *resolved*: single coherent status source.

### Low (9)

- **[Rubric]** "GDPR destructive" vs "GDPR action" button label drift — *accepted* (visual vs behavioral naming; unambiguous).
- **[Rubric]** Edit-profile / Sign-in have no narrated flow/failure copy — *residual* (mocked + State Patterns cover them).
- **[Rubric]** `freshness.stale` shorthand vs full token path — *accepted* (resolves unambiguously).
- **[Rubric]** Picker `PartyPickerSearchState` not in the page-level State table — *accepted* (enum named verbatim; add a sub-table if the picker becomes its own story).
- **[Accessibility · 4.1.3]** Freshness indicator not itself a live region — *addressed in spine* (pairs an aria-live announcement on transition).
- **[Accessibility · 1.3.1]** Consent `aria-describedby` tie not demonstrated — *addressed in spine* (mandated on the control).
- **[Accessibility · 1.4.4]** Consumer secondary text at 12px — *residual* (real build floors at 13–14px; mocks illustrative).
- **[Regulated]** Profile email view↔edit mismatch — *resolved* (aligned).
- **[Regulated]** "Manage all consent" link surface — *resolved* (anchored to My consent; privacy card is a summary). PII-in-copy: spec already clean; implementation note to keep admin typed-name in-memory only.

## Reviewer files

- `review-rubric.md`
- `review-accessibility.md`
- `review-regulated-language.md`
