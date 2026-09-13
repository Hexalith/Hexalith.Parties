---
name: Parties UI
description: Enterprise party-management portal (Admin + Consumer self-service) on FrontComposer + FluentUI Blazor V5.
status: final
created: 2026-06-09
updated: 2026-09-08
colors:
  # Brand DELTA only. Everything unlisted inherits FluentUI V5 (Fluent 2) +
  # the FrontComposer shell. Do NOT redeclare Fluent 2 custom properties in CSS —
  # they are JS-emitted; the shell owns the theme call (FcShellOptions.AccentColor).
  accent: '#0097A7'            # teal accent SEED — non-text use only (3.51:1 on white, fails AA for text)
  accent-dark: '#0097A7'       # single seed; Fluent 2 derives the dark ramp from it (see Colors: acceptance table)
  brand-fill: 'var(--colorBrandBackground)'  # filled primary buttons — computed 2026-09-08: #15737f light (5.54:1) / #18646e dark (6.80:1) under on-brand
  on-brand: 'var(--colorNeutralForegroundOnBrand)'  # the ONLY text token on a brand or danger *Background3 fill
  link: 'var(--colorBrandForegroundLink)'    # link text via FluentLink/FluentAnchor — 6.80:1 light; 3.90:1 DARK (fails; platform item, see Colors)
  info-foreground: 'var(--colorPaletteBlueForeground2)'  # Fluent 2 has NO --colorStatusInfo*; this pair is the raw fallback
  info-background: 'var(--colorPaletteBlueBackground2)'  # …when a component (FluentBadge Informative / MessageBar Info) can't be used
  # Inherited Fluent 2 tokens (referenced by name, never restated):
  #   surfaces/text  → --colorNeutralBackground1..6 / --colorNeutralForeground1..4
  #   strokes        → --colorNeutralStroke1..3 / --colorNeutralStrokeAccessible
  #   brand          → --colorBrand{Background,Foreground1/2,ForegroundLink,Stroke1/2}
  #   status         → --colorStatus{Success,Warning,Danger}{Foreground1,Background1..3,Border2}  (Success/Warning/Danger ONLY — no Info family exists)
  #   focus ring     → --colorStrokeFocus2
typography:
  # The ramp is inherited; the one delta is expressed as a component parameter, not CSS.
  app-title:
    note: 'Inherited — shell Typography.AppTitle (FluentText Size/Weight/Tag)'
  body-admin:
    note: 'Inherited — Fluent 2 --fontFamilyBase (Segoe UI), --fontSizeBase300 (14px), --fontWeightRegular'
  body-consumer:
    fontFamily: 'Segoe UI'                   # same family
    fontSize: 16px                           # = Fluent 2 --fontSizeBase400 via FluentText Size="TextSize.Size400"
    fontWeight: '400'
    lineHeight: 'var(--lineHeightBase400)'   # inherited with the size step — never a literal ratio
rounded:
  # Inherited Fluent 2 radii, no overrides. Named here only for prose reference; the token is the value.
  sm: 'var(--borderRadiusSmall)'      # 2px  — inputs
  md: 'var(--borderRadiusMedium)'     # 4px  — buttons, cards
  lg: 'var(--borderRadiusLarge)'      # 6px  — panels
  xl: 'var(--borderRadiusXLarge)'     # 8px  — dialogs, command palette
  full: 'var(--borderRadiusCircular)' # 10000px — status badges
spacing:
  # Fluent 2 --spacing{Horizontal,Vertical}{XXS..XXXL} inherited; no overrides.
  # No density tokens: density is one shell-owned posture — see Layout & Spacing.
  page-measure: 75rem          # inherited --fc-page-max-inline-size
components:
  party-state-badge:
    component: 'FluentBadge Appearance=Tint Shape=Circular'
    erased: 'BadgeColor.Danger'
    restricted: 'BadgeColor.Warning'
    active: 'BadgeColor.Success'
    inactive: 'BadgeColor.Subtle'
  freshness-indicator:
    component: 'FluentBadge Appearance=Tint Shape=Circular + FluentText'
    fresh: 'BadgeColor.Success'
    stale: 'BadgeColor.Warning'
    degraded: 'BadgeColor.Informative'
  gdpr-destructive-button:
    background: 'var(--colorStatusDangerBackground3)'        # allowed fallback: V5 has no danger button appearance
    background-hover: 'var(--colorStatusDangerBackground3Hover)'
    background-pressed: 'var(--colorStatusDangerBackground3Pressed)'
    foreground: 'var(--colorNeutralForegroundOnBrand)'       # white in both themes — 6.07:1 light on Background3 (computed 2026-09-08)
    radius: '{rounded.md}'
---

# Parties UI — Design

> Visual identity for `parties-ui`: a single Blazor app with two role-gated areas
> (**Admin** and **Consumer self-service**) on the FrontComposer shell + FluentUI
> Blazor V5. This spine specifies only the **brand-layer delta**; the component
> library and shell own the rest. Paired with `EXPERIENCE.md`. **Spine wins on
> conflict with any mock.** Where this file supersedes a frozen `UX-DR` from
> `epics.md`, the Components section says so.

## Brand & Style

Parties UI is an **enterprise records tool wearing a calm face**. Admins manage
person and organization records all day; consumers visit rarely, often anxiously,
to see and control the personal data held about them. Both must read as **one
product** — trustworthy, plain, unmistakably Microsoft-Fluent — never two brands
bolted together.

The visual language is **inherited, not invented**. `parties-ui` takes the
FrontComposer shell and FluentUI V5 (Fluent 2) **wholesale** — its layout, accent
(`{colors.accent}` teal seed), neutral/brand/status palettes, Segoe UI type ramp,
radii, shadows, theme toggle (Light/Dark/System), and the user's density choice.
This DESIGN.md changes only what the *brand discipline* justifies: a slightly
larger consumer body size and a handful of domain-specific components
(party-state badge, freshness indicator, GDPR destructive action, and the
modernized party picker). **If the brand can't justify overriding a token, it
doesn't override it** — Fluent 2's defaults are the contract, and a token that
Fluent 2 does not emit is never referenced.

## Colors

- **Accent — Teal (`{colors.accent}` = `#0097A7`)** is the seed the shell hands
  to Fluent (`FcShellOptions.AccentColor` → `ThemeSettings`); the shell owns the
  theme call and the module never re-issues it. **Contrast caveat
  (load-bearing):** the *raw* seed contrasts with white at **only 3.51:1 — it
  FAILS WCAG AA (4.5:1) as a fill under white text.** So the raw seed is reserved for **non-text accents
  only**: the active-nav indicator stripe, selection tints, focus-adjacent chrome.
  **Filled primary buttons (white text) bind to `{colors.brand-fill}`**
  (`--colorBrandBackground`, the derived ramp's `brand[80]` light / `brand[70]`
  dark), never the raw seed. The accent is never decorative and never a
  background wash; one primary action per view.
- **Brand fill is a computed value, not an estimate.** Read back on 2026-09-08
  from the FluentUI V5 (5.0.0-rc.5) ramp generator with the shell's settings
  (`isExact:false`, no torsion or vibrancy): `--colorBrandBackground` =
  `#15737f` light / `#18646e` dark, **5.54:1 / 6.80:1** under `{colors.on-brand}`
  — AA in both schemes, so the seed stays. The same readback found the one
  failing pair: **dark-scheme link text** (`--colorBrandForegroundLink` =
  brand[100] `#0592a1` on `--colorNeutralBackground1` `#292929`) is **3.90:1**,
  and it is *seed-independent* — fourteen teal seeds all land between 3.81:1 and
  3.95:1 because the ramp normalizes luminance. Changing the seed does not fix
  it; `--colorBrandForeground2` (brand[110], 4.70:1) would. This is a **platform
  item** (the FrontComposer theme call / Fluent's dark link token), routed in
  `EXPERIENCE.md.Open Items` — never patched in module CSS. The e2e lane
  re-records the table on every FluentUI or shell bump.
- **Neutrals** inherit Fluent 2 entirely: `--colorNeutralBackground1..6` for
  surfaces (master list / detail aside / cards), `--colorNeutralForeground1..4`
  for text hierarchy, `--colorNeutralStroke1..3` for dividers and the
  master-detail split. The shell aliases the primary text as `--fc-color-neutral`.
- **Status colors carry domain meaning** — this is the one place color is
  semantically load-bearing, so it is fixed, not free, and it is expressed through
  **component color parameters first** (`BadgeColor.*`, `MessageBarIntent.*`),
  raw tokens second:
  - **Erased / destructive** → `BadgeColor.Danger` / `--colorStatusDangerForeground1`
    on `--colorStatusDangerBackground1` (party erased, validation rejection); the
    erase **Confirm** alone uses the fill pair defined in Components.
  - **Restricted / caution / stale** → `BadgeColor.Warning` / `--colorStatusWarning*`
    (processing restricted, stale-but-usable read with a known age).
  - **Active / success / fresh** → `BadgeColor.Success` / `--colorStatusSuccess*`
    (active party, command accepted, fresh read).
  - **Processing / informational / degraded** → `BadgeColor.Informative` /
    `MessageBarIntent.Info` (command accepted-but-projecting, degraded read on a
    last-known cache). **Fluent 2 emits no `--colorStatusInfo*` token** — the status
    family is Success / Warning / Danger only, and the shell's `--fc-color-info`
    alias resolves to nothing (filed as a FrontComposer defect). Any reference to
    either is a build error. Where a component cannot carry the color, the raw
    pair is `{colors.info-foreground}` on `{colors.info-background}`.
- **Links** are `FluentLink` / `FluentAnchor` (`{colors.link}` =
  `--colorBrandForegroundLink`), never `color:` on an `<a>` and never the raw seed.
- **Focus** inherits the Fluent 2 `--colorStrokeFocus2` ring on every Fluent
  component; the module draws it only for **non-Fluent focusables** (the picker's
  shadow root, grid rows it owns). Never removed.
- **Dark mode is gated, not assumed** — the ramp is derived from the seed and a
  derived ramp is not automatically AA. **Computed-pairs acceptance table**
  (read from the running theme via the e2e accessibility lane, both color
  schemes; owner: Story 1.9 a11y gate; re-run on every FluentUI/shell bump):

  | Pair | Light | Dark | Verdict (floor ≥ 4.5:1) |
  |---|---|---|---|
  | `{colors.on-brand}` on `{colors.brand-fill}` | 5.54:1 (`#15737f`) | 6.80:1 (`#18646e`) | pass |
  | `{colors.on-brand}` on `{components.gdpr-destructive-button.background}` | 6.07:1 (`#c50f1f`) | 6.07:1 (`#c50f1f`) | pass |
  | `{colors.on-brand}` on `{components.gdpr-destructive-button.background-hover}` | 7.12:1 | 7.12:1 | pass |
  | `--colorStatusDangerForeground1` on `--colorStatusDangerBackground1` | 6.55:1 | 4.96:1 | pass (dark is thin — watch on bump) |
  | `--colorStatusWarningForeground1` on `--colorStatusWarningBackground1` | 4.85:1 | 6.97:1 | pass (light is thin) |
  | `--colorStatusSuccessForeground1` on `--colorStatusSuccessBackground1` | 5.89:1 | 6.06:1 | pass |
  | `{colors.info-foreground}` on `{colors.info-background}` | 6.42:1 | 6.42:1 | pass |
  | `{colors.link}` on `--colorNeutralBackground1` | 6.80:1 | **3.90:1** | **fails in dark — platform item, seed-independent** |
  | `--colorStatusDangerForegroundInverted` on `*Background3` (banned) | 1.74:1 | 1.17:1 | never use |

  The `*ForegroundInverted` family (`--colorStatusDangerForegroundInverted` etc.)
  is **text on dark neutral surfaces** — it is **never** placed on a `*Background3`
  fill (the pair computes to 1.74:1 light / 1.17:1 dark).

Everything to avoid here is a Don't cell in the closing table.

## Typography

Type is **inherited Fluent 2 Segoe UI** (`--fontFamilyBase`) with one deliberate
delta. The **Admin** area uses the default body ramp `--fontSizeBase300` (14px) —
dense screens, scanning, grids. The **Consumer** area bumps body to
`{typography.body-consumer.fontSize}` (16px) by rendering body copy in
`FluentText Size="TextSize.Size400"`, which brings `--lineHeightBase400` with it —
privacy copy is read slowly and often on a phone, so it gets air. Never recreate this with CSS
`font-size` / `line-height`. Headings, captions, labels, weights
(`--fontWeight{Regular,Medium,Semibold,Bold}`), and the app title (`app-title`,
the shell's `Typography.AppTitle`) are inherited unchanged in both areas. No
second typeface. The serif/display flourish other brands reach for is
**explicitly banned** — enterprise trust reads as restraint.

## Layout & Spacing

Spacing inherits the Fluent 2 `--spacing{Horizontal,Vertical}*` ramp; no token
overrides. **Rhythm is set by the shell's density posture, which the user owns:**
FrontComposer resolves one global level (in precedence order: viewport tier,
then user preference, then `FcShellOptions.DefaultDensity`, then factory default)
and applies it once on
`<body data-fc-density>`. The deployment default is **Comfortable**; the module
**never** sets `--fc-spacing-unit` or `data-fc-density` in its CSS or markup —
doing so would silently override the user's `Ctrl+,` choice. The earlier
"Consumer roomy" intent is a FrontComposer feature request (route-scoped
`DensitySurface` resolution), not a rule here. Page measure inherits
`--fc-page-max-inline-size` (`{spacing.page-measure}`), with logical properties
(`margin-inline`, `max-inline-size`) so RTL works for free.

Layout inherits the shell's `<FluentLayout>` — header, navigation rail/drawer,
content (`FcPageLayout Mode=Constrained`), footer; their dimensions are the
shell's and are **not restated here**. The Admin detail views use a
**master-detail split** (list left, `<aside>` detail right); Consumer views are
**single-column, centered, narrow measure** for readability. Page-like surfaces
with two or more sibling titled sections use one `FluentAccordion` (map in
`EXPERIENCE.md.Information Architecture`).

## Elevation & Depth

Inherited from Fluent 2 (`--shadow2 … --shadow64`): cards/flyouts/dialogs/menus
rise on the standard ramp. **Parties UI adds nothing here.** Hierarchy on the
admin master-detail is carried by **neutral background steps and strokes**
(`--colorNeutralBackground1/2`, `--colorNeutralStroke1`), not by inventing
elevation. Brand discipline: Fluent 2's shadows are correct.

## Shapes

Inherited Fluent 2 radii, mapped by surface: `{rounded.sm}` inputs/search,
`{rounded.md}` buttons and cards, `{rounded.lg}` panels and the detail aside,
`{rounded.xl}` dialogs and the Ctrl+K command palette. `{rounded.full}` is
reserved for the **party-state badge**, the **freshness badge** and count badges
only (`BadgeShape.Circular`). Crisp, low-radius corners read "system of record,"
not "consumer app" — which is the intended enterprise posture even on the
consumer side.

## Components

Parties UI uses these FluentUI V5 components **as-is, unchanged** — do not
customize them; the shell/library defaults are the contract:
`FluentLayout` · `FluentLayoutItem` · `FluentProviders` · `FluentNav` ·
`FluentNavItem` · `FluentNavCategory` · `FluentDataGrid` · `TemplateColumn` ·
`FluentTextInput` · `FluentSelect` · `FluentRadioGroup` · `FluentButton` ·
`FluentBadge` · `FluentMenu` · `FluentMenuButton` · `FluentStack` · `FluentText` ·
`FluentSpacer` · `FluentSwitch` · `FluentDialog` · `FluentMessageBar` ·
`FluentSkeleton` · `FluentAccordion` / `FluentAccordionItem` · `FluentLink` /
`FluentAnchor` · `FluentField`.
Which component serves which use is behavior and lives in the **V5 component**
column of `EXPERIENCE.md.Component Patterns`; two visual notes belong here: the
tablet detail sheet is a `FluentDialog` (V5 ships no drawer), and titled sibling
sections sit in `FluentAccordion` (`Header=`, never the V4 `Heading=`).

Brand-layer / domain components (the only specified deltas) — rendered in
[`mockups/admin-parties.html`](mockups/admin-parties.html) and
[`mockups/consumer-privacy.html`](mockups/consumer-privacy.html). **Mocks are
illustrative and never ported:** their hex values, rem sizes, `.dot`/`.sw`/
`.dialog`/`.toast`/`.b-*` classes and fixed nav width stand in for tokens and
components; each mock carries that caption.

- **Party-state badge** — one shared component (consumed by both portals, never a
  bespoke admin badge or plain consumer text) on `FluentBadge`
  (`{components.party-state-badge.component}`) bound to the party lifecycle:
  `active` → `{components.party-state-badge.active}`, `inactive` →
  `{components.party-state-badge.inactive}`, `restricted` →
  `{components.party-state-badge.restricted}`, `erased` →
  `{components.party-state-badge.erased}`. Text label always accompanies color
  (never color-only — accessibility). Color comes from the `Color` parameter;
  never hand-mix a status foreground onto an arbitrary tint (warning-on-pale-tint
  lands ~4.44:1, marginal). (`UX-DR4`.)
- **Freshness indicator** — `FluentBadge` + `FluentText`
  (`{components.freshness-indicator.component}`) expressing the read model's
  `ProjectionFreshnessMetadata`. **Three states, not two:** `fresh`
  (`{components.freshness-indicator.fresh}`, "Up to date"), `stale`
  (`{components.freshness-indicator.stale}`, always with "As of HH:MM"),
  `degraded` (`{components.freshness-indicator.degraded}`, "Showing what we last
  knew — refreshing…" — Informative, **never Warning**). No hand-rolled dot.
  Visual only; behavior lives in `EXPERIENCE.md.State Patterns`. (`UX-DR5`,
  **renamed** from "Data-freshness indicator" — the frozen name is superseded.)
- **GDPR destructive button** — one shared
  `GdprDestructiveButton` on `FluentButton`, consumed by both portals (no literal
  hex anywhere), filled with `{components.gdpr-destructive-button.background}`
  (hover `{components.gdpr-destructive-button.background-hover}`, pressed
  `{components.gdpr-destructive-button.background-pressed}`) under
  `{components.gdpr-destructive-button.foreground}` text
  (`--colorStatusDangerBackground3` + `--colorNeutralForegroundOnBrand`; white
  in both themes, 6.07:1 — see the acceptance table), radius `{rounded.md}`. The fill
  is an **allowed fallback** because V5 has no danger button appearance. Never
  `*ForegroundInverted` text on this fill (Colors), and never a `*Foreground1`
  token as a fill. Always paired with a confirmation step
  (behavior in EXPERIENCE.md). The danger fill belongs on the **in-dialog
  Confirm only**; the on-page erase *trigger* is `ButtonAppearance.Outline`
  everywhere (the safer affordance). Restrict / lift / withdraw / cancel use
  `ButtonAppearance.Outline`, never the danger fill. (`UX-DR6` **superseded**: the
  frozen row binds the fill to `--colorStatusDangerForeground1`, a *text* token;
  this spine binds the matched fill pair above — the behavioral half of `UX-DR6`
  stands.)
- **Control-pair symmetry** — the two halves of every consent or erasure choice
  ("Keep my data" / "Delete my data", grant / withdraw) get the **same visual
  weight and size**; the only asymmetry allowed is the danger fill on an
  irreversible Confirm.
- **Party picker** (`<hexalith-party-picker>`) — combobox, used as-is
  behaviorally; it stays a custom element (carve-out stated in
  `EXPERIENCE.md.Component Patterns`; `UX-DR7`). **Re-skinned to Fluent 2 tokens (done,
  2026-08):** its `--hx-picker-*` vars map onto `--colorNeutralStroke1`,
  `--colorNeutralBackground1/2`, `--colorNeutralForeground1/3`,
  `--colorStatusDangerForeground1`; keep any accent mapping in the
  `{colors.accent}` family (no foreign-brand fallback hex). Two standing visual
  requirements: a **visible `--colorStrokeFocus2` focus ring styled inside the
  shadow root** (never suppressed, never on `--hx-picker-accent`), and a clear
  affordance that is a **real `<button aria-label="Clear selection">`** (≥24px),
  not a decorated span. Selected options carry a forced-colors-surviving outline.

## Do's and Don'ts

| Do | Don't |
|---|---|
| Inherit Fluent 2 + FrontComposer for everything outside the brand layer | Redeclare Fluent 2 custom properties in CSS (they're JS-emitted), or reference a token Fluent 2 doesn't emit (`--colorStatusInfo*`, `--fc-color-info`) |
| Let the shell own the theme call (`FcShellOptions.AccentColor`) | Hard-code colors, re-issue the theme, or fork the accent off `{colors.accent}` |
| Map party/GDPR/freshness states to `BadgeColor.*` / `MessageBarIntent.*`, then `--colorStatus{Success,Warning,Danger}*` | Hand-pick hex for state colors (breaks dark mode + forced-colors) |
| Pair every state color with a text label | Communicate party state or erasure by color alone |
| Leave density to the shell (`FcShellOptions.DefaultDensity = Comfortable`, user overrides via `Ctrl+,`) | Set `--fc-spacing-unit` or `data-fc-density` in module CSS/markup, or build two density postures |
| Fill primary buttons with `{colors.brand-fill}` under `{colors.on-brand}` and record the computed ratio | Put white text on the raw seed `{colors.accent}` — it's 3.51:1, fails AA |
| Render links as `FluentLink` / `FluentAnchor` (`{colors.link}`) — links are text | Paint link text with CSS `color:` or the raw seed |
| Use the raw seed `{colors.accent}` for non-text accents only (nav stripe, tints) | Use accent decoratively, as a background, or more than one brand color |
| Express type via `FluentText Size/Weight` (`TextSize.Size400` consumer body) | Recreate a ramp step with `font-size` / `line-height` / `font-weight` in CSS |
| Keep **every area** on Fluent 2 tokens (picker re-skin done 2026-08) | Style any surface against the legacy FAST families `--type-ramp-*`, `--neutral-*`, `--accent-*`, `--neutral-fill-*`, `--palette-*` (including `--*-rest`, `--neutral-foreground-hint`) — they don't resolve in the V5 shell; a var with no fallback silently disappears |
| Give selected/active states a forced-colors-surviving indicator (`outline: var(--strokeWidthThick) solid var(--colorStrokeFocus2)`; `outline-color: Highlight` under forced-colors; + `aria-current`/`aria-selected`) | Signal selection by background tint, inset box-shadow, or an `--accent-fill-rest` outline — forced-colors strips the first two and the third never resolves |
| Reserve the danger fill for the irreversible-action **Confirm** | Use the danger color for triggers, ordinary buttons or emphasis; put `*ForegroundInverted` text on a `*Background3` fill |
| Group ≥2 sibling titled sections in one `FluentAccordion` (`Header=`), primary expanded | Stack `<section><h4>` blocks, or use the V4 `Heading=` (silently dropped) |
| Draw the focus ring only for non-Fluent focusables | Re-draw or suppress the ring on Fluent components |

**Enforcement.** These rules are tested, not trusted. One Fluent-conformance test
runs across **all four UI projects** (`Hexalith.Parties.UI`, `.AdminPortal`,
`.ConsumerPortal`, `.Picker`), mirroring FrontComposer's `FluentConformanceTests`
(`tests/Hexalith.FrontComposer.Shell.Tests/Governance`): the `RawInteractiveControl`,
`LegacyFluentToken`, `LegacyErrorToken` and `V4AccordionHeadingAttribute` regexes,
plus a raw-color check, a focus-suppression check and a `--colorStatusInfo*` /
`--fc-color-info` check, each with a **shrinking allowlist** seeded with the
offenders listed in the 2026-09-08 implementation-drift review. The existing
`AccessibilityStyleGuardTests` (one project root today) grows into it rather than living
beside it. The axe gate that records the computed-pairs table above is defined
once, in `EXPERIENCE.md.Accessibility Floor`.
