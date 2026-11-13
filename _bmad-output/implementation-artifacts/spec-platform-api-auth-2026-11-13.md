---
title: 'Authenticate official Builds metadata reads in tuple CI'
status: 'in-review'
baseline_commit: 'da27ecf004300bc3678bed05320bfd3b1803cf3b'
review_loop_iteration: 0
context:
  - '/home/administrator/projects/hexalith/parties/AGENTS.md'
  - '/home/administrator/projects/hexalith/parties/references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
  - '/home/administrator/projects/hexalith/parties/references/Hexalith.Platform/eng/builds_publication.py'
  - '/home/administrator/projects/hexalith/parties/references/Hexalith.Platform/eng/test_builds_publication.py'
  - '/home/administrator/projects/hexalith/parties/references/Hexalith.Platform/.github/workflows/tuple-ci.yml'
---

<frozen-after-approval reason="user authorized CI repairs and normal prerequisite releases; human checkpoints waived">

## Intent

Platform current tuple CI43425860578 job131887186602 failed before compilation because eng/builds_publication.py read_official used the anonymous GitHub API limit. Retained artifact14036857652 published-artifacts.stderr shows HTTP403 rate limit exceeded at the official Builds releases endpoint. Fix this using the existing read-only CI job token, preserving all signature, publication, source, count and automated release gates. Keep all previous signed5.2.1 identities and Story2.12 work. Do not broaden workflow permissions, add secrets or catch-and-ignore HTTP failures.

Implementation is local source/config/tests only. No skills, staging, commits, submodule changes, remote operations or publication by the implementation agent. Parent owns fresh source commit, all three new immutable companions, descriptor commit, push and hosted qualification. Every non-tuple edit requires new companions. Never edit historical frozen metadata or existing immutable refs.

</frozen-after-approval>

## Code Map

Owner: ../../references/Hexalith.Platform from this spec.

- eng/builds_publication.py constructs only the fixed official https://api.github.com/repos/Hexalith/Hexalith.Builds/ API requests. Read optional GH_TOKEN from environment and add a Bearer authorization header to that official metadata request when provided. Keep existing anonymous local behavior when no token is available, metadata size/timeout bounds, JSON parsing and HTTP failures. Never log or return token values and never send them to NuGet or another origin. Keep verification receipts unchanged.
- .github/workflows/tuple-ci.yml: provide GH_TOKEN from github.token only in the actual tuple execution step environment of current tuple, legacy-public-cohort and pre-extension-public-cohort jobs, not job-wide. These already run read-only; do not add permissions. Header authentication replaces shared anonymous quota use.
- eng/test_builds_publication.py: retain all seven existing positive/negative controls. Add meaningful mocked transport checks demonstrating the token reaches exactly the fixed API request and is absent from returned metadata, no-token requests omit Authorization, metadata length bounds still reject, and HTTP403 is propagated without an anonymous fallback or success claim. Fake test tokens only. Keep tests in this existing helper test file.
- docs/source-releases.md or eng/tuple-source-preparation.md: add only a concise note explaining the existing read-only job-token metadata authentication and unchanged independent provenance checks, if needed for current workflow documentation.

## Tasks & Acceptance

- [ ] Authenticate fixed official metadata reads when GH_TOKEN is provided, preserving bounded reads and hard failures.
- [ ] Wire the existing read-only token into all three actual tuple execution jobs, without permission or release-gate changes.
- [ ] Run the existing publication/binding tests, new transport controls, source-release controls and relevant workflow syntax check. Retain exact commands/results under a new /tmp directory.
- [ ] Parent regenerates all companions from final reviewed source, publishes them on new refs, commits only descriptors afterward, pushes main, requires all normal qualification jobs then normal source publication.

## Verification

Run python3 -B eng/test_builds_publication.py and eng/test_builds_tuple.py, npm run test:source-release, and available actionlint on the changed workflow. Preserve original refusal controls and do not claim hosted/runtime qualification. Tests should assert meaningful outgoing request/header and refusal behavior rather than merely matching implementation text.

## Review Patch Instructions

Run test_builds_publication.py in the existing current-job automatic controls before tuple qualification. Scope workflow GH_TOKEN to each actual tuple execution step. In eng/run_tuple_ci.py ensure all ordinary subprocess environments remove GH_TOKEN, preserving other existing environment values/overrides; only the published-artifacts verification subprocess may receive it. Use a small private testable environment helper if needed; no public CLI/API or new module is needed. Test default/explicit caller environments, token removal from ordinary subprocesses and token availability only for metadata verification, without printing test token values. Clarify docs to describe subprocess scoping accurately. Preserve fresh signed-artifact preparation (CI must not reuse a caller receipt).

## Review Triage Log

| Finding | Verdict and evidence | Route |
| --- | --- | --- |
| Blind1 stale companions | medium: non-tuple auth changes make prior descriptors stale; all three fresh companions are already explicit pending parent work and must be published before main. | patch: parent generation |
| Blind2 missing automatic transport tests | medium: tracked current controls and run_tuple_ci omit this test file; header-forwarding regressions could evade automatic qualification. | patch: register existing publication controls |
| Blind3 job-wide token inheritance | medium: job env reaches npm and all runner subprocesses, beyond the intended metadata verifier. Existing run() passes inherited os.environ when no environment is supplied. | patch: step scope plus private subprocess environment filtering |
| Verification1 missing automatic transport tests | medium: independently traced concrete failing redirect assertion currently absent from automatic gate. | patch: same registration as Blind2 |
| Edge review | no findings. | no patch |


## Implementation Notes

Review patch tracing also confirmed indirect Git subprocesses from source_state/builds_tuple and signature/tool child processes from prepare_builds_artifacts. These receive no token after the scoped patch. Preserve all other environment values and explicit runtime overrides.


Parent completion note: reviewed patch111 Python/82 Node/actionlint pass. Auth source rebased over preserved e13bb671 review records; reverted dependency pointers restored in8c762201. Fresh immutable companions56495a34/d64ff090/3ef724f2 are published and source comparisons/17-owner reader pass. Descriptor49edf00b is pushed; tuple43431743956 and catalog43431743889 are running. No native or Story acceptance is claimed.
