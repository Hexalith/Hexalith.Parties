# DW-91 resumed source delivery — 2026-10-21

`validation.json` and `review.md` preserve the earlier candidate and its dated
results. They are historical; `validation-resume.json`, `review-resume.md` and
the tracked `.txt` outputs describe the final source candidate. No published
package adoption or protected owner release is claimed.

## Consumer boundary

The published FrontComposer 4.7.0 package does not contain this fix. The normal
Parties suite retains its original six assertions. Two additional tests run
only with `PARTIES_E2E_FORWARD_SKIP_LINKS=1`; this flag enables assertions and
never rewrites assets or skips based on observed application behavior.

The consumer qualification builds the actual Parties UI in Debug with package
dependencies. One host serves those unchanged assets on port 5072. An isolated
diagnostic host serves the same binaries and assets on port 5073, replacing
only the `js/fc-keyboard.js` leaf through a temporary static-asset manifest.
The helper leaves the built manifest unchanged. This checks the candidate
keyboard module with Parties' real FocusOnNavigate/router while preserving the
package boundary. It is not a full source-reference or Release qualification.
The receipt records SHA-256 from both served endpoints and the candidate.

From the repository root, after the documented Debug UI build:

```bash
python3 _bmad-output/implementation-artifacts/tests/dw-91-forward-skip-links-2026-10-21/prepare-keyboard-asset.py \
  src/Hexalith.Parties.UI/bin/Debug/net10.0/Hexalith.Parties.UI.staticwebassets.runtime.json \
  references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Shell/wwwroot \
  /tmp/dw-91-source-assets.runtime.json
ASPNETCORE_ENVIRONMENT=Test Hexalith__Parties__AccessibilitySpecimens__Enabled=true \
Hexalith__Parties__AdminPortalE2E__Enabled=true Parties__BaseUrl=http://127.0.0.1:59999 \
Parties__Tenant=test-tenant dotnet run --project src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj \
  -c Debug --no-build --no-restore --no-launch-profile -- \
  --urls http://127.0.0.1:5073 --staticWebAssets /tmp/dw-91-source-assets.runtime.json
```

From `tests/e2e`, run the flagged source command recorded in the receipt. Run an
unmodified package host without `--staticWebAssets` on 5072 for the default
suite. Enabling both forward-Tab tests against that package host deliberately
fails; `parties-negative-browser.txt` preserves this control.

## Review and remaining delivery

The final producer suite covers all three browser engines, native reverse and
pointer traversal, authored tabindex 0/-2 with onward traversal, overlay return,
reused heading nodes across URLs, modal isolation and register/unregister.
Review findings and patches are recorded separately. Browser outputs retain
actual test-run summaries; local workspace prefixes are replaced by
`{project-root}` where necessary; trailing line whitespace is trimmed.

DW-91 remains open for owner-controlled FrontComposer publication, adoption in
the Builds catalog/Parties package graph, package-mode verification and removal
of the source opt-in. No release was dispatched. The concurrent workspace sync
is retained as user work; its unrelated gitlinks are outside this change.
