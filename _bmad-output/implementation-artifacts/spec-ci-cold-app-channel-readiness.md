---
title: 'Qualify cold topology app channels with concrete consumer endpoints'
type: 'bugfix'
created: '2026-10-28'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="user-authorized CI repair; parent approved fixture-only scope">

## Intent

**Problem:** Parties CI 40623509857 starts the gateway sidecar against a listening
proxy before Kestrel, loses the initial actor configuration and later returns
HTTP 500 during the mandatory persisted counter seed. Admin.Server also receives
a producer-only DCP target-port template rather than a usable sidecar URI.

**Approach:** In the owned fixture, make Dapr wait on the actual gateway HTTP
listener, preserving dynamic allocation and the existing direct Admin UI listener.
Bind Admin.Server through the generated CLI's deferred consumer EndpointReference,
matching the toolkit's own cross-resource wiring. Regress the producer-template
and proxy-port distinction and actual app-channel endpoint selection. Preserve
all actor/state/isolation assertions, randomization, mTLS, startup deadlines,
production configuration, root refs and the prohibition on nested initialization.
Run focused controls, independent review and pinned commitlint before publishing.

</frozen-after-approval>

## Implementation Notes

- Baseline: ebdaef74d3fd3572174d187b281842463de25d21; clean and current on fetch.
- Actual CI: 170 tests, 155 passed, 14 existing optional skips, one counter seed
  failure. Both isolated fixtures start and all four real live shutdown cases pass.
- First gateway daprd discovers the proxy at 03:08:41; actual Kestrel starts
  03:11:04. Placement receives initial actor types []. Second gateway loads and
  registers AggregateActor. Dapr1.18.2 runtime appHealthReadyInit loads app config
  once; loadAppConfiguration silently returns on GetAppConfig error.
- Primary runtime source: https://github.com/dapr/dapr/blob/v1.18.2/pkg/runtime/runtime.go
- Admin DCP logs reject portForServing for the CLI service because the consumer
  does not produce that service. Toolkit source commit
  523b84b27c5d019d242d95c2252046ba3619303c assigns CLI EndpointReference to its
  parent's DAPR_HTTP_ENDPOINT; use that same supported consumer value.
- Small reversible footprint: fixture, PartiesMtlsProfile, its focused tests and
  this owning spec. No intent gaps or irreversible runtime/data effects.
- Review exposed the same captured race in the first complete-profile Memories
  actor host: proxy03:08:50, actual Kestrel03:10:58; only built-in workflow actor
  types registered. Parent approved identical listener treatment for this exact
  resource. It applies only to complete or Memories-scoped fixtures; the ordinary
  profile does not declare Memories. Existing scoped construction explicitly
  enables and requires it before entering fixture initialization.
- Actual toolkit BeforeStart rendering now qualifies generated --app-port for
  gateway, Memories and explicit-start Admin UI against an injected listener.
  The consumer binding regression retains a real producer-only portForServing
  template, distinct fixed/target/proxy ports and allocation changes.
- Final IntegrationTests Debug build with warning-as-error: zero warnings/errors.
  Focused fixture/endpoint/cleanup/session/Redis rendering controls: 45 passed,
  zero errors/failures/skips/not-run. Existing CI bootstrap/cleanup/standalone
  controls: 34 passed, zero errors/failures/skips/not-run.
- Exact commit message validated with owning commitlint21.2.2 under workflow-pinned
  Node24.22.1: zero problems or warnings. Full hosted Aspire lane remains the
  unchanged qualification for the two-session runtime and live shutdown cases.
- Parent independently accepted final fixture-only scope and blind review triage.

## Review Triage Log

- medium, patch: the captured cold Memories host also lost initial actor config.
  Apply the exact listener correction in complete and Memories-readiness profiles;
  ordinary construction omits this optional resource and remains unchanged.
- medium, patch: annotation controls alone did not qualify generated app-port.
  Reused the existing installed-toolkit lifecycle rendering seam and compare the
  generated app argument to an injected listener for all three affected channels.
- low, rejected: an additional expected-actor metadata parser would move an
  already mandatory seed/state failure earlier. Actual-listener configuration
  fixes the demonstrated cause; existing actor/state assertions still fail
  closed. Expanding resource-specific metadata contracts is unnecessary here.
- Blind reviewer rechecked both patches and found no concrete introduced bug.
- Concurrent upstream e1650f8c5db42a668fec3cbe7bbef17adeae200c independently
  added the same gateway listener correction and a gateway actor metadata guard
  before this commit. Fast-forwarded and preserved that guard and its four
  boundary controls; shared helper replaces the duplicate listener block.
  Earlier triage records our proposed scope, not a removal of upstream guards.
- Final integrated controls after upstream adoption: 49 passed, zero errors,
  failures, skips or not-run; warning-error build again has zero warnings/errors.
