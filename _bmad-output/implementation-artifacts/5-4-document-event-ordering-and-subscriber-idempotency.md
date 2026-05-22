# Story 5.4 - Document Event Ordering and Subscriber Idempotency

## Status

Done - 2026-05-21T18:25:00Z

## Scope

Added ordering and idempotency hardening for subscriber guidance and sample behavior:

- Sample event handler now tracks the highest processed `sequenceNumber` per aggregate and acknowledges older/replayed sequences without mutating local state.
- Duplicate delivery with a different CloudEvents id but the same aggregate sequence is now skipped.
- Out-of-order older updates no longer overwrite newer local read-model state.
- Event subscription docs clarify broker-specific per-aggregate ordering requirements for Redis Streams, RabbitMQ, Kafka, and Azure Service Bus.
- Handler pattern docs now require aggregate sequence guards in addition to CloudEvents id deduplication.
- Documentation guardrail tests pin ordering guidance and sequence-guard examples.

## Validation

```powershell
dotnet test tests\Hexalith.Parties.Sample.Tests\Hexalith.Parties.Sample.Tests.csproj -p:TreatWarningsAsErrors=false -p:WarningsAsErrors=
```

Result: Passed, 55/55 tests.

Warning override note: the focused command used the existing warning override because the EventStore submodule still has unrelated warning-as-error failures outside this story scope.
