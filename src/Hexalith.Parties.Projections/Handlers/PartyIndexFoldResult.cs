using Hexalith.EventStore.Contracts.Projections;
using Hexalith.Parties.Projections.Models;

namespace Hexalith.Parties.Projections.Handlers;

/// <summary>Captures one shared-index fold result and its post-commit search notification.</summary>
internal readonly record struct PartyIndexFoldResult(
    PartyIndexSdkReadModel Model,
    ProjectionEventDto? LastIndexedEvent,
    bool Removed,
    string? FailureReason);
