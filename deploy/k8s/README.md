# Kubernetes Manifests for Hexalith.Parties

These manifests deploy the **Parties + sibling-submodule topology** to a Kubernetes cluster. They are generated from the Aspire AppHost (`src/Hexalith.Parties.AppHost`) by [aspirate (aspir8)](https://github.com/prom3theu5/aspirational-manifests) and laid out under this directory. Container images are built by the .NET SDK Container Publish target and pushed to the project's authoritative OCI registry — the Zot registry at `registry.hexalith.com` (ADR 9.5-1).

> **Authoritative source:** the Aspire AppHost is the single source of truth for what gets deployed. To add or remove a resource, change `Program.cs` in the AppHost and re-run `publish.ps1`. **Do not hand-edit the YAML files in this directory** — your changes will be erased on the next publish run.

## Prerequisites

| Tool | Version | Notes |
|---|---|---|
| .NET SDK | `10.0.300` (pinned in `global.json`, `rollForward: latestFeature`) | Required for `dotnet aspirate generate`, `dotnet build`, and the SDK Container Publish target. |
| aspirate | `9.1.0` (pinned in `.config/dotnet-tools.json`) | Installed via `dotnet tool restore`. **Do not install globally.** Targets `net9.0`; runs on .NET 10 via `rollForward: true`. |
| Kubernetes cluster | any context the operator confirms via `-ConfirmContext` | The deploy-target platform is the in-MVP cluster (`kubernetes-admin@cluster.local`). Local clusters (kind, k3d, minikube, Docker Desktop) work too — pass the matching `-ConfirmContext` value. ADR 9.5-2 replaces the prior local-only regex allowlist. |
| `kubectl` | recent | Active context must match the value the operator passes via `-ConfirmContext <name>` exactly (case-sensitive). |
| DAPR CLI | recent | `publish.ps1` installs the DAPR control plane with `dapr init -k` if it is not already present (skip with `-SkipDaprInit`). |
| Docker login for Zot | `parties-publisher` builder account | One-time `docker login -u parties-publisher registry.hexalith.com` on the operator workstation. See "Zot credentials" in `docs/deployment-guide.md`. Docker credential helpers (`credsStore` / `credHelpers`) are NOT supported in MVP. |
| PowerShell (`pwsh`) | 7+ | All scripts in this directory use `pwsh` cross-platform. |

Verify your toolchain:

```bash
dotnet --version
kubectl version --client
dapr --version
pwsh --version
docker version
```

## AppHost-current resources

The set of resources emitted by `publish.ps1` is exactly what `src/Hexalith.Parties.AppHost/Program.cs` composes today:

| App id | Source project | DAPR sidecar | Notes |
|---|---|---|---|
| `eventstore` | `Hexalith.EventStore/src/Hexalith.EventStore` | indirect (via siblings) | Public command/query gateway. |
| `eventstore-admin` | `Hexalith.EventStore/src/Hexalith.EventStore.Admin.Server.Host` | — | Admin API behind the Admin UI. |
| `eventstore-admin-ui` | `Hexalith.EventStore/src/Hexalith.EventStore.Admin.UI` | — | Generic stream/event browser. |
| `parties` | `src/Hexalith.Parties` | yes (`app-id=parties`, ACL `accesscontrol.parties.yaml`) | Domain actor host. |
| `parties-mcp` | `src/Hexalith.Parties.Mcp` | — | Separate MCP host. |
| `tenants` | `Hexalith.Tenants/src/Hexalith.Tenants` | yes (`app-id=tenants`, ACL `accesscontrol.tenants.yaml`) | Tenant lifecycle authority. |
| `memories` | `Hexalith.Memories/src/Hexalith.Memories.Server` | yes (`app-id=memories`, ACL `accesscontrol.memories.yaml`) | Memories service host. |
| `keycloak` | hand-authored `deploy/k8s/keycloak/` (Story 9.3 AC4 / ADR 9.3-3, path b) | — | Hand-authored carve-out preserved by `publish.ps1` `$PreservedNames`. Admin password sourced from `hexalith-keycloak-admin` Secret bootstrapped by `publish.ps1`. |
| `redis` | hand-authored `deploy/k8s/redis/` (Story 9.3 AC5) | — | Backing store for Dapr state + pubsub Components (`redis:6379`). MVP scope is `emptyDir`-backed — no PVC / AUTH / TLS. |

Hexalith.FrontComposer and Hexalith.AI.Tools are **not** wired into `Program.cs` today and therefore not in this manifest set. FrontComposer is carved out to Story 9.4 (`9-4-frontcomposer-deployable-host`); when it lands, the new `frontcomposer` resource will be picked up by `publish.ps1` automatically.

## Layout

```
deploy/k8s/
├── README.md
├── publish.ps1                 # One-command publish: MinVer → aspirate build+push → Secret bootstrap → kustomize apply
├── teardown.ps1                # Clean teardown + residual probe
├── kustomization.yaml          # Top-level kustomize entry (aspirate-generated)
├── namespace.yaml              # `hexalith-parties` namespace
├── eventstore/                 # Per-app workload (Deployment + Service + per-app kustomization + ConfigMap generator)
├── eventstore-admin/
├── eventstore-admin-ui/
├── parties/
├── parties-mcp/
├── tenants/
├── memories/                   # Story 9.3 AC2 — Memories.Server (Dapr-enabled, ACL accesscontrol-memories)
├── keycloak/                   # Story 9.3 AC4 — hand-authored carve-out, preserved by publish.ps1 (path b)
└── redis/                      # Story 9.3 AC5 — hand-authored backing store (emptyDir, redis:6379)
```

Aspirate's emitted layout is **per-app folders at the top level**, not the `deployments/` subfolder originally envisioned by the story spec. This is aspirate's canonical kustomize layout and the publish script applies it via `kubectl apply -k deploy/k8s/`.

## Publishing to the cluster

```bash
# From the repository root, with `docker login -u parties-publisher registry.hexalith.com`
# already populated in ~/.docker/config.json:
dotnet tool restore
kubectl config use-context kubernetes-admin@cluster.local     # or your local context name
pwsh deploy/k8s/publish.ps1 -ConfirmContext kubernetes-admin@cluster.local
```

The script does all of the following in one pass:

1. **Context gate.** Requires `-ConfirmContext <name>` matching `kubectl config current-context` exactly (case-sensitive). Mismatch → exit 2 with `expected '<arg>', got '<active>'`. Replaces the prior local-cluster regex allowlist (ADR 9.5-2).
2. **MinVer resolution.** Reads `dotnet msbuild src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj -getProperty:MinVerVersion -nologo -v:q`. Validates the result against the SemVer shape; exits 5 on empty / non-SemVer. Dirty-tree marker (`+dirty`) emits a warning but does not refuse (CI gate deferred to Story 9.6).
3. **Aspirate generate + push.** Runs `dotnet aspirate generate` **without** `--skip-build`, passing `--container-image-tag <minver>` and `--container-registry registry.hexalith.com`. Aspirate orchestrates `dotnet publish /t:PublishContainer` per `AddProject<>` resource and pushes each image to `registry.hexalith.com/<app-id>:<minver>` using the `parties-publisher` credentials in `~/.docker/config.json`.
4. **Post-aspirate patches.** Three idempotent patches on consumer Deployments:
   - Dapr annotations (`app-port: '8080'` + per-app `dapr.io/config`),
   - JWT `secretKeyRef` (sourced from `hexalith-jwt-signing` Secret),
   - `imagePullSecrets: [{ name: zot-pull-secret }]` for every `registry.hexalith.com/*` image (vendor-image carve-outs `keycloak/` + `redis/` are excluded).
5. **DAPR control plane.** Optional `dapr init -k` (skip with `-SkipDaprInit`).
6. **Namespace + resiliency dry-run.** Ensures `hexalith-parties` exists, then `kubectl apply --dry-run=server -f deploy/dapr/resiliency.yaml` to catch CRD schema drift before any namespaced write.
7. **Secret bootstrap.** Three idempotent Secrets in `hexalith-parties`:
   - `hexalith-jwt-signing` — 32-byte random base64 key (Story 9.3 AC4),
   - `hexalith-keycloak-admin` — 24-byte random base64 admin password (Story 9.3 AC4),
   - `zot-pull-secret` — `kubernetes.io/dockerconfigjson` rendered from the operator's `~/.docker/config.json` `auths["registry.hexalith.com"]` block (Path B; never decoded). Operator credential never appears in stdout / stderr / any committed file.
8. **DAPR components.** Applies `deploy/dapr/*.yaml` (skipping alternative-backend templates and Hexalith CRDs).
9. **Workloads.** `kubectl apply -k deploy/k8s/`.

Two consecutive `publish.ps1` runs against the same commit + the same MinVer version + the same aspirate version produce zero diff in `deploy/k8s/<app-id>/deployment.yaml` for the three patches. Image-tag lines vary across commits with MinVer; all other lines are byte-stable per commit (Story 9.5 AC4).

## Tearing down the deployment

```bash
pwsh deploy/k8s/teardown.ps1 -ConfirmContext kubernetes-admin@cluster.local
# Add -PurgeDapr to also uninstall the DAPR control plane (interactive
# confirmation required; -Force bypasses the prompt for CI).
```

The teardown script enforces the same `-ConfirmContext` exact-match gate, deletes via kustomize, removes the authoritative DAPR component CRs, and probes for residual resources in the `hexalith-parties` namespace (Deployments, Services, ConfigMaps, Secrets, DAPR Components / Subscriptions / Configurations / Resiliencies). Platform-owned ConfigMaps (`kube-root-ca.crt`, `default-token-*`) are excluded from the residual list.

**About `-PurgeDapr`:** DAPR is cluster-wide shared state. `dapr uninstall -k --all` removes the `dapr-system` namespace, control-plane Deployments, and CRDs — which breaks ANY other DAPR project running on the same cluster. The teardown script prompts for an explicit `PURGE` confirmation before running it (pass `-Force` to bypass for CI).

## DAPR component parity with `deploy/dapr/`

`deploy/dapr/*.yaml` remains the **authoritative DAPR component source** for Hexalith.Parties. Aspirate emits placeholder CRs from the AppHost's `WithReference(...)` graph but does not translate file-based DAPR configurations (access control, subscriptions, resiliency, real Redis/Kafka backing) into Kubernetes-equivalent CRs. The publish script therefore applies `deploy/dapr/` directly before the aspirate-generated set.

| `deploy/dapr/` template | `deploy/k8s/dapr/` analog | Component type | Notes |
|---|---|---|---|
| `statestore.yaml` | (stripped by `publish.ps1`) | `state.redis` | **Authoritative local-cluster default.** Carries `actorStateStore: true` and `keyPrefix: none`. |
| `pubsub.yaml` | (stripped by `publish.ps1`) | `pubsub.redis` | **Authoritative local-cluster default.** Carries `enableDeadLetter: true`. |
| `statestore-cosmosdb.yaml` | — | `state.azure.cosmosdb` | Operator opt-in alternative backend; publish script skips files matching `statestore-*`. |
| `statestore-postgresql.yaml` | — | `state.postgresql` | Operator opt-in alternative backend. |
| `pubsub-kafka.yaml` | — | `pubsub.kafka` | Operator opt-in alternative backend. |
| `pubsub-rabbitmq.yaml` | — | `pubsub.rabbitmq` | Operator opt-in alternative backend. |
| `pubsub-servicebus.yaml` | — | `pubsub.azure.servicebus` | Operator opt-in alternative backend. |
| `accesscontrol.yaml` | — | `Configuration` (deny by default) | Authoritative only; aspirate does not emit `Configuration` CRs. |
| `accesscontrol.eventstore-admin.yaml` | — | `Configuration` (locked down) | Authoritative only. |
| `accesscontrol.parties.yaml` | — | `Configuration` (eventstore → `/process` POST only) | Authoritative only. |
| `accesscontrol.tenants.yaml` | — | `Configuration` (cross-service ACL) | Authoritative only. |
| `subscription-parties.yaml` | — | `Subscription` | Authoritative only. |
| `subscription-tenants.yaml` | — | `Subscription` | Authoritative only. |
| `resiliency.yaml` | — | `Resiliency` (Dapr 1.14.4 schema) | Authoritative only. Server-side dry-run pre-flight in `publish.ps1`. |
| `topology.yaml` | — | `PartiesTopology` (`hexalith.io/v1` custom CRD) | Skipped at apply time; Hexalith CRD deferred to Story 9.3+. |
| `tenants-integration.yaml` | — | `TenantsIntegration` (`hexalith.io/v1` custom CRD) | Same Hexalith CRD gap. |

**Drift summary.** `deploy/dapr/` is the single authoritative source for every DAPR Component, Configuration, Subscription, Resiliency, and Topology CR. Aspirate's broken placeholders are stripped by `publish.ps1`. Operators switching to a managed backend overwrite `deploy/dapr/statestore.yaml` or `deploy/dapr/pubsub.yaml` with a copy of the matching variant template.

## Known aspirate limitations (recorded for this story)

These are accepted behaviors of aspirate 9.1.0 and not regressions in the Parties code base.

1. **Aspire manifest side effect.** `dotnet aspirate generate` writes an intermediate `src/Hexalith.Parties.AppHost/manifest.json` as a build artifact. `publish.ps1` cleans it after a successful run.
2. **DAPR component CRs are broken placeholders.** Aspirate emits `dapr/statestore.yaml` with `metadata: []` and `dapr/pubsub.yaml` with `spec.type: pubsub` (invalid). `publish.ps1` strips them after aspirate emits and removes the matching kustomization references in the same step.
3. **DAPR annotations are patched post-aspirate.** Aspirate 9.1.0 does not emit `dapr.io/app-port` and uses `dapr.io/config: tracing` as a hardcoded default. `publish.ps1` patches both annotations into every DAPR-enabled Deployment so sidecar callbacks reach port 8080 and the per-app access-control Configuration CRs are actually consulted. The patch is deterministic and idempotent.
4. **No `Subscription`, `Resiliency`, or `Configuration` CRs are emitted.** Those come from `deploy/dapr/` as described above.
5. **Hexalith custom CRDs (`topology.yaml`, `tenants-integration.yaml`) are skipped at apply time.** `apiVersion: hexalith.io/v1`; the CRDs themselves are not shipped today (deferred to Story 9.3+).
6. **Keycloak K8s manifests are hand-authored, not generated (Story 9.3 AC4 / ADR 9.3-3, path b).** Aspirate `9.1.0` cannot express stable `secretKeyRef` admin credential, `WithRealmImport` realm-bootstrap ConfigMap, or consumer-side JWT `secretKeyRef` envFrom. Story 9.3 closed the gap with `deploy/k8s/keycloak/` preserved by `publish.ps1`'s `$PreservedNames` and excluded from the byte-determinism contract on that subfolder only.

## JWT signing key + Zot pull secret bootstrap

`publish.ps1` bootstraps three operator-managed Secrets in the `hexalith-parties` namespace as Step 11 of the pipeline:

- `hexalith-jwt-signing` — 32-byte cryptographically random base64 key under `Authentication__JwtBearer__SigningKey`. Consumer Deployments source the JWT signing key via `valueFrom.secretKeyRef`. Idempotent re-apply.
- `hexalith-keycloak-admin` — 24-byte random base64 password under `KEYCLOAK_ADMIN_PASSWORD`. The Keycloak Deployment sources its admin password from this Secret.
- `zot-pull-secret` — `kubernetes.io/dockerconfigjson` rendered from the operator's `~/.docker/config.json` `auths["registry.hexalith.com"]` block (Path B emission — never decoded; the credential string lives on the heap only during a single `ConvertTo-Json → GetBytes → ToBase64String` pipe). Required by every `registry.hexalith.com/*` consumer Deployment via `spec.template.spec.imagePullSecrets`.

All three Secrets are applied via `kubectl apply -f -` (idempotent). On the `zot-pull-secret` path, the operator's credential never appears in stdout / stderr / any committed file. Docker credential helpers (`credsStore`, `credHelpers`) are explicitly NOT supported in MVP; `publish.ps1` exits 6 with an actionable error if it sees either directive.

## Dapr resiliency CRD schema drift (Story 9.3 AC6)

`deploy/dapr/resiliency.yaml` was rewritten against the Dapr 1.14.4 `resiliencies.dapr.io` CRD. `publish.ps1` runs `kubectl apply --dry-run=server -f deploy/dapr/resiliency.yaml` immediately after `dapr init -k` so any future Dapr CRD upgrade that drops or renames a field surfaces here (exit 1) before any namespaced resource is applied. The matching static lint `K8sDapr-ResiliencyCrdSchemaDrift` in `validate-deployment.ps1` catches drift in the committed file without requiring a live cluster.

## K8s manifest lint

Story 9.2 + Story 9.3 + Story 9.5 extend `deploy/validate-deployment.ps1` with a Kubernetes-manifest lint that runs alongside the existing DAPR config checks. It is **read-only** (no `kubectl`, no DAPR install) and inspects the aspirate-generated tree under `deploy/k8s/` plus the authoritative DAPR templates under `deploy/dapr/`.

```pwsh
pwsh deploy/validate-deployment.ps1 --config-path deploy/dapr -K8sPath deploy/k8s/
pwsh deploy/validate-deployment.ps1 -K8sPath deploy/k8s/ --output json
```

The lint emits one finding per category code. Findings are sorted ascending by `(category, code, target)` so two consecutive runs against the unchanged tree produce byte-identical JSON modulo the `timestamp` field.

| Category code | Severity | What fires |
|---|---|---|
| `K8sWorkload-MissingImage` | fail | Deployment container image is missing, empty, `[]`, or `null`. |
| `K8sWorkload-MissingDaprAnnotation` | fail | DAPR-enabled app missing one of `dapr.io/enabled`, `dapr.io/app-id`, `dapr.io/app-port`, `dapr.io/config`. |
| `K8sWorkload-UnresolvedConfigMapRef` | fail | `envFrom.configMapRef.name` does not match any `configMapGenerator.name` in the same app folder. |
| `K8sWorkload-UnresolvedKustomizationResource` | fail | Top-level `kustomization.yaml` `resources:` entry that does not resolve under `deploy/k8s/`. |
| `K8sWorkload-MissingImagePullSecret` | fail | A `registry.hexalith.com/*` Deployment is missing `spec.template.spec.imagePullSecrets: [{ name: zot-pull-secret }]` (Story 9.5 AC9). Vendor-image carve-outs are excluded by the image-prefix gate. |
| `K8sWorkload-MissingProbes` | warn | Deployment missing both `readinessProbe` and `livenessProbe`. |
| `K8sWorkload-MissingResources` | warn | Container missing `resources.requests`/`resources.limits`. |
| `K8sWorkload-LatestImageTag` | warn | Container image uses `:latest` (explicit or implicit). |
| `DAPR-ACL-DefaultActionNotDeny` | fail | An `accesscontrol*.yaml` Configuration CR has `defaultAction` other than `deny`. |
| `DAPR-ACL-WildcardAppId` | fail | An ACL policy uses a wildcard `appId: '*'` / `'**'`. |
| `DAPR-Subscription-MissingDeadLetter` | fail | A Subscription CR is missing `deadLetterTopic`. |
| `DAPR-Subscription-WrongPubsubName` | fail | A Subscription CR uses `pubsubname` other than `pubsub`. |
| `DAPR-Regen-PlaceholderNotStripped` | fail | `deploy/k8s/dapr/statestore.yaml` or `deploy/k8s/dapr/pubsub.yaml` re-appeared (publish invariant violated). |
| `K8sSecret-PlaintextCredential` | fail | A `password=`/`secret=`/`token=`/`api_key=` shape with a non-placeholder value. |
| `K8sSecret-UrlEmbeddedCred` | fail | URL with embedded user:password. |
| `K8sSecret-JwtTokenLiteral` | fail | JWT literal (`eyJ...` three-segment base64). |
| `K8sSecret-AwsAccessKey` | fail | AWS access key (`AKIA[0-9A-Z]{16}`). |
| `K8sSecret-AzureConnString` | fail | Azure connection string. |
| `K8sSecret-PrivateKey` | fail | PEM private-key header. |
| `K8sSecret-StaticTenantId` | fail | `Tenants__TenantId`/`TENANT_ID`/`*__TenantId` with a literal value. |
| `K8sSecret-CommittedSecretValue` | fail | A `kind: Secret` whose `stringData`/`data` carries a non-placeholder value. |
| `K8sSecret-JwtSigningKeyLiteral` | fail | Inline JWT signing-key literal (must use `valueFrom.secretKeyRef`). |
| `K8s-NonLocalClusterCapability` | fail (warn with `-AllowCloudCapabilities`) | Cloud-only `StorageClass`/`IngressClass`/`Service.type: LoadBalancer`. |
| `K8s-YamlParseError` | fail | A YAML document failed to parse; the offending content is **not** echoed. |
| `K8s-PathTraversal` | fail | A symbolic link under `deploy/k8s/` resolves outside the repo. |
| `K8sDapr-ResiliencyCrdSchemaDrift` | fail / warn | `deploy/dapr/resiliency.yaml` carries the legacy nested or flat-component shape rejected by the Dapr 1.14.4 CRD. |
| `K8sTopology-MissingService` | fail | Expected per-app folder is missing or its Service selector does not match the Deployment label. |

The validator output never echoes raw env-var values, `Secret.data`/`Secret.stringData` contents, ConfigMap literal values, tenant identifiers, bearer/JWT tokens, DAPR access-control policy bodies, or **operator Docker credentials**. Offending values are reported as `<redacted:N chars at <file>:<line>>`. Control characters in file paths are sanitized to `?`.

## Out of MVP scope

The following are **out of scope** for the Story 9.x set and the MVP. Future stories own them.

- Automated CI step that runs `publish.ps1` and validates the diff — Story 9.6 (deferred per `sprint-change-proposal-2026-05-20-zot-build-push.md`).
- Image signing (cosign / sigstore) and SBOM emission — post-MVP supply-chain hardening.
- Multi-registry support (ACR / Harbor / Docker Hub / GHCR) — single-registry MVP. The `--container-registry registry.hexalith.com` flag is hard-wired into `publish.ps1`.
- Docker credential helper (`credsStore` / `credHelpers`) support — `publish.ps1` Step 11 reads plain-text `auths["registry.hexalith.com"].auth` only.
- Per-image resource limits / probes / pod-disruption budgets — tracked as "Aspirate output hardening" candidate.
- TLS termination beyond `kubectl port-forward`.
- Ingress controllers, TLS issuer CRDs, or DNS wiring inside the deployed namespace.
- Production-grade Zot (Postgres-backed metadata, HA, external storage) — cluster-side infra-team concern.

## Troubleshooting

- **`publish.ps1` errors with `aspirate is not a tool`.** Run `dotnet tool restore` from the repository root.
- **`publish.ps1` exits with code `2`.** `-ConfirmContext` value does not match `kubectl config current-context`. Switch with `kubectl config use-context <name>` or pass the correct `-ConfirmContext` argument.
- **`publish.ps1` exits with code `5`.** MinVer resolved to empty or non-SemVer. Check `Directory.Build.props` `<MinVerTagPrefix>v</MinVerTagPrefix>` and ensure `v*` git tags are visible (`git fetch --tags`).
- **`publish.ps1` exits with code `6`.** No plain-text credentials for `registry.hexalith.com` in `~/.docker/config.json`, OR the config uses `credsStore` / `credHelpers`. Run `docker login -u parties-publisher registry.hexalith.com`, or set `$env:DOCKER_CONFIG` to point at a directory whose `config.json` has no credential helper.
- **`kubectl apply -k` fails with namespace conflict.** Run `pwsh deploy/k8s/teardown.ps1 -ConfirmContext <name>` first to remove a previous deployment.
- **DAPR sidecars not starting.** Verify `dapr status -k` shows the control plane is `Running`. Re-run `publish.ps1` without `-SkipDaprInit`.
- **`publish.ps1` produces unexpected diff.** Confirm the pinned aspirate version (`.config/dotnet-tools.json`) matches what the AppHost expects. Per-commit diff on image-tag lines is expected (MinVer); non-image lines should be byte-stable.
