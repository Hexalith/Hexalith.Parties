---
title: 'Restore default Parties AppHost startup'
type: 'bugfix'
created: '2026-11-26'
status: 'in-progress'
baseline_commit: 'b9c19657f4616ab5da18433159754a650f7f643e'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent">

## Intent

Restore the default Parties AppHost launch after `b9c19657`. Reproduce the NU1109 restore failures and align the two Microsoft.Extensions packages with Commons.ServiceDefaults 3.0.4 in the Parties central catalog. Preserve caller-selected catalogs, NuGet audits and the DW-97 edits.

Verify isolated startup, Parties and Dapr health, and cleanup of owned resources. Rerun the focused package checks for DW-50, DW-58, DW-82, DW-100 and DW-168. Record the effective versions, commands and results. Keep the five entries open for the required production evidence and owner approvals.

</frozen-after-approval>

## Implementation Notes
