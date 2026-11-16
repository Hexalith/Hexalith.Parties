# Release and adoption checklist

Parties preparation is local. App adoption and central nightly acceptance are
pending. Preserve legacy publishing grants until adoption. The previous checklist
is retained as [historical reference](docs/legacy-release-checklist.md).

- [ ] Complete the approved maintainer prerequisites, migration order and pilot
      acceptance before registration or delivery.
- [ ] Qualify the package-mode standalone solution and every retained product
      suite, including native authentication, persistence, isolation and cleanup,
      browser/accessibility, package boundaries and compatibility cohorts.
- [ ] Verify that runtime/composition source dependencies are explicitly declared,
      gitlink-pinned and built once, while ordinary libraries remain packages.
- [ ] Consume the separately released shared frozen publisher-created capability.
      Preserve the exact source SHA, release version and one valid publisher-owned
      UTC instant across all images and runtime identifiers.
- [ ] Authenticate candidate validation, App delivery and foreign-content
      preservation. Require green shared CI for the adopted exact source.
- [ ] Immediately after adoption, enable immutable releases and remove only the
      scoped legacy publishing policies and credential grants.
- [ ] Require successful `python3 references/Hexalith.Builds/check.py` from a
      standalone module clone before every post-adoption module push.
- [ ] Observe the next central scheduled nightly. Accept either authenticated
      publication or a valid authenticated “nothing to release” outcome.
- [ ] For publication, verify package identity and source plus OCI identity,
      provenance and the approved common timestamp for `parties`, `parties-mcp`
      and `parties-ui` across all selected platforms.

Keep dependency/security audits, provenance and tests intact when a product,
pipeline or permission failure blocks a gate. Preserve exact-input diagnostics
and resolve the failing owner boundary before claiming acceptance.
