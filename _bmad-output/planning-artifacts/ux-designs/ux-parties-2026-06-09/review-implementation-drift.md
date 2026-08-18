# Implementation Drift Review — parties

> Reviewed 2026-08-18 against `DESIGN.md` + `EXPERIENCE.md` (final, 2026-06-09) and
> `.decision-log.md` residuals. Implementation state: Epics 4–8 built (AdminPortal,
> ConsumerPortal, GDPR flows, a11y gates, MainLayout rework). Review only — no code
> or spine changes were made. All paths relative to the repo root.

## Overall verdict

The **behavioral spine (EXPERIENCE.md) is implemented with high fidelity** — the
status-kind vocabulary, the polite/assertive live-region split, consent
`role=switch` + default-Off + lawful-basis honesty, the Admin typed-name erase
dialog, and the regulated erasure/export microcopy are all in code, several of them
near-verbatim and pinned by tests. The **visual spine (DESIGN.md) drifts materially
at the token layer**: both portals style domain states against the legacy FAST
`--*-rest` token family the spine explicitly bans (the exact debt the spine logged
for the picker — which, ironically, is the one place it has been fixed), the danger
fill carries hand-picked hex, and the per-area density promise (`--fc-spacing-unit`
4px/6px) was never wired. Reconciliation direction: **fix code** for the token
layer, IA reachability, and the consumer erasure pseudo-dialog; **update the spine**
for a handful of places where the code made the better call (danger-fill token
pair, inline status text instead of toasts, consumer erasure as
reversible-until-begins).

## Drift findings

### High

- **[high]** EXPERIENCE §Information Architecture promises nav-reachable consumer
  surfaces ("My consent — Nav → Consent", "My data & privacy — Nav → Privacy",
  "Edit my profile — My profile → Edit") vs
  `src/Hexalith.Parties.UI/Composition/PartiesUiFrontComposerRegistration.cs:42-53`,
  which registers only **two** nav entries ("Parties" → `/admin/parties`, "My
  space" → `/me`). `MyProfilePage.razor` renders **no Edit link**; the only inbound
  link to `/me/consent` in the whole app is
  `src/Hexalith.Parties.ConsumerPortal/Components/MyPrivacyPage.razor:94`, and
  `/me/privacy` and `/me/edit` have **no inbound link anywhere** (verified by
  repo-wide grep). The pages exist and work, but Marc cannot reach the Flow 1/2
  surfaces without typing a URL. *Reconcile:* **fix code** (nav entries for
  Consent/Privacy under the Consumer policy + an Edit action on My profile).

- **[high]** DESIGN front-matter ("Do NOT redeclare Fluent 2 custom properties…
  inherited Fluent 2 tokens"), §Components picker note and Do/Don't ("Don't ship…
  against legacy FAST `--*-fill-rest` tokens") vs both portals' scoped CSS, which
  styles **domain state colors and type against the legacy FAST V4 token family**
  the spine says does not resolve in the V5 shell:
  `src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor.css:52`
  (`outline: 2px solid var(--accent-fill-rest)` — **no fallback**, so the
  selected-row indicator computes to invalid and silently disappears), `:68-86`
  (badge states on `--success/--warning/--error-*-rest`), `:102-108` (freshness on
  `--success/--warning-foreground-rest`);
  `PartyGdprOperationsPanel.razor.css:17-31` (`--error-fill-rest`);
  `CreateEditPartyPage.razor.css:25,48-57`; consumer-side
  `MyProfilePage.razor.css:5,32,37,51-62,143-144`, `MyPrivacyPage.razor.css:5,32,83,120,133`,
  `ConsumerRouteShell.razor.css:5,18` (`--neutral-foreground-rest/-hint`,
  `--neutral-stroke-rest`, `--type-ramp-*`). The decision-log confined this debt to
  the **picker**; the portals re-introduced the pattern at scale while the picker
  itself was fixed. *Reconcile:* **fix code** — map to `--colorNeutral*`,
  `--colorStatus*Foreground1/Background1` pairs, `--fontSizeBase*`, per DESIGN.

- **[high]** EXPERIENCE §Interaction Primitives ("on **dialog** open trap focus, on
  close restore to the trigger") + §Inspiration ("all confirmation is in-app
  (`FluentDialog` + typed confirm)") vs
  `src/Hexalith.Parties.ConsumerPortal/Components/MyPrivacyPage.razor:119-136`: the
  consumer erasure confirmation is an **inline `<div role="dialog"
  aria-modal="true">`** — not a `FluentDialog`, with **no focus trap and no focus
  restore**, while claiming `aria-modal="true"` to AT (a semantics the page does
  not honor). The Admin side does this correctly
  (`PartyGdprOperationsPanel.razor:30-73` uses `FluentDialog Modal="true"`).
  *Reconcile:* **fix code** (FluentDialog or drop the false `role=dialog`/
  `aria-modal` and present it as an inline confirm section).

### Medium

- **[medium]** DESIGN §Colors "Avoid… hand-picked hex for party/GDPR states" and
  Do/Don't ("Hand-pick hex for state colors — breaks dark mode + forced-colors")
  vs `src/Hexalith.Parties.AdminPortal/Components/PartyGdprOperationsPanel.razor.css:18-30`:
  the admin danger fill's effective values are literal hex (`#b10e1c`, `#8f0b16`,
  `#6f0811`, `#fff`) because the primary vars are non-resolving FAST tokens.
  Separately, the host component `GdprDestructiveButton.razor.css:11-15` binds
  `--colorStatusDangerBackground3` + `--colorStatusDangerForegroundInverted` — a
  **matched, AA-designed pair** — instead of DESIGN's declared
  `components.gdpr-destructive-button.background: var(--colorStatusDangerForeground1)`
  (a foreground token used as a fill). *Reconcile:* **fix code** for the admin hex;
  **update spine** to adopt the Background3/ForegroundInverted pair the host
  component ships (it is the more correct Fluent 2 binding).

- **[medium]** DESIGN `components.freshness-indicator` declares three states
  (fresh=Success, stale=Warning, **degraded=Info**) vs every shipped indicator
  collapsing to two: `src/Hexalith.Parties.UI/Components/Shared/DataFreshnessIndicator.razor.css:14-22`
  (current=Success, everything-else=**Warning** — "Showing last known"/degraded
  gets Warning, not Info), duplicated at
  `ConsumerPortal/Components/MyProfilePage.razor.css:119-127` and
  `AdminPortal/Components/PartiesAdminPortal.razor.css:102-108`. Also the spine's
  stale "as of HH:MM" timestamp is rendered on **no real surface**: the host
  component supports `AsOf` (`DataFreshnessIndicator.razor:42-45`) but only the
  test specimen consumes it; consumer `FreshnessStatus.razor:17-23` and admin
  `PartiesAdminPortal.razor:1616-1640` show untimed messages. *Reconcile:*
  **decide** — either fix code to the 3-state Info mapping + as-of time, or
  simplify the spine to the shipped 2-state model.

- **[medium]** DESIGN §Layout & Spacing ("Rhythm is set by FrontComposer density…
  Admin comfortable `--fc-spacing-unit: 4px`, Consumer roomy `6px`) + EXPERIENCE
  §Foundation defaults vs the codebase: **zero references** to `--fc-spacing-unit`
  or any density configuration anywhere in `src/` (repo-wide grep). No code sets a
  per-area density posture; consumer pages instead hard-code their own rhythm
  (paddings + `font-size: 16px` in scoped CSS, e.g. `MyProfilePage.razor.css:3-7`).
  The 16px consumer body itself **is** honored. *Reconcile:* **decide** — wire the
  shell density per area, or rewrite DESIGN's spacing section to the shipped
  hard-coded posture.

- **[medium]** DESIGN §Components party-state-badge ("Built on `FluentBadge`
  (`Appearance.Tint`); **never a bespoke element**") vs
  `src/Hexalith.Parties.AdminPortal/Components/PartiesAdminPortal.razor:121-122,204-205`
  + `:1605-1614`: the Admin portal renders a **bespoke
  `<span class="hx-parties-admin__badge party-state-badge">`** with hand-rolled CSS
  (`PartiesAdminPortal.razor.css:56-86`). The compliant FluentBadge-based
  `PartyStateBadge.razor` exists in the host (`UI/Components/Shared/`) but is
  consumed **only by the accessibility specimen**. The Consumer profile shows
  lifecycle state as plain text (`MyProfilePage.razor:147-148`) with no badge at
  all (spine says badge is used in *Both* areas). Text labels always accompany
  state everywhere ✔. *Reconcile:* **fix code** (move the shared component to an
  RCL both portals can reference, or annotate the spine that RCL layering forces
  per-portal implementations).

- **[medium]** EXPERIENCE §State Patterns TenantUnavailable ('Copy: "Your workspace
  is still warming up — try again shortly," **not** "access denied"') vs
  `src/Hexalith.Parties.AdminPortal/Services/AdminPortalLabels.cs:153` — `"Tenant
  context is unavailable"` / `"Select a tenant to browse parties"`. Not an
  access-denied framing, but technical operator jargon ("tenant context"), not the
  reassuring warming-up copy the spine mandates for this exact state. *Reconcile:*
  **fix copy** (or update the spine if the terse Admin register is deliberately
  preferred here).

- **[medium]** EXPERIENCE §Component Patterns consent row + Voice & Tone ("For a
  legitimate-interest basis, **offer Object (Art. 21)**") vs
  `src/Hexalith.Parties.ConsumerPortal/Components/MyConsentPage.razor:107-110`: the
  Object button is rendered permanently **`Disabled="true"` with no handler** — a
  control that can never be activated. The spine's honesty stance (no dead
  controls implying agency) argues a rendered-but-inert Object button is worse
  than deferring the surface. *Reconcile:* **decide** — build the objection flow,
  or replace the dead button with copy + a support path until it exists.

- **[medium]** EXPERIENCE §Accessibility Floor is **product-wide** vs what the
  guard tests actually pin — a narrower floor:
  `tests/Hexalith.Parties.UI.Tests/AccessibilityStyleGuardTests.cs:9-12` scans only
  `src/Hexalith.Parties.UI/Components` (so the no-raw-hex / focus-suppression rules
  never see AdminPortal/ConsumerPortal/Picker CSS — which is exactly where the hex
  lives, `PartyGdprOperationsPanel.razor.css:18`);
  `SharedDomainComponentStyleTests.cs:7-15` covers just the three host shared
  components; and the axe pass runs only against the **synthetic specimen route**
  (`tests/e2e/specs/parties-accessibility.spec.ts:5` —
  `/__parties/specimens/accessibility`), never `/admin/parties*` or `/me*`.
  `MainLayoutAccessibilityTests.cs:41-86` pins the spine's shell floor (skip links
  first two tab stops → `#fc-main-content`/`#fc-nav`, one named nav + one main
  landmark) faithfully ✔, and the behavioral e2e specs do cover many spine state
  patterns on real routes (assertive validation, degraded-preserves-rows, empty +
  clear-filters, phone sheet + focus restore, switch default-Off). *Reconcile:*
  **fix tests** — extend the style-guard roots to all four UI projects and add axe
  passes on the real admin/consumer routes.

### Low

- **[low]** DESIGN badge radius `{rounded.full}` (pill; "`{rounded.full}` is
  reserved for the party-state badge") vs
  `src/Hexalith.Parties.UI/Components/Shared/PartyStateBadge.razor:5` using
  `BadgeShape.Rounded` (FluentUI's rounded-rectangle; the pill is
  `BadgeShape.Circular`). The Admin bespoke badge *does* use the 999px pill
  (`PartiesAdminPortal.razor.css:61`), so the two implementations also disagree
  with each other. *Reconcile:* fix code (`Circular`).

- **[low]** EXPERIENCE Flow 3 ("She clicks **Erase party** (danger fill)") vs
  `PartyGdprOperationsPanel.razor:78-82`: the erase **trigger** is
  `ButtonAppearance.Outline`; only the in-dialog Confirm carries the danger fill.
  *Reconcile:* decide (outline trigger + filled confirm is arguably the safer
  affordance; bless it in the spine or fill the trigger).

- **[low]** EXPERIENCE §Component Patterns GDPR action button ("Both… irreversible
  actions (erase) use… **typed confirmation**") vs consumer erasure
  (`MyPrivacyPage.razor:117-136`): single Confirm/Keep buttons, no typed input.
  The code reads the consumer *request* as reversible-until-begins (it is
  cancellable), and `docs/accessibility.md:17-18` codifies that reading; the spine
  text is ambiguous for the consumer case. *Reconcile:* **update spine** to state
  explicitly that the consumer erasure *request* is the reversible pattern (or
  mandate typed confirm there too).

- **[low]** EXPERIENCE "Command result **toast**" vs the implementation using
  inline `role=status`/`role=alert` text regions everywhere and **no toasts at
  all** (`PartiesAdminPortal.razor:20`, `MyConsentPage.razor:18,87`,
  `MyPrivacyPage.razor:19`). The behavioral contract (politeness split, no focus
  steal, never a blocking `alert()` — repo-wide grep confirms zero native dialogs)
  is fully honored; only the delivery vehicle differs. *Reconcile:* update spine
  wording ("status region" rather than "toast").

- **[low]** EXPERIENCE IA "Sign in — Shell — App entry" (+ `mockups/signin.html`)
  vs the implementation delegating sign-in entirely to the OIDC provider:
  `UI/Components/Routes.razor:15-18` challenges via `RedirectToChallenge`;
  Keycloak's hosted page is the sign-in surface. Role routing after sign-in
  matches the spine exactly (`RoleLandingRedirect.razor:30-48`, Admin/TenantOwner →
  `/admin`, bound Consumer → `/me`). *Reconcile:* update spine (external IdP page
  is the intended architecture).

- **[low]** EXPERIENCE Flow 1 describes an async export job whose download
  "appears when ready" vs `MyPrivacyPage.razor:378-424,426-458`: the export is
  prepared in a single awaited call and downloaded from a circuit-buffered payload
  via JS interop. The user-visible copy ("Preparing your export — … We'll show it
  here the moment it's ready", `ConsumerPortalResources.resx`
  `PrivacyExportPreparing`) and JSON machine-readability match the spine verbatim;
  only the job mechanics are simplified. *Reconcile:* update spine or leave —
  no user-visible contract broken (e2e even asserts the banned "under one minute"
  phrasing is absent, `consumer-portal-routes.spec.ts:59`).

## Spine-only (specified, not yet built)

- **Auto-refresh on stale reads + "aria-live announces when fresh"** (EXPERIENCE
  §State Patterns, Stale row): stale/degraded surfaces render static last-known
  data with the polite message, but nothing refreshes automatically. The live
  mechanism exists — SignalR projection stream, `OptimisticReconcile`, degraded
  fallback (`UI/Services/`, registered at `UI/Program.cs:110-118`) — but **no page
  consumes it** (the Program.cs comment says so explicitly). Backlog, not drift.
- **Object (Art. 21) behavior** (§Component Patterns / Voice & Tone): control
  rendered but inert (also listed as medium drift for the dead-control aspect).
- **Per-area density posture** (DESIGN §Layout & Spacing): no density wiring
  anywhere (also listed as medium drift because DESIGN states it as a token
  contract, not a future).
- **Stale "as of HH:MM" timestamps on real surfaces** (DESIGN
  freshness-indicator): supported by the host component, surfaced nowhere real.

## Code-only (built, never specified)

- `/no-party-binding` fail-closed surface for an unbound Consumer
  (`UI/Components/Account/NoPartyBinding.razor`; routed from
  `RoleLandingRedirect.razor:41`) — a good fail-closed state the spine never
  named; worth back-porting into EXPERIENCE §State Patterns.
- "No area assigned" landing state for a user with neither role
  (`RoleLandingRedirect.razor:14-21,44-48`).
- `/admin` compatibility landing redirect (`UI/Components/Areas/AdminLanding.razor:1-17`).
- Accessibility/picker specimen routes `/__parties/specimens/*`, config-gated to
  Dev/Test (`UI/Components/Specimens/PartiesAccessibilitySpecimenRoutes.cs:23-30`).
- Admin list **pagination** (`PartiesAdminPortal.razor:145-150`) and the
  display-name/email/identifier **search-mode buttons** with rich-search probe
  degradation (`:86-90`) — beyond the spine's "debounced search + type/active
  filters".
- EventStore-admin deep links from party detail and GDPR correlation links
  (`PartiesAdminPortal.razor:359`,
  `AdminPortal/Services/AdminPortalEventStoreAdminLinks.cs`).
- DPO operational summary panel
  (`AdminPortal/Components/DpoOperationalSummaryPanel.razor`) — the spine's GDPR
  surface lists erase/restrict/consent/export/records/verify only.
- Identity-binding provisioning machinery in the UI host
  (`UI/IdentityBinding/*`, `UI/Program.cs:106-108`) — host plumbing with no UX
  spine coverage (fine; it has no consumer-visible surface yet).
- E2E fixture authentication scheme + fixture endpoints baked into the host
  (`UI/Program.cs:54-73,221-224,228-240`) — test scaffolding.

## Known debt confirmed

- **Picker re-skin (FAST → Fluent 2) + combobox ARIA** — decision-log residual,
  carried in both spines: **RESOLVED in code.**
  `src/Hexalith.Parties.Picker/Components/PartyPicker.razor.css:2-8` now maps every
  `--hx-picker-*` var onto Fluent 2 tokens (`--colorNeutralStroke1`,
  `--colorNeutralBackground1/2`, `--colorNeutralForeground1/3`,
  `--colorStatusDangerForeground1`), and `PartyPicker.razor:11-21,57-67` carries
  the full WAI-ARIA combobox contract (`role=combobox`, `aria-expanded`,
  `aria-controls`, `aria-autocomplete=list`, `aria-activedescendant`,
  `role=listbox`/`option` + `aria-selected`), plus `forced-colors` and
  `prefers-reduced-motion` blocks (`PartyPicker.razor.css:138,157`). The spines'
  "design debt" annotations can be retired. One nit: the accent maps to
  `--colorBrandStroke1` with a `#0067b8` (Microsoft-blue) literal fallback rather
  than DESIGN's `{colors.accent}` teal — cosmetic, fallback-only.
- **Mock-only sub-24px/12px secondary text; real build floors 13–14px** —
  honored: consumer secondary text uses `--type-ramp-minus-1` = 14px equivalents
  (e.g. `MyProfilePage.razor.css:89`), and the danger action floors 44px
  (`PartyGdprOperationsPanel.razor.css:14`).
- **Admin typed-name kept in-memory (PII residual)** — honored:
  `PartyGdprOperationsPanel.razor:543-560` sends only `partyId`; the typed value
  lives in `_erasureTypedName`, is compared locally, and is cleared on
  close/cancel (`ClearErasureDialogState`, `:531-537`).
- **Phone-reflow mock for Admin master-detail deferred** — the *behavior* is now
  built and e2e-pinned: full-screen detail sheet + row-focus restore + zoom
  narrow-emulation (`PartiesAdminPortal.razor.css:117-126`,
  `tests/e2e/specs/admin-parties-list.spec.ts:500-543`). The deferred mock itself
  is moot.
