---
baseline_commit: 4a3b518
---

# Story 6.7: Shared portal display formatters (A10)

Status: ready-for-dev

## Story

As a maintainer,
I want pure display helpers shared without coupling portals to the UI host,
so that date and boolean formatting rules do not drift across Admin and Consumer surfaces.

## Acceptance Criteria

1. Given date and boolean display helpers are duplicated, when `PartyDisplayFormat` is introduced, then the helper lives in `Contracts` and contains no UI host, FluentUI, ASP.NET, localization-service, or portal-specific dependencies.
2. Given Admin and Consumer can have different density and copy needs, when callers adopt the helper, then Admin can preserve compact `"g"` date formatting and Consumer can preserve plain date formatting unless a separate product decision changes that behavior.
3. Given boolean text must be localized, when the helper formats booleans, then callers pass localized labels or a BCL-only label shape rather than hard-coded English strings.
4. Given portals are independent RCLs, when this story is implemented, then neither portal references `Hexalith.Parties.UI` to get formatting logic.
5. Given the consolidation is complete, when tests run, then culture-sensitive date formatting, localized boolean text, and absence of circular references are covered.

## Tasks / Subtasks

- [ ] Add `PartyDisplayFormat` in Contracts (AC: 1, 3, 4)
  - [ ] Keep the API pure and BCL-only.
  - [ ] Accept `CultureInfo`, format style, and localized labels as inputs where needed.
  - [ ] Avoid resource-manager, UI host, FluentUI, or portal dependencies.
- [ ] Replace duplicated portal helpers (AC: 1-4)
  - [ ] Update AdminPortal formatting call sites.
  - [ ] Update ConsumerPortal formatting call sites.
  - [ ] Preserve current visible formatting unless tests identify drift already approved by product.
- [ ] Add tests (AC: 2-5)
  - [ ] Cover Admin compact date style.
  - [ ] Cover Consumer plain date style.
  - [ ] Cover localized true/false labels.
  - [ ] Cover project-reference boundaries or packaging scans so portals do not reference the UI host.
- [ ] Validate (AC: 5)
  - [ ] Run `git diff --check`.
  - [ ] Run focused AdminPortal, ConsumerPortal, Contracts, and packaging tests.
  - [ ] Run solution build if available.

## Dev Notes

### Decision Context

- This story implements Class A item A10.
- The approved destination is `Contracts`, not the UI host, because portals cannot depend on the UI host without creating the wrong direction of reference.

### Guardrails

- This is formatting consolidation only. Do not rewrite page layout, regulated copy, consent semantics, export/erasure behavior, or localization resource ownership.
- Keep all user-facing strings resource-backed in portals. The shared helper should format supplied labels, not own product copy.
- Do not introduce a new localization framework or third-party formatting package.
- Do not create circular project references.

### References

- `_bmad-output/planning-artifacts/epics.md#Story-6.7-Shared-portal-display-formatters-A10`
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-06-28.md#Class-A--Internal-consolidation-approved--Epic-6`
- `_bmad-output/project-context.md#Code-Quality--Style-Rules`
- `src/Hexalith.Parties.AdminPortal/`
- `src/Hexalith.Parties.ConsumerPortal/`

