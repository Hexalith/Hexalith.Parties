# DW-74 validation

Baseline commit: `b776849c1f19b50e940b8fd10d70d4611fecbcc6`.

The final Python suite passes 17/17 methods. The Debug CI test project builds
with zero warnings/errors, and its 48 publication workflow contract tests pass
without errors, failures, skips or not-run cases. actionlint and the warning /
submodule guard pass. Real central configuration and all 49 installed skill
customizations match the baseline exactly.

`validation.json` records commands, results, implementation hashes and limits.
The initial unmodified helper accepted duplicate inputs (23 failing subcases
across 12 test methods). Independent review findings and corrections are
recorded in the linked spec; the final reviewer reports no remaining findings.

The default whitespace checker treats CRLF additions as trailing whitespace;
the recorded CRLF-aware check preserves the repository's required line endings
while checking actual trailing spaces. Paths in command evidence use
`{project-root}` for the working directory.

The dedicated BMAD CI job checks configuration tooling independently from the
existing shared .NET tiers. No dependency version, gitlink or runtime setting
was changed.
