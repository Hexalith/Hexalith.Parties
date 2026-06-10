using Hexalith.Parties.Contracts.Models;

namespace Hexalith.Parties.ConsumerPortal.Services;

public sealed record ConsumerPrivacyErasureResult(
    ConsumerPrivacyErasureOutcome Outcome,
    ConsumerPrivacyErasureState State,
    bool CanCancel,
    ProjectionFreshnessMetadata? Freshness = null)
{
    public static ConsumerPrivacyErasureResult Active(ProjectionFreshnessMetadata? freshness = null)
        => new(ConsumerPrivacyErasureOutcome.Ready, ConsumerPrivacyErasureState.Active, CanCancel: false, freshness);

    public static ConsumerPrivacyErasureResult Failure(ConsumerPrivacyErasureOutcome outcome)
        => new(outcome, ConsumerPrivacyErasureState.Unknown, CanCancel: false);
}
