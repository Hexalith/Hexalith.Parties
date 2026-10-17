# DW-171 bounded cache read tracking

Completed DW-171: historical eviction keys are replaced by capped active-read tokens. Every query and convenience store releases its registration. Eviction removes all pending tokens for its exact key without retaining inactive identifiers or invalidating other keys. Token capacity and overflow fail closed, preserving existing degraded fallback.

The regression test fails against the original production cache: 512 missing keys remain despite capacity four. The final test requires zero retained identifiers. Final Debug/source build: zero warnings/errors. Focused cache/query class: 93/93 passed. Full service assembly: 1066/1066 passed, with zero failures/errors/skips. See `validation.json` for commands, source hashes, XML digests and runtime evidence. Result paths/redirection are omitted from rerunnable commands; raw local output is not published.

Coverage includes all three slots, overlapping reads, tenant/party/slot isolation, old-token completion after a fresh read, tracking saturation with empty and seeded caches, fallback identities, repeated completion/error/cancellation, convenience-store failure cleanup, token exhaustion, and 32 coordinated readers against capacity eight.

Independent review raised six findings. Five verification/documentation gaps were corrected and verified in follow-up. The bounded eviction scan was retained after explicit low-severity triage; no concrete latency regression was demonstrated. No new deferred work. Cross-instance cache invalidation remains DW-172.

The Aspire baseline reached healthy Parties resource status and was stopped before building. No dependency, gitlink, runtime configuration or deployment change is part of this fix. Changed C# files follow CRLF; the recorded whitespace command accepts that required line ending without changing Git configuration.
