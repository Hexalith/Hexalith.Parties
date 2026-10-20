# DW-73 publish OIDC secret validation

The AppHost rejects missing, empty and whitespace secrets before constructing its publish model. Valid secret bytes are forwarded unchanged. Local runs preserve both enabled and disabled Keycloak behavior.

`validation.json` retains exact commands and results: Debug/source builds with zero warnings/errors, 40 focused model checks, 29 hostile-environment checks and 2004 service tests, with no failures or skips. It also records the selected source graph, source hashes and superseded failures corrected during development. `commitlint.txt` retains successful validation of the exact commit message using the repository-pinned CLI.

For persistent ambient-isolation verification, rerun the `ambient-isolation` command recorded in `validation.json` after building the integration project. It deliberately supplies a placeholder secret and an invalid target in the environment; all new cases must pass. The environment-only case uses the normal configuration defaults, saves and restores its environment value, and runs in the assembly's non-parallel collection.

Independent review corrected the test-provider ordering issue and added environment-input, disabled-Keycloak and AppHost/runtime mapping coverage. Follow-up review found no introduced defect. Additional fixture-testing automation was rejected as low because the repeatable hostile-environment check covers the observed fault without adding test-process scaffolding.

The owned local AppHost was started in isolation before and after the change and stopped successfully. Parties and UI were healthy in the final run. Other runtime participants exited 134; full topology, deployment and release qualification are separate from these model checks. Raw resource environment snapshots are excluded from evidence.
