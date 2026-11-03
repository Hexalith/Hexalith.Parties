# Exact FrontComposer 4.7.5 package capability proposal

Recorded 2026-11-03 by codex-agent under the user's delegated Story 2.8 reference-PR scope. This proposes the catalog-selected package capabilities; it records no new human ratification, producer source/reconciler parity, hosted lifecycle or candidate acceptance. Existing 4.7.4 receipts and dated owner approvals remain historical evidence.

The official [v4.7.5 release](https://github.com/Hexalith/Hexalith.FrontComposer/releases/tag/v4.7.5), tag and sealed manifest bind source `07089dcbdad784a1d53037e9cea78d1d7becd8d6` (release 430861316, published 2026-11-01T21:14:03Z). All six consumed package IDs/versions/source metadata match the release inventory. GitHub release archives match published checksums but are unsigned (first NU3004 outcome retained); the corresponding NuGet.org archives pass repository-signature verification with offline revocation. Their payloads match the unsigned release archives, isolated restored archives and extracted files. All consumed runtime DLLs, including French Shell resources, match Release UI output.

| Signed NuGet.org package | SHA-256 |
| --- | --- |
| Hexalith.FrontComposer.Contracts 4.7.5 | `e291682908bce270fa9465912b56aa9b83ef22944ec082aae4d3ae7fca68893a` |
| Hexalith.FrontComposer.Contracts.UI 4.7.5 | `c52756c8a9e5a6b4c33a52f6b5de98c2a5967bb715735219a8ab8acc924e7fc3` |
| Hexalith.FrontComposer.Mcp 4.7.5 | `74d1ba72b5cb50c40d0f297a2448ec54e78b771a95ee3a8f0be30ff742dde019` |
| Hexalith.FrontComposer.Schema 4.7.5 | `b1d0c5a5b47632042979fd5c2bdde7e459d9ad23cbf926c5337f777dca5a3d67` |
| Hexalith.FrontComposer.Shell 4.7.5 | `93f88a4383d8f3b8547195c9c5934db1ac84fd0051d09654fbd9f9d23bb14b31` |
| Hexalith.FrontComposer.SourceTools 4.7.5 | `19b16a1c8be4f0757f96caceddb95a3f3ecf528472ac7ee9abad55717f26439a` |

All four guarded static assets are byte-identical to official 4.7.4. The relevant Shell source change removes duplicate spoken home-card names/counts; it adds no reconciler adoption.

| Served Shell asset | Bytes | SHA-256 |
| --- | ---: | --- |
| js/fc-focus.js | 55317 | `cf0d25667667fab41a6a146a280d7777644b132ffac021f013072b2e01650aca` |
| js/fc-keyboard.js | 20025 | `ec2ca7ae51b6597ab102a1623530e9367fc3f342041ba81835a6ce90ce08b7e5` |
| js/fc-download.js | 1196 | `ce8a8f32468f1d1fd5a84fdc0667d7ba8f792f49c4be36880db5b8a8ac3a0181` |
| css/fc-shell.css | 2042 | `dceb51b05c81b0b5f1a90b5318ede9366dfdb9c2d10b6b1d9f89f4389a35d3a3` |

The browser helper admits exactly 4.7.5 and still compares all four served response bodies with evaluated package-folder bytes. Source-mode mapping validation is unchanged. `HFC_SHARED_UI` adds only exact 4.7.5 to the existing qualified package list; unknown versions remain refused, and `HFC_SHARED_RECONCILE_READINESS` remains source-only. Eight real MSBuild selector controls cover previous versions, exact 4.7.5, rejected versions and source mode.

Release UI build: 0 warnings/errors. Locked Node 24.22.1/npm 11.19.1 installation, configuration tests, typecheck and restored-asset resolver pass. Full owning `CI=true npm run test:ui`: **68 passed, 0 failed/skipped/flaky**, each at retry 0, 53.8 seconds. Six asset attachments bind all four official body hashes. No functional/accessibility assertion or browser retry setting changed.

First hosted d72 browser outcome (61 passed/7 guard failures) remains retained. The first local full run after the version guard update produced 65 passed/3 failures because the old compile-time capability list selected fallback UI; its screenshots/traces/video remain retained. After the exact capability correction and Release rebuild, the full gate above passed.

Independent consumer restore/build/run subprocesses now remove only the same seven parent mapping environment keys used by existing package fixtures. Both real consumers retain package-only references, the local CI package feed and official dependencies. All 11 owning Release packages pack/validate; both consumers restore/build; the client verifies 41 routes, 41 operation schemas and 28 adapters. Five helper controls include a real restore/build with invalid inherited parent imports and existing feed/fallback refusals. Production HXW005 remains strict.

Detailed sanitized receipts are in Platform Story 2.8 evidence directory `release-phase/remote-resumption-20261103`: `frontcomposer-475-official-package-proof.json`, `frontcomposer-475-served-assets.json`, `frontcomposer-475-browser-{first,corrected}.json`, corresponding artifact archives and `parties-consumer-corrected-smoke.json`. Hosted isolated/native execution, real services, admission/withdrawal/cancellation/partial-startup cleanup and candidate acceptance remain unqualified.
