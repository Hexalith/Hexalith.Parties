# Story 9.4: Dapr Control Plane, Components, Access Control & Subscriptions

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story Boundary

Story 9.4 is a **declarative manifest story only**. It creates and validates the Dapr CR files that become Source 2 in `docs/kubernetes-deployment-architecture.md`; it does not apply those files to a cluster, install Dapr, patch generated Deployments, or prove runtime traffic behavior.

Ownership split:

| Story | Owns | Must not claim |
|---|---|---|
| 9.4 | `deploy/dapr/*.yaml`, static/schema validation, exact app-id/topic/route handoff, README status update | Cluster installation, ordered apply automation, live sidecar behavior |
| 9.5 | `publish.ps1`, `teardown.ps1`, `dapr init -k`, Components -> Resiliency -> Configurations -> Subscriptions apply order, post-Aspirate `dapr.io/config` annotation patching | Re-designing the Dapr CR topology |
| 9.6 | Static lint tool and CI JSON output | Changing the accepted Dapr topology |
| 9.7 | Runtime/live-cluster fitness tests | Replacing 9.4 manifest acceptance |

## Story

As a developer relying on Dapr for state, pub/sub, and service invocation across the 5 daprd-equipped Hexalith services,
I want the Dapr control plane installed cluster-wide and the project-specific Components, Access Control configurations, and declarative Subscriptions hand-authored under `deploy/dapr/` outside the Aspirate composition,
so that the security-sensitive Dapr surface is explicit, reviewable, deny-by-default, and not dependent on Aspirate emission defaults.

## Scope & Non-Scope

**This story delivers:**

- New root production Dapr folder `deploy/dapr/` with the exact project CR set required by Epic 9 v2.
- `deploy/dapr-alternatives/` only if alternative backend templates are moved or created; it must not be applied by Story 9.5.
- Redis-backed Dapr `statestore` and `pubsub` Components targeting the Story 9.3 `redis` Service at `redis:6379` with no Redis password.
- Five deny-by-default Dapr `Configuration` CRs for `eventstore`, `eventstore-admin`, `parties`, `tenants`, and `memories`.
- Two declarative `Subscription` CRs for party events and tenant lifecycle events.
- `resiliency.yaml` suitable for server-side dry-run before apply.
- `deploy/k8s/README.md` roadmap/status update showing Story 9.4 delivered.
- Local static validation of the CR files using deploy-validation tests, YAML parsing, `kubectl apply --dry-run=client --validate=false`, and server-side dry-run where a Dapr-enabled cluster is available.

**This story does not deliver:**

| Out-of-scope | Owned by |
|---|---|
| `publish.ps1`, `teardown.ps1`, `-ConfirmContext`, `dapr init -k` execution, ordered apply implementation, and skip flags | Story 9.5 |
| Static lint tool `deploy/validate-deployment.ps1` and JSON CI output | Story 9.6 |
| Deployment fitness tests and live-cluster integration tests | Story 9.7 |
| Redis PVC, Redis AUTH, Redis replication, network policy, Dapr mTLS, Dapr secret stores, non-Redis state/pubsub production backends | Deferred per `docs/kubernetes-deployment-architecture.md` section 12 |
| Reworking the local AppHost `DaprComponents` run-mode files unless a direct mismatch must be documented | Out of scope unless required for parity notes |

## Required File Contract

The Epic 9 v2 source requires a flat root `deploy/dapr/` file set. Do **not** introduce `deploy/dapr/components/`, `deploy/dapr/configuration/`, `deploy/dapr/subscriptions/`, or `deploy/dapr/resiliency/` subfolders in this story unless `epics.md` is changed first.

| Path | Kind | Purpose | Action | Owner |
|---|---|---|---|---|
| `deploy/dapr/statestore.yaml` | Dapr `Component` | Redis-backed actor/shared state | New | 9.4 |
| `deploy/dapr/pubsub.yaml` | Dapr `Component` | Redis Streams pub/sub | New | 9.4 |
| `deploy/dapr/resiliency.yaml` | Dapr `Resiliency` | Retry, timeout, circuit-breaker policy | New | 9.4 |
| `deploy/dapr/accesscontrol.yaml` | Dapr `Configuration` | Receiving-sidecar ACL for `eventstore` | New | 9.4 |
| `deploy/dapr/accesscontrol-eventstore-admin.yaml` | Dapr `Configuration` | Receiving-sidecar ACL for `eventstore-admin` | New | 9.4 |
| `deploy/dapr/accesscontrol-parties.yaml` | Dapr `Configuration` | Receiving-sidecar ACL for `parties` | New | 9.4 |
| `deploy/dapr/accesscontrol-tenants.yaml` | Dapr `Configuration` | Receiving-sidecar ACL for `tenants` | New | 9.4 |
| `deploy/dapr/accesscontrol-memories.yaml` | Dapr `Configuration` | Receiving-sidecar ACL for `memories` | New | 9.4 |
| `deploy/dapr/subscription-parties.yaml` | Dapr `Subscription` | Party-event sample/reference subscription | New | 9.4 |
| `deploy/dapr/subscription-tenants.yaml` | Dapr `Subscription` | Tenant lifecycle delivery to Parties | New | 9.4 |
| `deploy/k8s/README.md` | Markdown | Roadmap/status and Story 9.5 handoff | Update | 9.4 |

## Invocation And Event Contracts

Use this matrix while authoring ACL and subscription files. Do not guess app ids or routes.

### Dapr-Equipped App IDs

| App id | Dapr-equipped | Configuration name | Notes |
|---|---:|---|---|
| `eventstore` | Yes | `accesscontrol` | EventStore gateway/projection host |
| `eventstore-admin` | Yes | `accesscontrol-eventstore-admin` | Admin command server; inbound peer invocation denied unless proven |
| `parties` | Yes | `accesscontrol-parties` | Actor host; `POST /process` is internal Dapr service-invocation plumbing |
| `tenants` | Yes | `accesscontrol-tenants` | Tenant service |
| `memories` | Yes | `accesscontrol-memories` | Memories service host |
| `eventstore-admin-ui` | No | none | Must not receive Dapr annotations/scopes |
| `parties-mcp` | No | none | Must not receive Dapr annotations/scopes |
| `redis` | No | none | Backing store only |
| `keycloak` | No | none | OIDC issuer only |

### Allowed Service-Invocation Matrix

| Target config | Caller app id | Required operation | Verb | Rule |
|---|---|---|---|---|
| `accesscontrol` | `eventstore-admin` | exact EventStore admin/API route list if known; otherwise prefix-scoped route only, never bare `/**` | `GET`, `POST`, `PUT` only if required | Document any prefix wildcard in YAML header and Dev Agent Record |
| `accesscontrol` | `tenants` | exact EventStore command route list if known; otherwise prefix-scoped route only, never bare `/**` | `POST` | No query/read broadening |
| `accesscontrol` | `parties` | exact EventStore command route list if known; otherwise prefix-scoped route only, never bare `/**` | `POST` | No query/read broadening |
| `accesscontrol-eventstore-admin` | none | none | none | `policies: []` unless a concrete peer call is proven |
| `accesscontrol-parties` | `eventstore` | `/process` | `POST` | Must stay exact |
| `accesscontrol-tenants` | `eventstore` | exact Tenants command route list if known; otherwise prefix-scoped route only, never bare `/**` | `POST` | Must not allow `tenants -> parties` |
| `accesscontrol-tenants` | `parties` | `/ready` only if current health/lookup behavior still requires it | `GET` | Remove if not required |
| `accesscontrol-memories` | `parties` | exact Memories route list used by current integration; `/process` only if still the real route | `POST` | If Parties uses direct HTTP instead of Dapr invocation, use `policies: []` and document why |

Bare `/**` is not acceptable in production ACLs unless implementation proves there is no narrower valid route contract and records that as an explicit risk/handoff for Story 9.6 linting.

### Subscription Matrix

| File | Subscriber app id | Pubsub | Topic | Route | Dead-letter |
|---|---|---|---|---|---|
| `subscription-parties.yaml` | `sample` only if the sample subscriber is intentionally supported by this topology; otherwise mark reference-only | `pubsub` | `tenant-a.parties.events` unless the publisher contract has changed | `/events/parties` | `deadletter.tenant-a.parties.events` |
| `subscription-tenants.yaml` | `parties` | `pubsub` | `system.tenants.events` | `/tenants/events` | `deadletter.system.tenants.events` |

Story 9.4 wires declarative subscription manifests only. It must not add or change consumer endpoint behavior. Existing handler contracts are in `samples/Hexalith.Parties.Sample/Program.cs`, `src/Hexalith.Parties/Program.cs`, and `tests/Hexalith.Parties.Tests/Tenants/TenantEventInfrastructureTests.cs`.

## Acceptance Criteria

### AC1 - Dapr control-plane install contract is documented for Story 9.5

- Given Dapr is a cluster-wide capability shared with other projects,
- When this story updates deployment docs and handoff notes,
- Then it states that Story 9.5 `publish.ps1` step 9 invokes `dapr init -k` against the active kubectl context unless `-SkipDaprInit` is passed.
- And the Dapr control plane namespace is `dapr-system`, never `hexalith-parties`.
- And the pinned deployment baseline remains Dapr runtime `1.14.4` from `docs/kubernetes-deployment-architecture.md` section 13.
- And mismatch with an already-installed cluster version is a warning, not a blocking failure, because Dapr is cluster-wide.
- And this story does not install or remove the Dapr control plane.

### AC2 - `deploy/dapr/` contains only the committed production CR set

- Given `docs/kubernetes-deployment-architecture.md` section 7 Source 2,
- When `deploy/dapr/` is inspected,
- Then it contains exactly these hand-authored files:
  - `statestore.yaml`
  - `pubsub.yaml`
  - `resiliency.yaml`
  - `accesscontrol.yaml`
  - `accesscontrol-eventstore-admin.yaml`
  - `accesscontrol-parties.yaml`
  - `accesscontrol-tenants.yaml`
  - `accesscontrol-memories.yaml`
  - `subscription-parties.yaml`
  - `subscription-tenants.yaml`
- And no Aspirate-emitted files, generated readmes, sample placeholders, or local AppHost build outputs are committed there.
- And alternative backend templates, if retained, live outside `deploy/dapr/` in `deploy/dapr-alternatives/` and are explicitly documented as excluded from Story 9.5 apply.
- And root `deploy/dapr/` filenames use the Epic 9 v2 hyphen naming convention even though local run-mode files under `src/Hexalith.Parties.AppHost/DaprComponents/` currently use dot-separated names such as `accesscontrol.parties.yaml`.

### AC3 - Redis-backed state store component is production-shaped for MVP

- Given Story 9.3 delivered a Redis Service named `redis` in namespace `hexalith-parties`,
- When `deploy/dapr/statestore.yaml` is inspected,
- Then it is a Dapr `Component` named `statestore` with `apiVersion: dapr.io/v1alpha1`, `kind: Component`, `spec.type: state.redis`, and `spec.version: v1`.
- And it references Redis using cluster DNS `redis:6379`.
- And it does not include `redisPassword`, `redisPasswordFromSecret`, `secretKeyRef`, or any Secret dependency.
- And `actorStateStore` is `"true"`.
- And `keyPrefix` is `"none"` unless the developer proves Dapr Redis component docs require a different production-safe value and records the rationale.
- And scopes include only daprd-equipped apps that need shared state: `eventstore`, `eventstore-admin`, `parties`, `tenants`, `memories`.
- And Redis MVP no-AUTH is documented as intentionally inherited from Story 9.3, not an omission.

### AC4 - Redis Streams pub/sub component is scoped and passwordless

- Given domain and tenant events flow over Redis Streams,
- When `deploy/dapr/pubsub.yaml` is inspected,
- Then it is a Dapr `Component` named `pubsub` with `spec.type: pubsub.redis` and `spec.version: v1`.
- And it references Redis using `redis:6379`.
- And it does not reference any Redis password field or Secret.
- And dead-letter support is enabled if the Redis pub/sub component version supports the metadata key used.
- And publishing/subscription scopes are explicit and deny unlisted apps:
  - `eventstore` can publish party domain events to the party-event topic used by `subscription-parties.yaml`.
  - `tenants` can publish tenant lifecycle events to `system.tenants.events`.
  - `parties` can subscribe to `system.tenants.events`.
  - the sample subscriber app-id can subscribe to the sample party-events topic only if that sample app is intentionally part of the MVP Dapr topology; otherwise the sample subscription must be marked non-runtime/reference-only.
- And `parties-mcp`, `eventstore-admin-ui`, `redis`, and `keycloak` are not granted Dapr pub/sub scopes.

### AC5 - Access control is deny-by-default and uses explicit operations

- Given each daprd-equipped service has a receiving sidecar,
- When the five access-control files are inspected,
- Then each is a Dapr `Configuration` with `spec.accessControl.defaultAction: deny`.
- And none includes wildcard app ids.
- And none grants `parties-mcp`, `eventstore-admin-ui`, `redis`, or `keycloak` as Dapr callers.
- And allowed paths match the topology from `docs/kubernetes-deployment-architecture.md` sections 4.2 and 9:
  - `accesscontrol.yaml` for `eventstore` allows `eventstore-admin`, `tenants`, and `parties` only where needed for EventStore-fronted commands and queries.
  - `accesscontrol-eventstore-admin.yaml` denies peer invocation by default and has no inbound policies unless a concrete admin peer call is proven necessary.
  - `accesscontrol-parties.yaml` allows `eventstore` to call `POST /process`; tenant lifecycle delivery remains pub/sub component scope, not service-invocation ACL.
  - `accesscontrol-tenants.yaml` allows only the EventStore-fronted command path and the existing Parties health/tenant lookup path if it is required by current code.
  - `accesscontrol-memories.yaml` allows `parties` to call the concrete Memories command/search operation paths needed by the current Memories integration.
- And every operation follows the Allowed Service-Invocation Matrix above.
- And bare `/**` is forbidden by default. Prefix-scoped wildcards are allowed only when implementation proves the route catalogue cannot be enumerated in this story, documents the exact reason in the YAML header, and records the risk in Dev Agent Record.
- And the YAML comments preserve the important distinction from `src/Hexalith.Parties/Program.cs`: `POST /tenants/events` is pub/sub event delivery, not a Dapr service-invocation ACL entry.

### AC6 - Declarative subscriptions use the real topic and endpoint contracts

- Given Dapr declarative subscriptions wire Redis Streams topics to HTTP routes,
- When `deploy/dapr/subscription-parties.yaml` is inspected,
- Then it subscribes the intended party-event consumer to the same topic contract used by the publisher and sample documentation.
- And if the sample subscriber is not one of the 9 MVP workloads, the file must explicitly document that this subscription is a reference/sample subscription and should not block the 9-pod topology.
- And `subscription-parties.yaml` uses Dapr `apiVersion: dapr.io/v2alpha1`, `kind: Subscription`, `spec.pubsubname: pubsub`, `spec.routes.default`, `spec.deadLetterTopic`, and explicit `scopes`.
- When `deploy/dapr/subscription-tenants.yaml` is inspected,
- Then it subscribes app-id `parties` to `system.tenants.events` with route `/tenants/events`.
- And this matches the current code in `src/Hexalith.Parties/Program.cs` and `TenantEventInfrastructureTests`, which map and assert `/tenants/events` with pubsub `pubsub` and topic `system.tenants.events`.
- And both subscriptions align retry/dead-letter expectations with `resiliency.yaml`.
- And no new code-based subscription handler, controller, route attribute, or programmatic subscription is added by this story.

### AC7 - Resiliency CR is valid and applied before consumers

- Given Dapr resiliency policy must be valid before any consumer applies,
- When `deploy/dapr/resiliency.yaml` is inspected,
- Then it is a Dapr `Resiliency` CR with `apiVersion: dapr.io/v1alpha1`, policies for default sidecar retries/timeouts/circuit breakers, and pub/sub inbound/outbound policies.
- And targets cover the app ids and components present in the production Dapr topology, including `memories` if it is daprd-equipped.
- And `eventstore-admin` is omitted from app targets only if its inbound peer invocation remains denied by design.
- And retry counts, timeout values, and circuit-breaker thresholds are bounded and copied from or intentionally tightened relative to `src/Hexalith.Parties.AppHost/DaprComponents/resiliency.yaml`; no infinite retry or unbounded timeout is introduced.
- And every policy defined under `spec.policies` is referenced by at least one target, or the file explains why it is deliberately reserved for Story 9.5/9.7 runtime testing.
- And the story records that Story 9.5 step 10 must run `kubectl apply -f deploy/dapr/resiliency.yaml --dry-run=server`.
- And a server-side dry-run failure must exit Story 9.5 publish with code 1 and bounded output that names the offending CR without printing the full CR body.

### AC8 - Existing AppHost and generated deployment boundaries are preserved

- Given Story 9.2 and Story 9.3 established the current deployment tree,
- When this story is implemented,
- Then `src/Hexalith.Parties.AppHost/Program.cs` remains the source of truth for the 7 application services and their local run-mode Dapr component file references.
- And `deploy/k8s/redis/` and `deploy/k8s/keycloak/` remain untouched except for README references if needed.
- And the seven Aspirate-generated app folders are not regenerated by this story.
- And current committed `dapr.io/config: tracing` annotations in generated deployments are treated as a Story 9.5 patch target, not as final truth.
- And the future patch target is explicit:
  - `eventstore` -> `accesscontrol`
  - `eventstore-admin` -> `accesscontrol-eventstore-admin`
  - `parties` -> `accesscontrol-parties`
  - `tenants` -> `accesscontrol-tenants`
  - `memories` -> `accesscontrol-memories`
- And `eventstore-admin-ui` and `parties-mcp` continue to carry no `dapr.io/*` annotations.
- And Story 9.4 does not claim ACL enforcement is active until Story 9.5 attaches those configuration names to pod annotations and applies the CRs.

### AC9 - Verification proves CR shape and repo cleanliness

- Given the CR files are committed,
- When local validation runs,
- Then these commands exit 0. Use `--validate=false` for local/client dry-run when the active cluster does not yet have Dapr CRDs installed:
  ```bash
  kubectl apply --dry-run=client --validate=false -f deploy/dapr/statestore.yaml
  kubectl apply --dry-run=client --validate=false -f deploy/dapr/pubsub.yaml
  kubectl apply --dry-run=client --validate=false -f deploy/dapr/resiliency.yaml
  kubectl apply --dry-run=client --validate=false -f deploy/dapr/accesscontrol.yaml
  kubectl apply --dry-run=client --validate=false -f deploy/dapr/accesscontrol-eventstore-admin.yaml
  kubectl apply --dry-run=client --validate=false -f deploy/dapr/accesscontrol-parties.yaml
  kubectl apply --dry-run=client --validate=false -f deploy/dapr/accesscontrol-tenants.yaml
  kubectl apply --dry-run=client --validate=false -f deploy/dapr/accesscontrol-memories.yaml
  kubectl apply --dry-run=client --validate=false -f deploy/dapr/subscription-parties.yaml
  kubectl apply --dry-run=client --validate=false -f deploy/dapr/subscription-tenants.yaml
  ```
- And if a Dapr-enabled cluster is available, this command exits 0:
  ```bash
  kubectl apply -f deploy/dapr/resiliency.yaml --dry-run=server
  ```
- And these cleanliness checks return zero lines:
  ```bash
  grep -rEn 'redisPassword|redisPasswordFromSecret|secretKeyRef|password|Password|Bearer eyJ|auths[[:space:]]*:' deploy/dapr
  grep -rEn 'appId:[[:space:]]*['"'"'"]?\*|name:[[:space:]]*['"'"'"]?/\*\*' deploy/dapr
  grep -rEn 'appId:[[:space:]]*(parties-mcp|eventstore-admin-ui|keycloak|redis)\b' deploy/dapr/accesscontrol*.yaml
  find deploy/dapr -maxdepth 1 -type f | sort
  ```
- And the final file list from `find deploy/dapr -maxdepth 1 -type f | sort` matches AC2 exactly.
- And `git diff --name-only -- deploy/k8s/redis deploy/k8s/keycloak deploy/k8s/eventstore deploy/k8s/eventstore-admin deploy/k8s/eventstore-admin-ui deploy/k8s/memories deploy/k8s/parties deploy/k8s/parties-mcp deploy/k8s/tenants` is empty unless the README-only change is deliberately outside those folders.
- And `git diff --check` exits 0.

### AC10 - Static validation tests guard review-critical mistakes

- Given this story adds production Dapr YAML,
- When implementation is complete,
- Then add focused static validation in `tests/Hexalith.Parties.DeployValidation.Tests/` or another existing deploy-validation test location if the project has moved it.
- And the tests parse every `deploy/dapr/*.yaml` as YAML rather than relying only on grep.
- And the tests assert:
  - the exact AC2 file set exists and no extra root files are present;
  - each file has the expected `apiVersion`, `kind`, and `metadata.name`;
  - `statestore.yaml` and `pubsub.yaml` reference `redis:6379` and contain no password/Secret metadata;
  - access-control configs have `defaultAction: deny`;
  - no access-control policy grants wildcard app ids;
  - no production access-control operation is bare `/**`;
  - every subscription references Component `pubsub`;
  - every subscription has a non-empty topic, route, scope, and dead-letter topic;
  - every resiliency policy is bound to at least one real app/component target or explicitly documented as reserved.
- And document a Story 9.5 runtime smoke-test matrix in Dev Notes or README handoff:
  - unauthorized app id denied;
  - wrong method/path denied;
  - `eventstore -> parties POST /process` allowed after annotation patching;
  - `tenants -> parties` service invocation denied;
  - tenant lifecycle event reaches `POST /tenants/events`;
  - unsubscribed/non-scoped app id does not receive the tenant event.

## Tasks / Subtasks

- [x] Task 1 - Preflight and source reconciliation (AC: 2, 5, 6, 8)
  - [x] Confirm `deploy/dapr/` does not already exist in the root working tree; ignore historical copies in submodules, `.claude/worktrees`, and superseded Epic 9 v1 artifacts.
  - [x] Read all current local run-mode Dapr files under `src/Hexalith.Parties.AppHost/DaprComponents/` and record which content is copied, tightened, renamed, or deliberately diverged for production.
  - [x] Confirm `src/Hexalith.Parties.AppHost/Program.cs` still wires 5 daprd-equipped app ids: `eventstore`, `eventstore-admin`, `parties`, `tenants`, `memories`.
  - [x] Confirm `deploy/k8s/redis/service.yaml` still names the Redis Service `redis` on port `6379`.
  - [x] Confirm `src/Hexalith.Parties/Program.cs` still maps `/tenants/events` and `/dapr/subscribe`.

- [x] Task 2 - Create production Redis Components (AC: 3, 4)
  - [x] Create `deploy/dapr/statestore.yaml` from the current AppHost `statestore.yaml` shape, replacing local `{env:REDIS_HOST|localhost:6379}` with production `redis:6379`.
  - [x] Remove Redis password metadata entirely; do not leave empty password placeholders.
  - [x] Scope statestore to the 5 daprd-equipped services that need actor/state access.
  - [x] Create `deploy/dapr/pubsub.yaml` from the current AppHost shape, replacing local Redis metadata with production `redis:6379`.
  - [x] Make publishing/subscription scopes explicit and aligned with actual topics.

- [x] Task 3 - Create deny-by-default access-control configurations (AC: 5, 8)
  - [x] Create `deploy/dapr/accesscontrol.yaml` for EventStore.
  - [x] Create `deploy/dapr/accesscontrol-eventstore-admin.yaml` and keep inbound policies empty unless a current code path proves otherwise.
  - [x] Create `deploy/dapr/accesscontrol-parties.yaml` with `eventstore -> POST /process` and comments explaining pub/sub delivery is not service-invocation ACL.
  - [x] Create `deploy/dapr/accesscontrol-tenants.yaml` with only the required EventStore/Parties operations.
  - [x] Create `deploy/dapr/accesscontrol-memories.yaml` with only the required Parties-to-Memories operations.
  - [x] Avoid wildcard app ids and wildcard operation paths. If `/**` remains necessary for an EventStore gateway path, document why and add a Story 9.6 lint handoff note.

- [x] Task 4 - Create subscriptions and resiliency policy (AC: 6, 7)
  - [x] Create `deploy/dapr/subscription-parties.yaml` using the party-event topic contract and explicit app-id scope.
  - [x] Create `deploy/dapr/subscription-tenants.yaml` for `system.tenants.events` -> `/tenants/events` scoped to `parties`.
  - [x] Create `deploy/dapr/resiliency.yaml` with app and component targets matching the production topology.
  - [x] Keep `apiVersion` values aligned with official Dapr docs for the deployed baseline.

- [x] Task 5 - Add static deploy-validation tests (AC: 2, 3, 4, 5, 6, 7, 10)
  - [x] Add or update a deploy-validation test file under `tests/Hexalith.Parties.DeployValidation.Tests/` for `deploy/dapr/*.yaml`.
  - [x] Parse YAML with the existing test stack (`YamlDotNet` is already centrally versioned) instead of grep-only assertions.
  - [x] Assert the exact required file set, expected `apiVersion`/`kind`/`metadata.name`, and no unexpected root files.
  - [x] Assert Redis Components are passwordless, scoped only to intended app ids, and reference `redis:6379`.
  - [x] Assert ACL configs are deny-by-default with no wildcard app ids and no bare `/**` operation.
  - [x] Assert subscriptions reference `pubsub`, have non-empty topic/route/scopes/dead-letter fields, and match the Subscription Matrix.
  - [x] Assert resiliency policies bind to real app/component targets and have bounded retry/timeout values.

- [x] Task 6 - Documentation handoff (AC: 1, 2, 7, 8, 10)
  - [x] Update `deploy/k8s/README.md` roadmap so Story 9.4 Dapr CRs are delivered and Story 9.5 scripts remain forward references.
  - [x] Add a short note that `deploy/dapr/` is Source 2 from the canonical architecture doc and is applied by Story 9.5 in Components -> Resiliency -> Configurations -> Subscriptions order.
  - [x] Document `dapr init -k`, `-SkipDaprInit`, `dapr-system`, pinned runtime `1.14.4`, and mismatch-warning behavior as Story 9.5 handoff.
  - [x] Document the Story 9.5 runtime smoke-test matrix from AC10.
  - [x] Do not duplicate the full Dapr topology table in README; link the canonical architecture doc.

- [x] Task 7 - Verification (AC: 2, 7, 9, 10)
  - [x] Run the focused deploy-validation tests added by this story.
  - [x] Run all AC9 `kubectl apply --dry-run=client` checks.
  - [x] Run server-side dry-run for `resiliency.yaml` if the active cluster has Dapr CRDs installed; otherwise record the skip reason.
  - [x] Run AC9 cleanliness greps and exact file-list check.
  - [x] Verify protected K8s folders were not changed.
  - [x] Run `git diff --check`.
- [x] Do not run full solution tests unless C# code or project files are unexpectedly touched.

### Review Findings

- [x] [Review][Patch] Memories ACL grants Dapr invocation despite documenting direct HTTP [deploy/dapr/accesscontrol-memories.yaml:1]
- [x] [Review][Patch] Resiliency is omitted from the documented Story 9.5 apply order [deploy/k8s/README.md:91]
- [x] [Review][Patch] Dapr file-set test ignores non-YAML files and subdirectories [tests/Hexalith.Parties.DeployValidation.Tests/DaprManifestValidationTests.cs:42]
- [x] [Review][Patch] Manifest loader does not reject multiple YAML documents [tests/Hexalith.Parties.DeployValidation.Tests/DaprManifestValidationTests.cs:188]
- [x] [Review][Patch] Redis credential guard only checks three exact metadata names [tests/Hexalith.Parties.DeployValidation.Tests/DaprManifestValidationTests.cs:221]
- [x] [Review][Patch] Resiliency validation misses undefined target policy references and only partially checks bounded values [tests/Hexalith.Parties.DeployValidation.Tests/DaprManifestValidationTests.cs:149]
- [x] [Review][Patch] Story completion checklist and status text contradict review-ready implementation state [_bmad-output/implementation-artifacts/9-4-dapr-control-plane-components-acl-subscriptions.md:436]

## Dev Notes

### Current State To Preserve

- `deploy/dapr/` does not exist in the root working tree at story creation time. Do not copy from `.claude/worktrees` or nested submodule histories as source of truth; those are historical/generated side paths.
- `deploy/k8s/` already contains seven Aspirate-emitted app folders plus Story 9.3 Redis and Keycloak carve-outs. This story should not regenerate or hand-edit those folders.
- Current generated Dapr-equipped Deployments (`eventstore`, `eventstore-admin`, `parties`, `tenants`, `memories`) contain `dapr.io/config: tracing`. That is a known placeholder from Aspirate output; Story 9.5 post-generation patch must replace it with per-service config names. This story should document the mapping but avoid manual patching of generated YAML unless explicitly chosen.
- `eventstore-admin-ui` and `parties-mcp` currently have no Dapr annotations and must stay non-Dapr workloads.
- Redis and Keycloak are public vendor-image carve-outs with no Dapr annotations and no image pull secrets. Do not add them to any Dapr ACL or pub/sub scope.

### Existing Local Dapr Files

Local run-mode files under `src/Hexalith.Parties.AppHost/DaprComponents/` are useful starting points:

- `statestore.yaml` and `pubsub.yaml` currently use `{env:REDIS_HOST|localhost:6379}` and `{env:REDIS_PASSWORD|}` for local dev. Production `deploy/dapr/` must use `redis:6379` and omit password metadata because Story 9.3 Redis has no AUTH.
- `accesscontrol.yaml`, `accesscontrol.eventstore-admin.yaml`, `accesscontrol.parties.yaml`, `accesscontrol.tenants.yaml`, and `accesscontrol.memories.yaml` already capture the intended receiving-sidecar split. Production filenames must use Epic 9 v2 hyphen names in `deploy/dapr/`.
- Current local ACLs include some broad `/**` entries. Production should tighten to explicit operations wherever the application path is known.
- `resiliency.yaml` already has retry, timeout, circuit-breaker, `pubsub`, and `statestore` targets. Production must add or preserve the `memories` target because Memories is now daprd-equipped.
- `subscription-parties.yaml` currently uses local sample topic `tenant-a.parties.events` scoped to `sample`. Verify whether the Epic 9 v2 production topology really includes that sample subscriber. If not, mark it as a reference/sample subscription and keep it from blocking the 9-pod runtime.
- There is no current `subscription-tenants.yaml`; create it from the code contract: pubsub `pubsub`, topic `system.tenants.events`, route `/tenants/events`, scope `parties`.

### File Structure Requirements

Expected created files:

- `deploy/dapr/statestore.yaml`
- `deploy/dapr/pubsub.yaml`
- `deploy/dapr/resiliency.yaml`
- `deploy/dapr/accesscontrol.yaml`
- `deploy/dapr/accesscontrol-eventstore-admin.yaml`
- `deploy/dapr/accesscontrol-parties.yaml`
- `deploy/dapr/accesscontrol-tenants.yaml`
- `deploy/dapr/accesscontrol-memories.yaml`
- `deploy/dapr/subscription-parties.yaml`
- `deploy/dapr/subscription-tenants.yaml`

Expected modified file:

- `deploy/k8s/README.md`
- `tests/Hexalith.Parties.DeployValidation.Tests/DaprManifestValidationTests.cs` for static YAML validation, unless an existing deploy-manifest test file is the clearer local extension point

Forbidden edits:

- Do not move or delete `src/Hexalith.Parties.AppHost/DaprComponents/`; those files remain local run-mode inputs for the AppHost.
- Do not add `deploy/dapr/` files to `deploy/k8s/kustomization.yaml`; Dapr CRs are applied separately by Story 9.5.
- Do not add Redis AUTH, a Redis Secret, or a Dapr `redisPassword` field.
- Do not grant wildcard app ids.
- Do not add Dapr annotations to Redis, Keycloak, `parties-mcp`, or `eventstore-admin-ui`.
- Do not create `publish.ps1`, `teardown.ps1`, `_lib/Confirm-KubeContext.ps1`, or `deploy/validate-deployment.ps1`.
- Do not initialize nested submodules.

### Latest Technical Information

- Official Dapr docs still use Kubernetes CRDs with `apiVersion: dapr.io/v1alpha1` for Components, Configurations, and Resiliency, and `apiVersion: dapr.io/v2alpha1` for declarative Subscriptions.
- Dapr access-control operation matching uses app operation paths plus `httpVerb`; do not encode the full sidecar API URL as the operation path unless official docs for the deployed baseline require it.
- Dapr Redis state store metadata uses `redisHost` and `actorStateStore`; Redis password metadata is optional and should be absent for this MVP because Story 9.3 explicitly disables Redis AUTH.
- `dapr init -k` is still the Dapr CLI Kubernetes control-plane install command; this story only documents the Story 9.5 invocation contract.
- The project package baseline is Dapr .NET packages `1.17.9` in `Directory.Packages.props`, but the Kubernetes control-plane runtime baseline in the canonical architecture doc is Dapr `1.14.4`. Do not upgrade either in this story. If implementation finds the CR schema incompatible with `1.14.4`, document the conflict and stop for architecture guidance.

### Previous Story Intelligence - Story 9.3

- Redis Service `redis` on port `6379` now exists and is the production target for Dapr Components.
- Redis has no AUTH. A Dapr Component with `redisPassword` would contradict Story 9.3 and fail the intended MVP contract.
- Redis and Keycloak are hand-authored carve-outs and must not be touched by Dapr annotation or imagePullSecrets patches.
- Story 9.3 review disabled default service account token mounting for vendor pods; do not reverse that by copying broad deployment snippets.
- Keycloak is not a Dapr workload. Do not add it to scopes, ACLs, or subscriptions.
- Story 9.2/9.3 review history established that future patch logic must use explicit app-id allowlists. Do not detect Dapr workloads by searching for a `daprd` container; the Dapr operator injects it at admission time.

### Git Intelligence

Recent relevant commits:

- `4273b59 feat(deploy): story 9-3 add redis and keycloak carve-outs` - delivered Redis/Keycloak manifests and README updates.
- `6dbcb6e feat(deploy): story 9-2 aspirate apphost composition` - delivered seven app service folders and the placeholder Dapr annotations that Story 9.5 later patches.
- `12a0f47 feat(deploy): story 9-1 zot registry documentation` - delivered Zot and deployment entry-point documentation.
- `4f84aa8 feat(deploy): Epic 9 v2 greenfield rewrite - wipe v1 artefacts + replan as 7 stories` - made the v2 7-story plan authoritative.
- `9c97b8a feat(docs): add Kubernetes deployment architecture documentation` - introduced the canonical architecture document.

### Testing Requirements

This story is YAML/docs plus focused deploy-validation tests. It should not touch production C# code unless the developer discovers an AppHost/source mismatch that must be corrected.

Required verification:

- `kubectl apply --dry-run=client --validate=false -f <each deploy/dapr/*.yaml>`
- `kubectl apply -f deploy/dapr/resiliency.yaml --dry-run=server` when Dapr CRDs are installed and a cluster context is available
- AC9 cleanliness greps
- exact `deploy/dapr/` file-list check
- protected-folder diff check for `deploy/k8s` app folders and Redis/Keycloak carve-outs
- `git diff --check`

Recommended focused tests only if C# code changes:

```bash
dotnet test tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj --configuration Release --filter AppHostTenantsTopologyTests
```

Full solution tests are not required for the intended Dapr manifest/docs/deploy-validation change.

### Final Implementation Checklist

- [x] `deploy/dapr/` contains exactly the ten required root YAML files.
- [x] `deploy/dapr/` contains no alternatives, generated readmes, or build outputs.
- [x] Redis Components reference `redis:6379` and contain no password or Secret metadata.
- [x] ACL files use the exact app ids from the Dapr-Equipped App IDs table.
- [x] No ACL policy grants wildcard app ids or bare `/**` operations.
- [x] Subscriptions match the Subscription Matrix and do not add consumer endpoint code.
- [x] Resiliency policies have bounded values and are attached to real targets.
- [x] Deploy-validation tests parse YAML and cover the AC10 assertions.
- [x] `kubectl apply --dry-run=client --validate=false -f <each file>` passes.
- [x] Server-side resiliency dry-run is run when Dapr CRDs are available, or the skip is recorded.
- [x] `deploy/k8s/README.md` marks Story 9.4 delivered and Story 9.5 still responsible for apply/install/annotation patching.
- [x] Protected `deploy/k8s` application folders and Redis/Keycloak carve-outs are unchanged.
- [x] Story 9.5 runtime smoke-test matrix is documented as handoff, not claimed as complete.

### Common LLM Mistakes To Avoid

1. Do not copy old Epic 9 v1 `deploy/dapr` files from nested submodules, `.claude/worktrees`, or historical branches without reconciling them to Epic 9 v2.
2. Do not put alternative backend templates in root `deploy/dapr/`; Story 9.5 applies root files.
3. Do not include `redisPassword` or a Secret reference. Redis MVP has no AUTH.
4. Do not put `keycloak`, `redis`, `parties-mcp`, or `eventstore-admin-ui` in Dapr scopes or access-control policies.
5. Do not change the AppHost local `DaprComponents` filenames to match production hyphen filenames unless a separate architecture decision is made.
6. Do not use `/**` casually. If it remains for EventStore gateway compatibility, explain why in the file.
7. Do not claim `dapr init -k` or live-cluster readiness was executed unless it actually was.
8. Do not add Dapr CRs to the K8s Kustomization. They apply separately in Story 9.5.
9. Do not run recursive submodule initialization.
10. Do not weaken the `parties` access-control rule that only EventStore may call `POST /process`.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` Epic 9 v2 Story 9.4] - story statement, BDD acceptance criteria, file-set contract.
- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-21-epic9-greenfield-rewrite.md` Appendix A Story 9.4] - authoring source for the v2 greenfield story.
- [Source: `docs/kubernetes-deployment-architecture.md` sections 3-4] - 9-workload topology and Dapr control-plane model.
- [Source: `docs/kubernetes-deployment-architecture.md` section 7 Source 2] - `deploy/dapr/*.yaml` ownership.
- [Source: `docs/kubernetes-deployment-architecture.md` section 8] - publish flow, Dapr init, resiliency dry-run, and apply order.
- [Source: `docs/kubernetes-deployment-architecture.md` section 13] - pinned Dapr runtime baseline `1.14.4`.
- [Source: `_bmad-output/planning-artifacts/prd.md` FR31a] - one-command publish pipeline and Components -> Configurations -> Subscriptions apply order.
- [Source: `_bmad-output/planning-artifacts/architecture.md` ADR D-K8s-4] - greenfield v2 rewrite and canonical architecture source.
- [Source: `_bmad-output/implementation-artifacts/9-3-hand-authored-carve-outs-redis-and-keycloak.md`] - Redis/Keycloak handoff and no-AUTH/no-Dapr/no-imagePullSecrets boundaries.
- [Source: `src/Hexalith.Parties.AppHost/Program.cs`] - current app ids, local Dapr config names, publish/run mode split, and Dapr sidecar wiring.
- [Source: `src/Hexalith.Parties.AppHost/DaprComponents/*.yaml`] - current local Dapr component and access-control shapes.
- [Source: `src/Hexalith.Parties/Program.cs`] - `/dapr/subscribe`, `/tenants/events`, and `/process` internal Dapr plumbing.
- [Source: `tests/Hexalith.Parties.Tests/Tenants/TenantEventInfrastructureTests.cs`] - tenant event topic/route metadata contract.
- [Official: Dapr Kubernetes hosting docs](https://docs.dapr.io/operations/hosting/kubernetes/kubernetes-deploy/) - `dapr init -k` and Kubernetes hosting guidance.
- [Official: Dapr Redis state store docs](https://docs.dapr.io/reference/components-reference/supported-state-stores/setup-redis/) - Redis state component metadata including actor state store.
- [Official: Dapr Redis pub/sub docs](https://docs.dapr.io/reference/components-reference/supported-pubsub/setup-redis-pubsub/) - Redis pub/sub component metadata and Streams backend.
- [Official: Dapr access control docs](https://docs.dapr.io/operations/configuration/invoke-allowlist/) - allowlist/deny-by-default service invocation configuration.
- [Official: Dapr declarative subscriptions docs](https://docs.dapr.io/developing-applications/building-blocks/pubsub/subscription-methods/) - v2alpha1 subscription CR shape.
- [Official: Dapr resiliency docs](https://docs.dapr.io/operations/resiliency/resiliency-overview/) - resiliency CR model and targets.

## Story Completion Status

Implementation and code-review patch pass completed. Story is done.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-05-22 12:10:37 +02:00 - Moved sprint status for `9-4-dapr-control-plane-components-acl-subscriptions` to `in-progress`.
- Red phase: `/home/quentindv/.dotnet/dotnet test tests/Hexalith.Parties.DeployValidation.Tests/Hexalith.Parties.DeployValidation.Tests.csproj --configuration Release --filter DaprManifestValidationTests` failed 6/6 before `deploy/dapr/` existed.
- Green/refactor: focused `DaprManifestValidationTests` passed 6/6 after production CR files were added.
- Dapr CRD reconciliation: `kubectl apply -f deploy/dapr/resiliency.yaml --dry-run=server` initially rejected the local AppHost-shaped resiliency schema; updated production `timeouts` and component targets to the installed Dapr CRD shape.
- Final validation: full deploy-validation test project passed 6/6, all AC9 client dry-runs passed, server-side resiliency dry-run passed, AC9 greps returned zero lines, protected K8s folder diff was empty, exact Dapr file list matched AC2, and `git diff --check` passed.

### Implementation Plan

- Created the flat root `deploy/dapr/` production CR set required by Epic 9 v2 without subfolders or generated content.
- Copied local AppHost component intent while replacing local Redis env metadata with `redis:6379`, removing Redis AUTH metadata, adding `memories` to state scope, and keeping the sample party subscription reference-only.
- Tightened local broad ACL shapes to deny-by-default production Configurations: exact `/process` and `/ready` where known, EventStore/Memories prefix-scoped routes where the current route catalogue spans submodule surfaces, and no bare `/**`.
- Added YamlDotNet-based deploy-validation tests to guard file set, headers, Redis metadata, ACL wildcards, subscriptions, and resiliency policy bindings.
- Updated `deploy/k8s/README.md` with the Story 9.5 handoff for `dapr init -k`, `dapr-system`, runtime baseline warning behavior, Dapr apply order, configuration annotation mapping, and runtime smoke matrix.

### Completion Notes List

- Delivered exactly ten production Dapr CR files under root `deploy/dapr/`: statestore, pubsub, resiliency, five access-control Configurations, and two Subscriptions.
- Preserved Story 9.2/9.3 generated and carve-out folders; `deploy/k8s/redis`, `deploy/k8s/keycloak`, and the seven Aspirate app folders were not modified.
- Documented Redis MVP no-AUTH as inherited from Story 9.3 without using forbidden Redis credential metadata in the CR files.
- Kept Story 9.4 manifest-only: no Dapr install, no apply automation, no generated Deployment annotation patching, and no runtime behavior claims.
- Server-side Dapr resiliency dry-run passed against the available cluster CRDs.

### File List

- `_bmad-output/implementation-artifacts/9-4-dapr-control-plane-components-acl-subscriptions.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `deploy/dapr/accesscontrol-eventstore-admin.yaml`
- `deploy/dapr/accesscontrol-memories.yaml`
- `deploy/dapr/accesscontrol-parties.yaml`
- `deploy/dapr/accesscontrol-tenants.yaml`
- `deploy/dapr/accesscontrol.yaml`
- `deploy/dapr/pubsub.yaml`
- `deploy/dapr/resiliency.yaml`
- `deploy/dapr/statestore.yaml`
- `deploy/dapr/subscription-parties.yaml`
- `deploy/dapr/subscription-tenants.yaml`
- `deploy/k8s/README.md`
- `tests/Hexalith.Parties.DeployValidation.Tests/DaprManifestValidationTests.cs`

### Change Log

| Date | Author | Change |
|---|---|---|
| 2026-05-22 | bmad-code-review (Codex) | Applied review patches: denied Memories peer invocation for direct-HTTP integration, added Resiliency to Story 9.5 apply order, hardened deploy/dapr file-set, YAML document, credential, and resiliency validation tests, and completed story status cleanup. |
| 2026-05-22 | bmad-dev-story (Codex) | Implemented Story 9.4 v2: added production Dapr CR set, deploy-validation tests, README handoff, and completed AC9 validation. |
| 2026-05-22 | bmad-create-story (Codex) | Story 9.4 v2 ready-for-dev. Created comprehensive context for Dapr Components, deny-by-default access control, declarative subscriptions, resiliency validation, Story 9.3 Redis/Keycloak handoff, and Story 9.5 apply-order handoff. |
| 2026-05-22 | bmad-party-mode review patch (Winston + Amelia + Murat + Paige) | Tightened story after review: added explicit Story Boundary, required file table, app-id/config matrix, service-invocation and subscription matrices, manifest-only versus runtime-proof split, AC10 static deploy-validation requirements, runtime smoke-test handoff to Story 9.5, and final implementation checklist. |
