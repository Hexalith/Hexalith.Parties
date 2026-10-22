using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.AdminPortal.Services;

/// <summary>Retains expected accepted state only for the lifetime of the current circuit.</summary>
/// <param name="PartyId">The accepted party identifier.</param>
/// <param name="ContextSignature">The authorization context that submitted the command.</param>
/// <param name="ExpectedUpdate">The submitted edit snapshot, or null for create identity convergence.</param>
internal sealed record AdminPortalAcceptedCommand(
    string PartyId,
    string ContextSignature,
    UpdatePartyComposite? ExpectedUpdate)
{
    /// <summary>Checks the complete replacement details and each added child's identity and values.</summary>
    /// <param name="detail">The current projected detail.</param>
    /// <returns>Whether the projected state contains all submitted edits.</returns>
    internal bool Matches(PartyDetail detail)
        => ExpectedUpdate is { } update
            && string.Equals(PartyId, detail.Id, StringComparison.Ordinal)
            && !detail.IsErased
            && (update.PersonDetails is null
                || (detail.Type == PartyType.Person && update.PersonDetails == detail.PersonDetails))
            && (update.OrganizationDetails is null
                || (detail.Type == PartyType.Organization && update.OrganizationDetails == detail.OrganizationDetails))
            && update.AddContactChannels.All(expected => detail.ContactChannels.Any(actual =>
                string.Equals(expected.ContactChannelId, actual.Id, StringComparison.Ordinal)
                && expected.Type == actual.Type
                && string.Equals(expected.Value, actual.Value, StringComparison.Ordinal)
                && expected.IsPreferred == actual.IsPreferred))
            && update.AddIdentifiers.All(expected => detail.Identifiers.Any(actual =>
                string.Equals(expected.IdentifierId, actual.Id, StringComparison.Ordinal)
                && expected.Type == actual.Type
                && string.Equals(expected.Value, actual.Value, StringComparison.Ordinal)));
}
