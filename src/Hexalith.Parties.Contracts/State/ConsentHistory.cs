using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.Contracts.State;

/// <summary>Folds ordered consent events while retaining lifecycle and ownership evidence.</summary>
internal static class ConsentHistory
{
    /// <summary>Adds a renewal, ignoring grant replay and same-owner active retries.</summary>
    internal static bool Record(List<ConsentRecord> records, ConsentRecorded e)
    {
        ConsentRecord grant = new()
        {
            ConsentId = e.ConsentId,
            ChannelId = e.ChannelId,
            Purpose = e.Purpose,
            LawfulBasis = e.LawfulBasis,
            GrantedAt = e.GrantedAt,
            GrantedBy = e.GrantedBy,
            Source = e.Source,
        };
        if (records.Any(c => (c with
            {
                RevokedAt = null,
                RevokedBy = null,
                RevocationReason = null,
                RevocationSource = null,
            }) == grant
            || (c.ConsentId == e.ConsentId && c.ChannelId == e.ChannelId && c.IsActive)))
        {
            return false;
        }

        // Never discard foreign-owner evidence: it must continue to block commands.
        records.Add(grant);
        return true;
    }

    /// <summary>Closes all active records for one owner without rewriting past evidence.</summary>
    internal static bool Revoke(List<ConsentRecord> records, ConsentRevoked e)
    {
        ConsentRecord[] matching = records.Where(c => c.ConsentId == e.ConsentId).ToArray();
        if (matching.Select(c => c.ChannelId).Distinct(StringComparer.Ordinal).Skip(1).Any()
            || matching.Any(c => c.RevokedAt == e.RevokedAt
                && c.RevokedBy == e.RevokedBy
                && c.RevocationReason == e.Reason
                && c.RevocationSource == e.Source))
        {
            // The event has no owner/generation discriminator. Do not guess on ambiguity
            // or let an exact old revocation replay close a later renewal.
            return false;
        }

        bool changed = false;
        for (int i = 0; i < records.Count; i++)
        {
            if (records[i].ConsentId == e.ConsentId && records[i].IsActive)
            {
                records[i] = records[i] with
                {
                    RevokedAt = e.RevokedAt,
                    RevokedBy = e.RevokedBy,
                    RevocationReason = e.Reason,
                    RevocationSource = e.Source,
                };
                changed = true;
            }
        }

        return changed;
    }
}
