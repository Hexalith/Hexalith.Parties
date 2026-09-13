# Editorial review — DESIGN.md + EXPERIENCE.md (2026-09-08 update pass)

- **Run as:** `bmad-ux` Finalize → `doc_standards` → `skill:bmad-review lenses=structure,prose`
- **Content class:** docs · **Style guide:** Microsoft Writing Style Guide · **Readers:** humans
- **Baseline reviewed:** spines as rewritten on 2026-09-08 (structure lens), then the same spines after the accepted structure fixes (prose lens)
- Editorial lenses hold content sacrosanct; every item below is about organization or expression only.

## Structure lens — findings and disposition

| # | Location | Finding | Disposition |
|---|---|---|---|
| 1 | EXPERIENCE Voice and Tone ↔ Component/State Patterns | Canonical strings re-quoted in several table rows (paused-consent ×4, degraded ×3, as-of ×3, service-unreachable ×2, erased-self ×3, cancel-too-late ×2, warming ×2) | **Kept as-is.** State rows are read as self-contained contracts by implementers and the validation lenses; the lens's own trade-off (table hopping) outweighs ~90 words. Voice and Tone remains the declared source; any wording change is made there first. |
| 2 | EXPERIENCE Component Patterns (picker, consent, GDPR button), State Patterns (erasure) | Cells 10× longer than neighbours | **Kept.** Cells already carry bold leads per rule; splitting into sibling rows would break the one-row-per-component contract the rubric walker checks. |
| 3 | Density rule stated six times across the pair | Same rule + rationale in DESIGN frontmatter, Layout & Spacing, Do/Don't, EXPERIENCE Foundation, Responsive, Inspiration | **Applied.** DESIGN Layout & Spacing is the single home; EXPERIENCE Foundation reduced to one pointer sentence; DESIGN frontmatter comment reduced to a pointer; Do/Don't row stays as recap. |
| 4 | EXPERIENCE Inspiration "Rejected" bullets | Restate rules specified earlier | **Kept.** The section is the product's principles list; each bullet's rationale is unique and readers use it stand-alone. |
| 5 | EXPERIENCE Foundation ¶1 | Repeats the header blockquote | **Applied.** Opens on the role-routing rule now. |
| 6 | GDPR guard rules (Art. 18(3), erasure pause, Art. 21) in three or four places | Same guard, three tables | **Kept.** Each row is a distinct state the guard applies to; the Consent control row is the behavioral home and the others name the state, not a second rule. |
| 7 | DESIGN Components use-mapping sentence + picker carve-out | Copied verbatim into EXPERIENCE Component Patterns | **Applied.** Replaced by a pointer to the V5 component column; picker carve-out points at EXPERIENCE. |
| 8 | DESIGN Enforcement paragraph | Regex inventory, dated hit count, axe gate duplicated with EXPERIENCE Accessibility Floor | **Partly applied.** Dated hit count replaced by a pointer to the drift review; axe gate defined once (EXPERIENCE). Regex names kept — they are the concrete mandate `bmad-build` implements. |
| 9 | EXPERIENCE Accessibility Floor | Restates rules owned elsewhere | **Kept.** The Floor is the a11y gate checklist and must read stand-alone; lens flagged the trade-off itself. |
| 10 | DESIGN Colors closing "Avoid:" line | Every item is a Don't cell | **Applied.** Reduced to one pointer sentence. |
| 11 | DESIGN Colors brand-fill bullet | Three bullets explain the seed rule | **Superseded.** The bullet now carries the computed readback (the lens saw the pre-readback text); the table directly follows it. |
| 12 | DESIGN frontmatter comments | Restate body rationale | **Kept.** Token consumers read the frontmatter alone; one-line intents are deliberate. |
| 13 | `*ForegroundInverted` ban stated three times, with dated reversal history in Components | Decision history inside a spec bullet | **Applied.** Components bullet reduced to a pointer at Colors; the reversal history lives in `.memlog.md`. |
| 14 | EXPERIENCE Interaction Primitives "Banned:" bullet | Mid-section recap | **Kept.** It is the section's negative checklist. |
| 15 | Key Flows, Open Items, Do's and Don'ts | PRESERVE | Preserved. |

Minor items (accordion sentence duplicated in DESIGN Layout & Spacing / Components; "Spine wins" ×3; "one primary action per view" placement; Key Flows visual-reference line): kept — each is a deliberate cross-reference at the point of use.

## Prose lens — findings and disposition

27 recommendations plus 4 minor. **All applied**, including the six "Consider" rows (density phrasing, precedence-order wording, seed-contrast framing, picker announcement label, `Enter` clause, live-region baseline note), which each removed a real ambiguity without changing a rule. Product copy in quotes was left untouched, as the lens required. Highlights:

- Supersession notes for `UX-DR5` and `UX-DR6` moved to the end of their Components bullets so the definition leads.
- US spelling ("color"), one hyphenation of "master-detail", "V4" casing, spelled-out "including / especially / for example".
- Foundation routing sentence split into three; `accessible-authentication` no longer wraps at the hyphen.
- "Two platform facts constrain this spine"; "Never recreate this with CSS…"; "The accent is never decorative…"; parallelism in the Help & contact row; tense in the Erased / Gone — Admin row.

## Verdict

Both spines keep the locked section order, every `{token}` reference resolves, and no content was added or removed by this pass. Ready for `bmad-build` as the 2026-09-08 baseline.
