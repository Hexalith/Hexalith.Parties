using Hexalith.Parties.Contracts.State;

namespace Hexalith.Parties.Domain;

public sealed partial class PartyAggregate
{
    private const string ChannelIdCollisionMessage = "Contact channel IDs must be unique ignoring case.";
    private const string ConsentIdCollisionMessage = "Consent ID has ambiguous contact channel ownership.";

    private static bool HasCaseOnlyChannelCollision(IEnumerable<string> channelIds)
    {
        Dictionary<string, string> seen = new(StringComparer.OrdinalIgnoreCase);
        foreach (string id in channelIds)
        {
            if (seen.TryGetValue(id, out string? previous)
                && !string.Equals(previous, id, StringComparison.Ordinal))
            {
                return true;
            }

            seen[id] = id;
        }

        return false;
    }

    private static bool HasConflictingChannelId(PartyState state, string channelId)
        => state.ContactChannels.Any(c =>
            string.Equals(c.Id, channelId, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(c.Id, channelId, StringComparison.Ordinal));

    private static bool HasAmbiguousChannelId(PartyState state, string channelId)
        => state.ContactChannels.Count(c => string.Equals(c.Id, channelId, StringComparison.OrdinalIgnoreCase)) > 1;

    private static bool HasConflictingConsentOwner(PartyState state, string consentId, string channelId)
        => state.ConsentRecords.Any(c =>
            string.Equals(c.ConsentId, consentId, StringComparison.Ordinal)
            && !string.Equals(c.ChannelId, channelId, StringComparison.Ordinal));
}
