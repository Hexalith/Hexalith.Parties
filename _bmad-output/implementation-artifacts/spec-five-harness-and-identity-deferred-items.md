---
title: Close five harness and identity deferrals
type: bugfix
created: 2026-11-10
status: done
route: oneshot
review_loop_iteration: 1
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Resolve five existing deferred entries: propagate the lifecycle qualification
deadline through initial persistence and its recheck; clean private preparation
directories when ownership-marker initialization fails; exercise the actual
split-source catalog generator in ordinary CI; refuse a missing identity result
if UTC moves behind the authoritative source observation during re-admission;
and reconcile the already delivered public image measurement interval checks.

Preserve existing authorization, source/package selection, native reporter
identities, bounded operation lifetimes, concurrent directory ownership and
unreaped-process retention. Add focused failure and boundary controls. Mark
these five ledger entries resolved only after verification and independent
review, then commit and push the changes on main as requested.

</frozen-after-approval>

## Implementation Notes

- Investigation confirms three code gaps and a missing executed generator
  regression. The timestamp reader already checks the issued persistence
  interval, including nanosecond boundaries; all 24 public reader controls pass.
- Small reversible corrections to existing helpers and identity query logic;
  no intent gaps, dependency updates, deployments or persisted-state migration.
  This explicit five-item maintenance request uses the oneshot route.
- `PartyEnvironmentTests.ExerciseRealDependenciesAsync` must link the caller's
  token to its existing test/descriptor lifetime. `PartyPersistenceAssertions`
  supplies initial/recheck entry points and `LifecycleQualificationTests`
  passes its single 240-second token through both operations. Preserve the
  original fact identity and parameterless recheck compatibility.
- `PartiesDevelopmentCatalogTool.RunOwnedPreparationAsync` initializes its
  marker in a private sibling staging directory before atomic admission.
  Failure must remove only this invocation's staging directory; after admission
  retain existing marker checks and process-cleanup refusal. Deterministically
  exercise partial marker initialization, foreign destinations and competition.
- Extend `DevelopmentCatalogPreparationCleanupTests`, already selected by
  ordinary CI, to execute `GenerateAsync` with the real split CatalogBuilder
  sources and coherent prepared inputs. Assert generated receipt, staged
  companion source and input hashes. Keep package/source authority intact.
- `PartyIdentityQueryService.ResolveAsync` checks completion against the source
  observation on the empty-state branch, reusing one UTC sample for admission.
  Extend `PartyIdentityQueryHandlerTests` with rollback/equality/forward tick
  controls whose authority remains valid throughout.
- Verification: pinned module-tool source mapping, focused Debug builds and
  built xUnit assemblies; existing public Python controls; independent blind
  review; exact pinned commitlint validation before the authorized Git push.
- Touched repository: Parties only. Existing production, owner acceptance,
  schema-completeness and dependency-publication gates receive no new credit.

- Final verification passes 495 focused tests: 325 identity controls, 85 Python
  script controls, 70 CI workflow controls, 13 catalog cleanup/generation
  controls and two unchanged native reporter facts. No targeted failures/skips.
- Source Debug service, lifecycle and CI builds pass with zero warnings/errors.
  The source IntegrationTests build retains existing HXW006 from Admin.UI's
  UseHexalithProjectReferences conflict. The permitted serialized package
  Release fallback builds cleanly and executes all 13 catalog controls.
- Initial service HXW005 restore-state duplication was corrected by a forced
  restore through the same pinned source mapping. No gate was weakened.
- Executing the new generator exposed inherited source-mapping settings in its
  intentional package child. That child now excludes only the owning tool's
  seven mapped build environment variables, retains exact package checks and
  copies global.json. The fixture follows its executing assembly configuration.
- The unchanged historical hosted artifact passes with its original recorded
  GitHub context and all 28 cases; this supplies compatibility evidence only.
- Durable commands, final source/binary hashes, XML and compressed logs are in
  `tests/five-harness-and-identity-2026-11-10/verification.json`.

## Review Triage Log

- R1 — medium, patch: clean CI lacked Platform for the newly executed generator.
  Added its root checkout and the existing workflow contract guard.
- R2 — medium, patch: pre-canceled controls missed dropped in-flight linkage.
  Existing native facts now cancel an actual stalled HTTP body through the
  shared persistence lifetime; reporter names and counts remain unchanged.
- R3 — medium, patch: temporary builds could select another installed SDK.
  Copy the exact repository global.json into private reader and fixture paths.
- R4 — medium, patch: fixture always built Debug in Release CI. Derive the
  fixture configuration from the executing test assembly.
- R5 — low, patch: source hashes alone missed owner/adapter receipt omissions.
  Assert their actual staged bytes and corresponding inputHashes entries.
- R6 — low, patch: late foreign-directory admission lacked a direct control.
  Verify its marker/content survive failed atomic admission with no staging leak.
- R7 — low, patch: cleanup-failure diagnostics lacked a direct control. Assert
  original exception identity, attached cleanup failure and foreign content.

All seven findings are addressed. Independent follow-up inspection found no
confirmed new defects. No review findings were deferred. The five selected
ledger entries are resolved; the image-timestamp entry was reconciled against
existing delivery. Existing source-build and production qualification limits
remain explicit.
