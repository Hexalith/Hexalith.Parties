# Five deferred items — 2026-10-22

The local handoff is recorded in validation.json. Parent verification and final
review dispositions are in parent-verification.json and the batch spec. The final
AdminPortal suite passes 283 cases, bringing the unique .NET coverage across the
selected assemblies/classes to 1,205 passing cases without skips.

The source launcher remains blocked by HXP020; package-mode Debug checks passed.
No release, deployment, platform owner approval or integrated topology is claimed.
One pre-existing form-context authorization issue remains explicitly deferred.

qualify-node.py reproduces the official archive checksum comparison, locked
installs, wrong-engine rejection with before/after digests, npm signature checks,
browser configuration tests and type checking in owned temporary directories.
See node-reproducible-qualification.json for exact sanitized commands and inputs.
Published command logs redact local paths and normalize trailing whitespace;
local originals remain outside this repository.

The live scheduler acceptance is pending the reviewed main pushes. Its receipt
will record exact run/job identities, overlapping lifetimes and before-ranges;
the ledger must remain open for this item until that evidence passes.
