# Process containment and UI implementation evidence

This sequential continuation implements the worker containment and UI conformance
tasks in the approved five-item spec. The frontmatter context list was empty.
Existing lifecycle/search/retention changes were preserved. No dependencies,
gitlinks, public wire values, deployment state or owner signoffs were changed.

## Owned test-process lifetime

`PackageTestProcessSupervisor` launches each Linux command in an exclusive session
under a dedicated Python 3/procfs subreaper. It terminates/reaps owned descendants
after normal parent exit, early failure and timeout, including adopted descendants
that start separate sessions. An unrelated worker remains alive in the controls.
No runner-wide subreaping or process-name matching is used. The original command
exit code and concurrently drained streams are retained. Both argument overloads
use .NET runtime argument parsing; parity controls include escaped quotes, empty
arguments and backslash-terminated paths.

Linux is qualified. Windows and macOS retain the previous bounded runner and its
exited-parent descendant-containment limitation. Python 3 and procfs are explicit
Linux prerequisites. Cleanup failures remain bounded and are reported.

## UI audit

All 52 Razor/CSS files under the four component roots were inspected. The style
guard now scans every root, including Razor for current-token, color-literal and
typography checks, without migration allowlists. Findings and dispositions:

- Admin detail sections, including the GDPR sibling/fallback, share one Fluent
  accordion; GDPR operations retain their nested operational accordion and focus
  destination. All items initially expand. Public operational subpanels retain
  standalone section/title rendering and opt into accordion items when composed.
- Consumer profile, edit, consent and privacy sibling sections use Fluent
  accordions with primary content initially expanded. The existing edit context,
  fieldset, form submission and confirmation code remains intact.
- The shell accessibility specimen uses the same component grouping. Route titles,
  toolbars, navigation and the single Admin create/edit form remain outside it.
- Legacy theme tokens and hard-coded color fallbacks were replaced with Fluent 2
  tokens. Redundant CSS typography was removed or supplied by FluentText;
  handwritten Admin status badges were replaced with FluentBadge. Native route
  h1 focus destinations are retained. Scoped danger/focus rules reach rendered
  Fluent controls through their app-owned ancestors.
- The Picker retains its established native combobox and selection/focus contract
  and dependency set; its styles now bind directly to Fluent 2 tokens and preserve
  forced-color focus controls.
- Browser assertions distinguish action buttons from same-named accordion headers.
  New Consumer controls exercise default expansion and keyboard collapse/reopen at
  narrow width in normal and forced-color modes. Browser execution remains for
  the parent validation pass; no new conditional skip was introduced.

## Commands and results

Each affected test project was built individually with:

```text
dotnet build tests/Hexalith.Parties.Contracts.Tests/Hexalith.Parties.Contracts.Tests.csproj -c Release -m:1
dotnet build tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj -c Release -m:1
dotnet build tests/Hexalith.Parties.ConsumerPortal.Tests/Hexalith.Parties.ConsumerPortal.Tests.csproj -c Release -m:1
dotnet build tests/Hexalith.Parties.UI.Tests/Hexalith.Parties.UI.Tests.csproj -c Release -m:1
```

All exited 0 with zero warnings/errors; bounded build summaries are adjacent.

```text
dotnet tests/Hexalith.Parties.Contracts.Tests/bin/Release/net10.0/Hexalith.Parties.Contracts.Tests.dll -class '*PackageTestProcessTests'
# Exit 0: 18 passed; zero failed/errors/skipped/not-run.
dotnet tests/Hexalith.Parties.AdminPortal.Tests/bin/Release/net10.0/Hexalith.Parties.AdminPortal.Tests.dll
# Exit 0: 322 passed before the final standalone-panel test was added.
dotnet tests/Hexalith.Parties.AdminPortal.Tests/bin/Release/net10.0/Hexalith.Parties.AdminPortal.Tests.dll -class '*ErasureVerificationReportPanelTests'
# Exit 0: 12 passed, including the new standalone-panel regression.
dotnet tests/Hexalith.Parties.ConsumerPortal.Tests/bin/Release/net10.0/Hexalith.Parties.ConsumerPortal.Tests.dll
# Exit 0: 82 passed; zero failed/errors/skipped/not-run.
dotnet tests/Hexalith.Parties.UI.Tests/bin/Release/net10.0/Hexalith.Parties.UI.Tests.dll
# Exit 0: 413 passed; zero failed/errors/skipped/not-run.
dotnet tests/Hexalith.Parties.UI.Tests/bin/Release/net10.0/Hexalith.Parties.UI.Tests.dll -class '*AccessibilityStyleGuardTests' -class '*PartiesAccessibilitySpecimenTests'
# Exit 0: 12 passed after final style/specimen edits.
npm --prefix tests/e2e run typecheck
# Exit 0, pinned Node/npm path supplied by the handoff.
git -c core.whitespace=blank-at-eol,blank-at-eof,space-before-tab,cr-at-eol diff --check
# Exit 0; recognizes the repository's required CRLF line endings.
```

Earlier test failures were resolved: count mutually exclusive person/organization
sections correctly; use FluentBadge selectors; preserve native route h1 focus
destinations in standalone fixtures. No behavior assertions or gates were waived.

The parent still owns full affected-suite/source Debug verification, managed
browser execution and visual-baseline inspection/update, evidence/ledger closure,
review, commitlint, main reconciliation and push. These receipts do not claim
those pending gates passed.

## Managed browser selector qualification

The first managed run exposed assumptions about Fluent shadow headings and
slotted shell ancestry. Browser scopes now address the real GDPR panel heading,
assert the explicit named processing-record region while checking all its fields
through a unique Fluent component host, and exercise Consumer accordion/focus/
overflow through the actual Consumer content root. No semantic, PII, keyboard or
field assertions were removed and no duplicate region role was introduced.

```text
dotnet build src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj -c Release -m:1
# Exit 0: zero warnings/errors.
npm --prefix tests/e2e run typecheck
# Exit 0 using pinned Node/npm.
CI=true npm --prefix tests/e2e run test -- specs/admin-parties-list.spec.ts specs/consumer-portal-routes.spec.ts --grep 'desktop row click|direct GDPR route for an erased|detail GDPR action|section accordions preserve' --retries=0 --max-failures=5
# Exit 0: all 5 formerly failing cases passed, no skipped cases, 12.8 seconds.
```

The managed server stopped after the run. Full browser and source qualification
remain the parent's gate; this focused pass makes no full-suite claim.

## Accordion focus clearance

The managed accessibility run detected actual clipping of the primary control's
2px outline plus 4px offset at the accordion item boundary. Interactive section
content now receives a FluentStack with Fluent spacing S padding on every edge.
An inner normal-flow container preserves the existing inline/block layout. No
component overflow, theme, focus-ring width, outline offset or contrast rule was
overridden to make the checks pass. Static sections require no extra wrapper.

The production audit applies the same clearance to Admin stream links and
erasure/consent/restriction/export/processing controls, and to Consumer editable
form, consent and privacy sections. New narrow-width production checks focus
Admin operations and Consumer form/switch/export controls and require the full
module-ring footprint to fit every owning accordion. Native Fluent controls may
draw internal indicators; the original specimen checks still assert the actual
outer ring, contrast and forced-color appearance with no exemptions.

```text
CI=true npm --prefix tests/e2e run test -- specs/parties-accessibility.spec.ts specs/admin-parties-list.spec.ts specs/consumer-portal-routes.spec.ts --grep 'content controls have visible keyboard focus|GDPR operation focus rings|section accordions preserve' --retries=0 --max-failures=6
# Exit 0: 6 passed, zero skips/retries, 8.9 seconds.
```

The adjacent normal/forced-color JSON receipts report `clippedBy: []` for both
specimen controls, with the original 2px outline and 4px offset. The full
whitespace-rule command above also passes after removing wrapper trailing
whitespace. The managed server stopped; snapshot update/inspection and final
all-browser verification remain the parent's gate.

After the clearance changes, individually built Release projects passed with zero
warnings/errors. The full Admin assembly passed 323 tests; the full Consumer
assembly passed 82; the focused UI style/specimen/destructive-control classes
passed 16. All had zero errors, failures, skips and not-run cases. TypeScript
typecheck passed with the pinned toolchain. Bounded receipts are adjacent.
