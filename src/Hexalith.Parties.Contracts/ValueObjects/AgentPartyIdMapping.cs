using System.Numerics;
using System.Security.Cryptography;
using System.Text;

using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.Parties.Contracts.ValueObjects;

/// <summary>Version-one tenant/Agent mapping; existing Consumer and Organization IDs are never remapped.</summary>
public static class AgentPartyIdMapping
{
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    /// <summary>Derives a canonical ULID in the reserved zero-time provisioning namespace.</summary>
    public static string Create(string tenantId, string agentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        var identity = new AggregateIdentity(tenantId, "party", agentId);
        if (identity.TenantId != tenantId || agentId.Length != 26 || agentId[0] > '7'
            || agentId.Any(static character => !Alphabet.Contains(character, StringComparison.Ordinal)))
        {
            throw new ArgumentException("Agent identity must be canonical.");
        }

        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes("hexalith-agent-party-v1\0" + tenantId + "\0" + agentId));
        byte[] mapped = new byte[16];
        digest.AsSpan(0, 10).CopyTo(mapped.AsSpan(6));
        var number = new BigInteger(mapped, isUnsigned: true, isBigEndian: true);
        Span<char> result = stackalloc char[26];
        for (int i = result.Length - 1; i >= 0; i--)
        {
            result[i] = Alphabet[(int)(number & 31)];
            number >>= 5;
        }

        return new string(result);
    }

    /// <summary>Detects the reserved new Agent provisioning namespace before generic creation.</summary>
    public static bool IsReserved(string? partyId)
        => partyId is { Length: 26 } && partyId.StartsWith("0000000000", StringComparison.Ordinal);
}
