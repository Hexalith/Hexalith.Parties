# Deferred Work

Items raised during code review that are real but not actionable in the current story. Pick up in a follow-up story or hardening sprint.

## Deferred from: code review of 9-6-hexalith-memories-backed-party-search (2026-05-03)

- Static `s_lastKnownDetails` ConcurrentDictionary on `PartyDetailProjectionActor` — pre-existing pattern (not introduced by 9-6). Static actor state is an anti-pattern that compounds under the new synchronous-projection load path. Hardening belongs in a separate Tier-3 story (e.g., move the cache to per-actor instance state with a bounded eviction policy, or remove it once the actor service can satisfy reads from the projection store). [src/Hexalith.Parties.Projections/Actors/PartyDetailProjectionActor.cs]
- AppHost does not wire `Parties__MemoriesSearch__ApiToken` env var — deployment-config concern. Follow up when the cleanup-auth patch lands so dev/staging/prod all have a coherent token-injection path. [src/Hexalith.Parties.AppHost/Program.cs]
- `ContractsArchitectureFitnessTests` resolves the contracts csproj via hardcoded `..\..\..\..\..\src\Hexalith.Parties.Contracts\...` — works in current CI structure but breaks if the test runner cwd or output layout changes. Refactor to `[CallerFilePath]` or an MSBuild-injected source-project path when next touching this test. [tests/Hexalith.Parties.CommandApi.Tests/FitnessTests/ContractsArchitectureFitnessTests.cs]
