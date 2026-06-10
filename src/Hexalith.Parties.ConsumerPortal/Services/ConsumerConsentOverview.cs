using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.ConsumerPortal.Services;

public sealed record ConsumerConsentOverview(
    bool IsErased,
    ProjectionFreshnessMetadata? Freshness,
    IReadOnlyList<ConsumerConsentChannel> ContactChannels,
    IReadOnlyList<ConsentRecord> ConsentRecords);
