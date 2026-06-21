---
project_name: parties
created: 2026-06-21
last_updated: 2026-06-21
status: active
purpose: Tracked technical-debt items carried across the project. Each item is a
  conscious, non-blocking deferral with a clear decision owner and trigger — not a
  defect backlog. Promote an item to a story when its trigger fires.
---

# Technical-Debt Register — parties

This register tracks deliberate, non-blocking technical-debt items so they are not
lost in retrospectives or architecture footnotes. It is the durable home for the
"tracked tech-debt item" recommendations raised by readiness assessments and epic
retrospectives.

**Severity:** `low` (cosmetic / contained by a workaround) · `medium` (re-evaluate
before the named trigger) · `high` (address before next release).

**Status:** `open` · `mitigated` (a workaround is in place; the underlying decision
remains) · `accepted` (we will live with it) · `resolved` (closed out).

| ID | Title | Severity | Status | Decision owner | Trigger |
|----|-------|----------|--------|----------------|---------|
| [TD-1](#td-1) | RCL status/freshness primitive sharing boundary | low | mitigated | Architect (Winston) + Dev (Amelia) | A 3rd RCL needs the primitives, or drift appears between the AdminPortal/ConsumerPortal copies |

---

## TD-1 — RCL status/freshness primitive sharing boundary

**Severity:** low &nbsp;·&nbsp; **Status:** mitigated &nbsp;·&nbsp; **Owner:** Architect (Winston) + Dev (Amelia)
**Origin:** architecture.md Gap #4 (§ Validation Issues / discovered in Epic 1) · Epic 1 retro (2026-06-10)
**Surfaced as tracked item by:** `implementation-readiness-report-2026-06-21.md` § 4 residual #5 / Recommendation #3.

### What it is

The canonical `StatusKind` → UI mapping, the `DataFreshnessIndicator` freshness
primitives, and the shared domain components currently live in the `Hexalith.Parties.UI`
**host**, not in a neutral shared UI package. Before any RCL page consumes them, a
decision is required: **promote these primitives into a shared UI package** (e.g. a
`Hexalith.Parties.UI.Components` RCL) **or keep mapping them at the host composition
boundary**. The hard constraint either way: **an RCL must never reference the
`Hexalith.Parties.UI` host** to reach these types — that would invert the intended
package direction (`architecture.md` §"RCL status/freshness boundary").

### How it has been mitigated so far (no host references introduced)

| Epic | What was done | Retro verdict |
|------|---------------|---------------|
| 2 (AdminPortal) | AdminPortal kept **host-agnostic**; added **local** badge/freshness semantics plus drift-prevention tests. | "Completed for Epic 2, architecture follow-up remains" |
| 3 | Architecture follow-up logged: decide neutral package vs duplicated-with-drift-tests. | Open follow-up |
| 4 (ConsumerPortal) | ConsumerPortal used **RCL-local** display components reached only through **RCL-owned ports adapted by the UI host**; no neutral package introduced. | "In progress" |
| 5 | Same port/adapter pattern continued across consent/privacy surfaces; ConsumerPortal stayed independent of the UI host. | "Completed" (pattern), shared-package decision still open |

**Net effect:** both RCLs avoid referencing the host (the load-bearing constraint is
upheld) by **duplicating** the primitives locally behind ports/adapters, guarded by
drift tests. The package direction is safe; the cost is duplicated primitives that can
drift over time.

### Open decision

Promote the duplicated `StatusKind`/freshness primitives into a **neutral shared UI
package** (removes duplication and the drift risk) **or** formally **accept** the
host-mapped-plus-local-duplication pattern as permanent (drift tests are the backstop).

### Why it is not a blocker

The intended package boundary is already preserved everywhere, all RCLs ship and are
tested, and drift tests catch divergence. This is a structural cleanliness / future-reuse
decision, not a correctness or release issue.

### Trigger to act

Promote to a story when **a third RCL** needs these primitives, **or** when the
AdminPortal and ConsumerPortal copies are observed to **drift** (drift test fails or a
behavioural mismatch is reported). Until then: **mitigated, no action required.**
