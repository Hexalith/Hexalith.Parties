# Story 3.8: Provide Runnable Sample Integration

Status: in-progress

## Story

As a developer evaluating Parties,
I want a runnable sample consuming application,
so that I can see command, query, event subscription, and MCP usage in one concrete integration.

## Acceptance Criteria

1. Given the sample application is built, when a developer opens it, then it demonstrates `AddPartiesClient()` registration and required configuration, and it does not reference Server, Projections, DAPR actors, or service internals.
2. Given the sample is run against a local Parties instance, when the developer executes the command scenario, then it creates a party and adds representative contact/identifier data, and it handles typed success and rejection outcomes.
3. Given the sample is run against a local Parties instance, when the developer executes the query scenario, then it retrieves, lists, filters, and searches parties through the client/API boundary, and it displays freshness/degradation metadata where applicable.
4. Given party events are published, when the sample event subscription/read-model scenario runs, then it demonstrates how a consuming application handles party lifecycle events, and it includes guidance for future `PartyErased` cleanup handling even if GDPR is deferred.
5. Given MCP support is available in the local topology, when the sample demonstrates AI agent usage, then it shows the intended MCP interaction path or configuration, and it does not duplicate MCP tool implementation in the sample.
6. Given sample validation runs in CI or a scripted check, when the sample is built and smoke-tested, then it compiles, runs its documented scenarios, and remains aligned with the current client/API contracts.

## Tasks / Subtasks

- [x] Verify the sample uses only the approved consumer boundary. (AC: 1)
  - [x] Keep production references limited to `Hexalith.Parties.Client` and subscriber-owned DAPR ASP.NET Core support.
  - [x] Keep the sample configured through `Parties:BaseUrl` and `Parties:Tenant`.
- [x] Make command outcomes explicit. (AC: 2)
  - [x] Use typed `*WithResultAsync` client methods for create/contact/identifier operations.
  - [x] Print correlation id and optional updated party payload details.
  - [x] Demonstrate typed ProblemDetails/rejection handling through `PartiesClientException`.
- [x] Make query scenarios explicit. (AC: 3)
  - [x] Retrieve a created party by id.
  - [x] Search parties by display name.
  - [x] List active person parties with filter parameters.
  - [x] Print projection freshness metadata when present.
- [x] Verify subscriber-owned event handling. (AC: 4)
  - [x] Keep lifecycle event handlers and local `CustomerSummary` read model.
  - [x] Add an explicit future `PartyErased` cleanup handler that removes local read-model state.
  - [x] Preserve tolerant handling for unknown additive events.
- [x] Verify MCP guidance. (AC: 5)
  - [x] Keep MCP as configuration for the separate `parties-mcp` host.
  - [x] Do not add MCP tool implementation to the sample.
- [x] Add or extend validation guardrails. (AC: 6)
  - [x] Assert sample project references stay inside approved consumer packages.
  - [x] Assert command/query/freshness/rejection sample calls remain present.
  - [x] Assert event subscription and future erasure cleanup guidance remain present.

## Dev Notes

Story 3.8 builds on the existing `samples/Hexalith.Parties.Sample` project. The sample is a consuming application, not a second Parties implementation. Keep it at the typed EventStore-fronted client boundary and subscriber-owned event/read-model boundary. Do not reference `Hexalith.Parties.Server`, projections, DAPR actors, or internal service types from production sample code.

Relevant existing surfaces:

- `samples/Hexalith.Parties.Sample/Program.cs` demonstrates typed client commands, queries, and MCP configuration guidance.
- `samples/Hexalith.Parties.Sample/PartyEventHandler.cs` demonstrates DAPR pub/sub event handling into a local read model.
- `samples/Hexalith.Parties.Sample/DaprComponents/subscription-sample.yaml` defines the sample subscription.
- `tests/Hexalith.Parties.Sample.Tests/SampleOnboardingGuardrailTests.cs` validates sample boundary and onboarding contract.

## References

- Story source: `_bmad-output/planning-artifacts/epics.md#Story 3.8: Provide Runnable Sample Integration`
- Prior story: `_bmad-output/implementation-artifacts/3-7-write-getting-started-documentation.md`
- PRD: `_bmad-output/planning-artifacts/prd.md` FR59, FR26, FR27, FR28, FR29, FR35, FR38
- Architecture: `_bmad-output/planning-artifacts/architecture.md` developer integration, event subscription, MCP boundary, and client package guidance

## Dev Agent Record

### Agent Model Used

Parent-managed Codex recovery flow after nested Codex story sessions were previously blocked from writing and test execution by read-only/no-approval child policy.

### Debug Log References

- Story automator resumed from `_bmad-output/story-automator/orchestration-1-20260521-062818.md` after user requested continuation.
- Existing sample already covered core client registration, command/query calls, DAPR event handling, and MCP configuration; this story tightened typed outcomes, filtered listing, freshness output, erasure cleanup guidance, and guardrails.

### Completion Notes List

- Pending validation.

### File List

- `_bmad-output/implementation-artifacts/3-8-provide-runnable-sample-integration.md`
- `samples/Hexalith.Parties.Sample/Program.cs`
- `samples/Hexalith.Parties.Sample/PartyEventHandler.cs`
- `tests/Hexalith.Parties.Sample.Tests/SampleOnboardingGuardrailTests.cs`

## Change Log

| Date | Author | Change |
|------|--------|--------|
| 2026-05-21 | bmad-story-automator (AI) | Created story artifact and tightened runnable sample integration scenarios. |
