# Accessibility Review (WCAG 2.2 AA) — parties

> Lens: Accessibility (WCAG 2.2 AA), consumer-facing. Reviewed `DESIGN.md`,
> `EXPERIENCE.md`, `.decision-log.md`, and all five mockups. Review-only; no files
> changed. The spines are the contract; mocks are evidence of intent. Contrast
> figures below are computed (sRGB relative-luminance) against the exact hex values
> declared in the spine and mock `:root`.

## Overall verdict

This is a genuinely strong, accessibility-literate spine pair: it mandates
color-plus-text for every state, names `aria-live`/`role=status` for async
outcomes, specifies dialog focus trap + restore-to-trigger, demands typed
confirmation reachable by keyboard, and pushes forced-colors + reduced-motion
product-wide. The contract is in good shape. But it ships one hard, screen-wide
defect that no amount of behavioral care fixes: **the teal accent `#0097A7` used as
a button fill with white text is 3.51:1 — it fails 1.4.3 on the single primary
action of every view** (sign-in, "Export my data", "New", "Save changes", "Create
party"). Beyond that, the live-region politeness is under-specified for errors
(everything is `polite`, including validation rejections and the danger toast), and
several keyboard/focus details the spine claims as "specified" are actually only
implied — the combobox is missing `aria-controls`/`aria-activedescendant`/`role` in
the mock, the typed-confirm field is an unlabeled fake, and skip links/focus order
are asserted but never demonstrated.

## Findings

- **[critical]** **1.4.3 Contrast (Minimum)** — Teal accent `#0097A7` filled with
  white text is **3.51:1**, below the 4.5:1 floor for normal-size text. This is the
  product's *one primary action per view* (DESIGN.md "Colors"), so it fails on the
  most important control of literally every screen: "Sign in" (`signin.html`),
  "Export my data" / "Edit my profile" (`consumer-profile.html`,
  `consumer-privacy.html`), "New" / "Try again" (`admin-parties.html`), "Create
  party" (`create-edit-party.html`), "Save changes". The spine asserts the accent is
  "verified to hold ratios" (EXPERIENCE.md "Accessibility Floor") — that assertion is
  false for white-on-teal. *Fix:* do not put white text on raw `#0097A7`. Either (a)
  darken the brand fill to ~`#00767f` or darker for white text (≥4.5:1), or (b) bind
  the primary button to Fluent 2's `--colorBrandBackground` (which Fluent derives to
  meet AA) instead of feeding `#0097A7` straight to `accentBaseColor` as the fill.
  Keep `#0097A7` for the active-nav inset bar and focus accents where it is not
  carrying text. Add this as an explicit AA gate in DESIGN.md "Colors", replacing the
  unverified "ratios hold" claim.

- **[high]** **4.1.3 Status Messages / 3.3.1 Error Identification** — Live-region
  *politeness is wrong for errors*. The spine says "every async outcome … announced
  via `role="status" aria-live="polite"`" (EXPERIENCE.md "Accessibility Floor") and
  every mock live region is `polite`: the green "Saved — updating…" toast
  (`consumer-privacy.html`, `consumer-profile.html`), and — by the spine's blanket
  rule — the *danger* validation-rejection toast and the inline field errors too.
  A `polite` region defers; a rejection the user is waiting on, and that blocks them
  from proceeding, should interrupt. The spine never distinguishes polite (success /
  stale→fresh / accepted-but-processing) from assertive (validation rejected /
  transient failure / load failure). *Fix:* mandate `aria-live="assertive"` (or
  `role="alert"`) for the validation-rejected and failure paths
  (`PartyCommandValidationRejected`, `TransientFailure`, `LoadFailure`) and keep
  `polite` for accepted/processing and freshness transitions. State this split
  explicitly in "Component Patterns › Command result toast", "State Patterns ›
  Validation rejected/Transient failure", and "Accessibility Floor".

- **[high]** **4.1.2 Name/Role/Value / 1.3.1 Info & Relationships** — The party
  picker **does not implement the WAI-ARIA combobox pattern it claims**. EXPERIENCE.md
  cites `aria-autocomplete=list` and the keyboard map, but the contract never names
  the structural attributes the pattern requires, and the mock (`create-edit-party.html`)
  proves the gap: the `<input>` has `aria-expanded`/`aria-autocomplete` but **no
  `role="combobox"`, no `aria-controls` pointing at the listbox, and no
  `aria-activedescendant`**; the active option (`.o.act`) is shown only by background
  tint with **no `aria-selected`/`id`**, so a screen-reader user gets no spoken
  "active option" as they arrow. A sighted-only highlight is exactly the failure mode
  this product otherwise avoids. *Fix:* the spine must mandate the full pattern on
  `<hexalith-party-picker>`: input `role="combobox" aria-controls="<listbox-id>"
  aria-expanded aria-activedescendant="<option-id>"`, listbox `role="listbox"`,
  options `role="option" id aria-selected`, and a `role="status"` count of results.
  Fold this into the existing "re-skin the picker" design-debt item so it ships
  re-skinned *and* pattern-correct, not just re-skinned.

- **[high]** **2.4.3 Focus Order / 2.4.7 Focus Visible / 2.4.1 Bypass Blocks** —
  Focus management is *asserted but not specified or demonstrated*. The spine says
  "skip links (to content, to nav) as first tab stops" and "on async result, move
  focus to the status region; on dialog open trap; on close restore to trigger" — but
  no mock shows a skip link, a visible focus ring, a programmatic tab order, or which
  element receives focus on async result. "Pattern already in AdminPortal — keep it"
  is the only specification, which leaves the *new Consumer area and the freshness
  indicator* (explicitly called out as needing the extension) with no concrete focus
  contract. Moving focus to a transient toast on every optimistic save (Flow 2 fires
  on each consent flip) can also be disruptive if mis-applied. *Fix:* in
  "Interaction Primitives" specify per-surface: (1) skip-link targets and that they
  are the first two tab stops product-wide incl. Consumer; (2) that the focus ring
  `--colorStrokeFocus2` is present on the *fake* controls too (the consent toggle,
  the segmented Person/Org control, the picker clear "✕" are non-focusable `<div>`s
  in the mocks — see next finding); (3) for async results, prefer announcing via the
  live region over *stealing* focus on routine optimistic saves; reserve focus-move
  for errors the user must act on.

- **[high]** **4.1.2 Name/Role/Value / 2.1.1 Keyboard** — Several **interactive
  controls are non-semantic `<div>`/`<span>`** in the mocks, so as specified they are
  not keyboard-operable or named: the consent switch (`.sw` `<div>`,
  `consumer-privacy.html`) — a custom toggle with no `role="switch"`,
  `aria-checked`, label association, or tabindex; the Person/Organization segmented
  control (`.seg .opt` `<div>`s, `create-edit-party.html`) with no `role="radio"`/
  `radiogroup`; the GDPR action rows, the picker clear "✕", and the data-grid rows
  (`.grow` `<div>`s — the spine promises arrow-key row nav + Enter, but the mock grid
  is non-semantic divs). These are mock shortcuts, but the spine never *forbids*
  them, and the consent toggle is the centerpiece of consumer Flow 2. *Fix:* the
  spine must require these be real components (`FluentSwitch` with label +
  `aria-checked`; `FluentRadioGroup`; `FluentDataGrid` rows with `role=row/gridcell`)
  and state that no interactive affordance ships as a bare styled `<div>`. Confirm
  the consent toggle exposes its on/off *and* its purpose name to AT.

- **[high]** **3.3.2 Labels or Instructions / 4.1.2** — The **typed-confirmation
  field in the erase dialog is not a real, labeled input**. In `admin-parties.html`
  the confirm field is a `<div class="confirm-field">Type "Jordan Webb" to
  confirm…</div>` — placeholder text in a div, no `<input>`, no `<label>`, no
  `aria-describedby` tying the instruction to the field. The spine mandates
  "destructive actions require typed confirmation … confirmable by keyboard"
  (EXPERIENCE.md), but as drawn it is neither typeable nor labeled. The dialog itself
  is good (`role="dialog" aria-modal="true" aria-labelledby`). *Fix:* require a real
  `<input>` with an associated `<label>` (or `aria-label`) and `aria-describedby`
  pointing at the irreversibility warning; the Erase button stays disabled
  (`aria-disabled`) until the typed name matches, and that enable/disable transition
  is announced.

- **[medium]** **1.4.11 Non-text Contrast** — The teal accent as a **UI/component
  boundary** is also 3.51:1, below the 3:1 floor for UI components in some uses, and
  the active-state cues lean on it. The picker combobox border is `1px solid
  var(--accent)` (`create-edit-party.html`) at 3.51:1 — passes 3:1 as a focus/active
  boundary, but barely, and there is no *non-color* indication that the combo is the
  focused/active field. The freshness/consent dots are 8px (`.dot`) — small graphical
  objects that carry meaning; they pass 3:1 against white but the spine should not let
  the *dot alone* ever be the signal (it currently always pairs with a word, which is
  correct — keep that mandate). *Fix:* ensure active/selected affordances also carry a
  non-color cue (border-weight change, checkmark) and keep the dot-plus-word rule
  inviolable in the spine.

- **[medium]** **1.4.3 Contrast (Minimum)** — **Warning text on its tint badge is
  4.44:1** (`#bc4b09` on `#fbeee2`, the "Restricted" party-state badge in
  `admin-parties.html`) — just under 4.5:1 for the 12px/`font-weight:600` label.
  Borderline, and it is one of the four load-bearing state badges. (For reference,
  success 5.50:1, danger 6.08:1, info-banner 10:1 all pass; warning-on-white is
  5.06:1, fine; the failing case is warning-on-its-own-tint.) *Fix:* darken the
  warning foreground used *on the tint* (e.g. `#9a3e07`) or lighten/adjust the
  warning tint so the badge clears 4.5:1; verify in dark mode too. Add badge-on-tint
  to the AA gate, not just text-on-white.

- **[medium]** **2.5.8 Target Size (Minimum)** — The spine claims **≥44px** targets
  (well above the 24px AA floor — good intent), but the mocks contradict it and the
  spine doesn't say where 44px is measured. The 22×40px consent **toggle** (`.sw`),
  the 28px header **avatar**, the picker **clear "✕"**, the grid **filter selects**,
  and the dialog's small **Cancel/Erase** buttons are all under 44px and several are
  under 24px. AA (24px) is likely still met for most via spacing, but the spine's own
  44px promise is unmet and untested. *Fix:* state that 44px is the *target* incl. a
  44px touch slop even when the visual control is smaller (the toggle's hit area, the
  ✕'s padding), and call out the consent toggle and icon-only controls specifically
  since Consumer is phone-first.

- **[medium]** **1.4.10 Reflow / 1.3.4 Orientation** — Admin master-detail reflow at
  phone width is **described in the spine but not evidenced**. EXPERIENCE.md
  "Responsive & Platform" says list→full-screen detail with back-returns-to-list at
  <640px, but `admin-parties.html` only mocks the desktop two-pane grid
  (`grid-template-columns:48px 220px 1fr` then `1.35fr 1fr`) with a fixed 1160px
  window; there is no phone mock and no statement of *focus behavior* when the detail
  overlay opens/closes (where does focus go, is it trapped, does back restore focus to
  the originating row?). For a data-dense grid this is the highest reflow risk in the
  product. *Fix:* specify the detail-as-sheet focus contract (move focus into the
  sheet on open, restore to the row on back) and confirm the grid and detail reflow to
  a single column without horizontal scroll at 320px CSS px.

- **[low]** **1.3.1 / 3.3.2** — Consent purpose + lawful basis is shown as adjacent
  text but the spine's `aria-describedby` tie is **not demonstrated**. In
  `consumer-privacy.html` the lawful-basis line ("lawful basis: consent") sits in a
  sibling `<div class="basis">` near the toggle but with no programmatic association;
  the spine says it should be tied via `aria-describedby` — make sure that survives
  into the real switch component so the basis is announced *with* the control, not as
  loose nearby text. *Fix:* keep the `aria-describedby` mandate explicit on the
  consent control, referencing the purpose+basis element id.

- **[low]** **4.1.3** — The **freshness "stale→fresh" transition** announcement is
  specified ("`aria-live` announces when fresh", EXPERIENCE.md State Patterns/Freshness),
  but the freshness indicator in the mocks (`.fresh`/`.freshbar`) is **not itself a
  live region** — only the separate detail banner and toast are. If the dot/word is
  the only thing that changes on reconnect, that change is silent. *Fix:* make the
  freshness indicator's text node a `role="status" aria-live="polite"` (or update a
  shared status region) so "Up to date" is actually announced on transition, as the
  spine intends.

- **[low]** **1.4.12 Text Spacing / 1.4.4 Resize** — Consumer 16px body at line-height
  1.5 is good (per `body-consumer`), but several **secondary strings sit at 12–13px**
  (`.basis`, `.note`, `.fresh`, `.saving`, `.hint`, grid headers, `state-label`) on a
  phone-first surface. They pass contrast (`#616161` = 6.19:1) but 12px GDPR-relevant
  microcopy (lawful basis, "as of HH:MM", the picker keyboard hint) is small for the
  anxious, occasional consumer the spine describes. *Fix:* floor consumer secondary
  text at 13–14px and ensure all of it survives 200% zoom / text-spacing without
  clipping; nothing should be conveyed only at ≤12px.

## What's already strong

- **Color-is-never-alone is mandated, not hoped for** — DESIGN.md Do/Don't ("Pair
  every state color with a text label" / "never communicate state by color alone"),
  the party-state badge spec, and the freshness "dot **and** word" rule all enforce
  it, and the mocks comply (every badge and dot carries text). This is the single
  most-violated SC in real products and it's locked here (1.4.1).
- **Async outcomes are first-class for AT** — the contract explicitly routes every
  command result, staleness transition, and erasure change through `role=status`/
  `aria-live`, and the mocks actually carry the attributes (the stale banner, the
  "Saved — updating…" toasts). The *politeness split* needs fixing (above), but the
  intent and plumbing are right (4.1.3).
- **Dialog semantics are correct** — the erase dialog has `role="dialog"
  aria-modal="true" aria-labelledby`, modal depth is capped at ≤1, native
  `alert/confirm` are banned (they'd break focus + the Blazor loop), and focus
  trap + restore-to-trigger is named (2.4.3, 2.1.2).
- **Plain-language GDPR copy** directly serves cognitive accessibility (3.1.5
  spirit): "It'll be gone within 30 days. You can change your mind…" instead of
  "erasure request submitted to the data subject rights pipeline."
- **Forced-colors and prefers-reduced-motion are pushed product-wide**, not left on
  the legacy picker — the spine explicitly extends both from the picker to the whole
  app (1.4.1 forced-colors support, 2.3.3 reduced motion).
- **Inputs are properly labeled where real** — sign-in uses `<label for>` ↔ `id`
  (`signin.html`), the edit-profile and create forms use real `<label>` + required
  markers, and validation errors are shown inline with the input preserved and a
  suggestion ("That doesn't look like an email address") — good 3.3.1/3.3.3 intent.
- **Strong contrast almost everywhere else** — danger (7.12:1), info (6.66:1),
  success (6.28:1), warning-on-white (5.06:1) all clear AA on white; body and
  secondary neutrals clear AA; the 16px consumer body is a deliberate, correct call.
  The accent-fill defect is the conspicuous exception, not the rule.
</content>
</invoke>
