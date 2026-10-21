# Five runtime and configuration deferred items

DW-14, DW-164, DW-165, DW-181 and DW-186 are covered by the sibling
`validation.json`, exact command logs, and sanitized resource observations.
Both affected Debug builds pass with zero warnings/errors. The focused suites
pass 153 fitness tests, 42 topology tests and 20 Python tests without skips.
All eight independent review findings were fixed; follow-up found no unresolved
concrete issues. The two revised optional-resource cases also serialize the
publish environment and assert the Memories endpoint expression.

The historical G8-A check reads exact Git objects. It verifies provenance and
the existence of the recorded focused test lane, preserving the original dated
test results without claiming a fresh producer run or consumer approval.

Actual run and publish model callbacks verify the optional sample identity and
the Memories HTTP reference. Reallocating that reference to a second address
proves it follows Aspire allocation. A default live AppHost start/describe/stop
also succeeds; the final observation has healthy EventStore, Parties, Tenants
and Keycloak. Optional workload runtime parity is not inferred from that run.

`python3 -B _bmad-output/implementation-artifacts/tests/five-runtime-config-deferred-items-2026-10-21/config-trace.py`
reproduces the installed ledger and visual-tool observations without invoking
skills. Sweep already requires the orchestrator's resolved ledger path. No
installed tracked non-reference CIS consumer requires a visual-tool array;
layered scalar resolution and template replacement are covered separately.
Installer configuration and historical parity evidence remain intact.
