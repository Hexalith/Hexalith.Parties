---
project_name: parties
user_name: Administrator
date: 2026-06-21
scope_classification: Minor
status: implemented
supersedes: []
relates_to:
  - sprint-change-proposal-2026-06-21-planning-artifact-deploy-alignment.md
  - sprint-change-proposal-2026-06-12-eventstore-admin-ui-keycloak-https.md
  - implementation-readiness-report-2026-06-21.md
---

# Sprint Change Proposal — Deferred-work reconciliation (readiness recs #2 & #3 + proposal status hygiene)

## 1. Issue Summary

A `correct-course` run was invoked with the directive **"implement any deferred work or
pending proposals."** A sweep of the planning + implementation artifacts established that
the project is **fully implemented** — all 30 story specs are `done`, all 5 epics have
retrospectives, and the latest readiness assessment
(`implementation-readiness-report-2026-06-21.md`) is **✅ READY with 0 critical / 0
major** issues. There is therefore **no deferred story or code work**.

What remained open were **three documentation-reconciliation items** — two carried as
explicit recommendations in the 2026-06-21 readiness report, and one consistency gap
found during the sweep:

- **(A)** *Readiness rec #2 — the report's "single most valuable follow-up."* The Story
  1.10 epics entry was tightened at 2026-06-21 15:36 to fold in the 2026-06-16 deployment
  hardening (the `nginx-public` Ingress / cert-manager Let's Encrypt TLS / `publish.ps1`
  preflight contract). The downstream **story spec file** (`1-10-…md`, last touched
  2026-06-10) was **not** re-synced and still (i) lacked any `nginx-public` / Let's
  Encrypt / preflight acceptance criterion and (ii) named a **TLS Secret that no longer
  exists** (`hexalith-pages-tls`; the live secret is `hexalith-pages-letsencrypt-tls`).
- **(B)** *Readiness rec #3.* The **RCL status/freshness primitive sharing boundary**
  (architecture Gap #4, discovered in Epic 1) was only an architecture footnote + scattered
  retro lines, with **no durable tech-debt tracking artifact**.
- **(C)** *Consistency gap.* The `2026-06-12` Keycloak-HTTPS sprint-change-proposal was
  **fully implemented** in the deploy/AppHost layer but carried **no `status:` frontmatter**,
  unlike the three later proposals (all `status: implemented`).

**Discovery context:** this is the natural completion of the 2026-06-21 deploy-alignment
proposal, which reconciled `architecture.md` and `epics.md` but left the **story spec** and
these tracking artifacts as the last unreconciled layer.

## 2. Impact Analysis

**Epic impact:** None. No FR/NFR changes, no epic reorder, no new/obsolete epic. The
readiness verdict (✅ READY, 100% FR/NFR coverage) is unaffected.

**Story impact:** Story 1.10 (Deploy parties-ui) acceptance is **tightened** — a new
acceptance criterion (AC7) makes the `nginx-public` + Let's Encrypt + `publish.ps1`
preflight an explicit, testable deploy gate, and AC5's stale TLS-secret name is corrected.
This documents behaviour **already implemented** and **already covered** by
`OperatorScriptValidationTests` / `K8sManifestGenerationTests` (both verified present) — no
new code work.

**Artifact impact (this change):**

- `_bmad-output/implementation-artifacts/1-10-…md` — AC5 corrected; **AC7 added**; two
  stale `hexalith-pages-tls` task-note references corrected to `hexalith-pages-letsencrypt-tls`.
- `_bmad-output/implementation-artifacts/tech-debt-register.md` — **new file**; seeds
  **TD-1** (RCL status/freshness boundary).
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-06-12-…md` — frontmatter
  added with `status: implemented` + verification evidence.

**Technical impact:** None beyond documentation. **No manifests, code, tests, or live
cluster resources are touched.** The live cluster already serves both Let's Encrypt
certificates through `nginx-public`; `publish.ps1` already hardcodes the preflighted names
(`$IngressClassName = 'nginx-public'`, `$PagesTlsSecretName = 'hexalith-pages-letsencrypt-tls'`,
`$ZotTlsSecretName = 'registry-hexalith-letsencrypt-tls'`).

## 3. Recommended Approach

**Direct Adjustment** (checklist Option 1). No PRD/epic reorder, no rollback, no MVP
review — same classification as the two 2026-06-16 proposals and the 2026-06-21
deploy-alignment proposal this completes.

- **Effort:** Low (three documentation edits across three files).
- **Risk:** Low. Documents already-implemented, already-tested behaviour; verified against
  the live `deploy/` manifests and `publish.ps1` before editing.

## 4. Detailed Change Proposals

### Edit A — Story 1.10 spec · Acceptance Criteria (rec #2)

**A1 — AC5 corrected** (`deploy/k8s/ingress.yaml` is the source of truth):

> OLD: …`parties.hexalith.com` routes `/` … to `service/parties-ui:8080`, **`hexalith-pages-tls`** includes the host…
>
> NEW: …`parties.hexalith.com` routes `/` … to `service/parties-ui:8080`, **the `nginx-public` Ingress class is used**, **`hexalith-pages-letsencrypt-tls`** includes the host…

**A2 — AC7 added** (mirrors `epics.md` Story 1.10, tightened 2026-06-21):

> **AC7 — `publish.ps1` preflights the `nginx-public` / Let's Encrypt deploy path and fails
> closed.** Given the live Kubernetes cluster · When `deploy/k8s/publish.ps1` runs · Then it
> preflights the `nginx-public` Ingress class, the Zot registry Ingress
> (`registry.hexalith.com/` → `Service/zot:5000`, `ClusterIP`, no NodePort), and both
> cert-manager Let's Encrypt TLS Secrets (`hexalith-pages-letsencrypt-tls`,
> `registry-hexalith-letsencrypt-tls`), and **fails before image build/apply** if any is
> missing — there is **no local / host-level nginx bridge fallback**. Already covered by
> `OperatorScriptValidationTests` / `K8sManifestGenerationTests`.

**A3 — two stale `hexalith-pages-tls` task-note references** (Tasks/Subtasks) corrected to
`hexalith-pages-letsencrypt-tls` so the spec no longer points at a secret that does not
exist in `deploy/`.

### Edit B — New tech-debt register, seeded with TD-1 (rec #3)

Created `_bmad-output/implementation-artifacts/tech-debt-register.md` with **TD-1 — RCL
status/freshness primitive sharing boundary** (severity `low`, status `mitigated`). It
records: the architecture Gap #4 origin; the Epic 2–5 mitigation history (both AdminPortal
and ConsumerPortal duplicate the primitives **locally** behind RCL-owned ports/UI-host
adapters, with drift tests, and **never reference the host** — the load-bearing constraint
holds); the still-open decision (**promote to a neutral shared UI package vs accept the
duplication**); and the trigger to act (**a 3rd RCL needs them, or the copies drift**).

### Edit C — Stamp the 2026-06-12 proposal `status: implemented`

Added YAML frontmatter (`status: implemented`, `implementation_verified: 2026-06-21`) plus
the verification evidence to `sprint-change-proposal-2026-06-12-eventstore-admin-ui-keycloak-https.md`.
**Evidence:** no `http://auth.tache.ai:8080` authority/issuer remains under `deploy/` or
the AppHost; `https://auth.tache.ai/realms/tache` is used in the admin-ui kustomization,
`AppHost/Program.cs` (`PublishModeJwtIssuer`), and `publish.ps1` (`$TacheIssuer`); the
admin-ui deployment carries no `auth.tache.ai` hostAlias.

## 5. Implementation Handoff

**Scope classification:** Minor.

**Routed to:** Developer agent — **implemented in-line** (documentation reconciliation only).

**Success criteria (all met):**

- ✅ Story 1.10 spec names the `nginx-public`-only path, both Let's Encrypt TLS Secrets, the
  Zot registry ingress, and the `publish.ps1` preflight (new AC7); AC5 and the two task-note
  references use the live secret name `hexalith-pages-letsencrypt-tls`. The spec, `epics.md`,
  `architecture.md`, `deploy/`, and `publish.ps1` now agree — **zero residual drift**.
- ✅ Readiness rec #3 is now a durable tracked artifact (`tech-debt-register.md`, TD-1).
- ✅ Four of the six sprint-change-proposals now carry `status: implemented`; the 2026-06-12
  one is stamped with verification evidence.
- ✅ No manifests, code, tests, or live cluster resources changed.

**Out of scope (noted, not actioned):** the two **2026-06-09** proposals
(`sprint-change-proposal-2026-06-09.md`, `…-readiness-2026-06-09.md`) also lack a `status:`
field. They predate this initiative and were not part of the requested scope; stamping them
would require a separate verification pass. Flag if you want them reconciled in a follow-up.
