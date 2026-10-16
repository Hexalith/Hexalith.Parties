---
title: 'DW-195: align command enum binding'
type: 'bugfix'
created: '2026-10-16'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Parties clients serialize enum names, but EventStore's aggregate command reader has no string-enum converter. Production consent validation accepts `lawfulBasis: "Consent"`, then aggregate command binding throws. The same mismatch affects party, contact-channel, and identifier command types.

**Approach:** Declare the existing named-enum command policy on the six Parties-owned command properties using the built-in JSON converter. Accept both existing enum names and legacy numeric values, including nested composite commands and nullable contact-channel updates. Preserve client wire shape, domain validation and rejection behavior, enum definitions, persisted event/state serialization, shared serializer options, and dependencies. Verify the actual GDPR client payload against the EventStore reader and valid/invalid consent through production registration; close only DW-195 after independent review and passing checks.

</frozen-after-approval>

## Implementation Notes

- Planning facts: no unresolved intent gaps or irreversible actions. Small contract-annotation footprint: `CreateParty.Type`, `CreatePartyComposite.Type`, `AddContactChannel.Type`, `UpdateContactChannel.Type`, `AddIdentifier.Type`, `RecordConsent.LawfulBasis`; focused contract/client/processor tests, architecture documentation, this spec, ledger, and portable verification evidence. No public signatures, platform implementation, submodule pointers, dependencies, migrations, or remote publication change.
- Reuse `JsonConverter(typeof(JsonStringEnumConverter<TEnum>))` on command properties; for nullable `UpdateContactChannel.Type`, the converter uses the underlying `ContactChannelType`. Nested composites reuse the same child contracts. These annotations express domain wire policy without implementing a local platform serializer. Do not annotate enum definitions: the default EventStore event writer must continue producing numeric enums. Do not widen `EventStorePayloadSerialization.Options`: newer snapshot writers also use it.
- Relevant boundaries: `PartiesJsonOptions.Default` writes names; `HttpAdminPortalGdprClient.PostCommandAsync` serializes with it. `PartyDomainProcessor.TryRejectInvalidPayloadAsync` uses the same options, then `EventStoreAggregate.DispatchCommandAsync` binds with immutable `EventStorePayloadSerialization.Options`. Neither options instance should change. Existing DW-122 tests use a numeric-only serializer helper; add explicit numeric fixtures rather than mistaking the annotated default writer for historical input.
- Verification: Debug source-reference builds with `-m:1 -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false`; invoke each built test assembly directly with xUnit single-dash filters. Focus first on command enum contracts, GDPR transport, production validator registration, and the authenticated production process endpoint, then run the full affected Contracts.Tests, Client.Tests and Parties.Tests assemblies. Keep warning gates intact and record source identities and actual command results.
- Pre-edit runtime: explicitly targeted `aspire start --apphost src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj --no-build --non-interactive --format Json` succeeded; `aspire wait parties --apphost src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj --timeout 30 --non-interactive` reported healthy. Existing binaries are not source provenance or a passing full-topology qualification. Stop only this AppHost before compilation; leave the separately running FrontComposer AppHost alone.

- Implementation: added BCL property converters to all six enum-valued command properties; no serializer options, enum definitions, event types, state types, runtime processor code, or dependencies changed. Nested create/update composites reuse the annotated child contracts.
- Added SDK-reader contract coverage for canonical names, lowercase names, PascalCase/camelCase properties, numeric inputs, nested composites, unknown names, nullable contact-channel updates, and unchanged numeric event/snapshot writes. Real command/GDPR client tests bind captured outgoing payloads using the SDK reader. Production registration tests now use canonical client options and explicitly construct the retained numeric consent case, with unknown-name rejection before state access.
- Red evidence: after correcting test-only CA1062 parameter guards, Debug source build passed with zero warnings/errors. Before production annotation changes, the new contract class ran 17 cases with 7 failures (six command properties plus named nested composites), 10 passing, no skips/errors/not-run. After annotations, the same class passes 17/17. Real-client focused classes pass 87/87. Full affected verification is in progress.
- The runtime observation showed Parties and sidecars healthy; gateway/admin/Tenants exited 134 and UI exited 150. This pre-existing integrated-topology blocker is separate from serializer verification. The targeted AppHost stopped successfully before compilation; the separately running FrontComposer AppHost was preserved.

- Full affected assemblies passed with zero failures, errors, skips or not-run cases: Contracts.Tests 198/198, Client.Tests 174/174, Parties.Tests 1000/1000 (1372 total). Production registration/processor/authenticated-route focused tests passed 83/83. All successful Debug source-reference builds had zero warnings/errors. The final Contracts rebuild first overlapped the client's package fixture builds and failed with MSB3243/CS1704 duplicate UniqueIds references; the identical serialized retry after those tests completed passed without configuration/source changes. The warning-policy and CRLF-aware whitespace checks pass. Portable command summaries, source hashes, exact root dependency observations and the separate runtime limitation are recorded in `tests/dw-195-command-enum-compatibility-2026-10-16/validation.json`.


## Review Triage Log

Independent blind review examined 21.185 kB of changed source/spec content; finding floor `min(floor(sqrt(21.185) + 1), 10) = 5`. All five findings were verified and accepted as low-severity test/documentation improvements; no configured review layer was skipped.

- [low] [patch] Default serialization now writes command enum names, so historical numeric creation/composite processor fixtures were no longer explicit. Added raw numeric top-level and nested creation payloads through production registration, asserting the emitted enum values.
- [low] [patch] Defined numeric inputs alone did not characterize validation compatibility. Added int.MaxValue reader coverage for all six properties and production-processing cases that retain membership rejection for five commands and the existing absence of an enum-membership rule on contact-channel updates. Validation rules remain unchanged.
- [low] [patch] Persistence guards omitted nullable ContactChannelUpdated and populated contact/identifier snapshot collections. Added nonnull/null nullable-event assertions and numeric snapshot child assertions.
- [low] [patch] Unknown nested enum names lacked bounded processor rejection coverage. Added malformed contact-channel/identifier child payloads requiring InvalidJson, no payload echo, and no protected-state access.
- [low] [patch] Architecture guidance omitted write-side behavior. Documented default command writes as names and property-converter precedence over option-registered converters; historical numeric fixtures must be explicit. Current canonical client output remains unchanged.

Post-review corrections alter only tests and documentation. Preserved the original red source using a final-to-red reconstruction patch; git apply --check and actual isolated application reproduce the recorded red SHA-256. Pre-review tested-source hashes and results remain separate from final source hashes.

- [low] [patch; targeted follow-up] After the review additions, the evidence README still described LF normalization alone as sufficient to reconstruct the red source. Corrected it to require the verified final-to-red patch on an isolated LF-normalized copy, matching validation.json. The reviewer confirmed all five original corrections and found no defect in the added tests or compatibility documentation.


### Completion — 2026-10-16

Post-review Debug source builds passed with zero warnings/errors. The expanded production registration/processor/authenticated-route focus passed 93/93; final full Contracts.Tests passed 198/198 and Parties.Tests 1010/1010. The unchanged real-client source had already passed full Client.Tests 174/174: 1382 total full-project tests with zero failures, errors, skips or not-run cases. Independent review and targeted follow-up are complete; all five original coverage/documentation findings and the follow-up evidence-note correction are patched, with no deferred review finding. All final source hashes, exact root dependency identities and isolated red-source reconstruction checks pass. The candidate passed pinned commitlint 21.2.2 with zero problems/warnings. DW-195 is complete; the one-shot workflow requires a local commit. Integrated-topology startup failures remain a separately recorded pre-existing limitation; no remote push is performed.
