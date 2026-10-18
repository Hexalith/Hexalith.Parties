---
title: 'DW-46: generate projection-fold diagnostics'
type: 'refactor'
created: '2026-10-18'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '4cd914030bb595f8149bbdf50721cec845f1f3c5'
context:
  - AGENTS.md
  - .editorconfig
  - references/Hexalith.AI.Tools/hexalith-llm-instructions.md
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** DW-46 records that PartySdkProjectionFold uses ordinary logging calls and incorrectly claims the project cannot generate LoggerMessage methods. The project now directly references Logging.Abstractions and enables its generator.

**Approach:** Move the seven diagnostics into one dedicated source-generated logging type. Preserve message templates, structured fields, severity, checkpoint behavior, caller logger categories, and null logged exceptions. Convert exceptions to type-name strings before invoking generated methods so payload-bearing exceptions cannot reach logging sinks. User decision (2026-10-18): use stable distinct numeric event IDs 4802–4808, in the existing helper declaration order, and generated method names as event names. This intentionally replaces the previous generic event ID zero. Reuse the existing project configuration; dependency changes are unnecessary. Verify existing diagnostic and privacy coverage plus focused structured-field compatibility checks, then close only DW-46 in the ledger. DW-47 category redesign remains separate.

</frozen-after-approval>

## Boundaries & Constraints

Preserve templates, placeholder spelling, severity, checkpoint advancement, optional logging, and payload privacy. Reuse the existing Logging.Abstractions reference and in-box generator. Keep caller categories; category redesign belongs to DW-47. Do not modify dependency versions, gitlinks, shared build configuration, or persisted state.

## Code Map

- `src/Hexalith.Parties.Projections/Handlers/PartySdkProjectionFold.cs`: seven nested Log helpers; two accept exceptions but pass only type names to ILogger. Preserve DeserializeNew and checkpoint behavior.
- `src/Hexalith.Parties.Projections/Hexalith.Parties.Projections.csproj`: already references Logging.Abstractions and sets DisableMicrosoftExtensionsLoggingSourceGenerator=false. No changes required.
- `Directory.Build.props`: removes the competing telemetry generator. Preserve that configuration.
- `src/Hexalith.Parties.Projections/Handlers/PartyIndexSdkProjectionHandler.cs`: generated rebuild diagnostic uses ID 4801; 4802–4808 are unused in the current source.
- `tests/Hexalith.Parties.Projections.Tests/Handlers/PartySdkProjectionHandlerTests.cs`: all seven existing diagnostic scenarios; its CreateLogger/Summary methods demonstrate NSubstitute structured-state inspection.
- `tests/Hexalith.Parties.Projections.Tests/Handlers/PartySdkProjectionConverterFailureTests.cs`: verifies type-only diagnostics and privacy for converter failures.
- `src/Hexalith.Parties.Testing/RecordingLogger.cs`: captures rendered messages, levels, and exceptions but discards structured fields and EventId. Preserve the shared helper; use NSubstitute for focused metadata assertions.

## Tasks & Acceptance

- [x] `src/Hexalith.Parties.Projections/Handlers/PartySdkProjectionFoldLog.cs`: create one documented internal static partial logging type with seven LoggerMessage methods. Assign the approved stable IDs; each generated method accepts safe scalar values only.
- [x] `src/Hexalith.Parties.Projections/Handlers/PartySdkProjectionFold.cs`: call the generated type, converting exceptions to type-name strings at the catch site. Remove the nested Log type and stale generator comment while retaining the privacy rationale with the new type.
- [x] `tests/Hexalith.Parties.Projections.Tests/Handlers/PartySdkProjectionLoggingTests.cs`: assert structured fields, original templates, event metadata, enabled filtering, and null logged exceptions through DeserializeNew. Reuse existing coverage for all checkpoint and converter branches.
- [x] `_bmad-output/implementation-artifacts/deferred-work.md`: close only DW-46 with verified evidence after checks and independent review pass.

Given a dropped event with logging enabled, when DeserializeNew enumerates it, then its established severity, template, safe fields, and checkpoint result are preserved with the approved event metadata.

Given a corrupt or redacted payload containing private sentinel data, when deserialization fails, then the log contains only the exception type and allowed event metadata, with no raw exception or private bytes.

Given logging is disabled or no logger is supplied, when the fold enumerates events, then fold results remain identical and no generated diagnostic reaches the sink.

Given the unchanged dependency graph, when the target project builds in Debug source mode, then source generation succeeds with zero warnings or errors.

## Implementation Notes

- Implemented the dedicated generated logger and all seven call-site routes. Existing generator configuration and dependencies are unchanged. Source files use required CRLF line endings.
- Added 11 metadata/filter/privacy cases. Debug/source build passes with zero warnings/errors; focused tests pass 11/11 and full projection tests pass 346/346 without failures, errors, skips, or not-run tests. Warning-override and CRLF-aware whitespace gates pass. Evidence: `tests/dw-46-generated-projection-fold-logging-2026-10-18/README.md`.
- The ledger closure task is finalized after independent review by design; implementation and test tasks are complete.

- User approved the recommended distinct event IDs on 2026-10-18 and directed implementation.

- Investigation found the existing direct package reference and working generator, so configuration changes are unnecessary.
- Replanned before code changes because preserving ID zero across seven generated methods conflicts with the generator's duplicate-ID diagnostic. No warning suppression will be added. Microsoft diagnostic reference: https://learn.microsoft.com/en-us/dotnet/fundamentals/syslib-diagnostics/syslib1006.
- Baseline revision: 4cd914030bb595f8149bbdf50721cec845f1f3c5.
- Aspire baseline command: `aspire run --detach --apphost src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj --non-interactive --nologo` succeeded. `aspire describe --apphost src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj --non-interactive --format Json` showed security readiness unhealthy with an SSL unexpected-EOF error, leaving EventStore/Parties/Tenants waiting. This is a baseline observation, not a validated healthy topology. Stopped the exact AppHost successfully before building.

- Final parent verification after review fixes passes: Debug/source build has zero warnings/errors; focused tests 13/13 and full projection assembly 348/348 pass with no failures, errors, skips, or not-run tests. Warning and whitespace gates pass. Exact commands, results, source hashes, and repository-relative evidence are in `tests/dw-46-generated-projection-fold-logging-2026-10-18/validation.json`.
- Review patches 1–5 and 7–9 are complete. Findings 6 and 10 remain rejected with the evidence above; no new deferred work. DW-46 is closed; DW-47 remains unchanged. The operator event-ID migration table is in `docs/development-guide.md` relative to the repository root.

## Spec Change Log

## Review Triage Log

All three review layers completed. Edge Case Hunter returned no findings; Verification Gap Reviewer found no verification gaps. Blind Hunter findings are classified individually below.

1. **low / patch — evidence persistence:** README cites ignored `.log` files that are absent from the staged diff. Preserve the cited logs as tracked text evidence, with repository-relative paths.
2. **low / patch — XML documentation:** the new public test class and two public theory methods lack required XML docs. Add summaries and parameter documentation.
3. **low / patch — privacy wording:** the new logging remarks describe scalar metadata as intrinsically safe, although event names/formats retain producer-controlled strings. Qualify that documentation without changing the preserved fields.
4. **low / patch — operator migration:** only implementation artifacts explain the ID-zero replacement. Add the seven-ID/name mapping to the existing development documentation so operators can update alert filters.
5. **low / patch — private JsonException coverage:** the invalid-JSON case's private value is not itself echoed in the parser's error. The existing synthetic converter's fallback already throws JsonException with the full private message; add live/redacted rows using that fixture.
6. **low / rejected — converter-returned null branch:** focused 4807 currently exercises the early redaction path. Production event records have no root converters returning null, and both branches call the same generated method already pinned by metadata tests. Demonstrating the alternate route requires additional synthetic converter behavior for a future scenario; complexity outweighs negligible current benefit. No runtime change or deferred item.
7. **low / patch — positive rendering assertions:** new tests pin templates/fields but only exclude private content from rendered messages. Add positive expected text assertions to strengthen the compatibility claim.
8. **low / patch — structured-state interface:** requiring IReadOnlyList unnecessarily binds tests to the current generator representation. Accept enumerable key/value state instead.
9. **low / patch — IsEnabled call count:** exactly-one assertions bind tests to generator internals; disabled-sink and identical-result assertions already cover filtering. Remove the call-count assertions.
10. **low / rejected — fixture extraction:** the new theory intentionally constructs the existing test fixture to reuse its internal FailingEvent helper and scoped IDisposable cleanup. Each instance owns unique cache keys and cleanup; no shared lifecycle defect occurs. Extracting a new fixture would modify otherwise unchanged tests for an infrequent maintenance concern, so no additional abstraction or deferred item is added.

## Verification

- Build `tests/Hexalith.Parties.Projections.Tests/Hexalith.Parties.Projections.Tests.csproj` with `--configuration Debug -m:1 -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0`; expect zero warnings/errors.
- Invoke the built xUnit v3 DLL directly with `-class Hexalith.Parties.Projections.Tests.Handlers.PartySdkProjectionLoggingTests`, then run the full projection assembly; expect all tests passing without skips.
- Run `bash scripts/check-no-warning-override.sh` and a CRLF-aware whitespace check. Preserve full commands/results in the evidence directory.
- Independently review the actual diff and correct current-scope findings before marking done. Validate the exact Conventional Commit message with the pinned local commitlint CLI before any commit; do not push.

- Baseline focused Debug/source build passed with zero warnings/errors. The unchanged full projection assembly passed 335/335 tests with zero failures/errors/skips/not-run; raw baseline logs/XML are in `/tmp/dw-46-baseline-*`.
