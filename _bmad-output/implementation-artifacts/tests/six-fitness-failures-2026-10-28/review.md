# Independent review

A context-free reviewer inspected the root documentation/current-expectation diff
and the owning Tenants build/test diff. Its five findings were checked and patched:

1. Global MSBuild versions could bypass an assignment-only package floor.
2. A hyphen in stable SemVer metadata was mistaken for a prerelease marker.
3. The new evaluation tests lacked a subprocess deadline/concurrent stream drains.
4. Additional current table rows lagged the selected package catalog.
5. Dapr SDK and runtime versions needed an explicit distinction.

Follow-up independently exercised the real CollectPackageReferences and
PrepareForBuild entry points and reran the initial 23 owner controls, all passing.
Publication-time fetch then found incoming Tenants main work at
`d490461f4d6baeff4b2373ef9c54edfcd5e363a2`, including its stronger 3.128.4 floor.
The adapted three-file diff received a further independent review. Both entry
points reject 3.128.3; stable 3.128.4 metadata and newer 3.128.5/3.128.10 succeed;
source identity remains authoritative. No unresolved finding remains.

Owner final verification runs 25 focused controls and both complete affected
projects. The independently reproduced 193 missing-nested-Builds failures remain
an explicit broad-gate blocker. Review does not grant owner API acceptance,
installed custody qualification, migration, deployment or release approval.
