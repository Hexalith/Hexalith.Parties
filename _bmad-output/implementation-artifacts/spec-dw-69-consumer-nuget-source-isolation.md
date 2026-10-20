---
title: 'DW-69 isolate consumer validation NuGet sources'
type: 'bugfix'
created: '2026-10-20'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Package-only consumer validation inherits undeclared NuGet feeds and retains nuget.org even when callers explicitly select another source. Those feeds can hide missing local package output and make validation depend on machine configuration.

**Approach:** Clear inherited package sources before declaring both local feeds. Use nuget.org only when no `--nuget-source` option is supplied; otherwise use exactly the repeated explicit values alongside the local feeds. Preserve the isolated package cache, support-package versions and package-only Release consumer validation.

</frozen-after-approval>

## Implementation Notes

- Selected DW-69 as the next unresolved bounded consumer-validation item after completed DW-67/DW-68. Baseline: a4924d3eda23a9a57ddf3e859161077806b14490, clean main. Investigation delegated to a read-only agent as directed by bmad-build.
- Route assessment: no intent gap, irreversible implementation action or public API; small footprint limited to the consumer-validation script, existing CI behavior tests, concise CI documentation and workflow/evidence records. User separately authorized committing and pushing.
- `scripts/validate-consumer-package-references.py`: insert `clear` as the first packageSources child in `write_nuget_config`; replace argparse's append-list default with a fallback applied only when the option is absent; explain replacement semantics in CLI help. Keep both local feeds and source values in their current order with XML escaping.
- `tests/Hexalith.Parties.Ci.Tests/CommonsHttpRestoreRoutingTests.cs`: reuse the bounded Python subprocess harness for executable main probes of absent/single/repeated source options and real generated XML. Add an offline restore regression with a package present only on an inherited parent feed: generated consumer config must prevent resolution, while explicitly selecting that feed must restore successfully. Run NuGet itself against the generated config and private cache, without changing user configuration or contacting real remote feeds.
- `docs/ci.md`: describe default and explicit feed selection for the consumer validator. NuGet's documented `clear` behavior supports discarding inherited package sources: https://learn.microsoft.com/en-us/nuget/consume-packages/configuring-nuget-behavior/ . No source mapping, dependency-version, public-contract, runtime, gitlink or release publication change.
- Validation: required existing-build Aspire start/wait/describe/stop; focused Debug CI build with source dependencies and warnings as errors; direct xUnit v3 focused class and full affected CI assembly; Python syntax, warning-override and whitespace guards. Preserve full baseline identifier, red/green restore evidence and sanitized results under `tests/dw-69-consumer-nuget-source-isolation-2026-10-20/`.

- Implementation uses the two existing local feed identities and ordered `additional-source-N` keys. Four CLI scenarios exercise default, single, repeated and explicitly retained public sources, including XML-sensitive values. Real NuGet restore checks both exclusion and explicit selection with separate caches and exact assets source sets.
- Baseline regressions fail twice: missing `clear` in generated XML, and successful restoration from an undeclared parent feed. Both pass after the fix. An unmodified CLI control passes; independent in-memory mutations removing `clear` or retaining the public default are detected.
- Review corrections: the CI documentation states generated credential names and the supported NuGet environment/provider authentication route; its guarantee is limited to packageSources. The new Python probes use `-E` so inherited PYTHONOPTIMIZE cannot disable assertions or skip the main call. A PYTHONOPTIMIZE=1 run passes both regressions.

## Review Triage Log

- Medium, patch: clearing inherited packageSources removes the original feed-name association for private-feed credentials, even when the same URL is explicitly selected. Documented `additional-source-N`, credential-provider support and `NuGetPackageSourceCredentials_additional-source-N` using official NuGet guidance. No implicit feed is restored to preserve credentials.
- Medium, defer: inherited fallbackPackageFolders can still supply packages absent from all declared sources. This predates DW-69 and uses a separate NuGet setting. Narrowed the documentation and recorded fallback-folder isolation as separate hardening; the fix targets inherited packageSources and implicit public-source selection.
- Medium, defer: inherited disabledPackageSources entries matching generated keys can disable explicitly selected sources. This predates DW-69. Documented inherited disabled settings and recorded resetting/isolation as separate hardening.
- Medium, patch: Python assertions can be disabled by PYTHONOPTIMIZE; the new `assert main() == 0` also skipped setup in that environment. Both new probes now run with `-E`, and both pass with PYTHONOPTIMIZE=1 set on the .NET test runner.

- Final verification: post-review Debug build has zero warnings/errors; focused tests pass 16/16, both optimized-environment probes pass 2/2, and the complete affected CI assembly passes 130/130 without failures/errors/skips/not-run. The independent reviewer verified both corrections. The parent reproduced both retained NuGet settings limits and preserved a runnable offline probe. Two pre-existing limitations are deferred; no unresolved introduced finding remains. Pinned commitlint 21.2.2 validates the exact commit message. Evidence and source hashes are in tests/dw-69-consumer-nuget-source-isolation-2026-10-20/validation.json. Full Release consumer-package and fresh full-topology qualification remain separate gates.
