---
name: Parties UI
description: Enterprise party-management portal (Admin + Consumer self-service) on FrontComposer + FluentUI Blazor V5.
status: final
created: 2026-06-09
updated: 2026-06-09
colors:
  # Brand DELTA only. Everything unlisted inherits FluentUI V5 (Fluent 2) +
  # the FrontComposer shell. Do NOT redeclare Fluent 2 custom properties in CSS —
  # they are JS-emitted and immutable; override only via the design-token API.
  accent: '#0097A7'            # teal accent BASE — non-text use only (3.51:1 on white, fails AA for text)
  accent-dark: '#0097A7'       # single accent; Fluent derives dark-mode tints via baseLayerLuminance
  brand-fill: 'var(--colorBrandBackground)'  # AA-safe (~#00767f) — filled primary buttons w/ white text bind HERE, not to accent
  # Inherited Fluent 2 tokens (referenced by name, never restated):
  #   surfaces/text  → --colorNeutralBackground1..6 / --colorNeutralForeground1..4
  #   strokes        → --colorNeutralStroke1..3 / --colorNeutralStrokeAccessible
  #   brand          → --colorBrand{Background,Foreground1/2,Stroke1/2}
  #   status         → --colorStatus{Success,Warning,Danger,Info}Foreground1  (aliased by shell as --fc-color-*)
  #   focus ring     → --colorStrokeFocus2
typography:
  # Only the area-density delta is declared; the ramp is inherited.
  app-title:
    note: 'Inherited — shell Typography.AppTitle (FluentText Size/Weight/Tag)'
  body-admin:
    note: 'Inherited — Fluent 2 --fontFamilyBase (Segoe UI), --fontSizeBase300 (14px), --fontWeightRegular'
  body-consumer:
    fontFamily: 'Segoe UI'     # same family, larger base for calm readability
    fontSize: 16px             # = Fluent 2 --fontSizeBase400; the one type delta consumer-side
    fontWeight: '400'
    lineHeight: '1.5'
rounded:
  # Inherited Fluent 2 radii, no overrides. Named here only for prose reference.
  sm: 2px                      # --borderRadiusSmall  — inputs
  md: 4px                      # --borderRadiusMedium — buttons, cards
  lg: 6px                      # --borderRadiusLarge  — panels
  xl: 8px                      # --borderRadiusXLarge — dialogs, command palette
  full: 9999px                 # --borderRadiusCircular — status badges
spacing:
  # Fluent 2 --spacing{Horizontal,Vertical}{XXS..XXXL} inherited; no overrides.
  # Area rhythm is set by FrontComposer density (--fc-spacing-unit), NOT by new tokens:
  density-admin: 4px           # comfortable (data-dense default)
  density-consumer: 6px        # roomy (calm self-service)
  page-measure: 75rem          # inherited --fc-page-max-inline-size
components:
  party-state-badge:
    radius: '{rounded.full}'
    erased: 'var(--colorStatusDangerForeground1)'
    restricted: 'var(--colorStatusWarningForeground1)'
    active: 'var(--colorStatusSuccessForeground1)'
    inactive: 'var(--colorNeutralForeground3)'
  freshness-indicator:
    fresh: 'var(--colorStatusSuccessForeground1)'
    stale: 'var(--colorStatusWarningForeground1)'
    degraded: 'var(--colorStatusInfoForeground1)'
  gdpr-destructive-button:
    background: 'var(--colorStatusDangerForeground1)'
    radius: '{rounded.md}'
---

# Parties UI — Design

> Visual identity for `parties-ui`: a single Blazor app with two role-gated areas
> (**Admin** and **Consumer self-service**) on the FrontComposer shell + FluentUI
> Blazor V5. This spine specifies only the **brand-layer delta**; the component
> library and shell own the rest. Paired with `EXPERIENCE.md`. **Spine wins on
> conflict with any mock.**

## Brand & Style

Parties UI is an **enterprise records tool wearing a calm face**. Admins manage
person and organization records all day; consumers visit rarely, often anxiously,
to see and control the personal data held about them. Both must read as **one
product** — trustworthy, plain, unmistakably Microsoft-Fluent — never two brands
bolted together.

The visual language is **inherited, not invented**. `parties-ui` takes the
FrontComposer shell and FluentUI V5 (Fluent 2) **wholesale** — its layout, accent
(`{colors.accent}` teal), neutral/brand/status palettes, Segoe UI type ramp,
radii, shadows, theme toggle (Light/Dark/System), and density switch. This
DESIGN.md changes only what the *brand discipline* justifies: a slightly larger
consumer body size, a roomier consumer density, and a handful of domain-specific
components (party-state badge, data-freshness indicator, GDPR destructive action,
and the modernized party picker). **If the brand can't justify overriding a
token, it doesn't override it** — Fluent 2's defaults are the contract.

## Colors

- **Accent — Teal (`{colors.accent}` = `#0097A7`)** is inherited from the shell
  (`FcShellOptions.AccentColor` → `ThemeSettings.accentBaseColor`). **Contrast
  caveat (load-bearing):** the *raw* accent base is **only 3.51:1 on white — it
  FAILS WCAG AA (4.5:1) for white text.** So the raw base is reserved for
  **non-text accents only**: the active-nav indicator stripe, selection tints,
  focus-adjacent chrome. **Filled primary buttons (white text) MUST bind to
  Fluent's derived `--colorBrandBackground`** (the Fluent 2 brand ramp produces an
  AA-safe darker shade, ≈`#00767f`), never the raw `{colors.accent}`. One primary
  action per view; never decorative, never a background wash.
- **Neutrals** inherit Fluent 2 entirely: `--colorNeutralBackground1..6` for
  surfaces (master list / detail aside / cards), `--colorNeutralForeground1..4`
  for text hierarchy, `--colorNeutralStroke1..3` for dividers and the
  master-detail split. The shell aliases the primary text as `--fc-color-neutral`.
- **Status colors carry domain meaning** — this is the one place color is
  semantically load-bearing, so it is fixed, not free:
  - **Erased / destructive** → `--colorStatusDangerForeground1` (party erased,
    erasure action, validation rejection).
  - **Restricted / caution** → `--colorStatusWarningForeground1` (processing
    restricted, stale-but-usable read).
  - **Active / success** → `--colorStatusSuccessForeground1` (active party,
    command accepted, fresh read).
  - **Processing / informational** → `--colorStatusInfoForeground1` (command
    accepted-but-projecting, degraded read on last-known cache).
- **Focus** inherits the Fluent 2 `--colorStrokeFocus2` 2px ring on every
  interactive element. Never removed.

Avoid: a separate consumer brand color, gradients, more than the one teal accent,
hand-picked hex for party/GDPR states (always map to the Fluent 2 status token so
light/dark and forced-colors stay correct).

## Typography

Type is **inherited Fluent 2 Segoe UI** (`--fontFamilyBase`) with one deliberate
delta. The **Admin** area uses the default body ramp `--fontSizeBase300` (14px) —
dense screens, scanning, grids. The **Consumer** area bumps body to
`{typography.body-consumer.fontSize}` (16px / `--fontSizeBase400`) at line-height
1.5: privacy copy is read slowly and often on a phone, so it gets air. Headings,
captions, labels, weights (`--fontWeight{Regular,Medium,Semibold,Bold}`), and the
app title (`app-title`, the shell's `Typography.AppTitle`) are inherited unchanged
in both areas. No second typeface. The serif/display flourish other brands reach
for is **explicitly banned** — enterprise trust reads as restraint.

## Layout & Spacing

Spacing inherits the Fluent 2 `--spacing{Horizontal,Vertical}*` ramp; no token
overrides. **Rhythm is set by FrontComposer density, not new spacing tokens:**
Admin runs at **comfortable** (`--fc-spacing-unit: 4px`) for information density;
Consumer runs at **roomy** (`6px`) for calm. Page measure inherits
`--fc-page-max-inline-size` (75rem), with logical properties (`margin-inline`,
`max-inline-size`) so RTL works for free.

Layout inherits the shell's `<FluentLayout>`: **Header 48px** (`--layout-header-height`),
**Navigation 220px expanded / 48px collapsed rail** (auto-suppressed to a hamburger
drawer at tablet/phone), **Content** at `Padding.All3`, **Footer**. The Admin
detail views use a **master–detail split** (list left, `<aside>` detail right);
Consumer views are **single-column, centered, narrow measure** for readability.

## Elevation & Depth

Inherited from Fluent 2 (`--shadow2 … --shadow64`): cards/flyouts/dialogs/menus
rise on the standard ramp. **Parties UI adds nothing here.** Hierarchy on the
admin master-detail is carried by **neutral background steps and strokes**
(`--colorNeutralBackground1/2`, `--colorNeutralStroke1`), not by inventing
elevation. Brand discipline: Fluent 2's shadows are correct.

## Shapes

Inherited Fluent 2 radii, mapped by surface: `{rounded.sm}` (2px) inputs/search,
`{rounded.md}` (4px) buttons and cards, `{rounded.lg}` (6px) panels and the
detail aside, `{rounded.xl}` (8px) dialogs and the Ctrl+K command palette.
`{rounded.full}` is reserved for the **party-state badge** and count badges only.
Crisp, low-radius corners read "system of record," not "consumer app" — which is
the intended enterprise posture even on the consumer side.

## Components

Parties UI uses these FluentUI V5 components **as-is, unchanged** — do not
customize them; the shell/library defaults are the contract:
`FluentLayout` · `FluentLayoutItem` · `FluentProviders` · `FluentNav` ·
`FluentNavItem` · `FluentNavCategory` · `FluentDataGrid` · `TemplateColumn` ·
`FluentTextInput` · `FluentSelect` · `FluentButton` · `FluentBadge` ·
`FluentMenu` · `FluentMenuButton` · `FluentStack` · `FluentText` · `FluentSpacer`.

Brand-layer / domain components (the only specified deltas) — rendered in
[`mockups/admin-parties.html`](mockups/admin-parties.html) and
[`mockups/consumer-privacy.html`](mockups/consumer-privacy.html):

- **Party-state badge** — pill (`{rounded.full}`) bound to the party lifecycle:
  `active` → `{components.party-state-badge.active}`, `inactive` →
  `{components.party-state-badge.inactive}`, `restricted` →
  `{components.party-state-badge.restricted}`, `erased` →
  `{components.party-state-badge.erased}`. Built on `FluentBadge`
  (`Appearance.Tint`); never a bespoke element. Text label always accompanies
  color (never color-only — accessibility). Use Fluent's matched
  `--colorStatus*Foreground1` **on** `--colorStatus*Background1` token *pairs*
  (AA-designed together); do not hand-mix a status foreground onto an arbitrary
  tint (warning-on-pale-tint lands ~4.44:1, marginal).
- **Data-freshness indicator** — a small status dot + label expressing the read
  model's `ProjectionFreshnessMetadata`: `fresh`
  (`{components.freshness-indicator.fresh}`), `stale`
  (`{components.freshness-indicator.stale}`, "as of HH:MM"), `degraded`
  (`{components.freshness-indicator.degraded}`, "showing last known"). Visual
  only; behavior lives in `EXPERIENCE.md.State Patterns`.
- **GDPR destructive button** — erasure and other irreversible actions use
  `FluentButton` filled with `{components.gdpr-destructive-button.background}`
  (`--colorStatusDangerForeground1`), `{rounded.md}`, and always pair with a
  confirmation step (behavior in EXPERIENCE.md). Restrict/withdraw use
  `ButtonAppearance.Outline`, not the danger fill.
- **Party picker** (`<hexalith-party-picker>`) — combobox, used as-is behaviorally
  but **must be re-skinned to Fluent 2 tokens**. It currently styles against legacy
  FAST tokens (`--neutral-stroke-rest`, `--accent-fill-rest`) that don't resolve in
  the V5 shell; map its `--hx-picker-*` vars onto `--colorNeutralStroke1`,
  `--colorNeutralBackground1`, `--colorNeutralForeground1`, `{colors.accent}`,
  `--colorStatusDangerForeground1`. (Tracked as design debt — see EXPERIENCE.md.)

## Do's and Don'ts

| Do | Don't |
|---|---|
| Inherit Fluent 2 + FrontComposer for everything outside the brand layer | Redeclare Fluent 2 custom properties in CSS (they're JS-emitted/immutable) |
| Set theme via `IThemeService` / design-token API | Hard-code colors or fork the accent off `{colors.accent}` |
| Map party/GDPR/freshness states to `--colorStatus*` tokens | Hand-pick hex for state colors (breaks dark mode + forced-colors) |
| Pair every state color with a text label | Communicate party state or erasure by color alone |
| Keep Admin at comfortable density, Consumer roomy | Build two visual brands; both areas are one Fluent family |
| Fill primary buttons with `{colors.brand-fill}` (`--colorBrandBackground`, AA-safe) | Put white text on the raw accent base `{colors.accent}` — it's 3.51:1, fails AA |
| Use the raw accent `{colors.accent}` for non-text accents only (nav stripe, tints) | Use accent decoratively, as a background, or more than one brand color |
| Re-skin the party picker onto Fluent 2 tokens | Ship the picker against legacy FAST `--*-fill-rest` tokens |
| Reserve the danger fill for irreversible GDPR actions | Use the danger color for ordinary buttons or emphasis |
