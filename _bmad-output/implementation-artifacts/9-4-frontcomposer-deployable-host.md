# Story 9.4: FrontComposer Deployable Host Project

Status: blocked

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an operator deploying the full Hexalith.Parties topology to a local Kubernetes cluster,
I want the Hexalith.FrontComposer submodule to ship a deployable service host project that the Hexalith.Parties Aspire AppHost can compose as `AddProject<…>`,
so that FrontComposer participates in the in-cluster topology that Story 9.3 (Outcome B carve-out) deferred — closing FR31a's enumerative "AppHost is the single source of truth for the service graph" promise for the FrontComposer slot.

## Acceptance Criteria

1. **FrontComposer ships a deployable host project**
   - **Given** the FrontComposer submodule currently exposes `Hexalith.FrontComposer.Cli` (dotnet-tool Exe), `Hexalith.FrontComposer.Mcp` (`Microsoft.NET.Sdk` library, no `Program.cs`), and `Hexalith.FrontComposer.Shell` (`Microsoft.NET.Sdk.Razor` UI library) — none of which is a deployable service host today (verified during Story 9.3 Task 4 audit, 2026-05-19),
   - **When** the FrontComposer submodule maintainers ship a deployable host project (e.g., `Hexalith.FrontComposer.Server` as `Microsoft.NET.Sdk.Web` with a `Program.cs` host entry point, or extend `Hexalith.FrontComposer.Mcp` into an SDK-Web host),
   - **Then** `Hexalith.Parties.AppHost/Program.cs` can compose it as `builder.AddProject<Projects.Hexalith_FrontComposer_…>("frontcomposer")`,
   - **And** aspirate emits `deploy/k8s/frontcomposer/` (deployment + service + per-app kustomization) with the documented Dapr annotations on both annotation blocks if the host is Dapr-enabled,
   - **And** `deploy/k8s/regen.ps1`'s `$ExpectedAppFolders` post-condition list grows to include `frontcomposer`,
   - **And** Story 9.3's `K8sTopology-MissingService` lint expected-set constant in `deploy/validate-deployment.ps1` grows to include `frontcomposer`,
   - **And** Story 9.3 AC8's pod-count budget grows from 9 to 10 (`docs/getting-started.md` Step 1b readiness check updated accordingly).

## Dev Notes

This story is the FrontComposer follow-on carved out from Story 9.3 per **Outcome B** of Story 9.3 AC3. See Story 9.3 Completion Notes for the inspection rationale (which projects were inspected, why none qualified). See Story 9.3 ADR 9.3-4 for the Outcome A vs Outcome B decision frame.

Authoring a new host project from within Story 9.3 was explicit scope creep; this story is the dedicated work item.

This story remains blocked until the FrontComposer submodule exposes a deployable service host project that can be composed by the Parties AppHost. Keeping the artifact and marking it blocked preserves the Story 9.3 carve-out evidence while satisfying the sprint-status artifact invariant that `backlog` stories do not have story files.

Epic 9 retro is gated on Story 9.4 done (in addition to Story 9.3 done).

## References

- [Source: _bmad-output/implementation-artifacts/9-3-close-k8s-deployment-spec-gaps.md] — Outcome B carve-out rationale + Completion Notes record.
- [Source: _bmad-output/planning-artifacts/prd.md#FR31a] — Single-source-of-truth service-graph FR that drives the topology completeness contract.

## Change Log

- 2026-05-19: Marked blocked to reconcile predev preflight status-artifact drift; dependency is the missing FrontComposer deployable service host.
- 2026-05-19: Story created by Story 9.3 Task 4 Outcome B carve-out. FrontComposer host-readiness verified absent during Story 9.3 day-1 audit; this story tracks the upstream FrontComposer change needed to close the topology gap.
