---
title: 'DW-129 identity-history snapshot protection evidence'
type: 'bugfix'
created: '2026-10-17'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** DW-129 and its duplicate DW-148 track snapshot custody returning unchanged state while the adapter labels the wrapper Protected. Profile encryption leaves actor history readable inside its base64 snapshot payload. The two history inventory classifications also remain deferred.

**Approach:** Require custody snapshot writes to return the existing SDK SnapshotProtectionResult carrying current Protected metadata for party-actor-history-v1 and nonempty, transformed opaque ciphertext bytes. Fail closed for absent, untyped, unchanged, empty, or mismatched results before creating IdentityHistorySnapshot or reporting protected metadata. Preserve its marker/payload wire shape; normalize persisted base64 ciphertext on restore and retain independent custody authorization before profile decryption/redaction. Record HumanActorBindings and HumanActorTransitions as personal data protected by independent identity-history custody, without profile-key annotations. Cover rejection through the service and adapter, serialized persistence and restoration after profile-key destruction, missing custody, cancellation, and profile-only compatibility. Close only DW-129/DW-148 after verification; production custody lifecycle qualification remains separately outstanding.

</frozen-after-approval>

## Implementation Notes

Planning classification: no unresolved intent gaps or irreversible operations. Small local validation change, no public API signature, dependency, gitlink, retention-policy, deployment, or data migration changes. Use the oneshot route. Leave changes uncommitted under repository safeguards.

Code map: PartyPayloadProtectionService.ProtectSnapshotStateAsync currently wraps arbitrary provider output; UnprotectSnapshotStateAsync reads the wrapper. EventStorePartyPayloadProtectionAdapter.ProtectSnapshotAsync labels successful wrappers Protected. Reuse EventStore.Contracts SnapshotProtectionResult and metadata instead of creating a protocol or production encryption implementation. Existing IIdentityHistoryCustody returns object and has no production implementation in the inspected owner sources; snapshot implementations must now supply the typed ciphertext result. Tests use synthetic custody only, never claim live provider qualification. PersonalDataInventoryTests needs a purpose-specific personal-data classification for the two existing histories, preserving their separation from profile erasure.

Validation plan: Debug source-reference Security.Tests build; focused IdentityHistoryProtectionTests before/after the fix, then full Security.Tests and Contracts.Tests. Check the Security library against selected packaged SDK separately if necessary. Preserve warnings-as-errors and CRLF. Independent blind review, then relevant checks after any correction. Record exact commands, counts, and runtime baseline limitations in compact evidence.

Runtime baseline: exact Parties AppHost starts, but the external security resource is Unhealthy because its TLS health request to localhost:8543 terminates with unexpected EOF. Dependent resources wait; this is not a healthy full-topology result. Stop the owned AppHost before compiling. No production identity mutation or custody qualification is performed.

Baseline follow-up: `aspire wait parties --status healthy --timeout 10 --non-interactive --apphost src/Hexalith.Parties.AppHost/Hexalith.Parties.AppHost.csproj` succeeded. The final sanitized describe shows Parties Running and security Healthy; EventStore, Tenants, UI and their Dapr executables Finished. The initial TLS failure was transient, but full topology is still not qualified. Exact-target ordinary stop succeeded before production edits.

The initial test build caught a nonexistent legacy enum member in the new test; corrected it to the existing Legacy metadata factory. Focused pre-fix reproduction: 27 tests, 21 failed, 6 passed. These include 18 ineffective provider outcomes and missing/serialized custody cases, not a broad-gate failure.

Implementation: require current SDK snapshot protection metadata and detached nonempty byte[] ciphertext; reject JSON plaintext, untyped results and preexisting wrappers before reporting protected metadata. Restore accepts typed bytes or persisted base64 bytes and rechecks cancellation after custody. Existing profile-only and event behavior passes the full security suite. The purpose-specific inventory category preserves profile erasure separation. Architecture documentation explains the tightened provider contract and old unproven snapshot rejection.

The initial full Contracts run was 198/199: its unchanged public API snapshot test omitted two already-committed records from d26b0cea57df19d18281a337e66bc8ca4712fb37, both in ResolveChatBotPartyAuthority.cs. Reflection generated the current surface without modifying production source; the exact diff is 23 additions and zero deletions. Refreshed that expected snapshot for the existing additive API decision, then full Contracts passed 199/199. No public contract source or signature changed.

Initial full Security passes 206/206; Debug package-reference Security library compatibility build passes with zero warnings/errors. Additional focused coverage now checks buffer ownership and canceled custody reads, and uses the SDK snapshot writer's JsonSerializerOptions.Web for persistence rendering.

## Review Triage Log

- Medium, defer: SnapshotManager passes serialized JsonElement, which the unchanged typed-history guard rejects. This is a pre-existing SDK integration limitation, not a newly usable capture path. Preserve fail-closed behavior, document it, and append an owner integration/persisted-capture entry to the ledger. The existing adapter serialized-input rejection test covers the local denial; full SDK availability is not claimed.
- Medium, patch: provider KeyAlias and nonempty compatibility flags could carry information that the byte-only restore seam cannot receive. Require self-contained ciphertext and reject those metadata fields; document the contract and add both rejection cases.
- Medium, patch: JSON parsing alone misses literal-prefixed, base64 and deeply nested copies of the original snapshot. Add exact input/base64-copy detection and tests, zero transient snapshot/encoded/rejected buffers, and explicitly describe structural validation under a trusted provider, without claiming proof against false protection assertions.
- Medium, patch: validation previously examined provider-owned bytes before copying. Copy first, then validate and persist that same detached array; preserve the provider-buffer mutation regression test.
- Low, patch: success tests used the throwing adapter read entry point. Route both direct and serialized cases through TryUnprotectSnapshotAsync and assert readable state.
- Medium, patch: provider authentication failure, access refusal and null-history outcomes lacked direct coverage. Add all three and assert unreadable/null state with no profile-key decryption.
- Low, patch: success coverage always destroyed the profile key. Add live-key cases for direct and persisted wrappers and assert profile fields, history and exact binding versions together.

Independent follow-up confirmed all six patches resolved, the SDK capture limitation correctly deferred, and no additional concrete issue. Final Debug/source Security.Tests build has zero warnings/errors; the full assembly passes 218/218, including 47 snapshot/history cases. Full Contracts.Tests passes 199/199 without skips, errors or failures. Package-reference Debug compatibility, whitespace and build-policy results are recorded in the validation packet. DW-129/DW-148 are closed together; the new SDK capture integration entry stays open, and production custody remains unqualified. No commit or push.
