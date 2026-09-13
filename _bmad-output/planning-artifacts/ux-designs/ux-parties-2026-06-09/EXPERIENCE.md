---
name: Parties UI
status: final
sources:
  - '{planning_artifacts}/../project-context.md'
  - '{planning_artifacts}/parties-ui-prd.md'   # FR-* / NFR ids cited below
  - '{planning_artifacts}/epics.md'            # UX-DR1..16 (frozen); supersessions stated in DESIGN.md
updated: 2026-09-08
---

# Parties UI — Experience Spine

> Behavioral contract for `parties-ui`: a single Blazor app, two role-gated areas
> (**Admin** records management + **Consumer** GDPR self-service), on the
> FrontComposer shell + FluentUI Blazor V5. Visual identity lives in `DESIGN.md`;
> this spine owns *how it works*. **Spine wins on conflict with any mock.**
> Requirement ids (`FR-*`, `UX-DR*`) are cited where a row or flow delivers one;
> where this spine deliberately supersedes a frozen `UX-DR`, `DESIGN.md.Components`
> says so.

## Foundation

Two role-gated areas — **Admin** (manage Person/Organization party records
within tenant scope) and **Consumer** (self-service over one's own personal data)
— share one sign-in, and **role decides the landing area** (`FR-Shell`). `Admin` /
`TenantOwner` → Admin; bound `Consumer` → Consumer. A principal holding **both**
roles lands in Admin (Admin-policy roles are checked first); a principal with
**neither** sees the fail-closed **No area** state. The UI system does most
of the work: the shell supplies layout, navigation, theme (Light/Dark/System),
density, command palette, and skip links; FluentUI V5 supplies the components.
Brand discipline is "respect the defaults except where `DESIGN.md` overrides them."

Density is one posture — resolved by the shell, chosen by the user (`DESIGN.md.Layout & Spacing`);
the module never sets it per area, route or element. Theme follows the OS until
the user chooses.

This product sits in front of an **event-sourced, CQRS, eventually-consistent**
backend (commands route through the EventStore gateway; reads come from replayed
projections carrying `ProjectionFreshnessMetadata`). That single fact shapes more
of this spine than anything visual — see **State Patterns**.

Two platform facts constrain this spine. **Authentication is delegated:** the sign-in
surface is the tenant IdP's hosted page (Keycloak, via OIDC challenge), not a
page this app renders; role routing on return is ours, so the
accessible-authentication floor (WCAG 3.3.8) binds the **IdP configuration** — see
Accessibility Floor. **Launch gate (`GATE-KMS`):** consumer surfaces invite real
data subjects to submit personal data; they go live only once the production-KMS
prerequisite is met — synthetic data only until then.

The Accessibility Floor and Component Patterns below are verified against the
FrontComposer shell version the code pins (`HexalithFrontComposerVersion`); a
shell upgrade re-runs the floor.

## Information Architecture

| Surface | Area | Reached from | Purpose | Delivers |
|---|---|---|---|---|
| **Sign in** | Shell | App entry (unauthenticated) — the IdP's hosted page, role routing on return | Authenticate; role routing to landing | `FR-Shell` |
| **No area** | Shell | Role routing when an authenticated principal holds neither Admin nor Consumer | Fail-closed landing: explain that no area is assigned, Help & contact path; no data access | `FR-Shell` |
| **No party binding** | Shell | Role routing when a Consumer's `party_id` claim resolves to zero/multiple parties (`/no-party-binding`) — **never** for an erased party (see State Patterns "Erased / Gone — Consumer") | Fail-closed landing: reassuring copy + support path; no data access | `FR-Shell` |
| **Parties list** | Admin | Nav → Parties (`/admin/parties`; `/admin` redirects here) | Search/filter Person + Organization records | `FR-Admin-1` |
| **Party detail** | Admin | List row (`/admin/parties/{id}`) | View one record; entry to edit + GDPR | `FR-Admin-2` |
| **Create / Edit party** | Admin | List toolbar (`/admin/parties/new`) / detail action (`/admin/parties/{id}/edit`) | Author a party (validated → command) | `FR-Admin-3` |
| **GDPR operations** | Admin | Detail → GDPR (`/admin/parties/{id}/gdpr`) | DPO duties under the Admin policy: erase · restrict · consent · export · processing records · verify | `FR-Admin-4` |
| **My profile** | Consumer | Consumer landing (`/me`) | View own personal data | `FR-Consumer-1` |
| **Edit my profile** | Consumer | My profile → Edit (`/me/edit`) | Correct/update own data | `FR-Consumer-2` |
| **My consent** | Consumer | Nav → Consent (`/me/consent`) | Grant / withdraw consent, plain-language | `FR-Consumer-3` |
| **My data & privacy** | Consumer | Nav → Privacy (`/me/privacy`) | Export my data · request erasure · see what's processed about me | `FR-Consumer-4` |
| **Help & contact** | Both | Persistent affordance, same place on every page (shell footer `FluentAnchor`) | Consistent help (WCAG 3.2.6): names the **controller** and the **DPO contact**, links the **privacy notice**, and states the reply window ("We'll reply within a month"); every failure state's "support path" points here | Art. 12(3), 13(1) |

Every consumer surface above must be **nav-reachable** (no URL-typing paths):
Consent and Privacy are `FrontComposerNavEntry`s under the Consumer policy; Edit
is an action on My profile; the nav label for `/me` is "My profile".

**Navigation model:** the shell's `<FluentNav>` auto-populates from registered
domain manifests, gated by `<AuthorizeView Policy=…>` — Admin nav entries never
render for a Consumer, and vice-versa. Rail and drawer geometry are the shell's
(not restated here). Modal depth ≤ 1 (a confirm dialog never stacks on a dialog).
The `<hexalith-party-picker>` is **not a top-level surface** — it is an inline
control reused inside Admin Create/Edit to link a related party.

**Page sections (Hexalith UX rule):** a page-like surface with two or more
sibling titled content sections groups them in one `FluentAccordion`, one
`FluentAccordionItem` per section (V5 parameter `Header=`; the V4 `Heading=` is
silently dropped), primary item expanded by default; title, breadcrumb, toolbar
and a single primary region stay outside. Per-surface map:

| Surface | Outside the accordion | Accordion items (default-expanded first) |
|---|---|---|
| GDPR operations | Title, party header, freshness, status region | **Erasure** · Restriction · Consent · Export (portability) · Processing records · Verification |
| Party detail | Title, party-state badge, freshness, Edit action | **Details** · GDPR (summary + link to operations) |
| My data & privacy | Title, freshness, the Export / Delete action pair | **Things you control** · Things we keep to run your account · What's processed about me |

Surfaces with one primary region (Parties list grid, Create/Edit form, My
profile, My consent) use no accordion.

**Document title and language:** every surface sets a `PageTitle` ("{Surface} —
Parties"; WCAG 2.4.2) and the page `lang` binds to the active culture, never a
hard-coded `en` (3.1.1). Heading hierarchy is one `h1` per route (the shell's
focusable route heading), sections at `h2`, never skipping a level.

→ Composition reference:
[`mockups/signin.html`](mockups/signin.html) (IdP hand-off + role routing) ·
[`mockups/admin-parties.html`](mockups/admin-parties.html) (Admin master-detail + GDPR) ·
[`mockups/create-edit-party.html`](mockups/create-edit-party.html) (Create/Edit + in-form picker) ·
[`mockups/consumer-profile.html`](mockups/consumer-profile.html) (My profile view/edit) ·
[`mockups/consumer-privacy.html`](mockups/consumer-privacy.html) (My data & privacy).
**Spine wins on conflict; mocks are illustrative and never ported.**

## Voice and Tone

Microcopy only — brand voice lives in `DESIGN.md.Brand & Style`. Two registers,
one product: **Admin is terse and precise** (operator language); **Consumer is
plain and reassuring** (no legalese, no jargon, no blame). The hard rule for
Consumer GDPR copy: **say what will happen in human words, then name the right.**
Copy is localized; every string below is the canonical English source
(`UX-DR13`, `UX-DR15`, `UX-DR16`).

| Do | Don't |
|---|---|
| "We've started deleting your data. We'll confirm when it's done — usually within 30 days." | "It'll be gone within 30 days." (a hard SLA that GDPR Art. 12(3) lets you extend — don't commit a finish time) |
| "You can cancel until deletion begins — we'll tell you here when that is. Once it's done, it's permanent — we can't undo it." | State only the cancel window and hide that completed deletion is irreversible; or say "until deletion begins" without ever saying when that is |
| Completed: "We've deleted your personal data and verified it's gone. We keep only what the law makes us keep — {legal records category} — and a record that this deletion happened." (Art. 17(3)) | "We've deleted your data and verified it's gone" when legal-obligation records, ciphertext or the deletion record remain |
| "Delete my data" / "Withdraw consent" / "Grant consent" (plain verbs) | "Erasure" / "Toggle data-processing authorization flag" |
| Split "Things you control" (consent) from "Things we keep to run your account" (contract / legal), each row with its **purpose** and **how long we keep it** (Art. 13(2)(a)) | One list with a toggle on every row, implying you can switch off a contract/legal basis; or a basis with no purpose or retention stated |
| For a legitimate-interest basis, name the right as its own line: "You can object to this use — contact us and we'll review it. We'll reply within a month." (Art. 21 is an *assessment*, not a toggle; Art. 21(4) requires it to be presented clearly and separately) | Show a withdraw toggle on a basis the user can't actually withdraw — or a rendered-but-disabled Object button, or copy implying objection instantly stops the processing, or the sentence as a smaller sub-line |
| "Processing of your data is currently restricted — you can still change your consent choices." | Hide that a restriction exists, or disable consent while restricted (Art. 18(3) keeps consent editable) |
| "Consent changes are paused while your data is being deleted." (during erasure) | "We couldn't change this — please try again" on a rejection that can never succeed during erasure |
| "Preparing your export — this can take a little while. We'll show it here the moment it's ready." (the one canonical string; `UX-DR15`; machine-readable JSON, scope + download validity stated beside it) | "Ready in a moment / under a minute" (over-promises an async job; no format stated); a second wording of the same string anywhere |
| Admin: "Erase party — irreversible. Type the name to confirm." + "Records the law requires us to keep are retained and listed in the verification report." | Admin: a bare "Are you sure?"; "across all records" (over-promises against retention) |
| "Showing what we last knew — refreshing…" (degraded read) · "As of 10:42" (stale read, localized, zone-qualified) | "Stale projection / cache miss" |
| "We couldn't reach the service. Nothing about your data has changed — try again." | "500 Internal Server Error"; "Your data is safe" (an implied security guarantee) |
| Erased self, re-signing in: "Your data was deleted on {date}. Reference {ref}. Nothing else is held." | "Your account isn't linked to a data record yet — contact us and we'll fix it" (bug-shaped, invites re-binding) |
| Rejection reasons are **human, localized and field-scoped** ("Email needs an @"), for consumers too | A code, an English-only string, or "Something went wrong" with no field |

## Component Patterns

Behavioral rules. Visual specs live in `DESIGN.md.Components` (or FluentUI V5
defaults, when inherited). The **V5 component** column is the mandatory
implementation surface — no hand-rolled equivalents.

| Component | V5 component | Use | Behavioral rules |
|---|---|---|---|
| **Parties data grid** | `FluentDataGrid` (+ `TemplateColumn`) | Admin list | Server-driven search (debounced) + type/active filters via `FluentSelect`; row → detail; never block render on staleness (show freshness, render last-known). When the focused row disappears (erased, filtered out), focus moves to the next row, else the previous, else the search field — never to `body`. |
| **Party picker** | `<hexalith-party-picker>` (custom element; written carve-out: `FluentAutocomplete`/`FluentCombobox` cannot host its state machine + `party-selected` contract) | Admin link-a-party | Implements the full **WAI-ARIA combobox pattern** (`UX-DR7`): input `role=combobox` + `aria-expanded` + `aria-controls`→listbox; listbox `role=listbox`, options `role=option` with `aria-selected` **and per-option `id`s**, active tracked by `aria-activedescendant` (not color alone). `aria-autocomplete=list`, 300ms debounce; selecting fires `party-selected` `{partyId, partyType, status}` (`composed:true`); honor its state machine (Idle/Loading/Ready/Empty/LocalOnly/Degraded/Unauthorized/Forbidden/TransientFailure/NotFound/Gone/Error). Result-state transitions **announce via an internal `role=status` region**, debounced to one announcement per settled result set, phrased as a short plain statement ("12 people match", "No matches", "Limited results") — never a quiet visual note alone. Clear affordance is a real button (≥24px); focus ring styled in the shadow root (see DESIGN.md). |
| **Party-state badge** | `FluentBadge` (`Appearance=Tint`, `Shape=Circular`, `Color` per state) — one shared component consumed by both portals | Both | Lifecycle (`active/inactive/restricted/erased`) — color **and** text label, never color alone (`UX-DR4`). An `erased` party shows tombstone copy, not data. |
| **Person / Organization chooser** | `FluentRadioGroup` | Admin Create | `radiogroup` semantics (`UX-DR9`); **create-only**, **no default selection**, arrow keys move, validation names it if unset; on Edit it renders read-only text (type never changes after creation). |
| **Consent control** | `FluentSwitch` (its own `Label`; `CheckedMessage`/`UncheckedMessage` carry the state line) | Consumer | Real `role=switch` + `aria-checked`; the visible label **is the switch's own label**, purpose text + retention tied via `aria-describedby` — never a styled `<div>`. Row height ≥44px on touch. **Defaults Off; never pre-checked** (GDPR Art. 7). Every row states its **purpose** in one plain sentence. Optimistic: flip + announce "Saving…" via `aria-live=polite` — **do not steal focus on save**; reconcile on projection confirm; on rejection revert + inline, field-scoped reason. Only **consent-based** items get a toggle; contract/legitimate-interest data is read-only. Toggles **stay enabled while processing is restricted** (Art. 18(3)); **while erasure is in progress they render disabled** with "Consent changes are paused while your data is being deleted." **Object (Art. 21) is blocked on the backend:** until an objection command exists, render the contact path (see Voice and Tone) — never a disabled button. Admin-recorded consent captures **evidence of consent** (source + date; Art. 7(1)). |
| **GDPR action button** | `FluentButton`; the danger fill is one shared `GdprDestructiveButton` (DESIGN.md) | Both | The danger fill lives on the **in-dialog Confirm** only; the on-page trigger is `ButtonAppearance.Outline` (safer affordance) — **everywhere, Flow 3 included**. **Admin erase** requires **typed confirmation** (the person's name) in a real labeled input — irreversible from acceptance; matching is **trim + case-insensitive + Unicode NFKC**, the expected string is shown, paste is allowed; Confirm is `aria-disabled` until it matches (transition announced). **Consumer "Delete my data" is a request, reversible until deletion begins**: a real `FluentDialog` (trap/restore) with explicit consequence copy, the "What's being deleted" list including any **"Kept — legal requirement"** rows, and a single Confirm — **no typed transcription** (cognitive floor). Reversible actions (restrict, lift, withdraw, cancel) use outline + single confirm. **Lift restriction** adds a required attestation checkbox — "Before lifting, the person must be told. Confirm you have informed them." (Art. 18(3)). **Restrict** takes its reason from a **bounded picklist of the four Art. 18(1) grounds**, never free text (no special-category leak into immutable events). Never auto-fire on focus/blur. |
| **Freshness indicator** | `FluentBadge` (`Tint`, `Circular`, `Color` = `{components.freshness-indicator.*}`) + `FluentText` | Both | Surfaces `ProjectionFreshnessMetadata` — **three states** (`UX-DR5`, renamed): **fresh** ("Up to date") · **stale** (age known — **always "As of HH:MM"**, localized + zone-qualified, on real surfaces, not just specimens) · **degraded** (`StatusKind.Degraded`, last-known-cache fallback — "Showing what we last knew — refreshing…", Informative — never Warning). Icon/dot + word (never color alone). The text node is a `role=status` region; transitions announce via `aria-live=polite` **on change only**. |
| **Command result status region** | `FluentMessageBar` (`Intent.Info`/`Success`/`Warning` → polite; `Intent.Error` → `role=alert`) | Both | Inline `role=status aria-live=polite` region — not a floating toast — "Saved — updating…". **Validation rejection / failure → `role=alert` (assertive)** with the reason — not polite. An *erasure* acknowledgement uses `Intent.Info`, **never success-green** (deleting data isn't a "success" celebration). Never a blocking `alert()`/`confirm()`. |
| **Banner** | `FluentMessageBar` (persistent, `Intent` by state) | Both | Restricted / erasure-in-progress / tenant-warming banners; one banner per view; never overlays the focused element. |
| **Help & contact** | Shell footer `FluentAnchor` | Both | Same position on every page (3.2.6); content per IA row. |

## State Patterns

The defining section. The backend is **eventually consistent and at-least-once**;
the UI must make that legible without alarming anyone. States map to the real
`StatusKind` / `PartyPickerSearchState` enums already in the codebase, and each
row names the FrontComposer primitive that already renders it (map to it; do not
re-implement routing).

| State | Surface | Treatment |
|---|---|---|
| **Cold load** | All (`FcProjectionLoadingSkeleton`) | `FluentSkeleton` grid/panel skeleton; no spinner-only screens; shimmer respects reduced motion. |
| **Empty** (`NoData`) | List, search, consumer My consent (`FcProjectionEmptyPlaceholder`) | "No parties match." + clear-filters action; never a dead end. Consumer with zero defined consent purposes: "Nothing here needs a decision right now." |
| **Display-name-only** (`DisplayNameOnly`) | Detail (`FcAggregateDetailPage`) | Partial projection: show the name, mark the rest "still loading," and don't imply the record is empty. |
| **Accepted-but-processing** | After any command | Optimistic echo + freshness **degraded** (Informative) + status region "Saved — updating…". The view the user just acted on reflects their change immediately; the projection reconciles silently. **This is the core eventual-consistency UX.** |
| **Stale read** (age known) | Any read | Freshness **stale** (Warning) with "As of HH:MM"; **render the last-known data, never throw, never blank the screen.** Auto-refresh via the shipped projection SignalR mechanism; `aria-live` announces once when fresh. |
| **Degraded read** (`StatusKind.Degraded`, last-known-cache fallback) | Any read | Freshness **degraded** (Informative): "Showing what we last knew — refreshing…" using `{components.freshness-indicator.degraded}`; same render-last-known rule. **Export is disabled while degraded** (an Art. 15/20 response must not come from a stale projection); the export file carries the read's freshness. |
| **Editing over a stale/degraded read** | Create/Edit, Edit my profile | The form loads and stays editable; the freshness indicator stays visible above the form; on Save the backend's concurrency rejection (if any) arrives as a field-free `role=alert` with "This record changed while you were editing — review and save again", input preserved. |
| **Validation rejected** (`Validation`) | Create/Edit, consent | Inline field error from the `PartyCommandValidationRejected` event (not an exception); **announced via `role=alert` (assertive)**, the alert links to the first invalid field, each field carries `aria-invalid` + `aria-describedby`; preserve the user's input; offer retry. |
| **Erasure requested / in progress** | Consumer privacy, Admin GDPR | Two honest states: **(a) cancellable** until deletion begins — the surface **states when that is** (the grace period is a product value, see Open Items); **(b) permanent** once complete — worded as **verified** deletion with the retention clause (Voice and Tone; no certificate internals). `Intent.Info`, never success-green. Don't present the 30-day figure as the cancel window. At request time show a **PII-free request reference** ("keep this number"; one format shared with Admin's `#SR-…`, never encoding the ground); the status page confirms completion for the rest of the session. While in progress: consent toggles disabled ("Consent changes are paused while your data is being deleted."); a late **Cancel** on a stale view surfaces the backend's reason verbatim — "Deletion has already begun and cannot be cancelled." — via `role=alert`, never a generic or hedged retry. |
| **Erasure verification** | Admin GDPR (Verification item) | Three states: **pending** (Informative, "Verifying across projections…"), **verified** (Success, per-projection list + retained-records list), **failed** (`role=alert`, which projection, Retry). Flow 3's climax depends on this row. |
| **Restricted** (Art. 18) | Party detail, Consumer profile/privacy | `restricted` badge + `FluentMessageBar` banner. Consumer copy: "Processing of your data is currently restricted — you can still change your consent choices." **Consent controls stay enabled** (Art. 18(3)); admin lifecycle edits gray out per policy; consent still rejects during erasure (that guard wins). The privacy surface names how to *request* restriction ("You can ask us to restrict processing — contact us. We'll reply within a month."). |
| **No area** | Shell | Fail-closed: authenticated, neither role. "Your account has no area assigned yet." + Help & contact; no data access, no retry loop. |
| **No party binding** | Shell (`/no-party-binding`) | Fail-closed: a Consumer's `party_id` claim resolved to zero/multiple parties — **and the party is not erased**. Reassuring, no-blame copy ("Your account isn't linked to a data record yet — contact us and we'll fix it"), Help & contact path, no data access, no retry loop. |
| **Export preparing / ready** | Consumer privacy | Preparing: polite status with the canonical string. Ready: **polite announcement + a focusable "Download your export" control** — never a silent download appearing (4.1.3); beside it the scope ("everything we hold about you, as JSON") and how long the download stays valid. |
| **Transient failure** (`TransientFailure`) | Any | "We couldn't reach the service. Nothing about your data has changed — try again." + Retry; exponential backoff; keep prior content visible. |
| **Load failure** (`LoadFailure`) | Any | Non-transient: explain + Retry + support path; never a raw stack/500. |
| **Sign-in required** (`SignInRequired`) | Any | Route to the IdP challenge, preserve return URL. |
| **Tenant unavailable** (`TenantUnavailable`) | Admin | Tenancy is **fail-closed + eventually consistent**: after a restart it denies `UnknownTenant` until tenant events replay. Copy: "Your workspace is still warming up — try again shortly," **not** "access denied," and never "your access is fine" (it may not be). |
| **Admin required / Forbidden** (`AdminRequired`/`Forbidden`) | Admin | Explain the role needed; never expose record data; offer the Consumer area if applicable. |
| **Erased / Gone — Admin** (`Gone`/`NotFound`) | Detail, picker | Tombstone: "This party was erased." No personal fields, no PII in the message; links resolve gracefully (subscribers clean up dangling refs). |
| **Erased / Gone — Consumer** | My profile (`/me`), any `/me/*` | An erased subject who signs in again (deletion outlives any session) lands on the **PII-free tombstone** (`FR-Consumer-1`; code: `MyProfileDeletedTitle`): "Your data was deleted on {date}. Reference {ref}. Nothing else is held." + Help & contact. **Never** `/no-party-binding`. Requires the backend to distinguish "claim → erased party" from "claim → no party" (Open Items). |

## Interaction Primitives

- **Pointer + keyboard parity** everywhere; **touch** on tablet/phone (≥44px targets).
- **Inherited global keys** (shell): `Ctrl+K` command palette, `Ctrl+,` settings
  (density lives here — user-owned), skip links (to content, to nav) — their
  exact tab position after route focus is the shell's contract, verified per shell
  version, not asserted here.
- **Grid:** arrow-key row navigation, `Enter` opens detail; type-ahead into the
  search field applies only while focus is inside the grid (2.1.4: single-key
  shortcuts are scoped, never global).
- **Picker (combobox):** `↓/↑` move options (drives `aria-activedescendant`),
  `Enter` selects, `Esc` closes, `Backspace` on empty clears selection.
- **Forms:** `Enter` submits single-field steps; explicit Save button for
  multi-field. **Admin irreversible actions require typed confirmation** in a
  real input — `Enter` alone never confirms; consumer requests and all reversible
  actions use a single confirm.
- **Dialogs:** on open trap focus and place **initial focus on the safe
  control** — Cancel, or the typed-confirmation input — **never on the destructive
  Confirm** (2.4.3, 3.3.4); on close restore to the trigger; `Esc` closes any
  `FluentDialog` (`PreventDismissOnEscape` stays false, the erase dialog
  included). The tablet **detail sheet is a modal `FluentDialog`** (V5 ships no
  drawer; trap, `Esc` closes, restore to the originating grid row); the phone
  full-screen detail is a **page** (`FcAggregateDetailPage`: focus to the heading
  on open; Back restores the row).
- **Focus on results:** on **blocking** errors move focus to the alert. For
  **non-blocking, optimistic** results (consent save, "Saved — updating…")
  **announce via `aria-live` only — do not steal focus** (it's disruptive when it
  fires on every quiet save). Pattern already in AdminPortal — extend it, don't
  over-apply it.
- **One voice per transition (4.1.3):** a single polite announcement per state
  change per view — the status region speaks for a command result, the freshness
  region for a read transition; a banner never re-announces what a region just
  said.
- **Focus is never obscured (2.4.11):** focusable list/grid items carry
  `scroll-margin-top` ≥ the sticky header + pinned-row height; status regions and
  banners never overlay the element that holds focus.
- **Reduced motion inventory:** skeleton shimmer, switch travel, dialog/sheet
  entrance, smooth scroll-into-view, freshness transitions — all reduce to
  instant under `prefers-reduced-motion`.
- **Banned:** native `alert()`/`confirm()`/`prompt()` dialogs (they block the
  Blazor event loop — use `FluentDialog`/status regions); color-only state;
  auto-submit of destructive actions on blur; spinner-only screens; throwing on
  stale reads; global single-key shortcuts.

## Accessibility Floor

Behavioral. Visual contrast lives in `DESIGN.md` (including the computed-pairs
acceptance table). Target: **WCAG 2.2 AA** (consumer-facing; `NFR7`). The gate is
axe with the **`wcag22aa`** tag on **real routes** (not only the specimen), run in
**both color schemes** (`colorScheme: 'dark'` as a second pass), plus the
Fluent-conformance test that `DESIGN.md.Do's and Don'ts` (Enforcement) mandates.

- **Live regions exist before they speak (4.1.3):** each surface mounts its
  `role=status` and `role=alert` regions **empty at first render**; only their
  text content changes; regions are never conditionally rendered (the shipped
  `StatusLiveRegion` currently renders node and first message together; bUnit
  `StatusLiveRegionTests` asserts the required empty mount).
- **Announcement politeness is split (4.1.3, `UX-DR8`):** status + freshness +
  "Saved — updating…" use `role="status" aria-live="polite"`; **validation
  rejections, failures, and other errors use `role="alert"` (assertive)** — never
  silently polite. Extend the existing AdminPortal `aria-live` pattern to the
  Consumer area and the freshness indicator.
- **Real semantics, not styled divs (4.1.2, `UX-DR9`):** the consent control is
  `role=switch` + `aria-checked`; the Person/Organization chooser is a
  `radiogroup`; the picker uses the combobox roles (see Component Patterns); the
  typed-erase confirm is a labeled `<input>`. No interactive `<div>`s.
- **Accessible authentication (3.3.8):** sign-in is delegated to the IdP's
  hosted page — the IdP configuration must not block paste or password managers,
  must carry `autocomplete="username"` / `"current-password"` tokens, and must
  offer a non-cognitive-test path (SSO / passkey / email link qualify). This is a
  **requirement on the chosen IdP configuration**; its verification artifact is a
  line item in the deployment-security checklist, signed per deployment — a
  consumer who cannot clear sign-in can exercise no GDPR right.
- **Rejections are understandable (3.3.1, 3.3.3):** every rejection reason
  reaching a person is human-readable, localized and field-scoped; the consumer
  surfaces receive a reason, not a generic retry.
- Full keyboard operability; visible `--colorStrokeFocus2` ring never
  suppressed; logical focus order; skip links functional; focus moved only where
  it helps (see Interaction Primitives — not on every optimistic save).
- Support **forced-colors / high-contrast** (`@media (forced-colors: active)`)
  and **`prefers-reduced-motion`** **product-wide** (`UX-DR12`; shipped in the
  shell layout stylesheet and the picker — the portals' own selection and focus
  styles must join it). Selected/active states carry a **forced-colors-surviving
  indicator** (real border/outline + `aria-current`/`aria-selected`) — never
  background tint or box-shadow alone.
- **Consistent help (3.2.6):** the Help & contact affordance sits in the same
  place on every page in both areas (see IA); every failure state's "support
  path" points at it.
- **Titles and language:** every route sets `PageTitle` (2.4.2); the page `lang`
  binds to the active culture (3.1.1).
- State is never communicated by color alone (party-state badge always carries a
  text label; freshness indicator always carries a word).
- GDPR consent and erasure controls are reachable, labeled (`aria-describedby`
  ties the purpose / lawful-basis / retention text to the control), and
  confirmable by keyboard.
- Interactive targets meet **2.5.8** (≥24px; aim ≥44px on touch), gated by the
  `wcag22aa` axe tag; consumer body type at 16px (`DESIGN.md` `body-consumer`).

## Responsive & Platform

| Breakpoint | Behavior |
|---|---|
| **Desktop (≥1024px)** | Admin master-detail side-by-side; shell sidebar nav; Consumer single centered column. |
| **Tablet (640–1023px)** | Nav collapses to the shell's hamburger drawer; Admin detail becomes a modal `FluentDialog` sheet over the list; the shell forces **Comfortable** density for 44px touch targets. |
| **Phone (<640px)** | Single column; Admin list → tap → full-screen detail page (back returns to list); Consumer is the primary mobile experience (privacy/consent designed phone-first). |

Admin is **desktop-first** (data-dense, used at a desk) but degrades cleanly;
Consumer is **mobile-friendly first** (people check their privacy on a phone,
often after a prompting email). One responsive codebase, one shell-resolved
density posture, two copy registers.

## Inspiration & Anti-patterns

- **Lifted from FrontComposer + Fluent 2:** the entire surface vocabulary — layout,
  nav, theme/density, command palette, skip links, components, and the state
  primitives (`FcAggregateDetailPage`, `FcProjectionLoadingSkeleton`,
  `FcProjectionEmptyPlaceholder`, `FcPageLayout Mode=Constrained`). Parties UI's
  contribution is *what it adds for the party + GDPR domain*, not a new design
  system. Deliberate posture, not a shortcut.
- **Lifted from the existing AdminPortal:** the `StatusKind` state machine,
  `aria-live` status regions, and explicit focus management — these are good; make
  them product-wide (especially into the new Consumer area).
- **Rejected — legalese to consumers:** GDPR rights are stated in plain language;
  policy detail is one click away (Help & contact links the privacy notice), never
  an inline wall of text.
- **Rejected — consent dark patterns:** no pre-ticked consent (Art. 7), no
  confirm-shaming, no asymmetry making "keep my data / stay opted-in" easier than
  "withdraw / delete." Withdraw is as easy as grant; delete is as findable as keep;
  the two halves of every consent/erasure control pair get the same visual weight
  (`DESIGN.md` symmetry rule).
- **Rejected — blocking/alarming on eventual consistency:** stale and degraded
  reads render the last-known value with a quiet cue; we never blank the screen,
  throw, or shout "stale."
- **Rejected — a separate consumer brand:** one Fluent family, differentiated by
  copy register (and the 16px consumer body), not by a second visual identity or a
  second density posture.
- **Rejected — native modal dialogs** (`alert`/`confirm`): they freeze the Blazor
  event loop; all confirmation is in-app (`FluentDialog`; typed confirm for Admin
  irreversible actions only).
- **Rejected — dead controls implying agency:** a rights affordance is never
  rendered permanently disabled. If the backend can't deliver the right yet
  (Object, Art. 21 today), offer the honest contact path instead — and never
  fake dialog semantics (`role=dialog`/`aria-modal`) on an element that doesn't
  trap and restore focus.
- **Rejected — over-promising a right:** "all gone" when the law keeps some;
  "your data is safe" as a security claim; a cancel window with no date.

## Key Flows

_Visual reference: Flows 1–2 and 5 → [`mockups/consumer-privacy.html`](mockups/consumer-privacy.html) + [`mockups/consumer-profile.html`](mockups/consumer-profile.html); Flow 3 → [`mockups/admin-parties.html`](mockups/admin-parties.html); Flow 4 → [`mockups/create-edit-party.html`](mockups/create-edit-party.html). Entry: [`mockups/signin.html`](mockups/signin.html). Spine wins._

### Flow 1 — Marc checks what's held about him (Marc, customer, Sunday 9pm, on his phone) — `FR-Consumer-1`, `FR-Consumer-4`

1. Marc gets an email: "Review your data." He taps the link; the IdP's hosted
   page signs him in on his phone.
2. Role routes him to the **Consumer** landing. The app opens **My profile** at 16px
   body — calm, readable. His name and details render; the freshness badge reads
   "Up to date."
3. He taps **My data & privacy** in the nav. Two honest groups: **Things you
   control** (consent, off by default) and **Things we keep to run your account**
   (contract / legitimate interest, read-only, each with its purpose and how long
   it's kept, and the object-to-this-use contact path as its own line — Art. 21,
   see Voice and Tone), plus two actions — *Export my data*, *Delete my data*.
4. He taps **Export my data**. A status region reads the canonical string:
   "Preparing your export — this can take a little while. We'll show it here the
   moment it's ready." Beside it: "Everything we hold about you, as a JSON file."
   The portability job runs server-side.
5. **Climax:** the export is ready — announced politely, with a focusable
   **Download your export** control — *his own data, in his hands, in a portable
   file he can keep or move elsewhere, without a ticket or a call to support*.
   The right stopped being abstract.

Failure: export service unreachable → `TransientFailure` status "We couldn't
build your export just now. Nothing about your data has changed — try again."
Retry with backoff; the request is not lost. Degraded read → Export disabled with
"We're refreshing your data first — try again in a moment."

### Flow 2 — Marc withdraws a consent (Marc, customer, two minutes later) — `FR-Consumer-3`

1. From privacy, Marc taps **My consent**. Each consent shows its purpose in plain
   words and a switch whose label is the purpose name.
2. He flips "Marketing emails" **off**. The switch flips immediately and reads
   "Saving…" (optimistic) — the command is on its way through the gateway.
3. The projection confirms; the switch settles to "Off — you won't get marketing
   emails," and the freshness badge returns to "Up to date." One `aria-live`
   announcement carries the change for his screen reader.
4. **Climax:** Marc didn't wait, didn't reload, didn't doubt it took. The system
   *looked* done the instant he acted, and *was* done a breath later — eventual
   consistency made invisible. He closes the tab.

Failure: command rejected (`Validation`) → switch reverts to On, inline
field-scoped reason from the backend, input/intent preserved.

### Flow 3 — Priya fulfills an erasure request (Priya, data steward acting under the Admin policy, Tuesday 10:40am) — `FR-Admin-4`

1. A subject erasure request lands. Priya opens **Parties** in the Admin area at a
   desk, desktop master-detail.
2. She searches the name; the grid filters as she types. The matching person row
   shows an `active` state badge. She opens the detail.
3. She clicks **GDPR**, landing on `/admin/parties/{id}/gdpr` — one accordion:
   Erasure (expanded), Restriction, Consent, Export, Processing records,
   Verification.
4. She clicks **Erase party** (an **outline** trigger). A `FluentDialog` opens
   with focus on Cancel; it names the records the law keeps, and requires her to
   **type the person's name** to confirm — spaces and capitals don't matter; the
   danger-filled Confirm enables only once it matches. No accidental erasure.
5. The command is accepted; the detail shows the party-state badge flip toward
   `erased` with "Saved — updating…", freshness **degraded** while the
   crypto-shred propagates; the Verification item reads "Verifying across
   projections…".
6. **Climax:** minutes later the Verification item turns **verified** — the
   record confirmed shredded across projections, the retained legal records
   listed — *she can prove the right was honored*, not just assert it. Audit
   closed under request `#SR-4471`.

Failure: tenant projection still warming after a restart → `TenantUnavailable`,
copy "Your workspace is still warming up — try again shortly," never a hard denial
that looks like a permissions bug. Verification **failed** → `role=alert` naming
the projection + Retry.

### Flow 4 — Priya links a contact to an organization (Priya, onboarding a new org, Wednesday) — `FR-Admin-3`

1. Priya creates an Organization party (type chosen once in the `FluentRadioGroup`
   at creation), then needs to attach its primary contact Person.
2. In the Edit form she focuses the **party picker**; she types "acme cfo." After
   300ms it queries and shows matching people (`Ready`): "3 people match."
3. She arrows down, presses `Enter`. The picker emits `party-selected`
   `{partyId, partyType:"Person", status:"active"}`; the form binds the link.
4. **Climax:** the relationship is captured inline without leaving the form or
   memorizing an id — the picker turned "find the right person among thousands"
   into three keystrokes and an Enter.

Failure: search backend degraded → picker enters `Degraded`/`LocalOnly`, shows
last-known matches with "Limited results" announced once via the picker's
`role=status` region; selection still works, nothing blocks.

### Flow 5 — Marc deletes his data (Marc, customer, three weeks later, phone) — `FR-Consumer-4`

1. Back in **My data & privacy**, Marc taps **Delete my data** (outline).
2. A real dialog (focus trapped on "Keep my data", `Esc` closes) states the
   consequence in two honest halves: deletion starts on a stated date and is
   **cancellable until then**; once done it is **permanent**. Under "What's being
   deleted", *Legal records* reads **"Kept — legal requirement."** One Confirm —
   no typed transcription for a consumer.
3. He confirms. The page shows the `Intent.Info` (never success-green) state —
   "We've started deleting your data… You can cancel until deletion begins — {date}"
   — plus a **PII-free request reference** ("keep this number"). His consent
   switches gray out: "Consent changes are paused while your data is being
   deleted."
4. Next morning, second thoughts. He returns and taps **Cancel deletion** —
   inside the window, the request cancels and his profile is intact. (Had
   deletion already begun, he'd see the honest inline reason: "Deletion has
   already begun and cannot be cancelled.")
5. He re-requests a week later and lets it run. A month on, he signs in again to
   check. **Climax:** his `party_id` resolves to an erased party, and instead of
   a bug-shaped "not linked" page he reads "Your data was deleted on {date}.
   Reference {ref}. Nothing else is held." — and, from the completion notice,
   that only the legally required records remain. *Verified permanence, stated
   plainly and honestly to the person it matters most to.* The right was
   exercised start to finish without a ticket.

Failure: cancel raced past the window on a stale view → the specific rejection
reason above via `role=alert`, never a generic "try again."

_Restriction (Art. 18) and Edit-my-profile are deliberately **table-covered**
(State Patterns / Component Patterns) — simple enough not to need a walked flow._

## Open Items (routed, not invented)

Decisions this spine depends on but does not own; each is logged in `.memlog.md`
as an assumption and routed to `bmad-architecture` / the PRD:

- **Erased-self lookup** — the backend must distinguish "claim → erased party"
  from "claim → no party" (for example, a tombstone keyed by hashed claim) so the Consumer
  tombstone, not `/no-party-binding`, renders.
- **Erasure status contract** — exposes retained categories + legal reason, a
  PII-free request reference, and the rejection reason (`ConsumerPrivacyErasureResult`).
- **Cancel grace period** — the surface states the date deletion begins; the
  period's value is a product decision (`[ASSUMPTION]` not set here).
- **`LiftRestriction`** carries `SubjectInformedAt` + channel; **`RestrictProcessing`**
  reason becomes a bounded enum of the Art. 18(1) grounds.
- **Art. 19** recipient notification and the **Art. 22** stance are PRD items.
- **Objection (Art. 21)** command — until it exists the contact path stands.
- **Dark-scheme link contrast** — a platform item for FrontComposer / Fluent:
  `--colorBrandForegroundLink` computes to 3.90:1 in the dark scheme for any teal
  seed (`DESIGN.md.Colors` table); until the shell exposes a passing dark link
  token (`--colorBrandForeground2` is 4.70:1), dark-mode links are a **recorded
  AA gap** — never patched in module CSS.
