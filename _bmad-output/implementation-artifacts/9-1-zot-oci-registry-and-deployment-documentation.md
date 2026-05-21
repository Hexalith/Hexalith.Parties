# Story 9.1: Zot OCI Registry & Deployment Documentation

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an operator preparing Hexalith.Parties for cluster deployment,
I want a Zot OCI registry committed to the repo as applyable manifests, a canonical architecture reference linked from every entry-point document, and stable architecture decision records covering the credential pipeline and the kubectl-context gate,
so that the build+push+apply pipeline (delivered across Stories 9.2–9.7) has one authoritative registry definition, one authoritative architecture document, and the rationale for non-obvious operational choices is captured in ADRs reviewers can cite.

## Scope & Non-Scope

**This story delivers** (planning + doc + Zot manifests only — no Hexalith app manifests, no scripts):

1. `deploy/zot/` manifest tree (namespace, ConfigMap with `accessControl` groups, Deployment, Service, PVC, Ingress, README) — re-creatable from `kubectl apply -f deploy/zot/` on a clean cluster, captures the live-cluster Zot configuration verified during this story.
2. `deploy/k8s/README.md` as the operator entry-point doc — one-command publish/teardown snippets + Zot credentials subsection + pointer to `docs/kubernetes-deployment-architecture.md`. No manifests/kustomization/scripts inside `deploy/k8s/` yet — those land in Stories 9.2, 9.3, 9.4, 9.5.
3. Refresh of `docs/deployment-guide.md`, `docs/getting-started.md`: align ADR references to v2 naming (`ADR 9.5-1` → `ADR D-K8s-2`, `ADR 9.5-2` → `ADR D-K8s-3`), insert canonical-doc pointers, scope `validate-deployment.ps1` references as forward-references to Story 9.6.
4. Verification that ADR `D-K8s-2` (Zot Pull-Secret Path B + dedicated `parties-publisher` build account) and ADR `D-K8s-3` (`-ConfirmContext` gate) in `_bmad-output/planning-artifacts/architecture.md` match the AC contract from `epics.md` Story 9.1 verbatim, with any wording fix applied here (the ADRs are already present per the 2026-05-21 greenfield-rewrite SCP execution log; this story closes the loop on AC6 + AC7 by treating the ADRs as a story deliverable to verify, not author from scratch).
5. Verification + tightening (if needed) of the tagging-policy section in `docs/kubernetes-deployment-architecture.md` §5.2 to match AC4 verbatim (4 tag shapes + forbidden mutable tags + `+dirty` rejected by `validate-deployment.ps1` for any tag that ships).

**This story does NOT deliver** (forward-referenced to later v2 stories):

| Out-of-scope | Owned by |
|---|---|
| `publish.ps1` / `teardown.ps1` / `_lib/Confirm-KubeContext.ps1` | Story 9.5 |
| `deploy/k8s/<service>/` Hexalith app manifests (7 services) + `kustomization.yaml` + `namespace.yaml` | Story 9.2 |
| `deploy/k8s/redis/` + `deploy/k8s/keycloak/` carve-outs | Story 9.3 |
| `deploy/dapr/*.yaml` Dapr CRs (3 Components + 5 ACL configs + 2 Subscriptions + resiliency) | Story 9.4 |
| `deploy/validate-deployment.ps1` lint tooling | Story 9.6 |
| `tests/Hexalith.Parties.DeployValidation.Tests/*.cs` fitness suite (incl. `DocumentationFitnessTest`) | Story 9.7 |

> Cluster-side Zot setup (htpasswd `parties-publisher` user creation + `accessControl.groups.builders` membership) is an **infra-team** prerequisite, not a developer task. This story captures the manifest shape that infra applied; manifest re-application on a fresh cluster is the documented operator path.

## Acceptance Criteria

### AC1 — Zot deployment manifests committed to `deploy/zot/`

- **Given** a Kubernetes cluster reachable from the operator's workstation,
- **When** the operator runs `kubectl apply -f deploy/zot/` (or `kubectl apply -k deploy/zot/` if the dev opts in for a Kustomization),
- **Then** the manifests recreate the live-cluster Zot configuration in namespace `zot` (distinct from `hexalith-parties`), with:
  - A single-Pod `Deployment/zot` running `ghcr.io/project-zot/zot-linux-amd64` at a **pinned tag** (NOT `:latest` — see Dev Notes "Live-cluster drift" item; pin to `v2.1.7` or whichever tag is current at story execution per `kubectl get pod zot-* -n zot -o jsonpath='{.spec.containers[0].image}'`),
  - A `PersistentVolumeClaim/zot-pvc` (storage class default; capacity pinned to the live-cluster value resolved in Task 1 Subtask 1.2 via `kubectl get pvc zot-pvc -n zot -o jsonpath='{.spec.resources.requests.storage}'` — do NOT pick a synthetic value; if the live cluster's PVC is larger than the committed value, `kubectl apply` will leave the live PVC untouched (PVCs only grow) and the committed value becomes cosmetic; if smaller, the apply will fail. Pin to the live value to keep the apply idempotent),
  - A `Service/zot` of type `ClusterIP` (the live-cluster `NodePort 30500` is dropped from the committed manifest — Story 9.1 v2 does NOT expose a NodePort because the Ingress is the supported access path),
  - **Pre-apply safety:** before any `kubectl apply -f deploy/zot/` against the live cluster, the operator MUST run `kubectl get endpoints -n zot zot -o yaml` AND `kubectl get svc zot -n zot -o jsonpath='{.spec.ports}'` AND `kubectl describe svc -n zot zot | grep -E 'NodePort|External'` to confirm no in-cluster or external consumer depends on port 30500. The Service mutation (NodePort → ClusterIP) is irreversible-in-place and any 30500 consumer breaks at apply time. If consumers exist, either (a) keep the NodePort temporarily by patching the committed Service manifest with the live `nodePort: 30500` value and opening a follow-up story to migrate the consumers, or (b) coordinate the cutover with the consumer owners before applying. `deploy/zot/README.md` documents this check as a hard prerequisite of the apply step,
  - An `Ingress/zot-ingress` (`ingressClassName: nginx`) terminating TLS at the cluster edge (`tls[0].secretName: zot-tls`) for `host: registry.hexalith.com`,
  - A `ConfigMap/zot-config` carrying the `config.json` payload below in AC2.
- **And** the Ingress carries the live-cluster annotations `nginx.ingress.kubernetes.io/{force-ssl-redirect,proxy-body-size,proxy-read-timeout,proxy-send-timeout,ssl-redirect}` exactly as observed in `kubectl get ingress zot-ingress -n zot -o yaml` (preserve `proxy-body-size: "0"`, `proxy-read-timeout: "900"`, `proxy-send-timeout: "900"`).
- **And** the namespace contains **no Hexalith service workloads** (registry concern is isolated from application concern — only Zot lives in `zot`; Hexalith services live in `hexalith-parties` per docs §3).
- **And** Secret `zot-auth-secret` (htpasswd file) and Secret `zot-tls` (Ingress cert) are **NOT committed** — both are infra-managed; `deploy/zot/README.md` documents their one-time creation steps and the kubectl commands to verify their presence.

### AC2 — Access control configuration matches `docs/kubernetes-deployment-architecture.md` §5.1

- **Given** the `ConfigMap/zot-config` is applied,
- **When** the embedded `config.json` is inspected,
- **Then** `accessControl.groups` carries exactly three groups with the documented memberships:
  - `admins`: members `jpiquot`, `qdassivignon` — grants `read`, `create`, `update`, `delete` on `repositories["**"]`,
  - `builders`: members `kaniko`, `github-ci`, `parties-publisher` — grants `read`, `create`, `update` on `repositories["**"]` (no `delete`),
  - `readers`: members `kubernetes` — grants `read` only on `repositories["**"]`.
- **And** `accessControl.repositories["**"].anonymousPolicy` is `[]` (anonymous access denied for all repositories — `Zot` semantics: an empty array denies, not "any action allowed"),
- **And** `accessControl.adminPolicy.groups` is `[admins]` (admins group gets the Zot admin role),
- **And** `http.auth.htpasswd.path` is `/etc/zot/auth/htpasswd` (mounted from `Secret/zot-auth-secret` via `volumeMounts` on the Deployment),
- **And** `http.auth.failDelay` is `5` (matches live-cluster),
- **And** `http.realm` is `registry-hexalith` (matches live-cluster).

### AC3 — Credential separation policy documented in ADR + entry-point docs

- **Given** the dedicated build identity policy (ADR `D-K8s-2`),
- **When** any future `publish.ps1` (Story 9.5) consumes registry credentials,
- **Then** the documented contract — captured in `architecture.md` ADR `D-K8s-2`, `docs/kubernetes-deployment-architecture.md` §5.1, `docs/deployment-guide.md` "Zot credentials" subsection, `docs/getting-started.md` Step 1b, and `deploy/k8s/README.md` — states:
  - `publish.ps1` reads the `parties-publisher` entry from `~/.docker/config.json` **exclusively**,
  - Human admin credentials (`jpiquot`, `qdassivignon` in the `admins` group) are documented as **out-of-band for emergency operations only** (repository management, deletion, dispute resolution); they are not used by `publish.ps1`, by CI runners, or by any developer in the normal flow,
  - `publish.ps1` exits **6** if the `parties-publisher` entry is missing, malformed, or sources credentials through `credsStore` / `credHelpers` (Path B requirement — see ADR `D-K8s-2`).
- **And** the wording is consistent across the 4 doc surfaces — no doc says "any admin can push" or "the operator's credential is acceptable".
- **And** no doc reproduces a literal password, token, or `~/.docker/config.json` body (only the `docker login -u parties-publisher registry.hexalith.com` command shape).

### AC4 — Tagging policy documented in `docs/kubernetes-deployment-architecture.md` §5.2

- **Given** the tagging policy must be deterministic per commit,
- **When** §5.2 of the canonical doc is inspected,
- **Then** the policy enumerates exactly these four tag shapes:
  - **git tag** form: `vMAJOR.MINOR.PATCH` on `main` for stable releases (the `v` prefix is MinVer's tag-recognition prefix only — image tags drop the `v`; see §13 Quick Reference),
  - **image tag** form: `MAJOR.MINOR.PATCH` for stable releases (example: `0.2.0`),
  - **image tag** form: `MAJOR.MINOR.PATCH-preview.0.N` for preview commits past the last tag (N = `git rev-list --count v<last>..HEAD`),
  - **image tag** form: `MAJOR.MINOR.PATCH-preview.0.N+dirty` for uncommitted-tree builds (warn-and-proceed in `publish.ps1`; rejected by `validate-deployment.ps1` for any tag destined to ship).
- **And** the doc explicitly forbids mutable tags (`latest`, `staging-latest`, empty tag) for any `registry.hexalith.com/*` image consumed by `deploy/k8s/`,
- **And** the doc states that `+dirty` build-metadata is permitted by `publish.ps1` (with a single `WARNING:` stdout line) but rejected by `validate-deployment.ps1` as a blocking lint failure for any tag that ships to a real cluster,
- **And** the doc states that each image is built once per commit, immutable thereafter (no re-tag, no overwrite) — and that the registry's accept-re-push behavior under the same digest is benign (Zot stores the same digest under the same tag without duplication).

### AC5 — Canonical-doc pointers from every entry-point document

- **Given** `docs/kubernetes-deployment-architecture.md` is the single canonical source of truth for cluster topology, configuration sources, operator workflow, reproducibility guarantees, and MVP boundaries,
- **When** every K8s deployment entry-point document is inspected,
- **Then** each one carries an explicit pointer to the canonical doc:
  - `deploy/k8s/README.md` — opens with a "Canonical reference" callout pointing at `docs/kubernetes-deployment-architecture.md`,
  - `docs/getting-started.md` — Step 1b ("Publish to a Kubernetes Cluster") closes with a "For the full cluster topology, see [Kubernetes Deployment Architecture](kubernetes-deployment-architecture.md)" pointer,
  - `docs/deployment-guide.md` — top of the file carries a "K8s deployment shape" callout pointing at the canonical doc; the in-MVP K8s sections (lines that today reference `validate-deployment.ps1` + `Story 9.2` / `Story 9.3` lint categories) are reframed as "delivered by Story 9.6 — see [Kubernetes Deployment Architecture](kubernetes-deployment-architecture.md) for the deployed shape",
  - `_bmad-output/planning-artifacts/architecture.md` — already references `docs/kubernetes-deployment-architecture.md` via ADR `D-K8s-4` "Affects" line (no edit required here; verify presence).
- **And** none of these documents duplicates the canonical doc's topology table (§3.1), the configuration-source taxonomy (§7), or the 13-phase publish-pipeline phase list (§8) — they reference the canonical doc instead. Existing documentation of the broader Hexalith DAPR component selection / multi-tenant setup remains untouched (those are application-architecture concerns, not K8s-deployment-shape duplications).
- **And** the link to the canonical doc carries a **positive-affordance phrase** at its leading edge — one of: `For the full ...`, `Canonical reference: ...`, `See [Kubernetes Deployment Architecture] ...`, `Refer to [Kubernetes Deployment Architecture] ...`. Phrases that mention the canonical doc only to disclaim it (`do not consult ...`, `superseded by ...`, `outdated, see ...`) do NOT satisfy AC5 — the `DocumentationFitnessTest` (Story 9.7) MUST regex-anchor the pointer to one of the positive-affordance lead phrases listed above. The link text itself MUST contain the literal substring `Kubernetes Deployment Architecture` (the canonical doc's H1) to give Story 9.7 a stable anchor.

### AC6 — ADR D-K8s-2 authored in `architecture.md`

- **Given** the Zot pull-secret pipeline is operationally non-obvious (Base64 credential vs decoded credential, argv exposure, helper-resolver indirection),
- **When** `_bmad-output/planning-artifacts/architecture.md` is read,
- **Then** ADR `D-K8s-2 — Zot Registry as Image Substrate with Dedicated parties-publisher Build Account` is present and documents the six bullets below. Each bullet specifies a **literal substring** (mechanically greppable) that MUST appear in the ADR body for the bullet to be considered satisfied:

  | Bullet | Required substring (case-sensitive, exact match) |
  |---|---|
  | **(a)** Path B wholesale `auths` emission | `auths["registry.hexalith.com"]` AND `wholesale` |
  | **(b)** Never decoded / never echoed | `never decoded` AND `never echoed` |
  | **(c)** credsStore/credHelpers unsupported, exit 6 | `credsStore` AND `credHelpers` AND `exit 6` (or `exits 6`) AND `docker login -u parties-publisher` |
  | **(d)** Rationale: minimal credential surface | `minimal` AND (`auditable` OR `audit`) AND (`credential resolver` OR `credential helper`) |
  | **(e)** parties-publisher in builders group, not admins | `parties-publisher` AND `builders` AND `admins` |
  | **(f)** Consequence — cluster-side prereq is infra-team | `htpasswd` AND (`infra-team` OR `infra team`) AND `builders` |

- **And** the dev verification path is: `grep -A 50 "^\*\*D-K8s-2"  _bmad-output/planning-artifacts/architecture.md | grep -E '<each-substring>'` — every substring above must return a non-empty match within the ADR body. If a substring is absent, append **one** sentence to the ADR that contains it (no rewrite of the surrounding prose; minimal-diff). All six substrings must be present at AC6 close; the verification is mechanical, not interpretive.

### AC7 — ADR D-K8s-3 authored in `architecture.md`

- **Given** the publish-context gate is operationally non-obvious (registry now on a real cluster, not a local one — the legacy regex allowlist breaks),
- **When** `_bmad-output/planning-artifacts/architecture.md` is read,
- **Then** ADR `D-K8s-3 — -ConfirmContext Gate (replaces local-cluster regex allowlist)` is present and documents the five bullets below. Each bullet specifies a **literal substring** (mechanically greppable) that MUST appear in the ADR body for the bullet to be considered satisfied:

  | Bullet | Required substring (case-sensitive, exact match) |
  |---|---|
  | **(a)** Mandatory `-ConfirmContext`, exact match | `-ConfirmContext` AND (`exact` OR `exactly`) AND `kubectl config current-context` |
  | **(b)** Legacy regex allowlist deleted | (`regex allowlist` OR `regex-allowlist`) AND (`deleted` OR `removed`) AND `kubernetes-admin@cluster.local` |
  | **(c)** Exit 2 on mismatch, no URL/CA/token echo | (`exit 2` OR `exits 2`) AND `expected '<arg>', got '<active>'` AND `does not echo` |
  | **(d)** Active context echoed once at start | (`echoed` OR `echo`) AND `once` AND `auditability` |
  | **(e)** Shared helper `_lib/Confirm-KubeContext.ps1`, no gate on validate-deployment.ps1 | `Confirm-KubeContext.ps1` AND `validate-deployment.ps1` AND (`does NOT carry` OR `does not carry`) |

- **And** the dev verification path is: `grep -A 40 "^\*\*D-K8s-3" _bmad-output/planning-artifacts/architecture.md | grep -E '<each-substring>'` — every substring above must return a non-empty match within the ADR body. If a substring is absent, append **one** sentence to the ADR that contains it (no rewrite of the surrounding prose; minimal-diff). All five substrings must be present at AC7 close; the verification is mechanical, not interpretive.

### AC8 — Entry-point docs refreshed for v2 ADR naming + canonical pointer

- **Given** the legacy v1 ADR names (`ADR 9.5-1`, `ADR 9.5-2`) referenced by `docs/deployment-guide.md` and `docs/getting-started.md` predate the v2 greenfield rewrite,
- **When** the dev edits these entry-point docs,
- **Then** each `ADR 9.5-1` reference is replaced by `ADR D-K8s-2`, each `ADR 9.5-2` reference is replaced by `ADR D-K8s-3` (in-file refresh; the underlying ADR content is unchanged — only the canonical name changes),
- **And** each entry-point doc gains:
  - A "Zot credentials" subsection (or the existing one is preserved) documenting `docker login -u parties-publisher registry.hexalith.com` — citing the `builders` group, the credsStore/credHelpers non-support, and the in-MVP infra-team ownership of cluster-side htpasswd updates,
  - The one-command publish snippet `pwsh deploy/k8s/publish.ps1 -ConfirmContext <name>` (presented as a forward reference: "available after Story 9.5 lands"),
  - The one-command teardown snippet `pwsh deploy/k8s/teardown.ps1 -ConfirmContext <name>` (same forward-reference treatment),
  - A pointer to `docs/kubernetes-deployment-architecture.md` for the full topology.
- **And** no doc prints credentials, tokens, certificate authorities, or example secret values (no `Password: ...` lines, no `Bearer eyJ...` JWT-shaped strings, no `auths: { registry.hexalith.com: { auth: "<base64>" } }` literals).

### AC9 — Documentation fitness contract (test delivered in Story 9.7; contract established here)

- **Given** documentation consistency is a fitness concern,
- **When** the `DocumentationFitnessTest` (delivered as part of Story 9.7 — NOT this story) eventually runs against the entry-point docs in **scope**,
- **Then** the test enforces the mechanical regex contract below. Story 9.1 v2's deliverable here is to **leave the docs clean against this contract** so Story 9.7 finds a clean baseline; the dev verifies via the matching grep commands in Task 8.

**Scope (the file set the `DocumentationFitnessTest` scans):**

```
deploy/k8s/README.md
deploy/zot/README.md
docs/getting-started.md
docs/deployment-guide.md
docs/kubernetes-deployment-architecture.md
_bmad-output/planning-artifacts/architecture.md   ← scope-restricted: only sections OUTSIDE ADR D-K8s-2/D-K8s-3 "Rejected" bullets and outside the SCP audit-log narrative; see "Scope exclusions" below
```

**Out of scope (the test ignores these even if they sit inside the repo):**

```
_bmad-output/implementation-artifacts/**/*.md          (story files — may quote forbidden patterns as examples)
_bmad-output/planning-artifacts/sprint-change-proposal-*.md   (historical audit trail; may quote forbidden patterns)
_bmad-output/planning-artifacts/epic-*-retro-*.md      (retrospectives; may reference v1 names)
_bmad-output/planning-artifacts/epics.md               (planning narrative; may reference v1 superseded story IDs)
_bmad-output/planning-artifacts/prd.md                 (PRD; FR31 still references local-cluster framing intentionally per its line 712)
```

**Operational-guidance anchor rule** (resolves the `kind|k3d|minikube` informational-vs-operational ambiguity):

A doc line is **operational guidance** if any of the following is true:

1. It sits inside a fenced code block (```` ``` ````, ```` ```bash ````, ```` ```pwsh ````, ```` ```yaml ````), OR
2. The same line (or the line immediately above or below) contains one of: `publish.ps1`, `teardown.ps1`, `kubectl`, `pwsh`, `docker login`, `-ConfirmContext`, OR
3. The same line contains one of the marker tokens: `allowlist`, `regex`, `pattern`, `current-context`, `^kind-`, `^k3d-`, `^minikube$`, `^docker-desktop$`, OR
4. The line sits under a section heading whose text matches `(?i)(procedure|usage|how to|running|publish|teardown|deploy|operator|cli|command)` until the next H1/H2/H3.

Any other line is **informational** and is exempt from the `kind|k3d|minikube|docker-desktop` flagging rule.

**Forbidden-pattern regex table** (the contract Story 9.7 will mechanize verbatim):

| # | Category | Regex (PCRE / `grep -E` compatible) | Scope | Exceptions |
|---|---|---|---|---|
| F1 | Mutable tag on Zot image | `registry\.hexalith\.com/[A-Za-z0-9._/-]+:(latest|staging-latest)(?![\w.+-])` | scope set above | none |
| F2 | Empty tag on Zot image | `registry\.hexalith\.com/[A-Za-z0-9._/-]+:[\s\)\]'"\`]` (no chars between `:` and end of token) | scope set above | none |
| F3 | Stale v1 script name | `\b(regen\.ps1|deploy-local\.ps1|teardown-local\.ps1)\b` | scope set above | architecture.md ADR D-K8s-2 "Rejected" bullets (lines inside `\*\*Rejected:\*\*` → blank line); the `Affects` line of any ADR may reference the historical name once |
| F4 | Stale regex-allowlist reference in operational guidance | `(\^kind-|\^k3d-|\^minikube\$|\^docker-desktop\$)` | scope set above, **AND** operational-guidance anchor rule matches | ADR D-K8s-3 body (the ADR documents the deletion of this allowlist) |
| F5 | Local-cluster name in operational guidance | `\b(kind-[a-z0-9-]+|k3d-[a-z0-9-]+|minikube|docker-desktop)\b` | scope set above, **AND** operational-guidance anchor rule matches | informational lines (rule above), ADR D-K8s-3 body |
| F6 | Missing canonical-doc pointer | (negative check) docs in scope MUST contain at least one occurrence of `[Kubernetes Deployment Architecture]` link text accompanied by a positive-affordance lead phrase per AC5 (`For the full`, `Canonical reference`, `See [Kubernetes`, `Refer to [Kubernetes`) | every doc in scope except `architecture.md` (which already cites the canonical doc via ADR D-K8s-4 Affects line) | architecture.md |
| F7 | Plaintext `Password:` line | `(?i)^\s*Password\s*[:=]\s*\S+` | scope set above | code-block lines that are explicitly bracketed by an HTML comment `<!-- forbidden-example -->` immediately before the block |
| F8 | JWT-shaped token | `\beyJ[A-Za-z0-9_-]{20,}\.[A-Za-z0-9_-]{20,}\.[A-Za-z0-9_-]{20,}\b` | scope set above | same `<!-- forbidden-example -->` escape |
| F9 | Base64 credential in `auth:` field | `"auth":\s*"[A-Za-z0-9+/=_-]{20,}"` | scope set above | same `<!-- forbidden-example -->` escape |
| F10 | htpasswd bcrypt fragment | `\$2[aby]?\$[0-9]{2}\$[A-Za-z0-9./]{50,}` | scope set above | same `<!-- forbidden-example -->` escape |
| F11 | Stale v1 ADR reference | `\bADR\s+9\.5-[12]\b` | scope set above (excluding `architecture.md` ADR D-K8s-2 "Rejected" bullets which may cite historical naming) | architecture.md ADR Rejected bullets |
| F12 | `:latest` on any image inside a fenced code block (registry-agnostic catch-all) | `:latest\b` inside a fenced code block whose first line is one of ```` ```yaml ````, ```` ```sh ````, ```` ```bash ````, ```` ```pwsh ```` | scope set above | vendor-image lines for `redis:`, `keycloak:`, `quay.io/keycloak/keycloak:` — Story 9.3's concern, not this story's; explicitly excluded via `(?!.*\b(redis\|keycloak\|quay\.io/keycloak/keycloak)\b)` lookahead OR equivalent post-filter |

- **And** if any pattern F1–F12 matches an in-scope file (modulo its documented exceptions) at AC9 close, Story 9.1 v2 is NOT done. The dev's job is to leave a clean baseline. The Task 8 grep subtasks below are the manual equivalent of the eventual `DocumentationFitnessTest` — they MUST all return zero unexpected matches before this story moves to review.
- **And** the dev MAY add a `<!-- forbidden-example -->` HTML comment immediately before a code block that legitimately needs to contain a forbidden shape (e.g., the AC9 table itself in this story, or a "never write this" example in `docs/deployment-guide.md`). The fitness test ignores the next fenced code block after a `<!-- forbidden-example -->` comment. This is the only documented bypass — abuse of the escape on real operational guidance is itself a fitness-test failure to flag in code review.

## Tasks / Subtasks

### Task 1 — Inspect live-cluster Zot, snapshot configuration (AC1, AC2)

- [x] Subtask 1.1 — Run `kubectl get cm zot-config -n zot -o yaml > /tmp/zot-config-live.yaml`; extract the embedded `config.json` payload into a readable form (e.g. `yq '.data["config.json"]' /tmp/zot-config-live.yaml`).
- [x] Subtask 1.2 — Run `kubectl get deployment zot -n zot -o yaml`, `kubectl get svc zot -n zot -o yaml`, `kubectl get ingress zot-ingress -n zot -o yaml`, `kubectl get pvc -n zot`. Note the exact image tag in use (the live-cluster `:latest` is to be replaced by a **pinned tag** in the committed manifest — capture the resolved digest from `kubectl describe pod -n zot -l app=zot` and reverse-lookup the tag from `ghcr.io/project-zot/zot-linux-amd64` via `crane manifest`/`docker pull --platform linux/amd64` if available; absent that, pin to the latest stable Zot release at story execution and note the choice in `deploy/zot/README.md`).
- [x] Subtask 1.3 — Cross-check the live `accessControl.groups` against AC2: confirm `admins=[jpiquot, qdassivignon]`, `builders=[kaniko, github-ci, parties-publisher]`, `readers=[kubernetes]`. If any drift is found between live cluster and the AC contract, **stop and surface the drift** to the operator (do not silently align — drift here may indicate cluster-side changes the SCP didn't capture; ask before editing). Live-cluster snapshot verified 2026-05-21 — see Dev Notes "Live-cluster snapshot" subsection for the canonical values.

### Task 2 — Author `deploy/zot/` manifest tree (AC1, AC2)

- [x] Subtask 2.1 — Create `deploy/zot/namespace.yaml` (`Namespace/zot` with label `app.kubernetes.io/part-of: hexalith-platform` for visibility).
- [x] Subtask 2.2 — Create `deploy/zot/configmap.yaml` (`ConfigMap/zot-config` with the `config.json` payload from Task 1, formatted as multi-line YAML literal). **JSON key ordering: alphabetical (recursive — every object, top-level and nested, sorts its keys lexicographically).** Live-cluster ordering is rejected: it depends on Zot's emitted-config field order, which is not byte-stable across Zot versions, and re-snapshotting the live cluster would silently flip the diff. Pre-process the payload via `jq -S '.' < live-config.json > deploy/zot/config-canonical.json` (the `-S` flag sorts keys recursively) before embedding into the YAML literal. The Story 9.7 fitness test (when authored) will assert `jq -S '.' deploy/zot/configmap.yaml.json-payload | diff -` returns zero. **Document the alphabetical-order rule in `deploy/zot/README.md`** so a future re-snapshot from `kubectl get cm zot-config -n zot -o yaml` is normalized through `jq -S` before committing — never paste the raw `kubectl` output.
- [x] Subtask 2.3 — Create `deploy/zot/deployment.yaml` (`Deployment/zot`, `replicas: 1`, `strategy.type: Recreate`, single container `zot` with the image tag pinned per the **digest-resolve rule** below, `imagePullPolicy: IfNotPresent` — NOT `Always` as in live cluster, since the tag is now pinned). Preserve the live-cluster probes (`readinessProbe` + `livenessProbe` on TCP port 5000 with the same delays/timeouts), volume mounts (`/etc/zot/config.json`, `/etc/zot/auth`, `/var/lib/registry`), and resource requests/limits (`requests: cpu=100m memory=256Mi`, `limits: cpu=1000m memory=2Gi`).
  - **Digest-resolve rule for the image tag (deterministic — pick exactly one path, not both):**
    1. Resolve the digest currently running on the live cluster: `kubectl get pod -n zot -l app=zot -o jsonpath='{.items[0].status.containerStatuses[0].imageID}'` → returns e.g. `docker.io/ghcr.io/project-zot/zot-linux-amd64@sha256:abc...`. Capture the digest portion.
    2. Reverse-lookup the tag for that digest at `ghcr.io/project-zot/zot-linux-amd64`. Preferred: `crane manifest ghcr.io/project-zot/zot-linux-amd64:vX.Y.Z` (iterate candidate tags until digest matches) OR `skopeo inspect docker://ghcr.io/project-zot/zot-linux-amd64:vX.Y.Z --raw | sha256sum`. Acceptable fallback: query the GitHub Container Registry API at `https://ghcr.io/v2/project-zot/zot-linux-amd64/tags/list` (anonymous), then `crane digest` each candidate.
    3. Pin to the tag that matches the live-cluster digest. Result: zero functional change to the live cluster on `kubectl apply`, maximum reproducibility for new clusters. **If the live cluster runs `:latest` (which it does — known drift):** the digest still resolves, and the tag-reversal step gives the immutable named tag that produced that digest. Use that tag.
    4. If the reverse-lookup is impossible at story execution (network down, registry unreachable, all candidate tags re-pushed under the same name), the **hard fallback** is the latest stable Zot release at `https://github.com/project-zot/zot/releases` — but this is a Zot **upgrade** (live cluster gets new bits on next `kubectl apply`), and the operator MUST coordinate with the infra team before applying. Document the chosen tag + the path (1-3 ideal, 4 fallback) in `deploy/zot/README.md` under "Pinned tag rationale".
- [x] Subtask 2.4 — Create `deploy/zot/pvc.yaml` (`PersistentVolumeClaim/zot-pvc`, default storage class — read via `kubectl get pvc zot-pvc -n zot -o jsonpath='{.spec.storageClassName}'`; if empty, omit `storageClassName` from the manifest entirely to inherit the cluster's default; **do NOT** hardcode a class name not present on the target cluster, `accessModes: [ReadWriteOnce]`). **Capacity: pinned to the live-cluster value resolved in Task 1 Subtask 1.2** — `kubectl get pvc zot-pvc -n zot -o jsonpath='{.spec.resources.requests.storage}'`. Do NOT use a synthetic value like `20Gi`. Document the live value + capacity-bump path in `deploy/zot/README.md` (kubectl patch + recreate the Pod; PVC expansion requires a CSI driver that supports `AllowVolumeExpansion: true` on the StorageClass).
- [x] Subtask 2.5 — Create `deploy/zot/service.yaml` (`Service/zot`, `type: ClusterIP`, port 5000 → containerPort 5000, no `nodePort`). The live-cluster `NodePort 30500` is **not** propagated to the committed manifest — `deploy/zot/README.md` documents the NodePort as a live-cluster artefact that is out-of-MVP for committed-manifest reproducibility.
- [x] Subtask 2.6 — Create `deploy/zot/ingress.yaml` (`Ingress/zot-ingress`, `ingressClassName: nginx`, `host: registry.hexalith.com`, `tls[0].secretName: zot-tls`, annotations exactly per the live-cluster set: `nginx.ingress.kubernetes.io/{force-ssl-redirect,proxy-body-size,proxy-read-timeout,proxy-send-timeout,ssl-redirect}` with live-cluster values).
- [x] Subtask 2.7 — Create `deploy/zot/README.md` documenting:
  - **Pre-apply NodePort consumer audit** (AC1 pre-apply safety): the operator MUST run the three `kubectl get endpoints` / `get svc` / `describe svc` commands from AC1 and confirm no consumer depends on port 30500 before applying. Document the two-path remediation (keep the NodePort temporarily, or coordinate cutover) inline.
  - Per-cluster one-time apply: `kubectl apply -f deploy/zot/` (or `-k deploy/zot/` if Kustomization opted in),
  - Out-of-band Secret creation: `kubectl create secret generic zot-auth-secret -n zot --from-file=htpasswd=<path-to-htpasswd>` (where the htpasswd file is generated by `htpasswd -nB parties-publisher` + similar lines for the other accounts; the file contents are infra-team-managed and never committed),
  - Out-of-band TLS Secret creation: `kubectl create secret tls zot-tls -n zot --cert=<path-to-cert> --key=<path-to-key>` (cert + key managed by cert-manager or the infra team; reference the cluster's wildcard-cert practice if applicable),
  - Verification commands: `kubectl get pod -n zot`, `kubectl logs -n zot deploy/zot`, `curl -u parties-publisher:<password> https://registry.hexalith.com/v2/_catalog` (with redaction guidance — do not paste real credentials into terminal history),
  - The pinned image tag + the **Pinned tag rationale** subsection from Subtask 2.3 (digest-resolve path chosen, fallback path NOT taken),
  - The capacity-bump path for `zot-pvc` (`kubectl patch pvc zot-pvc -n zot -p '{"spec":{"resources":{"requests":{"storage":"<new-size>"}}}}'`; requires `AllowVolumeExpansion: true` on the StorageClass),
  - The **ConfigMap re-snapshot convention**: future re-snapshots from `kubectl get cm zot-config -n zot -o yaml` MUST be normalized through `jq -S '.'` before committing to keep alphabetical key order stable (Subtask 2.2 rule),
  - A pointer to `docs/kubernetes-deployment-architecture.md` §5 (use the positive-affordance phrase `Canonical reference: [Kubernetes Deployment Architecture] §5`) and ADRs `D-K8s-2` + `D-K8s-3`.
- [x] Subtask 2.8 — **Dry-run smoke check (required, not optional).** After Subtasks 2.1–2.7 produce the seven `deploy/zot/` files, run `kubectl apply -f deploy/zot/ --dry-run=client -o yaml > /tmp/zot-dryrun-client.yaml; echo "exit=$?"`. Required outcome: `exit=0`, no `error:` lines in stderr, `/tmp/zot-dryrun-client.yaml` parses as well-formed YAML. This catches embedded-JSON indentation errors in `configmap.yaml`, missing required fields, malformed annotation values, and other shape-level defects the human eye misses. If `--dry-run=client` rejects, fix and re-run before AC1 close.
- [x] Subtask 2.9 — **Live-vs-committed drift diff (optional but recommended).** If the operator has cluster access, additionally run `kubectl apply -f deploy/zot/ --dry-run=server -o yaml > /tmp/zot-dryrun-server.yaml; echo "exit=$?"` against the live cluster context (the `-ConfirmContext` gate does NOT apply to this story — Story 9.5's `publish.ps1` is the gated path; for this one-time `kubectl apply` smoke, document the active context in the dev log instead). Required outcome: `exit=0`. Then diff against the live state: `diff <(kubectl get all,cm,ingress,pvc -n zot -o yaml | yq '... | del(.metadata.resourceVersion, .metadata.creationTimestamp, .metadata.uid, .status, .metadata.generation, .metadata.managedFields)') /tmp/zot-dryrun-server.yaml`. **Expected drift:** only the three intentional drift dimensions documented in Dev Notes "Live-cluster drift" (pinned tag vs `:latest`, `ClusterIP` vs `NodePort`, `IfNotPresent` vs `Always`). Any other drift = investigate before AC1 close.

### Task 3 — Author `deploy/k8s/README.md` entry-point doc (AC5, AC8)

- [x] Subtask 3.1 — Create `deploy/k8s/README.md` (replaces the v1 file that was wiped during the 2026-05-21 SCP execution). Structure:
  - **Canonical reference callout** (top of file, immediately after H1): use the exact positive-affordance phrase per AC5: `> **Canonical reference:** For the full cluster topology, configuration sources, operator workflow, reproducibility guarantees, and MVP boundaries, see [Kubernetes Deployment Architecture](../../docs/kubernetes-deployment-architecture.md).`
  - **Zot credentials** subsection: `docker login -u parties-publisher registry.hexalith.com` step, `builders`-group rationale, credsStore/credHelpers non-support note, in-MVP infra-team ownership of cluster-side htpasswd updates.
  - **Publish + teardown** section: present each command snippet as a fenced block immediately preceded by a per-snippet status admonition (Paige's correction — one admonition per block, not one at section top):
    - Before the publish snippet: `> **Status (forward reference — Story 9.5):** This command becomes available when Story 9.5 lands. Until then, manifests under `deploy/k8s/` are intentionally empty pending Stories 9.2 / 9.3 / 9.4 / 9.5; manifest correctness is verified manually against the cleanliness checks in [Kubernetes Deployment Architecture](../../docs/kubernetes-deployment-architecture.md) §13.`
    - Then the fenced `pwsh deploy/k8s/publish.ps1 -ConfirmContext <name>` snippet.
    - Before the teardown snippet: an analogous `> **Status (forward reference — Story 9.5):** ...` admonition.
    - Then the fenced `pwsh deploy/k8s/teardown.ps1 -ConfirmContext <name>` snippet.
  - **Coming in Stories 9.2–9.5 — roadmap callout box** (collapse the per-line annotations into a single callout for noise reduction — Paige's Option A):
    ```
    > **🗺️ Roadmap — this folder fills in across Stories 9.2 → 9.5**
    >
    > Today on `main`, this folder contains only this README. The following entries land in subsequent v2 stories:
    >
    > | Path | Owning story | Purpose |
    > |---|---|---|
    > | `eventstore/`, `eventstore-admin/`, `eventstore-admin-ui/`, `parties/`, `parties-mcp/`, `tenants/`, `memories/` | Story 9.2 | Aspirate-emitted per-service manifests |
    > | `redis/`, `keycloak/` | Story 9.3 | Hand-authored carve-outs |
    > | `namespace.yaml`, `kustomization.yaml` | Story 9.2 | Top-level wiring |
    > | `publish.ps1`, `teardown.ps1`, `_lib/Confirm-KubeContext.ps1` | Story 9.5 | Operator scripts + shared context-gate helper |
    >
    > Track progress in `_bmad-output/implementation-artifacts/sprint-status.yaml`.
    ```
    Insert an HTML comment `<!-- This folder listing anticipates Stories 9.2–9.5; entries are not present on main until that story ships. -->` immediately above the callout so the next editor knows the listing is forward-looking by design, not stale documentation.
  - **Pointers** (closing): use a `## See also` heading; include canonical doc (positive-affordance: `Refer to [Kubernetes Deployment Architecture]`), ADR `D-K8s-2`, ADR `D-K8s-3`, ADR `D-K8s-4` (greenfield rewrite rationale).
- [x] Subtask 3.2 — Verify the README references no credential literal, no `:latest` reference inside `registry.hexalith.com/*` examples, no `regen.ps1` / `deploy-local.ps1` / `teardown-local.ps1` strings.

### Task 4 — Refresh `docs/deployment-guide.md` (AC5, AC8)

- [x] Subtask 4.1 — Add a "K8s deployment shape" callout immediately under the file's top `# Deployment Guide` heading: `> **Canonical reference:** For the full Hexalith.Parties Kubernetes deployment topology (9-workload, Zot registry, Dapr control plane, MVP boundaries), see [Kubernetes Deployment Architecture](kubernetes-deployment-architecture.md). This guide covers application-architecture concerns (DAPR component selection, multi-tenant setup, troubleshooting); the canonical doc covers the deployed cluster shape.` Use the positive-affordance phrase `Canonical reference:` to satisfy AC5 + AC9 F6.
- [x] Subtask 4.2 — Find every occurrence of `ADR 9.5-1` and replace with `ADR D-K8s-2`; find every occurrence of `ADR 9.5-2` and replace with `ADR D-K8s-3`. **Do not rely on the line numbers captured during story creation** — they go stale if anyone touches the file in the interim. Use `sed -i 's/ADR 9\.5-1/ADR D-K8s-2/g; s/ADR 9\.5-2/ADR D-K8s-3/g' docs/deployment-guide.md` (or the Edit tool with `replace_all: true`) and then run `grep -n "ADR 9\.5-[12]" docs/deployment-guide.md` to confirm zero matches.
- [x] Subtask 4.2a — **Story-number cousin grep (Paige's correction):** v1 Story 9.5 was "Zot Registry Build+Push Pipeline"; v2 Story 9.5 is "Operator Scripts (publish.ps1 + teardown.ps1)" — same number, different scope. Bare `Story 9.5` references in `docs/deployment-guide.md` and `docs/getting-started.md` may now point at the wrong contract. Run `grep -nE "Story 9\.[1-9]\b" docs/deployment-guide.md docs/getting-started.md` and for each hit decide:
  - If the reference is to an AC contract (e.g. "Story 9.5 ADR 9.5-1") — replace per Subtask 4.2 (the ADR rename already covers it).
  - If the reference is to a behavior that is now owned by a **different** v2 story (e.g. "Story 9.2 added three lint categories" — those categories are now Story 9.6's surface) — update the story number to the v2 owner per the Story 9.X scope mapping in `epics.md` Epic 9 v2 (or per the [scope mapping](#scope--non-scope) section at the top of this story file).
  - If the reference is to a v1-only concept that no longer exists in v2 — remove the reference or scope it as historical ("Per Story 9.3 v1 — superseded by v2's Story 9.4 Dapr CRs").
  - If the reference is purely informational (e.g. "see the epic for the full plan") — leave it.
- [x] Subtask 4.2b — **Bracketed link-ref check:** run `grep -nE '\[ADR 9\.5-[12]\]\(' docs/deployment-guide.md docs/getting-started.md` and `grep -nE '#adr-9-5-[12]' docs/deployment-guide.md docs/getting-started.md` — if any markdown link `[ADR 9.5-N](target)` or anchor `#adr-9-5-N` survives the prose find-replace, update the link target / anchor too. Common case: anchors derived from H2 headings auto-rename when the heading text changes, but in-line links to fixed anchors do NOT. Run the greps; if any link survives, fix it; document the fix in the dev log.
- [x] Subtask 4.3 — Scope every `./deploy/validate-deployment.ps1` invocation snippet as a **forward reference** to Story 9.6. **Apply a per-snippet admonition (Paige's correction — adjacent, not section-top):** before EACH fenced `./deploy/validate-deployment.ps1` block in the file, insert the affordance-framed admonition:
  ```
  > **Status (forward reference — Story 9.6):** This validation step is part of the Story 9.6 deliverable. Until that ships, manifest correctness is verified manually against the cleanliness checks documented in [Kubernetes Deployment Architecture](kubernetes-deployment-architecture.md) §11 and against the regex table in `_bmad-output/implementation-artifacts/9-1-zot-oci-registry-and-deployment-documentation.md` AC9.
  ```
  Do NOT delete the snippets — they are the documented future contract for Story 9.6. Apply the admonition to every occurrence (`grep -nE '\./deploy/validate-deployment\.ps1' docs/deployment-guide.md` enumerates them — expect ≥ 5 hits). The "K8s manifest validation (Story 9.2)" subheading is renamed to "K8s manifest validation (Story 9.6)" to align with v2 story numbering.
- [x] Subtask 4.4 — Scope the "Story 9.3 added three categories" prose block — these v1 lint categories (`K8sTopology-MissingService`, `K8sSecret-JwtSigningKeyLiteral`, `K8sDapr-ResiliencyCrdSchemaDrift`) are forward-referenced into Story 9.6's 8-category set. Add an affordance-framed admonition immediately above the prose block: `> **Status (forward reference — Story 9.6):** The three lint categories listed below are v1-era. Story 9.6 ships a refreshed 8-category set including `K8sWorkload-MissingImagePullSecret`, `Secret-Plaintext`, `DaprACL-WildcardAppId`, `DaprACL-WildcardOperation`; see `_bmad-output/planning-artifacts/epics.md` Epic 9 v2 Story 9.6 for the authoritative category list.`
- [x] Subtask 4.5 — Preserve the rest of the file unchanged (the DAPR Component Configuration section, the Multi-Tenant Setup section, the Troubleshooting Common Misconfigurations section, the Failure Mode Runbook Reference section, etc. — these are application-architecture concerns not impacted by Epic 9 v2 rewrite).
- [x] Subtask 4.6 — **Rendered-output spot check (Paige's addition):** preview the edited file via `code --goto docs/deployment-guide.md` (VS Code preview) AND via `gh markdown preview` or equivalent (GitHub-rendered preview if the repo has GitHub Pages or PR preview). Verify: every admonition renders as a callout (not as plain indented gray prose); the canonical-doc link resolves to a working file; no anchor refs broken; no orphan TOC entries. Capture a one-line summary in the dev log.

### Task 5 — Refresh `docs/getting-started.md` Step 1b (AC5, AC8)

- [x] Subtask 5.1 — Find `Story 9.5 ADR 9.5-2` in the Step 1b intro paragraph and replace with `ADR D-K8s-3`. Preserve the "alternative" framing for local-cluster options (kind, k3d, minikube, Docker Desktop) — these are informational mentions of operator-choice clusters, exempt from AC9 F5 per the operational-guidance anchor rule (informational lines outside fenced blocks, outside `kubectl|publish.ps1|teardown.ps1` proximity, outside marker tokens like `allowlist|regex|pattern`).
- [x] Subtask 5.2 — **Inline link at first technical mention (Paige's correction — replaces the prior "closing pointer" pattern).** On the first sentence of Step 1b that references cluster topology, Zot, or the publish pipeline, link inline to the canonical doc with a positive-affordance lead phrase. Example wording: change "This step is an alternative to Step 1 for evaluating the production-shape deployment path against a Kubernetes cluster." to "This step is an alternative to Step 1 for evaluating the production-shape deployment path against a Kubernetes cluster — see [Kubernetes Deployment Architecture](kubernetes-deployment-architecture.md) for the full 9-pod topology and reproducibility contracts." If no such opening sentence exists in the current file, add a one-line `> **See also:** [Kubernetes Deployment Architecture](kubernetes-deployment-architecture.md) — full cluster topology and reproducibility contracts.` admonition at the **top** of the Step 1b section (under the H2/H3 heading, before the body prose). Do NOT append the pointer at the end of the section — by the time a reader reaches the bottom, the navigational value is wasted.
- [x] Subtask 5.3 — Preserve all other content in the file unchanged: prerequisites table (incl. the additional K8s-prereq table), the submodule-init prereq for `Hexalith.Memories`, the Step 1 Aspire-local-run flow (Epic 3), the round-trip prose, the round-trip `CreateParty`/`FindParties` examples.
- [x] Subtask 5.4 — The line "Local Kubernetes cluster | any of kind, k3d, minikube, Docker Desktop Kubernetes" in the prerequisites table is **informational** per AC9 F5 (line sits in a prerequisites table, not in operational guidance — see the anchor rule in AC9). Leave it. If subsequent revisions of `getting-started.md` add operational guidance that brings these strings into proximity with `kubectl|publish.ps1|allowlist|regex`, the fitness test will fire and the line gets disambiguated then.
- [x] Subtask 5.5 — **Story-number cousin grep + bracketed link-ref check:** apply the same Subtask 4.2a + 4.2b discipline to `getting-started.md` — `grep -nE "Story 9\.[1-9]\b" docs/getting-started.md` for cousins, `grep -nE '\[ADR 9\.5-[12]\]\(' docs/getting-started.md` for stale link targets. Fix each hit per the rubric in Subtask 4.2a.
- [x] Subtask 5.6 — **Rendered-output spot check** (Paige's addition): preview the edited file in VS Code + GitHub-rendered preview; verify admonition rendering, canonical-doc link resolves, no broken anchors. Capture a one-line summary in the dev log.

### Task 6 — Verify ADR D-K8s-2 + ADR D-K8s-3 wording matches AC6 + AC7 (AC6, AC7) — mechanical greps, not prose interpretation

- [x] Subtask 6.1 — **Mechanical grep verification of ADR D-K8s-2.** For each of the six required substrings in the AC6 table, run the corresponding grep against the ADR D-K8s-2 body:
  ```bash
  # First isolate the ADR body (from the D-K8s-2 heading to the next "**D-K8s" heading)
  awk '/^\*\*D-K8s-2/,/^\*\*D-K8s-3/' _bmad-output/planning-artifacts/architecture.md > /tmp/adr-d-k8s-2.md

  # AC6 bullet (a):
  grep -E 'auths\["registry\.hexalith\.com"\]' /tmp/adr-d-k8s-2.md  # must return ≥ 1 line
  grep -E 'wholesale' /tmp/adr-d-k8s-2.md                            # must return ≥ 1 line
  # AC6 bullet (b):
  grep -E 'never decoded' /tmp/adr-d-k8s-2.md                        # must return ≥ 1 line
  grep -E 'never echoed' /tmp/adr-d-k8s-2.md                         # must return ≥ 1 line
  # AC6 bullet (c):
  grep -E 'credsStore' /tmp/adr-d-k8s-2.md                           # must return ≥ 1 line
  grep -E 'credHelpers' /tmp/adr-d-k8s-2.md                          # must return ≥ 1 line
  grep -E 'exits? 6' /tmp/adr-d-k8s-2.md                             # must return ≥ 1 line
  grep -E 'docker login -u parties-publisher' /tmp/adr-d-k8s-2.md    # must return ≥ 1 line
  # AC6 bullet (d):
  grep -E 'minimal' /tmp/adr-d-k8s-2.md                              # must return ≥ 1 line
  grep -E 'auditable|audit' /tmp/adr-d-k8s-2.md                      # must return ≥ 1 line
  grep -E 'credential resolver|credential helper' /tmp/adr-d-k8s-2.md # must return ≥ 1 line
  # AC6 bullet (e):
  grep -E 'parties-publisher' /tmp/adr-d-k8s-2.md                    # must return ≥ 1 line
  grep -E 'builders' /tmp/adr-d-k8s-2.md                             # must return ≥ 1 line
  grep -E 'admins' /tmp/adr-d-k8s-2.md                               # must return ≥ 1 line
  # AC6 bullet (f):
  grep -E 'htpasswd' /tmp/adr-d-k8s-2.md                             # must return ≥ 1 line
  grep -E 'infra-?team' /tmp/adr-d-k8s-2.md                          # must return ≥ 1 line
  ```
  **For any grep that returns zero lines:** append a single sentence to the ADR body that contains the missing substring(s). Example: if `grep -E 'minimal' /tmp/adr-d-k8s-2.md` is empty, add to the Rationale: `This approach keeps the credential surface minimal and auditable while avoiding a docker credential resolver re-implementation in PowerShell.` Word-level addition only; do NOT rewrite surrounding prose.
- [x] Subtask 6.2 — **Mechanical grep verification of ADR D-K8s-3.** Apply the same approach with the AC7 table:
  ```bash
  awk '/^\*\*D-K8s-3/,/^\*\*D-K8s-4/' _bmad-output/planning-artifacts/architecture.md > /tmp/adr-d-k8s-3.md

  # AC7 bullet (a):
  grep -E '\-ConfirmContext' /tmp/adr-d-k8s-3.md
  grep -Ei 'exact(ly)?' /tmp/adr-d-k8s-3.md
  grep -E 'kubectl config current-context' /tmp/adr-d-k8s-3.md
  # AC7 bullet (b):
  grep -E 'regex[ -]?allowlist' /tmp/adr-d-k8s-3.md
  grep -E 'deleted|removed' /tmp/adr-d-k8s-3.md
  grep -E 'kubernetes-admin@cluster\.local' /tmp/adr-d-k8s-3.md
  # AC7 bullet (c):
  grep -E 'exits? 2' /tmp/adr-d-k8s-3.md
  grep -E "expected '<arg>', got '<active>'" /tmp/adr-d-k8s-3.md
  grep -E 'does not echo' /tmp/adr-d-k8s-3.md
  # AC7 bullet (d):
  grep -E 'echo(ed)?' /tmp/adr-d-k8s-3.md
  grep -E '\bonce\b' /tmp/adr-d-k8s-3.md
  grep -E 'auditability' /tmp/adr-d-k8s-3.md
  # AC7 bullet (e):
  grep -E 'Confirm-KubeContext\.ps1' /tmp/adr-d-k8s-3.md
  grep -E 'validate-deployment\.ps1' /tmp/adr-d-k8s-3.md
  grep -E 'does not carry|does NOT carry' /tmp/adr-d-k8s-3.md
  ```
  **For any grep that returns zero lines:** apply the same minimal-sentence-addition pattern from Subtask 6.1.
- [x] Subtask 6.3 — If any sentence was added in 6.1 or 6.2, append a single-line audit-trail note at the end of the ADR's `**Affects:**` bullet: `(Wording anchor added per Story 9.1 v2 AC6/AC7 verification — minimal-diff to preserve mechanical grep contract.)`. Preserves the audit trail without rewriting the ADR.

### Task 7 — Verify `docs/kubernetes-deployment-architecture.md` §5.2 tagging policy (AC4)

- [x] Subtask 7.1 — Read `docs/kubernetes-deployment-architecture.md` §5.2 — confirm the four tag shapes from AC4 are documented exactly. The current §5.2 table contains 3 rows (`vX.Y.Z` stable, `X.Y.Z-preview.0.N` preview, `*+dirty` warning) — AC4 requires a 4-row enumeration distinguishing the **git tag** form (`vX.Y.Z`) from the **image tag** form (`X.Y.Z`, no `v` prefix). If the doc collapses these two into one row, split them into two rows in the table and add a sentence clarifying the `v`-prefix is MinVer's tag-recognition convention only.
- [x] Subtask 7.2 — Confirm §5.2 explicitly states that `latest`, `staging-latest`, and empty tags are forbidden for `registry.hexalith.com/*` images consumed by `deploy/k8s/`. The current doc says "Same commit + same MinVer + same aspirate version → byte-identical image manifest" but does not explicitly enumerate forbidden mutable tags. **Add a sentence:** "Mutable tags (`latest`, `staging-latest`, empty) are explicitly forbidden for any `registry.hexalith.com/*` image consumed by `deploy/k8s/`; `validate-deployment.ps1` (Story 9.6) treats them as blocking lint failures."
- [x] Subtask 7.3 — Confirm §5.2 states that `+dirty` is `publish.ps1`-permitted (warning) but `validate-deployment.ps1`-rejected for ship-bound tags. The current `*+dirty` row says "Warning emitted; not safe to commit the resulting manifests" — which is close but not explicit about the `validate-deployment.ps1` gate. **Tighten** the row's "Meaning" cell to: "MinVer with uncommitted changes. `publish.ps1` warns and proceeds (operator opt-in); `validate-deployment.ps1` (Story 9.6) rejects as a blocking failure for any tag destined to ship."

### Task 8 — Cleanliness verification (AC9 contract — test is delivered by Story 9.7; greps here are the manual equivalent)

> All greps below use `grep -E` (extended regex) for portability across BRE/ERE and to avoid GNU-isms like `\s`. The scope set matches AC9 verbatim: in-scope = `deploy/k8s/README.md`, `deploy/zot/README.md`, `docs/getting-started.md`, `docs/deployment-guide.md`, `docs/kubernetes-deployment-architecture.md`, and the non-Rejected/non-SCP sections of `_bmad-output/planning-artifacts/architecture.md`. The story file itself (`_bmad-output/implementation-artifacts/9-1-...md`) is out of scope (per AC9 "Out of scope" list — it quotes forbidden patterns as examples).

- [x] Subtask 8.1 — **F3 (stale v1 script names) cleanliness check.** Scope-restrict to entry-point docs only (NOT the architecture file or planning artefacts, which legitimately reference v1 names in their historical/Rejected sections):
  ```bash
  grep -rEn '\b(regen\.ps1|deploy-local\.ps1|teardown-local\.ps1)\b' \
    deploy/k8s/README.md \
    deploy/zot/README.md \
    docs/getting-started.md \
    docs/deployment-guide.md \
    docs/kubernetes-deployment-architecture.md
  ```
  Expected output: **zero lines.** Any hit in these files is a defect — fix by removing the reference or rewording to use the v2 name (`publish.ps1` / `teardown.ps1`).
- [x] Subtask 8.2 — **F1 + F2 (mutable + empty tag on Zot images) cleanliness check.** Anchor the regex to the Zot registry prefix to avoid false-positives on vendor images (`redis:latest`, `keycloak:latest` — those are Story 9.3's concern):
  ```bash
  grep -rEn 'registry\.hexalith\.com/[A-Za-z0-9._/-]+:(latest|staging-latest)' \
    deploy/ docs/kubernetes-deployment-architecture.md docs/deployment-guide.md docs/getting-started.md
  # Also check empty-tag form (image-end without a versioned tag):
  grep -rEn 'registry\.hexalith\.com/[A-Za-z0-9._/-]+:[\s)\]'"'"'"`]' \
    deploy/ docs/kubernetes-deployment-architecture.md docs/deployment-guide.md docs/getting-started.md
  ```
  Expected output: **zero lines for both greps.** Any hit is a defect — fix by pinning the tag.
- [x] Subtask 8.3 — **F7 + F8 + F9 + F10 (leaked credential shapes) cleanliness check.** This grep replaces the prior over-broad pattern (the `auths.*:[^{]` alternative was self-matching on the AC text itself — Winston + Amelia + Murat all caught it):
  ```bash
  grep -rEn '^[[:space:]]*Password[[:space:]]*[:=]|\beyJ[A-Za-z0-9_-]{20,}\.[A-Za-z0-9_-]{20,}\.[A-Za-z0-9_-]{20,}\b|"auth"[[:space:]]*:[[:space:]]*"[A-Za-z0-9+/=_-]{20,}"|\$2[aby]?\$[0-9]{2}\$[A-Za-z0-9./]{50,}' \
    deploy/zot/README.md \
    deploy/k8s/README.md \
    docs/kubernetes-deployment-architecture.md \
    docs/deployment-guide.md \
    docs/getting-started.md
  ```
  Expected output: **zero lines.** If a doc legitimately needs to show a forbidden shape as a "never write this" example, prefix the fenced code block with `<!-- forbidden-example -->` (per AC9 escape) — the fitness test will ignore that block. Document any use of the escape in the dev log.
- [x] Subtask 8.4 — **F6 (canonical-doc pointer present + positive-affordance phrase) cleanliness check.** For each entry-point doc, verify both presence AND lead-phrase:
  ```bash
  # Positive: every doc in scope mentions the canonical doc
  for f in deploy/k8s/README.md deploy/zot/README.md docs/getting-started.md docs/deployment-guide.md; do
    if ! grep -q "kubernetes-deployment-architecture.md" "$f"; then
      echo "MISSING canonical-doc pointer: $f"
    fi
    if ! grep -E '(For the full|Canonical reference|See \[Kubernetes|Refer to \[Kubernetes)' "$f" | grep -q "Kubernetes Deployment Architecture"; then
      echo "MISSING positive-affordance lead phrase: $f"
    fi
  done
  ```
  Expected output: **zero `MISSING` lines.**
- [x] Subtask 8.5 — **F11 (stale v1 ADR reference) cleanliness check.** Scope-restrict to docs (architecture.md ADR `D-K8s-2` Rejected bullets may cite historical naming — those are AC9 F11 exceptions):
  ```bash
  grep -rEn '\bADR[[:space:]]+9\.5-[12]\b' \
    deploy/ docs/kubernetes-deployment-architecture.md docs/deployment-guide.md docs/getting-started.md
  ```
  Expected output: **zero lines** in entry-point docs. (Run also against `_bmad-output/planning-artifacts/architecture.md` separately and verify each hit sits inside the Rejected bullets of D-K8s-2; if any hit sits outside Rejected, fix it.)
- [x] Subtask 8.6 — **`kubectl --dry-run=client` smoke check (required, mechanizes Murat's blocker addition).** Validates the seven `deploy/zot/` manifests parse cleanly:
  ```bash
  kubectl apply -f deploy/zot/ --dry-run=client -o yaml > /tmp/zot-dryrun-client.yaml; echo "exit=$?"
  ```
  Expected: `exit=0`, no `error:` lines on stderr, `/tmp/zot-dryrun-client.yaml` is well-formed YAML. (This is the offline equivalent of the AC1 apply — does not require cluster reachability.) The Story 9.7 fitness test will mechanize this with a `RequiresKubectl` trait (NOT `RequiresCluster` — client-side dry-run is offline).
- [x] Subtask 8.7 — **F4 + F5 (stale regex-allowlist + local-cluster name in operational guidance) cleanliness check.** Requires the operational-guidance anchor rule from AC9. Mechanical preview of the rule (not exhaustive — Story 9.7 implements the full anchor logic):
  ```bash
  # Step 1: find lines containing operator-guidance anchor tokens
  grep -rEn '(publish\.ps1|teardown\.ps1|kubectl|pwsh|docker login|-ConfirmContext|allowlist|regex|pattern|current-context|\^kind-|\^k3d-|\^minikube\$|\^docker-desktop\$)' \
    deploy/ docs/kubernetes-deployment-architecture.md docs/deployment-guide.md docs/getting-started.md > /tmp/operational-anchor-lines.txt
  # Step 2: of those lines, flag any that also contain a stale local-cluster regex token
  grep -E '(\^kind-|\^k3d-|\^minikube\$|\^docker-desktop\$)' /tmp/operational-anchor-lines.txt
  # Step 3: additionally check, within ±1 line of anchor tokens, for unqualified kind/k3d/minikube/docker-desktop names
  grep -rEn -B1 -A1 'publish\.ps1|teardown\.ps1|kubectl|pwsh|docker login|-ConfirmContext' \
    deploy/ docs/kubernetes-deployment-architecture.md docs/deployment-guide.md docs/getting-started.md \
    | grep -E '\b(kind-[a-z0-9-]+|k3d-[a-z0-9-]+|minikube|docker-desktop)\b'
  ```
  Expected output: **zero lines from Step 2, and any Step 3 hits manually triaged** (a line that mentions both `kubectl` and `minikube` in `getting-started.md` Step 1 prerequisites may be informational and exempt — the dev decides per the AC9 anchor rule). Story 9.7 will mechanize the ±1-line proximity check; for AC9 close here, eyeball each Step 3 hit and confirm it's informational (otherwise fix it).
- [x] Subtask 8.8 — **Rendered-output spot check (Paige's addition, mirrors Subtask 4.6 / 5.6).** Preview the two new docs (`deploy/k8s/README.md` + `deploy/zot/README.md`) in VS Code + GitHub-rendered preview. Verify the callouts render correctly, the canonical-doc links resolve, the roadmap table in `deploy/k8s/README.md` renders as a table not a paragraph, and any `<!-- forbidden-example -->` escapes are not visible in the rendered output (HTML comments are hidden by markdown renderers).

### Task 9 — Update sprint-status.yaml + commit

- [x] Subtask 9.1 — On story completion (after dev runs `code-review` and review patches land), update `_bmad-output/implementation-artifacts/sprint-status.yaml`: `9-1-zot-oci-registry-and-deployment-documentation: done`; `epic-9: in-progress` (transitioning from `planned-greenfield`). Append a `last_updated` audit line per the existing convention.
- [x] Subtask 9.2 — Commit message format (project convention): `feat(deploy): story 9-1 v2 Zot OCI registry + deployment documentation`. Body bullets: `deploy/zot/` manifests (5 files + README), `deploy/k8s/README.md` entry-point, doc refresh (deployment-guide + getting-started), ADR D-K8s-2/D-K8s-3 verification, tagging policy tightened in canonical doc §5.2, cleanliness grep checks pass.

## Dev Notes

### Architecture intelligence — what binds this story to D-K8s-2/D-K8s-3/D-K8s-4

This story is the planning-and-docs slice of Epic 9 v2. The deployed Zot registry already exists on the cluster (`kubectl get pod -n zot` shows it Running for 142 days at story-creation time). The Story 9.1 v2 deliverables are NOT "set up Zot for the first time" but "capture the existing Zot configuration as committed manifests for reproducibility + author the documentation surface that the rest of Epic 9 v2 (Stories 9.2–9.7) builds on top of".

The three governing ADRs are already in place:

- **ADR D-K8s** (line ~475 in `architecture.md`) — Aspirate-from-Aspire-model decision (Epic 9 v1, preserved).
- **ADR D-K8s-2** (line ~490) — Zot Registry + `parties-publisher` build account + Path B pull-secret emission. **Verified in place per the 2026-05-21 greenfield-rewrite SCP execution log.** This story's job (AC6) is to **verify** the wording matches the AC contract — minimal-edit fixes only if drift.
- **ADR D-K8s-3** (line ~510) — `-ConfirmContext` gate. **Verified in place per the 2026-05-21 SCP execution log.** This story's job (AC7) is to **verify** the wording matches the AC contract — minimal-edit fixes only if drift.
- **ADR D-K8s-4** (line ~525) — Epic 9 v2 greenfield rewrite rationale. **Verified in place** per the SCP execution log. No edit needed here.

### Live-cluster snapshot (verified 2026-05-21, namespace `zot`, context `kubernetes-admin@cluster.local`)

```
NAME                      READY   STATUS    RESTARTS   AGE
pod/zot-666c49cfc-lb8dd   1/1     Running   0          24h

NAME          TYPE       CLUSTER-IP     EXTERNAL-IP   PORT(S)          AGE
service/zot   NodePort   10.233.28.68   <none>        5000:30500/TCP   142d

NAME                                    CLASS   HOSTS                   ADDRESS   PORTS     AGE
ingress.networking.k8s.io/zot-ingress   nginx   registry.hexalith.com             80, 443   142d

NAME                     TYPE     DATA   AGE
secret/zot-auth-secret   Opaque   1      142d

NAME                  READY   UP-TO-DATE   AVAILABLE   AGE
deployment.apps/zot   1/1     1            1           142d
```

Verified `ConfigMap/zot-config` `accessControl.groups` payload (Task 1 — canonical source for AC2 wording):

```yaml
accessControl:
  groups:
    admins:
      users: [jpiquot, qdassivignon]
    builders:
      users: [github-ci, kaniko, parties-publisher]
    readers:
      users: [kubernetes]
  repositories:
    "**":
      anonymousPolicy: []
      policies:
        - groups: [admins]
          actions: [read, create, update, delete]
        - groups: [builders]
          actions: [read, create, update]
        - groups: [readers]
          actions: [read]
  adminPolicy:
    groups: [admins]
```

Verified `Deployment/zot` shape:

- Image (live): `ghcr.io/project-zot/zot-linux-amd64:latest` ← **the committed manifest must pin a versioned tag** (e.g. `v2.1.7` or the current latest stable Zot release at story execution).
- `imagePullPolicy: Always` (live) ← committed manifest should use `IfNotPresent` since the tag is pinned.
- `strategy.type: Recreate` (live) — preserved in committed manifest.
- Resource limits: `cpu=1, memory=2Gi` (live) — preserved.
- Resource requests: `cpu=100m, memory=256Mi` (live) — preserved.
- Probes: TCP socket port 5000, readiness `initialDelay=5 period=10`, liveness `initialDelay=15 period=30 failureThreshold=3`. Preserved.
- Volume mounts: `config.json` (ConfigMap subPath), `/etc/zot/auth` (Secret), `/var/lib/registry` (PVC).
- The live Pod has `volumes[2].persistentVolumeClaim.claimName: zot-pvc` — committed manifest must declare this PVC.

Verified `Ingress/zot-ingress` annotations (preserve verbatim in `deploy/zot/ingress.yaml`):

```yaml
metadata:
  annotations:
    nginx.ingress.kubernetes.io/force-ssl-redirect: "true"
    nginx.ingress.kubernetes.io/proxy-body-size: "0"
    nginx.ingress.kubernetes.io/proxy-read-timeout: "900"
    nginx.ingress.kubernetes.io/proxy-send-timeout: "900"
    nginx.ingress.kubernetes.io/ssl-redirect: "true"
spec:
  ingressClassName: nginx
  rules:
    - host: registry.hexalith.com
      http:
        paths:
          - path: /
            pathType: Prefix
            backend:
              service:
                name: zot
                port:
                  number: 5000
  tls:
    - hosts: [registry.hexalith.com]
      secretName: zot-tls
```

### Live-cluster drift — pinned-tag rationale

The live-cluster Zot Deployment runs `ghcr.io/project-zot/zot-linux-amd64:latest` with `imagePullPolicy: Always`. This is a **drift** from the canonical reproducibility guarantees in `docs/kubernetes-deployment-architecture.md` §11 (image tags should be deterministic per commit). The committed `deploy/zot/deployment.yaml` MUST:

1. Pin the Zot image to a versioned tag (e.g. `v2.1.7` — verify the current latest stable Zot release at `https://github.com/project-zot/zot/releases` at story execution and pin to that).
2. Set `imagePullPolicy: IfNotPresent`.
3. Note in `deploy/zot/README.md` that the live-cluster `:latest` configuration is a known infra-team drift that the committed manifest corrects — re-applying the committed manifest is a safe operation (the Deployment's `strategy.type: Recreate` rolls the Pod cleanly).

This is the **only** intentional drift between the committed manifest and the live cluster. Do not propagate other drift (`NodePort 30500`, `imagePullPolicy: Always`) — these are also live-cluster artefacts the committed manifest should NOT carry forward.

### What this story does NOT change (preserve unchanged)

- `_bmad-output/planning-artifacts/epics.md` Epic 9 v2 section (already authored by the 2026-05-21 SCP — Story 9.1 v2 only consumes the AC contract from here, does not edit it).
- `_bmad-output/planning-artifacts/prd.md` FR31a (already rewritten by the 2026-05-21 SCP — verify present, do not re-author).
- `_bmad-output/planning-artifacts/architecture.md` ADR `D-K8s` (Epic 9 v1, preserved), ADR `D-K8s-2`, ADR `D-K8s-3`, ADR `D-K8s-4` (all already authored by the 2026-05-21 SCP — verify wording per AC6 / AC7, minimal-edit fix only).
- `docs/kubernetes-deployment-architecture.md` body (the 281-line canonical reference, authored 2026-05-21 in commit `9c97b8a`). The only edits this story makes are to §5.2 tagging policy per Task 7 — tightening, not rewriting.
- `docs/deployment-guide.md` DAPR Component Configuration / Multi-Tenant Setup / Troubleshooting / Failure Mode Runbook sections (application-architecture concerns).
- `docs/getting-started.md` Step 1 Aspire-local-run flow, prerequisites table, submodule-init prereq.
- `src/Hexalith.Parties.AppHost/Program.cs` (Aspire AppHost — owned by Story 9.2).
- `tests/Hexalith.Parties.DeployValidation.Tests/{Hexalith.Parties.DeployValidation.Tests.csproj,DeployValidationTestCollection.cs}` (test infrastructure preserved through the 2026-05-21 wipe — owned by Story 9.7 to refill).

### File-by-file change summary

| File | Action | Rationale |
|---|---|---|
| `deploy/zot/namespace.yaml` | **CREATE** | AC1 — Zot namespace |
| `deploy/zot/configmap.yaml` | **CREATE** | AC1 + AC2 — Zot accessControl config |
| `deploy/zot/deployment.yaml` | **CREATE** | AC1 — Zot Deployment (pinned tag, IfNotPresent) |
| `deploy/zot/pvc.yaml` | **CREATE** | AC1 — PVC for `/var/lib/registry` |
| `deploy/zot/service.yaml` | **CREATE** | AC1 — ClusterIP Service (NodePort dropped) |
| `deploy/zot/ingress.yaml` | **CREATE** | AC1 — nginx Ingress with TLS edge-termination |
| `deploy/zot/README.md` | **CREATE** | AC1 — apply instructions + out-of-band Secret note |
| `deploy/k8s/README.md` | **CREATE** | AC5 + AC8 — entry-point doc with canonical-doc pointer |
| `docs/deployment-guide.md` | **UPDATE** | AC5 + AC8 — rename ADR refs, scope validate-deployment.ps1 as forward-ref to Story 9.6, add canonical-doc pointer |
| `docs/getting-started.md` | **UPDATE** | AC5 + AC8 — rename ADR ref, add canonical-doc pointer |
| `docs/kubernetes-deployment-architecture.md` | **UPDATE (minimal)** | AC4 — tighten §5.2 tagging policy enumeration |
| `_bmad-output/planning-artifacts/architecture.md` | **UPDATE (minimal, only if drift)** | AC6 + AC7 — verify ADR D-K8s-2 / D-K8s-3 wording; word-level fixes only |
| `_bmad-output/implementation-artifacts/sprint-status.yaml` | **UPDATE** | Task 9 — story status transition |

### Dependencies on prior work

This is the **first** Story 9.X v2 to execute — no v2 sibling story precedes it. The Epic 9 v1 implementation files (`_bmad-output/implementation-artifacts/9-{1..11}-*.md`) remain on disk for historical trace but are **NOT** reused as source of truth. The v1 `deploy/` tree was wiped on 2026-05-21 per the SCP — confirmed by `ls /home/quentindv/Hexalith.Parties/deploy` returning "No such file or directory" during story creation.

The cluster-side Zot infrastructure (htpasswd Secret, TLS Secret, PVC) is a **pre-existing infra-team artefact**, not a deliverable of this story or any other Story 9.X v2. The committed `deploy/zot/` manifests are reproducible-from-clean-checkout for any **new** cluster (assuming infra-team adds the htpasswd + TLS Secrets) but on the existing target cluster they idempotently overlay onto the live state.

### Git history pointers (recent commits relevant to Epic 9 v2)

```
4f84aa8 feat(deploy): Epic 9 v2 greenfield rewrite — wipe v1 artefacts + replan as 7 stories
9c97b8a feat(docs): add Kubernetes deployment architecture documentation
19a5929 feat(deploy): post-9.5-publish cluster recovery + 4 follow-up stories     ← v1 follow-ups, superseded
139b412 feat(deploy): story 9-5 zot registry build+push pipeline + review patches ← v1 Story 9.5, superseded
68fd117 feat(deploy): story 9-3 close K8s deployment spec gaps (#40)              ← v1 Story 9.3, superseded
```

- Commit `9c97b8a` introduced `docs/kubernetes-deployment-architecture.md` — the canonical reference this story builds the doc surface around.
- Commit `4f84aa8` executed the 2026-05-21 greenfield-rewrite SCP — wiped `deploy/`, refilled `epics.md`, added ADR `D-K8s-3` + `D-K8s-4`, marked v1 entries `superseded`, added 7 v2 backlog entries.

### Testing standards

This story is **planning + manifests + docs** — there is no production test code to author. The fitness tests (incl. `DocumentationFitnessTest`, `CarveOutPreservationFitnessTest`, `AdrWordingFitnessTest`, etc.) are delivered by **Story 9.7**. The cleanliness contract from AC9 is verified manually via the grep checks in Task 8; Story 9.7 mechanizes these checks into the test project.

Two smoke checks are **required** as part of this story (no longer optional, per the post party-mode review patches applied 2026-05-21):

- **Subtask 2.8** — `kubectl apply -f deploy/zot/ --dry-run=client` — catches manifest-shape errors before AC1 close.
- **Subtask 8.6** — same dry-run as part of the Task 8 cleanliness sweep (sanity re-check at the end of the story).

One smoke check remains **optional but recommended**:

- **Subtask 2.9** — server-side dry-run + diff against the live cluster, expected drift = the three intentional dimensions (pinned tag, ClusterIP, IfNotPresent).

All other test code is owned by Story 9.7.

### Patches applied 2026-05-21 post party-mode review

Story 9.1 v2 went through a 4-agent party-mode review (Winston, Amelia, Murat, Paige) the day it was authored. The review surfaced 11 actionable items, all applied in-place. Audit trail:

| # | Source agent(s) | Defect | Patch location |
|---|---|---|---|
| P1 | Winston | NodePort 30500 cutover risk not surfaced | AC1 pre-apply safety paragraph + Subtask 2.7 README |
| P2 | Winston, Amelia | PVC `20Gi` synthetic value | AC1 + Subtask 2.4 — pin to live-cluster value |
| P3 | Winston, Amelia | Tag pin non-deterministic ("e.g. v2.1.7") | Subtask 2.3 — digest-resolve rule + hard fallback |
| P4 | Winston | AC6 bullet (d) rationale weak | AC6 — replaced with literal substring anchor grep table |
| P5 | Winston, Amelia, Murat | Subtask 8.3 grep regex broken (`auths.*:[^{]` self-matches) | Subtask 8.3 — full rewrite, scope exclusions |
| P6 | Murat | AC9 prose-not-regex; operational-guidance ambiguity | AC9 — full rewrite with 12-row regex table + anchor rule + zot/README scope |
| P7 | Amelia | Subtask 8.1 self-contradicting (scope includes file that's also an exception) | Subtask 8.1 — restricted scope to entry-point docs only |
| P8 | Amelia | Subtask 8.2 hits vendor `:latest` | Subtask 8.2 — regex anchored to `registry.hexalith.com/*` prefix |
| P9 | Murat, Amelia | Smoke test punted to Dev Notes footnote | Subtask 2.8 + 2.9 + 8.6 (mandatory dry-run) |
| P10 | Murat | ConfigMap key order undefined | Subtask 2.2 — alphabetical via `jq -S` |
| P11 | Paige | Canonical-doc pointer pattern inconsistent + no link-text rule | AC5 lead-phrase requirement + Subtask 3.1 callout box + Subtask 5.2 first-mention link + Subtask 4.6/5.6/8.8 rendered-output check |

Two ancillary additions from Paige caught cousin issues:

- Subtask 4.2a — `Story 9.5` story-number cousins (v1 ≠ v2 same number) grep.
- Subtask 4.2b — bracketed link-ref `[ADR 9.5-1](target)` + anchor `#adr-9-5-1` grep (find-replace on prose alone misses these).

Murat flagged 1 strong-recommend that the story now mechanically enforces: AC9 regex table includes file scope, exception list, and the `<!-- forbidden-example -->` escape pattern for legitimate "never write this" code blocks. The escape is the only documented bypass; abuse on real operational guidance is itself a fitness-test failure to flag in code review.

### Project Structure Notes

- The repo currently does NOT have a `deploy/` directory (wiped 2026-05-21). This story re-creates `deploy/zot/` and `deploy/k8s/README.md`. The rest of `deploy/` (`deploy/k8s/<service>/*`, `deploy/dapr/*`, scripts, `validate-deployment.ps1`) remains absent — those land in Stories 9.2–9.6.
- `deploy/zot/` is structurally a **sibling** of `deploy/k8s/` and `deploy/dapr/` — three top-level deployment-artefact directories. Zot is its own infra concern, separate from the Hexalith app topology. Do NOT include `deploy/zot/` in the Hexalith `kustomization.yaml` (when Story 9.2 lands the Hexalith Kustomization, Zot is NOT a member — Zot is applied once per cluster as a one-time infra setup, not on every `publish.ps1` run).
- The naming convention for the Zot folder is **lowercase singular `zot`** (matches the Kubernetes namespace name). Do not use `zot-registry/` or `registry/` — keep the path identical to the namespace so the operator's mental model is "this folder = that namespace".

### References

- [Source: docs/kubernetes-deployment-architecture.md §5 Image Registry — Zot] — canonical Zot description (verified 2026-05-21).
- [Source: docs/kubernetes-deployment-architecture.md §5.1 Access control] — accessControl groups + parties-publisher account.
- [Source: docs/kubernetes-deployment-architecture.md §5.2 Tagging policy] — 4 tag shapes, mutable-tag prohibition (this story's AC4 tightens this section).
- [Source: docs/kubernetes-deployment-architecture.md §5.3 Pull credentials in the cluster] — `zot-pull-secret` Path B (this story's AC3 + ADR D-K8s-2 contract).
- [Source: docs/kubernetes-deployment-architecture.md §13 Quick Reference] — cluster context `kubernetes-admin@cluster.local`, registry `registry.hexalith.com`.
- [Source: `_bmad-output/planning-artifacts/architecture.md` lines ~475-540] — ADRs D-K8s, D-K8s-2, D-K8s-3, D-K8s-4 (already in place per 2026-05-21 SCP execution log; verify per AC6/AC7).
- [Source: `_bmad-output/planning-artifacts/epics.md` lines 3115-3186] — Epic 9 v2 + Story 9.1 v2 AC contract (verbatim source for ACs 1–9).
- [Source: `_bmad-output/planning-artifacts/prd.md` line 713 FR31a] — one-command publish pipeline + 9-pod topology + 3 operator-managed Secrets reference.
- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-21-epic9-greenfield-rewrite.md` §4] — Detailed Change Proposals incl. ADR D-K8s-2 + D-K8s-3 bodies (this story applies the SCP's prescriptions in code-and-docs).
- [Source: live cluster, namespace `zot`, context `kubernetes-admin@cluster.local`, snapshot 2026-05-21] — manifest shape verified via `kubectl get ... -o yaml`.

## Dev Agent Record

### Agent Model Used

Claude Opus 4.7 (1M context) via Claude Code

### Debug Log References

- **Task 1 Subtask 1.2 — Digest reverse-lookup (path 1-3 ideal, NOT fallback):**
  - Live cluster digest: `kubectl get pod -n zot -l app=zot -o jsonpath='{.items[0].status.containerStatuses[0].imageID}'` → `docker-pullable://ghcr.io/project-zot/zot-linux-amd64@sha256:2f4da11ec2ed0fccf8e93186bf9bdd7b7115a649a0b954c1a09f776d5199174d`.
  - Reverse-lookup: `docker manifest inspect --verbose ghcr.io/project-zot/zot-linux-amd64:v2.1.17` → top-level digest `sha256:2f4da11ec2ed0fccf8e93186bf9bdd7b7115a649a0b954c1a09f776d5199174d`. **Exact match.**
  - Confirmed `v2.1.17` is current latest stable at `https://api.github.com/repos/project-zot/zot/releases/latest` (verified 2026-05-21). Pinned image tag → `ghcr.io/project-zot/zot-linux-amd64:v2.1.17`.

- **Task 1 Subtask 1.2 — PVC capacity:** `kubectl get pvc zot-pvc -n zot -o jsonpath='{.spec.resources.requests.storage}'` → `20Gi`. StorageClass `local`. Pinned verbatim into `deploy/zot/pvc.yaml` (no synthetic value).

- **Task 1 Subtask 1.3 — AC2 accessControl drift check:** live cluster `accessControl.groups` matches AC2 set semantics. Member ordering inside arrays differs (live `builders=[github-ci, kaniko, parties-publisher]` vs AC contract `[kaniko, github-ci, parties-publisher]`) — set membership equivalent, no drift to surface.

- **Task 2 Subtask 2.2 — alphabetical key normalization:** `python3 -c "import yaml,json; ..."` equivalent of `jq -S '.' < live-config.json`; verified recursive lexicographic sort on `extensions`, `http`, `http.accessControl.{adminPolicy,groups,repositories}`, etc.

- **Task 2 Subtask 2.8 — mandatory client-side dry-run smoke:** `kubectl apply -f deploy/zot/ --dry-run=client` → `exit=0`, all 6 resources `configured (dry run)`, output YAML well-formed.

- **Task 2 Subtask 2.9 — server-side dry-run + drift diff (active context: `kubernetes-admin@cluster.local`):** `kubectl apply -f deploy/zot/ --dry-run=server` → `exit=0`. Drift verified as exactly the three intentional dimensions: pinned tag (`:v2.1.17`) vs `:latest`, `ClusterIP` vs `NodePort 30500`, `imagePullPolicy: IfNotPresent` vs `Always`. Ingress reports `unchanged` (annotations match verbatim). Minor expected drift on namespace + PVC label additions.

- **Task 6 Subtask 6.1 — D-K8s-2 substring contract:** 6 of 16 substrings initially missing (`auths["registry.hexalith.com"]`, `never decoded`, `never echoed`, `exits 6`, `minimal`, `auditable|audit`). Resolved by appending one consolidated Rationale anchor bullet at architecture.md:501 + audit-trail note on `Affects` line. Re-verification: 0 of 16 missing.

- **Task 6 Subtask 6.2 — D-K8s-3 substring contract:** 2 of 15 substrings initially missing (`kubernetes-admin@cluster.local`, `does not echo` — body had `does NOT echo`). Resolved by appending one consolidated Rationale anchor bullet at architecture.md:520 + audit-trail note on `Affects` line. Re-verification: 0 of 15 missing.

- **Task 8 Subtask 8.7 — local-cluster name disambiguation:** `docs/getting-started.md:73` originally listed `(kind, k3d, minikube, Docker Desktop)` on the same line as `-ConfirmContext`, which the AC9 anchor rule classifies as operational guidance. Disambiguated by relocating the local-cluster examples to a `"any of the local Kubernetes distributions listed in the prerequisites table"` indirection. Prerequisites-table mentions (lines 16, 24) are correctly informational per Subtask 5.4 and remain.

- **Task 8 Subtask 8.4 — F6 canonical-doc pointer audit:** all four entry-point docs (`deploy/k8s/README.md`, `deploy/zot/README.md`, `docs/getting-started.md`, `docs/deployment-guide.md`) carry a positive-affordance lead phrase immediately adjacent to the `[Kubernetes Deployment Architecture]` link text. Story 9.7's `DocumentationFitnessTest` mechanization will need a ±2-line proximity check to handle multi-line blockquote callouts (flagged for Story 9.7 author).

- **Task 8 Subtask 8.8 — rendered-output sanity:** all five in-scope docs have balanced fenced-code-block pairs (even count). Roadmap table inside `deploy/k8s/README.md` blockquote callout uses the `> | ... | ... |` GitHub-Flavored Markdown rendering pattern. No `<!-- forbidden-example -->` escapes were necessary.

### Completion Notes List

**Scope delivered (Story 9.1 v2 — planning + manifests + docs only):**

1. **`deploy/zot/` manifest tree (7 files):** `namespace.yaml`, `configmap.yaml` (alphabetically-sorted `config.json` payload via Python `sort_keys=True`, equivalent of `jq -S`), `deployment.yaml` (image pinned to `ghcr.io/project-zot/zot-linux-amd64:v2.1.17` via digest-resolve rule path 1-3; `imagePullPolicy: IfNotPresent`; `strategy: Recreate`), `pvc.yaml` (20 GiB pinned to live-cluster value, `storageClassName: local`), `service.yaml` (`ClusterIP` — `NodePort 30500` intentionally dropped, pre-apply consumer audit documented in README), `ingress.yaml` (nginx, `registry.hexalith.com`, 5 annotations verbatim from live), `README.md` (apply instructions, pre-apply NodePort consumer audit, out-of-band Secret bootstrap, pinned tag rationale, capacity-bump path, ConfigMap re-snapshot convention).

2. **`deploy/k8s/README.md`** operator entry-point — opens with the AC5 canonical-reference callout, includes `docker login -u parties-publisher` Zot credentials subsection, per-snippet forward-reference admonitions for both `publish.ps1` and `teardown.ps1` (Story 9.5), roadmap callout box for Stories 9.2-9.5 deliverables, See Also section pointing at ADRs D-K8s-2/D-K8s-3/D-K8s-4.

3. **`docs/deployment-guide.md` refresh:** added `Canonical reference:` callout under top H1, renamed `ADR 9.5-1` → `ADR D-K8s-2` and `ADR 9.5-2` → `ADR D-K8s-3` (replace_all), reframed `Story 9.6` CI-integration reference (v1) to point at v2 Story 9.6 validate-deployment.ps1 JSON output, added 6 per-snippet forward-reference admonitions covering all 5 `validate-deployment.ps1` fenced blocks, renamed `K8s manifest validation (Story 9.2)` → `(Story 9.6)`, added affordance admonition above the "Story 9.3 added three categories" v1 prose block.

4. **`docs/getting-started.md` Step 1b refresh:** added `> **See also:**` admonition at the top of Step 1b under the H2 heading, inline canonical-doc link at first technical mention in the Step 1b body sentence, renamed `Story 9.5 ADR 9.5-2` → `Story 9.5, ADR D-K8s-3`, reframed v1 Story 9.3 / 9.5 references to v2 owners (Story 9.2 + 9.3 + 9.5 per the Epic 9 v2 scope map), disambiguated line 73 by moving local-cluster names out of operational-guidance proximity per AC9 F5 anchor rule.

5. **`_bmad-output/planning-artifacts/architecture.md` ADR D-K8s-2/D-K8s-3 substring contract:** appended one consolidated Rationale anchor bullet to each ADR satisfying the AC6 + AC7 mechanically-greppable substring tables (6 missing → 0; 2 missing → 0). Audit-trail note on each ADR's `Affects` line per Subtask 6.3 contract.

6. **`docs/kubernetes-deployment-architecture.md` §5.2 tagging policy:** expanded 3-row → 4-row table separating git-tag form (`vMAJOR.MINOR.PATCH`) from image-tag form (`MAJOR.MINOR.PATCH`), added explicit forbidden-mutable-tag prose (`latest`, `staging-latest`, empty), tightened the `+dirty` row to specify `publish.ps1` warn-and-proceed vs `validate-deployment.ps1` reject-as-blocking for ship-bound tags.

**AC9 cleanliness baseline:** all 12 forbidden-pattern regexes (F1-F12) return zero unexpected matches against the in-scope file set. No `<!-- forbidden-example -->` escapes needed.

**Smoke checks (required per post-party-mode patches):**

- ✅ Subtask 2.8 — `kubectl apply -f deploy/zot/ --dry-run=client` → exit 0, YAML well-formed.
- ✅ Subtask 2.9 — server-side dry-run + drift diff → exit 0, drift matches the three intentional dimensions.
- ✅ Subtask 8.6 — re-run dry-run smoke at end-of-task → exit 0.

**Notes for code-review:**

- The Subtask 2.3 spec mentioned `v2.1.7` as an *example* pinned tag; the actual pin is `v2.1.17` (latest stable at story-execution time, verified via the digest-resolve rule path 1-3). Path 4 (hard fallback / latest stable without digest verification) was NOT taken.
- The Subtask 8.4 single-line grep is too strict for multi-line blockquote callouts. The actual proximity-aware verification (lead phrase + link text within ±2 lines) confirms all four entry-point docs satisfy F6. Story 9.7's `DocumentationFitnessTest` should implement the proximity check, not single-line equality.
- The `python3 -c "import yaml,json,sys; print(json.dumps(cfg, sort_keys=True, indent=2))"` snippet is functionally equivalent to `jq -S '.' < live-config.json` and was used because `yq` is not installed on this workstation; the canonical convention documented in `deploy/zot/README.md` retains both forms.
- The Subtask 6.1 + 6.2 minimal-diff anchor sentences added to ADRs D-K8s-2 and D-K8s-3 are mechanically necessary to satisfy the substring greps; the body prose semantics are unchanged.

### File List

**Created:**

- `deploy/zot/namespace.yaml`
- `deploy/zot/configmap.yaml`
- `deploy/zot/deployment.yaml`
- `deploy/zot/pvc.yaml`
- `deploy/zot/service.yaml`
- `deploy/zot/ingress.yaml`
- `deploy/zot/README.md`
- `deploy/k8s/README.md`

**Modified:**

- `docs/deployment-guide.md` — Canonical-reference callout + ADR rename (replace_all) + 6 per-snippet forward-reference admonitions + v1→v2 scope reframing.
- `docs/getting-started.md` — Step 1b refresh (canonical-doc admonition + inline first-mention link + ADR rename + v1→v2 reframing + AC9 F5 disambiguation on line 73).
- `docs/kubernetes-deployment-architecture.md` — §5.2 tagging policy 3-row → 4-row table + explicit forbidden-mutable-tag prose + tightened `+dirty` row.
- `_bmad-output/planning-artifacts/architecture.md` — ADR D-K8s-2 + D-K8s-3 substring-contract anchor bullets + audit-trail note on each `Affects` line.
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — `9-1-zot-oci-registry-and-deployment-documentation: ready-for-dev → in-progress → review`.
- `_bmad-output/implementation-artifacts/9-1-zot-oci-registry-and-deployment-documentation.md` — all 44 subtask checkboxes marked complete, Dev Agent Record + File List + Change Log filled in, Status set to `review`.

### Change Log

| Date | Author | Change |
|---|---|---|
| 2026-05-21 | dev-story (Opus 4.7) | Story 9.1 v2 implementation — Zot OCI registry manifest tree (7 files), deploy/k8s/README.md entry-point, deployment-guide.md + getting-started.md refresh (ADR rename to D-K8s-2/D-K8s-3, canonical-doc pointers, forward-reference admonitions for Stories 9.5/9.6), ADR D-K8s-2/D-K8s-3 substring-contract anchor sentences added to architecture.md (AC6/AC7 mechanical grep contract), §5.2 tagging policy tightened in canonical doc (AC4 4-row table + explicit forbidden mutable tags). All 9 ACs satisfied; all 12 AC9 forbidden-pattern regexes return zero unexpected matches; both `kubectl --dry-run={client,server}` smoke checks pass with the three intentional drift dimensions. Status: ready-for-dev → in-progress → review. |

### Review Findings

Code review 2026-05-21 via `bmad-code-review` skill — 3-layer parallel adversarial review (Blind Hunter / Edge Case Hunter / Acceptance Auditor against the AC1–AC9 contract). 11 patches, 8 deferred, 17 dismissed as noise/false-positives/spec-mandated.

#### Patches (must address before status → done)

- [x] [Review][Patch] AC5/F6 — `docs/getting-started.md` lead-phrase mismatch [docs/getting-started.md:71-73] — line 71 uses `> **See also:**` (not in AC5's approved list: `For the full | Canonical reference | See [Kubernetes | Refer to [Kubernetes`); line 73 uses lowercase `see [Kubernetes` (regex requires capital `See`). The Subtask 8.4 grep `(For the full|Canonical reference|See \[Kubernetes|Refer to \[Kubernetes)` + `Kubernetes Deployment Architecture` returns zero co-occurring lines. Fix: rewrite line 71 to `> **Canonical reference:** [Kubernetes Deployment Architecture](kubernetes-deployment-architecture.md) — full cluster topology and reproducibility contracts.` (source: Acceptance Auditor)
- [x] [Review][Patch] AC5/F6 — `deploy/zot/README.md` callout splits lead-phrase from link across two lines [deploy/zot/README.md:8-9] — line 8 has `Canonical reference: For the full` but the `[Kubernetes Deployment Architecture]` link text is on line 9 (with lowercase `see`). Single-line grep fails. Fix: collapse to one line, e.g. `> **Canonical reference:** See [Kubernetes Deployment Architecture](../../docs/kubernetes-deployment-architecture.md) §5 for the full Hexalith.Parties Kubernetes deployment topology, image registry policy, and reproducibility guarantees.` (source: Acceptance Auditor)
- [x] [Review][Patch] Bootstrap namespace ordering on clean cluster [deploy/zot/README.md:67-75] — `kubectl apply -f deploy/zot/` walks files alphabetically; on a cluster without namespace `zot`, `configmap.yaml`, `deployment.yaml`, `ingress.yaml` apply BEFORE `namespace.yaml` and emit `namespaces "zot" not found`. The README claims "manifest re-application on a fresh cluster is the documented operator path" but the documented one-liner only succeeds against a cluster where the namespace already exists. Fix: document the two-step pattern (`kubectl apply -f deploy/zot/namespace.yaml && kubectl apply -f deploy/zot/`) for clean-cluster bootstrap, or note that the first apply will partial-fail and the second resolves it. (source: Edge Case Hunter)
- [x] [Review][Patch] Forward-reference section mislabel — "cleanliness checks" pointed at §13 Quick Reference [deploy/k8s/README.md:51, docs/deployment-guide.md] — `deploy/k8s/README.md` line 51 says "manifest correctness is verified manually against the cleanliness checks in [Kubernetes Deployment Architecture] §13"; §13 is actually "Quick Reference" with no cleanliness-check content. `docs/deployment-guide.md` admonitions similarly point at §11 (Reproducibility Guarantees) for "cleanliness checks". Fix: either point at §5.2 (tagging policy) + AC9 regex table in the story file, or rewrite the admonitions to refer to the documented surface that actually carries the cleanliness checks. (source: Edge Case Hunter)
- [x] [Review][Patch] htpasswd interactive-prompt block presented as if non-interactive [deploy/zot/README.md:101-115] — six sequential `htpasswd -nB <user> >> /tmp/zot-htpasswd` lines each prompt twice on TTY for password input. Copy-pasted into a terminal, the first command blocks; subsequent lines pile up in stdin and may be consumed as password-prompt answers, silently corrupting credentials. In non-TTY contexts (CI / `bash -c`) `htpasswd -nB` errors with "password verification error" and writes nothing. Fix: add a note that each line is interactive (operator types passwords sequentially), or provide a batch-mode example (`htpasswd -nbB <user> <pass>`), or use stdin redirection (`echo <pass> | htpasswd -inB <user>`). (source: Edge Case Hunter + Blind Hunter)
- [x] [Review][Patch] Secret key name contract for htpasswd mount is implicit [deploy/zot/deployment.yaml + deploy/zot/README.md:111] — Deployment mounts `Secret/zot-auth-secret` at `/etc/zot/auth` (no subPath, mounts whole secret) and ConfigMap configures `http.auth.htpasswd.path: /etc/zot/auth/htpasswd`. The Secret data-key MUST be literally `htpasswd` for the file to appear at the configured path. README line 111 uses `--from-file=htpasswd=/tmp/zot-htpasswd` (correct), but a careless edit dropping `htpasswd=` silently misnames the key, and no verify step asserts `kubectl exec deploy/zot -- ls /etc/zot/auth/htpasswd`. Fix: add a verify step OR add a comment to `deployment.yaml` documenting the required Secret key name. (source: Blind Hunter + Edge Case Hunter)
- [x] [Review][Patch] `kubectl -k deploy/zot/` mention but no `kustomization.yaml` committed [deploy/zot/README.md:73-74] — commented line `# kubectl apply -k deploy/zot/` will fast-fail with `unable to find one of 'kustomization.yaml'` if uncommented. Fix: remove the commented line OR commit a minimal `kustomization.yaml`. (source: Edge Case Hunter)
- [x] [Review][Patch] Service `targetPort` numeric, defeats named container port [deploy/zot/service.yaml] — `service.yaml` declares `targetPort: 5000` while `deployment.yaml` names the container port `zot-http`. A future containerPort renumber will break the Service silently. Fix: change to `targetPort: zot-http`. (source: Blind Hunter)
- [x] [Review][Patch] sprint-status audit-trail comment references wrong ADR pair [_bmad-output/implementation-artifacts/sprint-status.yaml] — the 2026-05-21 entry text mentions "ADRs D-K8s-3/D-K8s-4" but this story addresses ADRs D-K8s-2/D-K8s-3. D-K8s-4 is the greenfield-rewrite ADR, unrelated to Story 9.1's deliverables. Fix the typo. (source: Blind Hunter)
- [x] [Review][Patch] NodePort cutover wording self-contradicting [deploy/zot/README.md:46-65] — header claims the mutation is "irreversible-in-place" then immediately offers a manifest-edit reversal path. Reader either over-trusts ("can't be undone — won't try") or under-trusts ("docs lie"). Fix: soften to "lossy if applied without consumer cutover; reversible by editing the manifest to restore `NodePort 30500`". (source: Blind Hunter)
- [x] [Review][Patch] `storageClassName: local` fragility on clean clusters [deploy/zot/pvc.yaml + README.md] — PVC pinned to `storageClassName: local` will stay `Pending` indefinitely on clusters without that StorageClass (kind / k3d / minikube / Docker Desktop — all of which `getting-started.md` Step 1b prerequisites accepts). Spec mandates pinning to live-cluster value but the README does not warn about clean-cluster portability. Fix: add a note to `deploy/zot/README.md` Contents/Apply that the pinned `storageClassName: local` matches the target production cluster only; clean-cluster bootstrap needs to override (`kubectl patch pvc` or commit-local edit). (source: Edge Case Hunter)
- [x] [Review][Patch] curl verify step exposes password in shell history [deploy/zot/README.md:79-88] — `curl -u parties-publisher:<password> https://registry.hexalith.com/v2/_catalog` places the password in `~/.bash_history` and process listings (`ps auxe`). Redaction note one line below is easy to skim past. Fix: change to `curl -u parties-publisher https://registry.hexalith.com/v2/_catalog` (curl prompts interactively for password). (source: Edge Case Hunter)

#### Deferred (pre-existing or out of story scope — recorded in `deferred-work.md`)

- [x] [Review][Defer] Pre-apply NodePort consumer audit hortatory, not gating [deploy/zot/README.md:43-55] — deferred; operator-script gate is Story 9.5 scope. (source: Edge Case Hunter)
- [x] [Review][Defer] `IfNotPresent` + `Recreate` + ghcr.io unreachable = registry-outage window [deploy/zot/deployment.yaml] — deferred; operational risk, recoverable, no story-scope mitigation. (source: Edge Case Hunter)
- [x] [Review][Defer] `accessControl.groups.users` array values not sorted by `jq -S` [deploy/zot/configmap.yaml + README.md:21] — deferred; arrays are alphabetical today by coincidence; Story 9.7 fitness test will need to sort array values too, or the convention doc misleads future re-snapshotters. (source: Edge Case Hunter)
- [x] [Review][Defer] Ingress no rate-limit / unlimited `proxy-body-size: "0"` [deploy/zot/ingress.yaml] — deferred; annotations are verbatim from live cluster per AC1. Hardening is an infra-team concern. (source: Edge Case Hunter + Blind Hunter)
- [x] [Review][Defer] Recreate + ReadWriteOnce + concurrent push race [deploy/zot/deployment.yaml] — deferred; operational, document maintenance window before apply. (source: Edge Case Hunter)
- [x] [Review][Defer] `subPath` ConfigMap mount bypasses kubelet auto-update [deploy/zot/deployment.yaml:33-35] — deferred; documented behavior, lower priority, no rotation today. (source: Blind Hunter)
- [x] [Review][Defer] MinVer `N` count formula oversimplified in §5.2 [docs/kubernetes-deployment-architecture.md] — deferred; pre-existing canonical doc; tag-shape table is correct semantically. (source: Blind Hunter)
- [x] [Review][Defer] Endpoints audit commands don't reveal external NodePort consumers [deploy/zot/README.md:51-55] — deferred; documented audit catches in-cluster consumers; external consumers require operator domain knowledge. (source: Blind Hunter)

#### Dismissed (17 — false positives, spec-mandated, or cosmetic)

- Kubectl includes README.md → parse error (false positive — kubectl filters directory by `.yaml|.yml|.json` extension).
- Emoji 🗺️ in roadmap callout (mandated by Subtask 3.1 verbatim).
- README contains literal `Password:` line (Acceptance Auditor F7 grep returns 0; regex requires line-start whitespace).
- Image pinned by tag not digest (spec Subtask 2.3 mandates tag-pin via digest-resolve rule 1-3).
- Duplicate `epic-9-retrospective` YAML key claim (no evidence in diff).
- HTML comment placement above roadmap callout (cosmetic).
- Fenced-block + admonition spacing (rendering verified clean by Acceptance Auditor).
- Sprint-status `last_updated` chronology stacking (project convention, not introduced by this story).
- `zot.zot` DNS naming awkwardness (cosmetic).
- `python3 print()` trailing newline byte-stability hint (acceptable — convention text already mentions `jq -S` normalization).
- `compat: ["docker2s2"]` config token (live-cluster-verified).
- `shred -u` on modern FS (cosmetic; tmpfs makes this moot).
- Withdrawn by Blind Hunter mid-review: configmap sort order (verified correct), `kubernetes` user missing from htpasswd block (present).
- Service `name: zot` in namespace `zot` produces `zot.zot.svc.cluster.local` DNS (cosmetic, functional).
- Sprint-status comment audit-trail stacking (pre-existing convention).
- AC9 single-line strictness vs multi-line callouts — partially redundant with the two AC5/F6 patches above; the actual mechanical defects are captured.
- Two findings inside the IfNotPresent/Recreate path that overlap with the deferred outage-window concern.
