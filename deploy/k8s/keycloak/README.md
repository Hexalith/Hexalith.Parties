# Keycloak — Hand-Authored Carve-Out (Story 9.3 AC4 / ADR 9.3-3)

This folder is the **hand-authored Keycloak carve-out** for the Hexalith.Parties MVP local-cluster deployment. Path (b) per Story 9.3 AC4 — chosen over aspirate-native emission because aspirate `9.1.0` cannot express:

1. Keycloak admin credential via stable `secretKeyRef` (aspirate captures a randomized value into the emitted ConfigMap each regen → breaks byte-determinism + violates the no-secret-values contract).
2. `WithRealmImport` realm-bootstrap ConfigMap.
3. Consumer-side JWT `secretKeyRef` envFrom from an Aspire `WithEnvironment(...)` literal.

## Byte-Determinism Carve-Out Scope

This folder is preserved by `regen.ps1` (`$PreservedNames` list) and is asserted on a **presence-only** basis by the topology lint (`K8sTopology-MissingService`). The Story 9.1 byte-determinism contract is **excluded** on this subfolder only — hand-authored edits are expected and survive the `regen.ps1` clean step.

## Realm Import Workflow

The committed `hexalith-realm.json` is the realm bootstrap source. Its authoritative copy lives at `src/Hexalith.Parties.AppHost/KeycloakRealms/hexalith-realm.json`; `deploy-local.ps1` refreshes the local copy in this folder immediately before invoking `kubectl apply -k deploy/k8s/` so the two stay in sync. The kustomize `configMapGenerator.files` entry then materializes the JSON into a `keycloak-realm-import` ConfigMap that the Deployment mounts at `/opt/keycloak/data/import`.

## Admin Bootstrap Secret

The Keycloak admin password is sourced from a Secret named `hexalith-keycloak-admin` (key: `KEYCLOAK_ADMIN_PASSWORD`). `deploy-local.ps1` bootstraps this Secret from a development-mode random value with `kubectl apply --dry-run=client | kubectl apply -f -` (idempotent). The Secret is **never committed**.

## Architectural Debt

This carve-out is documented as architectural debt for Epic 10 tool-choice review — see Story 9.3 Dev Notes § "Known Contradiction — Generator-of-Truth vs Hand-Authored Carve-Out" for the resolution-path options (extend aspirate / replace aspirate / formally split `deploy/k8s/` and `deploy/infra/`).
