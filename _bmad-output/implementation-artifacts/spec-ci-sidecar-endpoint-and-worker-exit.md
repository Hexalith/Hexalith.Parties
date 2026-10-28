---
title: 'Repair owned topology endpoint binding and exited-worker observation'
type: 'bugfix'
created: '2026-10-28'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="user-authorized CI repair; parent delegated Parties remediation">

## Intent

**Problem:** Parties CI 40617202875 exhausts the isolated topology deadline while
Admin.Server requests the pre-randomization gateway sidecar port. A separate
cleanup case races Linux process exit while reading its immutable start identity.

**Approach:** In the test fixture only, bind Admin.Server to the exact generated
gateway sidecar HTTP target endpoint through a deferred SDK reference. Preserve
random port allocation, session isolation, mTLS, health, state and cross-call
assertions. Handle a failed process-stat read only when the same PID is proven
exited or absent; propagate live-worker I/O errors and preserve PID-reuse checks.
Add focused regressions, run warning-error builds and relevant fixture controls,
and publish the owning change after independent review and pinned commitlint.
Do not change production configuration, startup deadlines, root refs or nested
submodule initialization.

</frozen-after-approval>

## Implementation Notes

- Baseline: 35e82673a1e3e7fa6c13a2c996fa4eb7f56c179e, upstream unchanged on fetch.
- Actual CI: Admin.Server requests localhost:44209; its gateway daprd listens18695.
- Verified Aspire.Testing13.6.4 defaults DcpPublisher:RandomizePorts=true.
- Toolkit13.6.1-beta.935 source commit523b84b27c5d019d242d95c2252046ba3619303c
  creates CLI endpoints before environment resolution and passes daprd the HTTP
  EndpointProperty.TargetPort. The fixture will follow that deferred endpoint.

- Bind Admin.Server in both fixture profiles; preserve the original production SDK
  and test randomization, target listener allocation, mTLS and startup deadline.
- Fresh Linux fallback reads the same PID stat independently. Only Z/X or a fresh
  FileNotFound/DirectoryNotFound proves exit; live or repeated I/O faults propagate.
- Final warning-error IntegrationTests and Ci.Tests Debug builds: zero warnings/errors.
- Final focused endpoint, fixture lifecycle and cleanup controls: 35 passed, zero
  failures/errors/skips/not-run. The unchanged three live topology shutdown cases
  remain in the hosted Aspire qualification, not this local infrastructure selection.
- Broader local CI contracts: 385 passed, 79 failed because the flat workspace has
  no nested Builds/Commons checkout, complete restored CI graph, published UI
  runtime configuration or release artifacts. These prerequisites were not fabricated
  and guards were not relaxed; hosted CI executes its unchanged standalone layout.
- Parent independent review accepted the final production-free helper change and
  fresh-kernel fallback. The one-shot blind review produced the dispositions below.

## Review Triage Log

- medium, patch: Process.HasExited can reuse .NET cached child wait state for a
  recycled PID. Replaced it with a fresh same-PID stat observation; live I/O and
  mismatched lifetime controls remain mandatory.
- medium, patch: the ordinary fixture also launches Admin.Server with the stale
  pre-allocation URL. Deferred target binding now applies to both fixture profiles.
- low, rejected: an additional isolated toolkit lifecycle test would duplicate the
  unchanged mandatory hosted ConcurrentSessions and shutdown topology controls.
  The focused regression proves deferred CLI lookup, fixed/proxy/target distinction
  and reallocation; the real Aspire lane remains the lifecycle qualification.
- Parent review, patch: add meaningful non-Linux behavior to the new read-race
  controls. They verify platform process lifetime and that Linux stat is unused.

- Integrated concurrent upstream 392705bfcc0aef061590e94acd52a918034b42bf
  before committing; retained its direct admin UI listener and initialization
  phase diagnostics. Its injected ESRCH control now verifies a living PID cannot
  be declared exited from an error code alone. Final combined focused controls:
  35 passed with no skips; rebuilt IntegrationTests with no warnings or errors.
- Final existing CI bootstrap, cleanup ownership and standalone solution guards:
  34 passed with no skips. Exact owning commit message passed commitlint21.2.2
  under the workflow-pinned Node24.22.1 with no problems or warnings.
