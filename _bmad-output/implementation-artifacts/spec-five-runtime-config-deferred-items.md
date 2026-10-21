---
title: 'Close five deferred runtime and configuration items'
type: 'bugfix'
created: '2026-10-21'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Five open items need verified disposition: G8-A delivery receipt integrity (DW-14), the optional sample's receiver identity (DW-164), the optional Memories run endpoint (DW-165), loop-sweep ledger configuration (DW-181), and the visual-tool configuration shape (DW-186).

**Approach:** Complete these five as requested. Bind the historical owner receipt to its exact Git objects, inventory and test lane; explicitly configure the sample receiver identity and use the Aspire endpoint reference for Memories. Trace the two BMAD configuration concerns through the installed consumers and close already-resolved or disproved reports with reproducible evidence. Preserve historical proof and dependency selections. Validate, independently review, reconcile the five entries, commit and push main.

</frozen-after-approval>

## Implementation Notes

- Initial clean main revision: fb07a6cf14507bcae371f957b0272f3f4bbead84 (git rev-parse HEAD). User authorized the five-item batch and push to main. No unresolved product choice, irreversible implementation action, dependency update, stored-data migration, new production API or deployment is needed. Bounded AppHost corrections and test/evidence work use the oneshot route.
- Code map: src/Hexalith.Parties.AppHost/Program.cs already sets EventStore__DomainService__AppId for parties and tenants, but omits sample. Match its actual Dapr AppId sample. Memories is a ProjectResource; replace the literal container hostname with memories.GetEndpoint("http") while retaining existing reference/wait and search options. Evaluate actual run and publish model callbacks in IntegrationTests/Topology and allocate a distinct run endpoint to prove dynamic resolution. Keep the optional resources disabled by default in run mode and preserve published connectivity.
- Code map: the G8-A section of story-8-3-platform-api-prerequisite-matrix.md records full producer base/delivery/integration and consumer identities, the four-file inventory and the exact JWT test class command with 26 historical cases. Reuse FitnessTests/RepositoryTestProcess.cs for bounded Git reads; inspect the delivery object's parent, changed paths, API and test blobs and integration ancestry rather than current upstream file bytes. Reject changed receipt identities, missing/extra inventory and unrelated test-lane claims. This validates provenance, not a new producer runtime run, publication or consumer-adoption approval.
- Code map: .agents/skills/bmad-loop-sweep/SKILL.md and its .claude counterpart now require the orchestrator-owned BMAD_LOOP_LEDGER and fail closed when absent. Do not restore an obsolete YAML or TOML fallback. _bmad/scripts/config_utils.py and resolve_config.py implement four-layer configuration; render_skill.py's scalar resolver requires strings. Trace modules.cis.visual_tools from base TOML through team/user overrides and renderer. Search tracked non-reference consumers before deciding whether the JSON-string setting is defective. Preserve installer-managed values when the concern is disproved.
- Acceptance: Given optional sample enabled in run or publish mode, its SDK identity equals the attached sidecar identity. Given an allocated Memories run endpoint, the injected search URL resolves to it; publish output uses the same resource reference. Given the historical owner receipt, all declared immutable identities, changed files and test-lane claims match the referenced Git objects; missing or altered claims fail. Given loop automation, the orchestrator's ledger path is authoritative and absent paths fail closed. Given the visual-tools setting, runtime resolution and actual consumers establish its accepted representation without speculative configuration changes.
- Verification: focused Debug builds and direct xUnit class runs, Python configuration tests and an evidence trace, diff and warning-policy checks. Before edits, Aspire start succeeded; describe showed dependent resources Waiting and security Unhealthy; exact-target stop succeeded. Record that runtime limitation separately from model verification. No broader deployment, live JWT enforcement or release qualification is claimed.

- Implementation completed: explicit sample receiver identity and resource-based Memories HTTP endpoint, exact-object G8-A receipt fitness with eleven tamper cases, and layered visual-tool scalar rendering regressions. Existing installed sweep behavior supersedes DW-181; no configuration or skill entry point was edited.
- Focused verification passes 143 fitness, 42 topology and 19 Python tests without failures or skips. Both Debug builds have zero warnings/errors. The final default AppHost starts and reports healthy EventStore, Parties, Tenants and security; exact-target stop succeeds. Optional resource behavior is model-qualified rather than claimed as a live runtime result. Exact commands and sanitized observations are in tests/five-runtime-config-deferred-items-2026-10-21/.


## Review Triage Log

- Medium, patch: identity matching accepted conflicting extra declarations. Count all declarations of each field independently of the expected value and cover conflicting base, delivery, integration, consumer and gitlink claims.
- Medium, patch: replacing every SHA in a fixture masked individual command-location gaps. Bind the clean-worktree, diff inspection and remote integration command/result cells to the same immutable identities; add isolated mutations for each location.
- Medium, patch: lane validation searched an entire Markdown row and could accept a failed result with success text in the command. Parse escaped-pipe-aware cells and require the exact focused command plus success in the result cell; cover the reproduced spoof.
- Medium, patch: callback evaluation alone did not establish the publish serialization claim. Use the actual ManifestPublishingContext environment writer and assert the persisted Memories endpoint expression; keep model proof distinct from target deployment/runtime parity.
- Low, patch: operation arguments were not asserted. Check IsRunMode and IsPublishMode explicitly for each theory case.
- Low, patch: visual configuration tests stopped before source rendering. Exercise _render_sources and assert the resulting Markdown for every configuration layer.
- Low, patch: the evidence trace unnecessarily pinned default selected tools. Validate a decoded string-list representation independently of its contents so valid user overrides remain reproducible.
- Low, patch: the standalone ledger trace was outside Python discovery. Add a normal discovered regression protecting both installed sweep entry points against the obsolete fallback.

- Independent follow-up confirms all eight review findings addressed with no unresolved concrete issues. No new deferred item was introduced. The reviewed changes preserve exact historical identity proof, optional-resource model behavior and installer configuration.

- Final verification after review: 153 fitness, 42 unique topology and 20 Python tests pass (215 unique cases; no failures/skips). Both affected Debug builds have zero warnings/errors. The two revised optional-resource cases pass again with actual manifest environment serialization; all eight independent findings are resolved, and no work was deferred. Pinned commitlint validates the exact commit message. All five selected entries are done.
