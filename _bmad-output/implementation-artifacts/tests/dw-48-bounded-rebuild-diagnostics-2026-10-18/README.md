# DW-48 validation evidence

Party index rebuilds retain seven fixed drop counters in their serialized candidate and completion manifest. Successful completion emits at most one summary per invocation; retries follow the SDK's at-least-once contract. Failed accumulation emits one bounded warning. Live diagnostics and canonical index values retain their existing semantics.

`validation.json` records the exact commands and final exit codes/counts. The retained build outputs and xUnit summaries correspond to final source; service output is reduced to its final execution summary to omit unrelated host traces. Test counts are checked against the full xUnit XML produced by each recorded command. `commitlint.txt` preserves successful validation of the exact commit message.

Independent review and two follow-ups resolved eight findings. There is no new deferred work. The separate Aspire baseline started, but the 20-second Parties health wait exited 17; no healthy full-topology or Release/CI qualification is claimed. The started app was stopped successfully before builds. `aspire-resources.json` contains only selected resource names/states, with endpoints and configuration omitted.
