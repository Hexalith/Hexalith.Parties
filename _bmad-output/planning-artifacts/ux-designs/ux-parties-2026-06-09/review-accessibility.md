# Accessibility Review — parties (re-review 2026-08-18)

> Lens: Accessibility (WCAG 2.2 AA), adversarial re-review of the finalized spine
> pair (`DESIGN.md`, `EXPERIENCE.md`), the decision-log resolutions from the
> 2026-06-09 review, and all five mockups. Contrast figures are computed (sRGB
> relative luminance) against the exact hex declared in the spine and mock
> `:root` blocks. Mocks are illustrative and the spine wins on conflict; mock
> findings below are limited to defects a developer would copy.

## Overall verdict

The June criticals were genuinely fixed in the spine text — the contrast rebind,
politeness split, combobox pattern, semantic controls, focus contract, and honest
erasure copy are all findable, specific, and correctly worded; this remains one of
the more accessibility-literate spine pairs I have reviewed. Two things keep it
from a clean bill: the mock repoint was incomplete (the sign-in mock still paints
link text in the raw 3.51:1 accent, and the restricted badge still hand-mixes the
exact 4.44:1 tint the spine now forbids), and the spine's claimed WCAG **2.2** AA
floor is silent on three of the criteria that are new in 2.2 where the product
triggers them — most importantly 3.3.8 Accessible Authentication on the sign-in
surface that gates every legal right downstream. No criticals; the highs are
cheap, surgical fixes.

## June resolutions — landed?

- **Contrast rebind (accent → `--colorBrandBackground` for filled buttons)** —
  **landed** in DESIGN.md frontmatter (`brand-fill` token), §Colors (load-bearing
  caveat, 3.51:1 stated), §Do's and Don'ts (two rows), and EXPERIENCE.md
  §Accessibility Floor (1.4.3 note). All five mocks fill `.btn.primary` with
  `--brand-fill:#00767f` (verified 5.39:1). **Caveat:** the log's claim "all 5
  mocks repointed" is incomplete — `signin.html` `.alt a` still binds *text* to
  the raw accent (see Findings, high).
- **Live-region politeness split (polite vs `role=alert`)** — **landed** in
  EXPERIENCE.md §Accessibility Floor (first bullet), §Component Patterns
  (Command result toast row), §State Patterns (Validation rejected row). Mock
  demonstration is partial (see Findings, low).
- **Full WAI-ARIA combobox on the picker** — **landed** in EXPERIENCE.md
  §Component Patterns (Party picker row names `role=combobox`, `aria-expanded`,
  `aria-controls`, listbox/option roles, `aria-selected`,
  `aria-activedescendant`, `aria-autocomplete=list`), folded into the re-skin
  debt as intended; `create-edit-party.html` is wired accordingly.
- **Semantic controls (consent `role=switch`; type `radiogroup`; real labeled
  erase-confirm input; no interactive `<div>`s)** — **landed** in EXPERIENCE.md
  §Accessibility Floor ("Real semantics, not styled divs") and §Component
  Patterns; mocks updated (`consumer-privacy.html` switch is a real `<button
  role="switch" aria-checked aria-labelledby>`; `admin-parties.html` confirm is a
  real `<input aria-label>`; `create-edit-party.html` chooser is a `radiogroup`).
- **Per-surface focus contract (trap/restore on dialogs, move-to-alert on
  blocking errors, announce-only on optimistic saves)** — **landed** in
  EXPERIENCE.md §Interaction Primitives (Focus management bullet) and
  §Accessibility Floor. Gap remains for the tablet/phone detail sheet (see
  Findings, medium).
- **Erasure copy honesty (start-not-finish, cancellable-vs-permanent, neutral
  ack, no success-green)** — **landed** in EXPERIENCE.md §Voice and Tone,
  §State Patterns (Erasure requested / in progress row), §Component Patterns
  (toast row); `consumer-privacy.html` State B matches (neutral `#1f2937` toast,
  both halves stated, 30-day figure separated from the cancel window).

## Findings

- **[high]** **1.4.3 — Sign-in mock still paints text in the raw accent.**
  `signin.html` `.alt a { color: var(--accent) }` renders the "Use your
  organization account" SSO link in `#0097A7` on white = **3.51:1**, on the one
  surface every user must pass. This is the exact failure mode the June critical
  fixed, surviving in the mock the fix claimed to have repointed — and it
  directly violates DESIGN.md §Colors ("raw accent … non-text accents only").
  A developer copying the sign-in card ships an AA failure on the auth path.
  *Fix:* repoint `.alt a` to `--brand-fill` (5.39:1) and add "links are text —
  never raw accent" to the DESIGN.md Don't column so the rule visibly covers
  hyperlinks, not just button fills.

- **[high]** **3.3.8 Accessible Authentication (Minimum) — unaddressed on a
  claimed WCAG 2.2 AA floor.** The Sign in surface is first-class in
  EXPERIENCE.md §Information Architecture and mocked (`signin.html` email +
  password + SSO), yet neither spine says anything about authentication
  accessibility: nothing guarantees paste/password-manager support, the mock
  inputs carry no `autocomplete="username"` / `"current-password"` tokens, and
  no rule bans a cognitive-function test (CAPTCHA, memorized-transcription) or
  requires an alternative. A consumer who cannot clear sign-in can exercise *no*
  GDPR right — this gates the entire product. *Fix:* add to §Accessibility
  Floor: sign-in must not block paste or autofill, must carry `autocomplete`
  tokens, and must offer a non-cognitive-test path (SSO/passkey/email link
  qualify); if authentication is delegated to the OIDC provider, say so
  explicitly and make 3.3.8 a requirement on the chosen IdP configuration.

- **[medium]** **1.4.3 — Restricted badge in the mock still hand-mixes the tint
  the spine forbids.** `admin-parties.html` `.b-restricted` is `#bc4b09` on
  `#fbeee2` = **4.44:1** at 12px/600 — under the 4.5:1 floor, on a load-bearing
  lifecycle state. DESIGN.md §Components (party-state-badge) now correctly
  mandates matched `--colorStatus*Foreground1`-on-`Background1` token pairs and
  even names this 4.44:1 hand-mix as the anti-pattern — but the mock was never
  updated, so the reference rendering *is* the anti-pattern a developer will
  copy. *Fix:* repoint the mock badge tints to the Fluent pair values (e.g.
  warning fg on `#fff9f5` = 4.85:1) or annotate the badge CSS "illustrative —
  use token pairs per DESIGN.md".

- **[medium]** **2.4.11 Focus Not Obscured (Minimum) — not covered anywhere.**
  The design ships the ingredients for obscured focus: a sticky grid header
  (`admin-parties.html` `.grow.head { position: sticky }`), toasts and freshness
  banners injected above content, and a fixed 48px app header. Neither spine
  mentions 2.4.11, so nothing stops a keyboard user's focused row scrolling
  under the sticky header or a toast landing over the focused control. *Fix:*
  add to EXPERIENCE.md §Interaction Primitives: focused elements must remain at
  least partially visible — `scroll-margin-top` ≥ header+sticky-row height on
  focusable list/grid items; toasts/banners never overlay the element that holds
  focus.

- **[medium]** **4.1.3 — The live-region strategy misses the picker's async
  states and the export-ready moment.** EXPERIENCE.md routes command results,
  freshness transitions, and erasure progression through named live regions —
  good — but two async changes have no announcement path: (a) the party picker's
  result-state transitions (`Ready` result count, `Empty` "no matches",
  `Degraded`/`LocalOnly` "limited results" — Flow 4's failure shows a *quiet
  visual note* only), and (b) Flow 1's climax, where "a download appears" for
  the export with no announced arrival — a blind consumer waiting on their
  Art. 20 export hears nothing. *Fix:* mandate a `role=status` region inside the
  picker announcing result count / empty / limited-results on state change
  (§Component Patterns picker row), and an explicit polite announcement +
  focusable "Download your export" control when the export readies (§State
  Patterns or Flow 1).

- **[medium]** **1.4.1/1.4.11 in forced-colors — the selected/active indicators
  are exactly what Windows High Contrast strips.** The spine mandates
  forced-colors support product-wide (§Accessibility Floor) but the design's
  selection affordances are a background tint + `box-shadow: inset 3px 0 0
  var(--accent)` (`admin-parties.html` `.nav a.active`, `.grow.sel`;
  `create-edit-party.html` `.seg .opt.on`, `.listbox .o.act`) — forced-colors
  mode removes both backgrounds and box-shadows, leaving the selected nav item,
  selected grid row, chosen radio, and active combobox option visually
  indistinguishable. *Fix:* specify a forced-colors-surviving indicator: a real
  (transparent-until-forced) border or outline on selected/active states, plus
  `aria-current`/`aria-selected` so AT state is independent of paint.

- **[medium]** **2.4.3 — The tablet/phone detail sheet has no focus contract.**
  §Interaction Primitives specifies trap/restore for *dialogs*, and §Responsive
  & Platform turns the Admin detail into an "overlay/sheet over the list"
  (640–1023px) and a full-screen page (<640px) — but nothing says whether the
  sheet traps focus, where focus lands on open, or that back/close restores it
  to the originating grid row. Dialog `Esc` behavior is also unstated (the
  picker's `Esc` is specified; the erase dialog's is not). The phone-reflow
  *mock* is a documented residual; this missing *spine text* is not. *Fix:*
  extend the Focus management bullet: detail sheet behaves as a dialog on
  tablet (trap, `Esc` closes, restore-to-row) and as a page on phone (focus to
  heading on open, back restores the row); state `Esc` closes any `FluentDialog`.

- **[medium]** **1.4.3/1.4.11 — Dark mode contrast is asserted, never gated.**
  DESIGN.md verifies the AA story for light mode only (`brand-fill` ≈ `#00767f`,
  5.39:1 on white). Dark mode relies on Fluent deriving tints from the *custom*
  accent base `#0097A7` via `baseLayerLuminance` (frontmatter `accent-dark`) —
  a derived ramp from a custom seed is not automatically AA for filled-button
  text or status-token pairs, and no dark-mode target is stated anywhere. *Fix:*
  add one line to DESIGN.md §Colors: the dark-theme derived brand fill and the
  four status token pairs must be verified ≥4.5:1 in dark mode as an
  acceptance check; if the derived fill misses, pin the dark brand fill
  explicitly.

- **[medium]** **3.2.6 Consistent Help — no help surface exists at all.**
  §State Patterns (Load failure) promises a "support path," but the
  §Information Architecture table contains no Help/Contact surface, and nothing
  specifies a consistent location for it across pages. A data subject exercising
  a legal right who hits a wall (erasure stuck, export failing, identity
  dispute) has no specified route to a human or the DPO — a GDPR Art. 12
  facilitation concern as much as a 3.2.6 one. *Fix:* add a persistent,
  consistently-placed help/contact affordance (footer or nav) to the IA for both
  areas, and point the Load-failure "support path" at it.

- **[low]** **4.1.3/3.3.1 — The validation-rejected mock doesn't carry the
  semantics the spine mandates.** In `create-edit-party.html`, the rejection
  banner (`.banner.warn`) has no `role="alert"`, the `.err` messages have no
  `id`/`aria-describedby` tie to their inputs, and the `.input.bad` fields lack
  `aria-invalid="true"` — while §State Patterns requires `role=alert` +
  `aria-describedby` for exactly this state. The spine wins, but this mock is
  the named visual reference for the rejected state. *Fix:* wire the mock (or
  annotate it) to match the spine's own mandate.

- **[low]** **4.1.2 — The picker's clear control is an `aria-hidden`
  interactive span.** `create-edit-party.html` `.combo .clear` is `<span
  aria-hidden="true">✕</span>` with `cursor:pointer` — invisible to AT,
  unfocusable, unnamed; the spine's picker spec covers `Backspace`-to-clear but
  never names the visible clear affordance's semantics. *Fix:* spec it as a real
  `<button aria-label="Clear selection">` in the picker's Component Patterns
  row (it ships inside the re-skin debt anyway).

- **[low]** **2.4.7 — The picker mock suppresses the focus outline.**
  `create-edit-party.html` `.combo input { border:0; outline:0 }` leaves the
  1px accent wrapper border (3.51:1, barely over the 3:1 non-text floor) as the
  only focus cue — against §Accessibility Floor's "ring never suppressed."
  Because the picker is a shadow-DOM custom element, the `--colorStrokeFocus2`
  ring must be *explicitly* styled inside it. *Fix:* add "visible
  `--colorStrokeFocus2` ring on the combobox input, styled within the shadow
  root" to the picker re-skin debt item.

- **[low]** **1.3.1 — Consent purpose/basis `aria-describedby` is mandated but
  never demonstrated.** `consumer-privacy.html` switches carry
  `aria-labelledby` to the purpose name only; the state line ("Off — you won't
  get product emails.") and the lawful-basis text are unassociated siblings.
  The spine's mandate (§Component Patterns, consent control) is correct — make
  sure the mock/real component ties the sub-text via `aria-describedby` so
  state + basis are announced with the switch, and that the sub-text's
  On/Off wording is updated in the same commit as `aria-checked`.

- **[low]** **4.1.3 — Freshness indicator in the mocks is not itself a live
  region.** The spine requires an `aria-live=polite` announcement on freshness
  transitions (§Component Patterns), but `.fresh`/`.freshbar` in
  `consumer-profile.html`, `consumer-privacy.html`, and `admin-parties.html`
  carry no `role="status"` — a dev copying the markup ships a silent
  stale→fresh change. *Fix:* add `role="status"` to the indicator's text node in
  the mocks or annotate the mandate inline.

- **[low]** **Cognitive floor — the consumer-side typed confirmation is
  underspecified.** §Component Patterns applies typed confirmation to
  irreversible actions in *Both* areas, but only the Admin dialog is specified
  ("type the person's name"); what a consumer must type for "Delete my data" is
  never stated, and transcription is a real barrier for the anxious, occasional,
  phone-first user the spine itself describes. *Fix:* specify the consumer
  confirm explicitly and allow a lighter-but-safe equivalent (e.g. type DELETE,
  or an explicit two-step with full-sentence consequence copy) — keep the
  friction, drop the transcription burden.

- **[low]** **4.1.2 — Listbox options lack per-option `id`s in the mock.** In
  `create-edit-party.html` only the active option (`#o1`) has an `id`;
  `aria-activedescendant` must retarget as the user arrows, which requires ids
  on *all* options. Trivial, but the mock is the wiring reference. *Fix:* give
  every `role="option"` an `id` in the mock.

Not triggered / no finding: **2.5.7 Dragging Movements** (no drag interactions
anywhere in the design); **3.3.7 Redundant Entry** (no re-requested data; the
typed confirm is an essential-confirmation exception, softened further by the
low finding above).

## Known residuals (not re-flagged)

- Phone-reflow mock for the Admin master–detail deferred (the *spine-text* focus
  contract for that sheet is flagged above as new, separate from the mock).
- Sub-24px decorative controls and 12px secondary text in mocks are
  illustrative-only; real build floors consumer secondary text 13–14px and
  applies 44px touch slop.
- Picker FAST→Fluent-2 re-skin (plus its ARIA wiring) carried as design debt —
  the two picker lows above should ride inside that same debt item.
- PII handling of the admin typed-name confirm is spec-clean; implementation
  keeps it in-memory.
