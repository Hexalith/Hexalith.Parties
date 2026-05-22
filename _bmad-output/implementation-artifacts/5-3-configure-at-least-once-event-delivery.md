# Story 5.3 - Configure At-Least-Once Event Delivery

## Status

Done - 2026-05-21T18:05:00Z

## Scope

Added at-least-once delivery validation for party event publication:

- Persisted party events publish through the configured EventStore-derived DAPR pub/sub topic.
- Event publication failure after persistence leaves persisted envelopes available for retry.
- Retry after a partial subscriber failure can redeliver the same envelope, proving duplicate delivery is a valid consumer scenario.
- Deployment validation now explicitly fails when no `pubsub*.yaml` component is present.
- Subscriber documentation now states persist-then-publish, acknowledgement, retry, and idempotent-handler expectations.

## Validation

```powershell
dotnet test tests\Hexalith.Parties.Tests\Hexalith.Parties.Tests.csproj --filter "FullyQualifiedName~PartyDomainEventPublicationContractTests" -p:TreatWarningsAsErrors=false -p:WarningsAsErrors=
```

Result: Passed, 10/10 tests.

```powershell
dotnet test tests\Hexalith.Parties.DeployValidation.Tests\Hexalith.Parties.DeployValidation.Tests.csproj --filter "FullyQualifiedName~MissingPubSubComponent|FullyQualifiedName~MissingDeadLetterConfig|FullyQualifiedName~SubscriberInPublishingScopes|FullyQualifiedName~WildcardAppId|FullyQualifiedName~MissingComponentResiliencyTargets" -p:TreatWarningsAsErrors=false -p:WarningsAsErrors=
```

Result: Passed, 6/6 tests.

Warning override note: the focused commands used the existing warning override because the EventStore submodule still has unrelated warning-as-error failures outside this story scope.
