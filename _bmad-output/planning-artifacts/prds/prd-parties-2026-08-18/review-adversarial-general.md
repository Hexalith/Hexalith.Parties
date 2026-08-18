# Adversarial Review — parties-ui PRD

Target: `_bmad-output/planning-artifacts/parties-ui-prd.md`
Reviewed: 2026-08-18. Stated purpose under attack: "the canonical, PRD-shaped
requirements source for implementation readiness checks."

## Attack summary

The document's central claim — canonical — is false by its own text: §Source
Artifacts cedes authority on every topic domain to another document, so the PRD
is authoritative for nothing while presenting itself to tooling as the single
source. It is also stale by construction: frontmatter says 2026-06-27, but git
history shows edits on 2026-07-06 and 2026-07-16 with no version, changelog, or
date bump, and the body cites events later than its own evidence date. The
requirement inventory a readiness tool would extract is unreliable: three
incompatible ID grammars, UX-DR requirements compressed into ranges whose item
counts don't match their ID counts (and whose declared authoritative source
doesn't even contain the UX-DR identifiers — they live in `epics.md`), no NFR or
UX-DR rows in the traceability matrix, no acceptance criteria, and normative
language ("honestly", "plain", "usable", "bounded", "agreed") that no check can
verify. A compliance-blocking KMS prerequisite is parked in "Out of MVP Scope"
where coverage extraction will never see it. As a prose consolidation the file
is readable; as a machine-consumed readiness source it fails its stated purpose.

## Findings

- **[critical]** Canonical in name, subordinate in fact (§Purpose vs §Source
  Artifacts) — The PRD declares itself "the canonical ... requirements source",
  then rules that on any conflict "the source artifact owning the topic wins:
  architecture for system decisions, UX spines for product experience, and
  implementation story records for completed work evidence." Those three domains
  partition essentially everything the PRD contains, so the document is
  authoritative for nothing. Worse, none of the six listed sources is pinned to
  a revision, hash, or date, so a readiness check that reads only this file has
  no way to detect that it has silently diverged — and §Current Implementation
  Evidence even instructs that readiness validation "must reconcile this PRD
  ... with implementation story records", i.e., the PRD admits the readiness
  check cannot trust the PRD. A verdict built on this file is unfalsifiable by
  design: any error can be excused as "the source artifact won." *Fix:* pick
  one: (a) make it genuinely canonical — changes flow into this file via change
  control and this file wins conflicts; or (b) rename it a "consolidated
  requirements index", and require the readiness tool to validate against
  pinned source revisions (commit SHA or content hash per source) recorded in
  this file.

- **[critical]** Frontmatter date is false and the document mutates without
  change control (frontmatter `date: 2026-06-27` vs §Current Implementation
  Evidence) — The body cites `sprint-change-proposal-2026-07-06.md` and states
  Epic 7 "is completed", both later than the document's only date. Git history
  confirms edits on 2026-07-06 ("Epic 7 completion and Epic 8 backlog") and
  2026-07-16 with the frontmatter date never updated; there is no version
  number, no changelog, no owner, no approver. The evidence section's "As of
  2026-06-27" heading sits directly above facts that only became true in July —
  an internal contradiction a tool timestamping its coverage snapshot will
  faithfully propagate. For a file that other tooling depends on, silent
  mutation is the worst possible failure mode. *Fix:* add `version`,
  `last_updated`, and an owner to frontmatter; add a changelog section; scope
  "as of" claims per subsection; bump on every edit.

- **[critical]** UX-DR requirements are unrecoverable from this file, and the
  declared authority does not define them (§UX Requirements) — Sixteen
  requirements are compressed into four range bullets. "UX-DR8 through UX-DR12"
  (5 IDs) is glossed by seven features (live-region split, real semantics,
  focus contracts, non-color cues, target sizing, forced-colors, reduced-
  motion); "UX-DR13 through UX-DR16" (4 IDs) by five (honest erasure, lawful-
  basis, export, plain-verb, single-status-source copy). No injective ID-to-
  requirement mapping exists, so a coverage extractor cannot attribute any
  individual UX-DR from this file alone. The escape hatch fails too: the
  section names "the final UX design set" as authoritative, but the UX-DR
  identifiers do not appear anywhere in `DESIGN.md`, `EXPERIENCE.md`, or
  `validation-report.md` — they are defined in `epics.md` (e.g., line 267,
  "UX-DR1 — AA-safe brand fill"). The PRD points readers and tools at a source
  that does not contain the IDs it cites. *Fix:* enumerate all 16 UX-DRs as
  individual one-line requirements in this file, and cite `epics.md` as their
  defining source (or move the definitions into the design set and re-point).

- **[high]** Untestable normative language throughout the FRs/NFRs
  (FR-Consumer-3, FR-Consumer-4, NFR1, NFR4, NFR6, NFR7, FR-Admin-1,
  FR-Admin-4) — A readiness check extracting FR/NFR coverage can verify none of
  these clauses: "grant and withdraw consent **honestly**" (FR-Consumer-3);
  copy "must be **plain, honest**" (FR-Consumer-4); "Legal bases are
  represented **honestly**" (NFR4); "**usable** target sizes" (NFR1 — WCAG 2.2
  supplies an actual number, 24×24 CSS px SC 2.5.8, which the PRD declines to
  state); tenant warm-up "**communicated as a temporary state**, not as
  misleading access denial" (NFR6); "stale/degraded read **handling**"
  (FR-Admin-1 — handling defined nowhere); "**bounded** verification report"
  (FR-Admin-4) and "**bounded** audit metadata" (FR-Consumer-4) — bounded by
  what, measured how; "the **agreed** domain deltas" (NFR7) — agreed where, by
  whom, recorded in what artifact. Each of these reduces coverage checking to
  keyword presence. *Fix:* replace every adverbial quality with an observable
  criterion (e.g., "consent toggles default Off and no consent event is emitted
  without an explicit user action"; "target sizes >= 24x24 CSS px per WCAG
  2.2 SC 2.5.8"; name the artifact that records the agreed deltas).

- **[high]** No acceptance criteria, no NFR thresholds, and whole NFR
  categories missing (§Functional Requirements, §Non-Functional Requirements) —
  Every FR is capability prose with no acceptance criteria; every NFR is
  unquantified. There is no performance, latency, availability, capacity,
  concurrency, or browser-support NFR at all; NFR2 makes freshness "first-
  class" but sets no bound on how stale data may be before the UI must say so;
  NFR8 mandates observability with no SLO or alerting requirement. For a
  document whose sole purpose is to feed readiness checks, "coverage"
  degenerates to "some story mentions the ID" — the check can never fail on
  substance. *Fix:* attach 2–5 verifiable acceptance criteria per FR,
  quantify each NFR, and explicitly declare any waived NFR categories
  ("performance: not a gate for this initiative because ...") so absence is a
  decision, not a hole.

- **[high]** Traceability matrix omits NFRs and UX-DRs entirely (§Traceability
  Matrix) — The matrix maps only the nine FRs to Epics 1–5. NFR1–NFR9 have no
  epic or surface mapping anywhere (Epic 6 "supports NFR9" appears only as
  prose in a different section); UX-DR1–16 map to nothing. A tool extracting
  "FR/NFR coverage" from this file finds NFR coverage undefined and will either
  crash, report 0%, or silently skip NFRs — all three outcomes defeat the
  document's purpose. *Fix:* add matrix rows for NFR1–9 and UX-DR1–16 (or an
  explicit "verified by" column naming the test lane/gate per NFR).

- **[high]** Three incompatible requirement ID grammars break mechanical
  extraction (§FR-Shell, §NFR1, §UX Requirements) — The file uses `FR-Shell`
  (area, no number), `FR-Admin-N`/`FR-Consumer-N` (area plus number), `NFR1`
  (no hyphen, no area), and `UX-DRn`. A regex like `FR-[A-Za-z]+-\d+` misses
  FR-Shell; `NFR-\d+` misses every NFR. The document also never declares which
  classes count as "functional requirements" for the Epic 7/8 scope invariant
  ("no new PRD functional requirement coverage") — do UX-DRs count? A tool must
  guess. *Fix:* normalize IDs (e.g., FR-Shell-1, NFR-1..NFR-9), state the ID
  scheme in a conventions note, and declare which ID classes participate in
  functional-coverage counting.

- **[high]** Role model is incoherent: DPO exists in FR-Admin-4 but not in the
  shell (§FR-Shell vs §FR-Admin-4) — FR-Shell routes exactly three roles
  ("Admin or TenantOwner users land in Admin; Consumer users land in
  Consumer"). FR-Admin-4 then grants erasure and Art.30 powers to "DPO/Admin
  users" — a DPO role the shell never routes, gates, or lands anywhere. Also
  unspecified: a user holding both Admin and Consumer roles (which landing
  wins?), and a user holding none (NoPartyBinding covers only Consumers
  without exactly one verified `party_id` claim). A hostile reviewer reads
  this as: the most privilege-sensitive role in the document has undefined
  navigation and undefined gating. *Fix:* add a complete role/landing/
  navigation table covering Admin, TenantOwner, DPO, Consumer, multi-role, and
  no-role principals.

- **[high]** Compliance-blocking prerequisite buried in "Out of MVP Scope"
  (§Out of MVP Scope) — "Production KMS provisioning is a deployment
  prerequisite before processing real regulated EU personal data" is a legal
  go-live gate for the product's core (GDPR) purpose, yet it is phrased as a
  non-requirement in the section coverage tooling is designed to ignore. No
  FR, NFR, or gate owns it. A readiness check can report the initiative green
  while the system is not lawfully deployable against real EU personal data —
  the exact false positive this PRD exists to prevent. *Fix:* promote it to an
  NFR or an explicit deployment-gate requirement with an owner and a
  verification method; "Out of MVP Scope" should contain only things nobody
  must do.

- **[medium]** NFR3 asserts an enforcement the out-of-scope section defers
  (§NFR3 vs §Out of MVP Scope) — NFR3 states "Parties-side defense-in-depth
  asserts `aggregateId == party_id`", while Out of MVP Scope says "Gateway-
  level data-subject/self principal support remains a future enhancement." The
  PRD never states which layer enforces own-data-only today versus later, so a
  reader cannot tell whether NFR3 is implemented fact or aspiration pending
  the deferred gateway work — precisely the ambiguity a readiness check on a
  security NFR must not have. *Fix:* state explicitly which assertion exists
  now, at which seam, and what the deferred gateway enhancement adds beyond it.

- **[medium]** Scope invariant excludes Epic 6 and strains against its own
  bullets (§Current Implementation Evidence) — The bolded "Scope invariant"
  binds only Epics 7 and 8; Epic 6's identical claim ("carries no new PRD
  functional requirement coverage") is an ordinary bullet with weaker
  normative status for no stated reason — a tool honoring only the invariant
  would treat Epic 6 differently. Meanwhile Epic 7 is described as "completed
  partial platform-alignment scope" inside an invariant that says
  "maintenance scope only"; "partial platform-alignment" is defined nowhere
  and reads as something other than maintenance. *Fix:* one invariant covering
  Epics 6–8 uniformly; drop or define "partial platform-alignment".

- **[medium]** Unresolvable references inside the evidence and NFRs (§Current
  Implementation Evidence, §NFR7, §FR-Admin-4) — "Story 3.5 completed D7
  erasure certificate": `D7` is defined nowhere in this file (it is an epics.md
  decision ID; the PRD imports the token without its source). "The agreed
  domain deltas" (NFR7) names no artifact recording the agreement. "Existing
  typed client/gateway seams" (FR-Admin-4) is unverifiable without naming the
  seams. Each forces a tool or reviewer to leave the "canonical" file to learn
  what the requirement even refers to. *Fix:* define or link every imported
  identifier at first use.

- **[medium]** FR-Consumer-4's cancellation window is undefined — by the PRD's
  own honesty standard (§FR-Consumer-4) — Consumers may "cancel erasure while
  cancellation is still allowed", but no event or boundary defines when
  cancellation stops being allowed. The same section demands copy "free of
  hard timing promises that the system cannot guarantee" — yet the PRD itself
  cannot state the boundary, so neither implementers nor testers can know what
  the UI is permitted to promise. The requirement fails its own honesty rule.
  *Fix:* define the cancellation boundary as an event (e.g., "until the
  erasure obligation transitions to started"), even if no wall-clock duration
  is promised.

- **[medium]** No definition of what "readiness" means against this file
  (§Purpose) — The document exists to serve readiness checks but never states
  the contract: what constitutes coverage (story reference? test evidence?
  both?), which requirement classes are gating, or how NFR satisfaction is
  demonstrated. Different tools will compute different verdicts from the same
  file and all can claim compliance. *Fix:* add a short "readiness contract"
  section: per requirement class, what evidence counts and where it lives.

- **[low]** NFR9 is a process gate, not a product NFR, and the frontmatter
  status is a self-description (§NFR9, frontmatter) — ".NET 10, central
  package management, `.slnx`, warnings as errors ... root-level submodules
  under `references/` only" are repository/build policy, not attributes of the
  parties-ui product; counting them as product NFR coverage pollutes the
  extraction. Frontmatter `status: canonical-requirements-source` is a claim
  of role, not a lifecycle state — tooling expecting draft/review/approved
  gets an unrecognized enum. *Fix:* move NFR9 to a "Engineering Constraints"
  section outside NFR numbering (or label the class explicitly); use a real
  lifecycle status plus a separate `role` field.
