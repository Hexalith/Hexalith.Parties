---
title: 'Set commitlint line length to 200'
type: 'feature'
created: '2026-08-01'
status: 'done'
route: 'one-shot'
---

# Set commitlint line length to 200

## Intent

**Problem:** The inherited commitlint configuration limits commit titles and body lines to 100 characters instead of the requested 200 characters.

**Approach:** Override the header and body line-length rules at error severity with a 200-character maximum, and pin both values in the existing CI contract test.

## Suggested Review Order

- The explicit overrides make 200 characters the enforced boundary for titles and body lines.
  [`commitlint.config.mjs:3`](../../commitlint.config.mjs#L3)

- The CI contract test prevents either configured limit from drifting silently.
  [`PartiesContainerPublishWorkflowTests.cs:182`](../../tests/Hexalith.Parties.Ci.Tests/PartiesContainerPublishWorkflowTests.cs#L182)
