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

The live scheduler acceptance passes: runs 39820004780 and 39820014459 overlap
twelve seconds, and their successful gate jobs overlap four seconds. Each log
binds the preceding main tip to its exact source. See scheduler-proof.json and
main-pushes.json. All five selected ledger entries are closed. The unrelated
remote CI assertion change was preserved by rebase; its 37 affected cases pass.
