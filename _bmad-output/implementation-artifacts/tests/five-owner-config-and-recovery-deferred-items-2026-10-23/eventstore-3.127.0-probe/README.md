# Archived EventStore 3.127.0 recovery probe

This is historical negative evidence, not current production source. The original
probe still belongs to exact Parties revision
`1ef274ea0471643d83f924147e85ac8326cf6a47`. Its observed superseding-state overwrite
and the publication/adoption boundaries in [the parent record](../README.md)
remain unchanged.

The three executable probe files were removed from the current tree because its
private `Directory.Packages.props` overrides the shared catalog. No production
package declaration or catalog-policy exemption replaces that override. The
original `Program.cs`, `Probe.csproj`, `Directory.Packages.props` and README remain
retrievable from that immutable revision.

To inspect or reproduce the historical probe, extract it outside the checkout:

```bash
probe_archive="$(mktemp -d)"
git archive 1ef274ea0471643d83f924147e85ac8326cf6a47 -- \
  _bmad-output/implementation-artifacts/tests/five-owner-config-and-recovery-deferred-items-2026-10-23/eventstore-3.127.0-probe \
  | tar -x -C "$probe_archive"
dotnet run --project "$probe_archive/_bmad-output/implementation-artifacts/tests/five-owner-config-and-recovery-deferred-items-2026-10-23/eventstore-3.127.0-probe/Probe.csproj" \
  --configuration Debug -p:NuGetAudit=false
```

The original command uses actual 3.127.0 packages and reports the observed recovery
result. It does not run production migration or cleanup. A new execution is a new
observation and does not rewrite the historical negative outcome.
