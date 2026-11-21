# Epic 8 reality check · November 21, 2026

The release selection, catalog behavior and closure status in the amended spine agree with the repository. Two clarifications affect how the team uses that evidence.

| Finding | Evidence | Correction |
| --- | --- | --- |
| The team overview says erasure stays pending until completion. | The Consumer page shows pending and cancellable for `ErasurePending`. It shows deletion started for `KeyDestroyed`, `VerificationInProgress` and `Verified`, and permanent for `Erased`. | Use the three user-visible stages in §8. Describe committed-event and verification evidence as the deployment qualification requirement. |
| I4 tells a source consumer to record the committed gitlink. A checkout used for validation can differ from that pin. | The parent records Builds `dc225a69`, CiCd `399b2f2d` and Platform `a74abe17`. Their checked-out revisions after the pull were `e1380b81`, `62dc50b1` and `abb62da8`. | Verify the source actually consumed matches the committed pin before assigning evidence to the release selection. |

## Evidence checked

- Parties `42b27f2e` contains eleven root gitlinks. Platform `a74abe17` also selects Builds `dc225a69`. The release signoffs identify Platform v2.2.4 and Builds v6.4.3. The Parties EventStore source gitlink is `7435254c`.
- MSBuild evaluation against that Builds catalog returned EventStore 4.2.0 for Parties and its gateway test host, and 5.4.0 for the attached qualification suite. Each accepted the opposite version as an explicit property. The private reader passes the selection to its package build and checks the restored EventStore libraries.
- Commit `c7dd607b` records six passing Round 10 lanes. The tracker marks Epic 8 and Story 8.10 done and Stories 8.7 through 8.9 blocked. Commit `0f261ccd` removed the two historical closure test classes named in the amendment.
- The November 20 sprint decision and PRD assign production key custody, first-request erasure execution and replica revocation evidence. The new links in both documents resolve, including local heading fragments.
