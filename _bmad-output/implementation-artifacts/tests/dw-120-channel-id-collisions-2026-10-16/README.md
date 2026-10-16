# DW-120 verification — 2026-10-16

The required Debug source-reference build passes with zero warnings/errors.
Focused channel/consent tests pass 138/138, including 32 new collision regression
cases. The full server suite passes 291/291, DW-119 focused tests pass 86/86,
and the complete Hexalith.Parties.Tests assembly passes 978/978. No tests were
skipped. Independent review and targeted follow-up completed; DW-120 is closed.

See [validation.json](validation.json) for exact commands, UTC execution times,
exit codes, runner counts, source hashes, dependency observations, and hashes of
the retained command summaries. Commands invoke built xUnit v3 assemblies
directly, following tests/README.md. Builds use Debug with
UseHexalithProjectReferences=true and UseNuGetDeps=false. Existing command/event
contracts, dependency versions/gitlinks, DW-119 validators, tests, gateway
fixture, source-receipt constants, and historical evidence remain unchanged.

The new cases cover standalone and composite creation/update rejection,
all-or-nothing composite behavior, exact retries, ordinal lookups, replay of
historical ambiguous channels, exact removal, unaffected channels, unchanged
consent IDs, active/revoked historical ownership after removal, ambiguous
revocation, and safe single-owner revocation during restriction. Cross-owner
consent history rejects rather than silently selecting or rewriting an owner;
channel removal cannot repair retained consent ownership.

The first server build caught two new assertions using PersonDetails instead of
the actual PartyState.Person property; the assertions were corrected. The first
focused run passed 130/131: an unrelated-channel test requested IsPreferred=true
but expected one event. Production correctly emitted ContactChannelUpdated and
PreferredContactChannelChanged. The corrected test asserts both exact events
and IDs. [initial-focused-summary.txt](initial-focused-summary.txt) retains the
failure; validation.json records both diagnoses. No existing assertion was
weakened and no production behavior was changed to satisfy either test mistake.

An added review regression initially confused an all-skipped composite's NoOp
classification with Success. The corrected test asserts IsNoOp=true and
IsRejection=false while retaining exact event/applied/rejected/skip assertions;
[review-retry-assertion-summary.txt](review-retry-assertion-summary.txt) preserves
that failure and validation.json records its diagnosis.

Review found two behavior gaps: update ambiguity checking before exact lookup,
and rejecting exact addition retries in historical ambiguous states. Both are
corrected and covered. Additional tests establish erasure/restriction precedence,
later historical owner detection, and exact-ID readdition. All seven findings
are triaged in the spec; the independent follow-up found no remaining concrete
issues. The old same-owner grant/revoke/regrant lifecycle bug is documented and
deferred separately. The optional family-index refactor was rejected without a
demonstrated normal-use performance regression. Commitlint 21.2.2 validated the
exact local commit message with zero problems/warnings; see
[commitlint.txt](commitlint.txt).

The pre-change Aspire --no-build observation found parties and eventstore
healthy, while eventstore-admin, parties-ui, and tenants were Finished and two
other projects were NotStarted. It is operational context using unverified
existing binaries, not full-topology qualification. The owned AppHost was stopped
before edits/builds. No dependency, release, publication, or deployment change
is included. No push was made.
