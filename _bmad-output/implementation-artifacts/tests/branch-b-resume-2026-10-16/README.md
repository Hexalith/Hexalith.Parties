# EXT-PARTIES-1 current-query actor-target verification — 2026-10-16

Current identity queries now reject missing or mismatched actor-target admission before reading protected source when an expected actor is supplied. The four new cases cover Person and Organization sources. Human/organization classification, binding mismatch outcomes, actor-free Organization reads and Consumer behavior retain passing coverage.

The complete normal Local command exited **0**, with **1,693 passing tests**, twelve owner lanes and all **57 required class filters**. Every build has zero warnings/errors; execution has zero failures/skips/not-run. [verification-manifest.json](verification-manifest.json) records exact commands, retained receipt digests, per-class counts and actual passing identities for each frozen matrix row. All twelve xUnit XMLs and build/test logs are retained here. The [independent parent audit](root-independent-audit.json) rechecked every receipt digest, all 57 required class filters, all four matrix rows, the four behavioral failures and 176 focused passes, selected identity source hashes, frozen intent and whitespace.

| Owner project | Passing cases |
| --- | ---: |
| Hexalith.EventStore.Contracts.Tests | 37 |
| Hexalith.EventStore.Client.Tests | 343 |
| Hexalith.EventStore.Server.Tests | 406 |
| Hexalith.EventStore.PayloadProtection.Tests | 33 |
| Hexalith.Platform.Identity.Tests | 2 |
| Hexalith.Platform.Custody.Tests | 171 |
| Hexalith.Parties.Contracts.Tests | 29 |
| Hexalith.Parties.Server.Tests | 94 |
| Hexalith.Parties.Tests | 378 |
| Hexalith.Parties.Security.Tests | 30 |
| Hexalith.Parties.Client.Tests | 98 |
| Hexalith.Parties.UI.Tests | 72 |

## Regression evidence

The aligned pre-fix run executed four actual assertion failures: human sources returned Ineligible and organization sources returned Resolved despite missing or different admitted actors. After the check moved before the source read, all **176** identity-query cases passed. The complete Local domain lane includes those cases; the focused run is not added to the Local total. Regression tests assert Unavailable, absent evidence and no ordinary-source, retained-source or custody lookup. Historical mismatch coverage also checks the actual retained reader.

The first pre-fix build passed, but its test invocation failed assembly loading because a Tenants project reference forwarded EventStore 3.120.0 over a 3.122.0 output. That failed invocation is retained as `ext-parties-1-20261016-domain-red-tests.log`; it is not behavioral red evidence. Rebuilding with `HexalithEventStoreVersion=3.122.0` produced the four actual red failures in `ext-parties-1-20261016-domain-red-aligned-tests.xml`. The normal verifier independently resolved and applied that same version.

## Scope and remaining work

The isolated Parties AppHost started, `aspire wait parties --status healthy --timeout 30` succeeded, resource states were inspected and that explicit AppHost stopped. `selected-source-and-baseline.json` contains only resource names/states and selected source hashes; dashboard credentials and endpoint data are excluded.

Before the user requested local-only continuation, Live and LiveReadiness stopped with exit 1 on absent inputs before any endpoint access. No further Live/LiveReadiness checks or installed operations ran. The approved policy remains `party-actor-retention-v1`, 365 fixed days from `binding-effective-at`. Platform's `PlatformCustodyServiceCollectionExtensions` still registers cleanup without a production `IIdentityHistoryCustody` backend. Qualified independent custody, all-copy destruction, nonrollback restore, approved continuation and complete installed P-01–P-10 persisted/restart/restore/failure-injection probes and targets remain unavailable. Both original open execution tasks and the spec's `in-progress` status are preserved.

These receipts establish local behavior only. Sibling EventStore and Platform already contained unrelated user work, which was preserved. Concurrent root edits appeared in `Directory.Packages.props`, `.gitlink-signoff.tsv`, release/scope documents and `PlatformApiPrerequisitesTests.cs`; these were also preserved. The two selected identity source/test digests match final bytes. No whole-worktree, final-build-graph, Portable-PDB, installed identity availability or production qualification assertion is made. This continuation performed no dependency update, staging, commit, push, deployment or production identity mutation. Full original artifacts remain at `/tmp/ext-parties-1-20261016`; the manifest maps retained files to their executed commands.
