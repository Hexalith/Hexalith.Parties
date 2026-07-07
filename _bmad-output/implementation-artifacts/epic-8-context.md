# Epic 8 Context: Domain-Focus Refactoring and Platform Extraction (Class C)

<!-- Generated from planning artifacts. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Epic 8 removes remaining generic platform infrastructure from Hexalith.Parties so the module conforms to the Hexalith domain-module contract: Parties keeps domain aggregates, contracts, validators, projection/query semantics, GDPR policy, typed domain clients, domain UI, MCP tool definitions, sample code, and a thin AppHost, while reusable platform mechanics move to Commons, EventStore, FrontComposer, Builds, or platform/ops owners. This is approved post-MVP maintenance only; it adds no PRD functional requirements and must not be reported as MVP feature delivery.

## Stories

- Story 8.1: Baseline and release-blocker stabilization
- Story 8.2: Identifier correctness and zero-risk hygiene
- Story 8.3: Platform API prerequisites
- Story 8.4: Leaf-project retirement
- Story 8.5: EventStore domain-service SDK host cutover
- Story 8.6: Projection and query SDK migration
- Story 8.7: Data-protection extraction
- Story 8.8: Client, MCP, AppHost, build, and deploy cleanup
- Story 8.9: UI FrontComposer and Fluent consolidation
- Story 8.10: Final readiness, documentation, and retirement gate

## Requirements & Constraints

Epic 8 must preserve completed product behavior from Epics 1-5 and must not change PRD FR coverage. It supports domain-boundary correctness, observability, build quality, brand discipline, and long-term maintainability.

Before deletion-heavy refactoring starts, the repository needs a trustworthy build/test baseline. Current release readiness is blocked until the full solution build, package compatibility lanes, UI accessibility lane, deploy validation test assembly, and drifted gitlinks are fixed, owner-validated, reset, or explicitly pinned with evidence. `scripts/test.ps1` must not omit ConsumerPortal tests or treat solution-level `dotnet test` lanes as a reliable green signal. Later stories must record direct xUnit v3 assembly execution guidance, sequential build guidance, MinVer override guidance, and sandbox/package-test network limitations where those affect validation.

Structural migration must be deletion-safe. Local rollback paths for projection, query, crypto, and release recovery stay in place until replacement APIs have parity evidence and rollback behavior is proven. Existing public package contracts, command/query behavior, self-scoped authorization, GDPR legal semantics, UI behavior, protected payload compatibility, exports, processing records, erasure reports, and no-leak diagnostics must remain stable or be intentionally versioned.

Identifier cleanup must stop rejecting valid ULID-compatible aggregate IDs while retaining replay compatibility for existing GUID-shaped IDs. Command, message, and correlation ID creation should use approved Commons unique ID helpers where identifier semantics require them.

## Technical Decisions

Hexalith.Parties should consume platform primitives instead of carrying parallel implementations. The target state replaces local service defaults, tenant-claim transformation, domain-service invoker, projection/query actors, projection rebuild service, generic crypto/key-management engine, command envelopes, paging/freshness models, ProblemDetails scrubbing, MCP plumbing, AppHost security/module helpers, build-root probing, and platform-owned deployment assets when approved shared surfaces exist.

The domain-service host target is the EventStore SDK host shape: `AddEventStoreDomainService` and `UseEventStoreDomainService`, with Parties retaining only domain registrations, Parties-specific policy, and payload-protection hooks that cannot yet be shared. Projection and query migration targets are `IDomainProjectionHandler`, `IDomainQueryHandler`, `IReadModelStore`, `ReadModelWritePolicy`, and `IQueryCursorCodec`.

Platform API prerequisites must be additive or explicitly proven already available before Parties source migration consumes them. The prerequisite set covers EventStore projection/query support, DataProtection, client envelopes/freshness/error codes, tenant claims transformation, Aspire publish helpers, FrontComposer UI primitives, Commons HTTP helpers, and Builds shared props/targets.

Build and package discipline remains unchanged: .NET 10, `.slnx` only, Central Package Management, warnings as errors, xUnit v3, Shouldly, NSubstitute, bUnit, Playwright accessibility evidence where UI is touched, and root-level submodules only.

## UX & Interaction Patterns

Epic 8 does not introduce new UX scope. UI work is conformance work: replace local FrontComposer-like status, freshness, optimistic reconcile, grid/list, picker, fixture, and specimen primitives only when replacement behavior preserves the approved FrontComposer and Fluent UI V5/Fluent 2 contract.

Production UI styles must purge legacy FAST/v4 token usage and keep Fluent 2 inheritance discipline. Raw teal accent is non-text only; primary filled actions bind to the AA-safe Fluent brand background. Accessibility contracts remain binding: WCAG 2.2 AA target, keyboard and pointer parity, functional skip links, visible focus rings, forced-colors and reduced-motion support, semantic controls, typed confirmation for destructive actions, split polite/assertive live regions, and no focus stealing for non-blocking optimistic updates.

GDPR copy and interactions must remain honest: no consent dark patterns, no over-promised export latency, erasure copy must distinguish cancellation before deletion starts from permanent completed deletion, and stale reads should render last-known values with quiet freshness cues instead of blanking or throwing.

## Cross-Story Dependencies

Epic sequencing is `8.1 -> 8.2 -> 8.3 -> 8.4 -> 8.5 -> 8.6 -> 8.7 -> 8.8 -> 8.9 -> 8.10`. Story 8.2 may start after the baseline is established if it does not depend on unresolved submodule release work. Stories 8.5 through 8.7 require platform API readiness from Story 8.3. Story 8.10 runs last and must close or explicitly defer remaining work with owners, proof requirements, rollback paths, and validation evidence.

Epic 8 depends on Epic 7 readiness evidence, which left useful adapters, compatibility harnesses, rollback plans, and known blockers in place. Do not rewrite Epic 7 history or treat Epic 8 as feature delivery; use Epic 7 evidence as the starting point for stronger domain-boundary cleanup.
