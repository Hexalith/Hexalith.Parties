# DW-20 validation — 2026-10-17

Both local search providers exclude the synthetic full phrase from matched-token coverage. Phrase candidates still determine the strongest field match and metadata. Existing ordinal-distinct real-token counting, original token-count denominator, field weights and score formula are preserved.

Initial regression cases run against unchanged production code: 19 total, 17 expected failures and two exact-match controls passing. Prefix relevance is incorrectly .87 instead of .82; contains relevance is .69 instead of .64. The phrase incorrectly wins a tie that should use name ordering. Repeated-token phrase cases distinguish this fix from merely clamping coverage to one. See baseline-summary.txt.

After independent review, 29 direct relevance cases cover single tokens, two/three-token phrases, identical and case-varied repeated tokens, contact/identifier weights, simultaneous multi-field phrase matches, name ordering and semantic SortName precedence across pages and reversed inputs. The focused provider, compatibility and performance classes pass 109/109. The full service assembly passes 1127/1127 without failures, errors, skips or unrun tests. Final Debug/source build has zero warnings/errors; warning and whitespace guards pass. All four review findings were patched and independently confirmed; no new work was deferred.

The 10K allocation regressions still pass: longer-query growth is 968 bytes and 2-to-32-token growth is 2160 bytes in each provider. Required latency assertions pass; the 100K fuzzy test is a measurement-only diagnostic, not a 500 ms acceptance assertion.

Commands and result counts are in validation.json. Invoke commands from the Parties root after creating /tmp/dw-20-validation. The baseline command describes the initial tests before the fix and review additions; it is not expected to fail on final code. Focused XML retains assertion outcomes and allocation/latency measurements, with machine identifiers and the absolute workspace prefix removed. Build and full-run summaries preserve exact totals without unrelated service logs. Raw logs and full XML remain under /tmp/dw-20-validation for this session.

The exact Parties AppHost was started, inspected and stopped. The baseline `aspire wait parties --timeout 20 --apphost src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj --non-interactive` exited 17: timed out after 20 seconds. Security was running but unhealthy because its TLS readiness connection ended unexpectedly; Parties and dependencies were Waiting. Full-topology acceptance is not claimed. No package-mode/Release or deployment qualification was attempted for this local scoring correction.

The exact local commit candidate passed repository-pinned commitlint 21.2.2 with zero problems/warnings; commitlint.txt retains that proof. No push. Existing source line endings are preserved; the new C# file uses CRLF. Git whitespace checks recognize CR at line ends.
