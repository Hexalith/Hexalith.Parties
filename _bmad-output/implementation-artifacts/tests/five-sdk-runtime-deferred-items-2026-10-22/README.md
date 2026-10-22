# Five SDK/runtime deferred items — 2026-10-22

Final parent checks pass: 2,534/2,534 Release service tests, 2,534/2,534 mapped
Debug service tests, 28/28 integration checks (seven live host cases), and 10/10
publisher controls, with zero qualifying skips. All four live cleanup modes pass
after review and the retry-state correction. See [final-verification.json](final-verification.json)
and [final-runtime-tests.json](final-runtime-tests.json). The integration Debug
mapper blocker and one unverified descendant-process deferral remain explicit.

The initial implementation Release/package qualification passed: full Parties.Tests 2534/2534,
explicit live integration batch 22/22, publisher controls 5/5, and the focused
non-child process observer 1/1. There are zero qualifying skips. Four affected
builds pass with zero warnings/errors. Exact commands, results and source hashes
are in [validation.json](validation.json); dependency/package/verifier identities
are in [dependency-identities.json](dependency-identities.json).

The HTTP regression uses the actual public gateway QueryRouter and handler with
substituted Dapr transport. The Memories repair cases read serialized canonical
state and write stored mappings. Neither is claimed as durable runtime proof.
The separate live batch runs real Dapr, mTLS, private Redis, Tenants readiness,
authenticated gateway create/query and persisted stream controls. Its methods,
counts and cleanup observations are in [runtime-tests.json](runtime-tests.json).

The owner verifier emits two actual Aspire SDK manifests and removes private
output, including on failure. [owner-publish-receipt.json](owner-publish-receipt.json)
contains only hashes/counts/grant names; [owner-publish-tests.txt](owner-publish-tests.txt)
records emitted-copy credential and endpoint rejection. These qualify local SDK
manifest emission; no target publisher or deployment qualification is claimed.

The [shutdown trace](shutdown-sdk-inspection.json) binds installed Aspire 13.6.1
and DCP 0.26.5 to their hashes. DCP's exact source has no executable-before-container
cleanup dependency, so the fixture explicitly drains owned hosts and then sidecars
before fully awaited SDK disposal. A separate cached Process false positive was
repaired by Linux kernel PID/start-clock identities, with a focused non-child
regression. One cumulative 30-second worker budget spans stop passes, exit checks,
the observer and automatic fallback. Other SDK/bootstrap budgets remain distinct.

All four cleanup modes prove worker exit while backing containers and certificates
still exist, then prove container/network/private-root removal and reusable ports:

| Mode | Recorded workers | Backing containers | Observed total cleanup |
|---|---:|---:|---:|
| Normal | 17 | 3 | 31.189 s |
| Canceled startup | 14 | 3 | 26.131 s |
| Failed startup | 16 | 3 | 15.759 s |
| Reported cleanup failure, then explicit retry | 17 | 3 | 5.764 s retry |

Normal and cleanup-failure ownership is refreshed after successful initialization,
including the explicitly started admin UI and all eight sidecars. Partial-start
cases record only resources actually started. The cleanup-failure case also proves
that the first reported observation failure retains ownership for retry. Actual
total cleanup is below the tests' 60-second observation deadline; this is not an
unconditional global SDK shutdown bound. No started disposal task is abandoned.

Earlier failed/canceled attempts are listed separately in validation.json and are
not acceptance evidence. [all-owned-cleanup.json](all-owned-cleanup.json) verifies
all 25 session roots/control planes absent; [all-backing-cleanup.json](all-backing-cleanup.json)
checks all 75 backing-container names recorded by these attempts. Unrelated
resources were untouched. Raw logs, DCP environment/status, tokens, certificates,
manifests and temporary paths are excluded from these normalized artifacts.

The final parent review, reruns and source qualification are recorded separately
in [final-verification.json](final-verification.json) and
[source-qualification.json](source-qualification.json). Historical counts and hashes
above describe the initial checkpoint; they do not supersede those final receipts. No dependencies, gitlinks, deployment assets, signoffs or persistence
formats changed; DW-116 and DW-126 remain open.
