using System.Text.Json;

using Hexalith.EventStore.Client.Security;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Parties.Authorization;

/// <summary>Fails closed on unsigned, stale, ambiguous and differently targeted source evidence.</summary>
public sealed class PartyIdentityAuthority(IIdentityAdmissionProof proofService) : IPartyIdentityAuthority
{
    /// <inheritdoc/>
    public PartyIdentityAdmissionResult Admit(CommandEnvelope command)
    {
        ArgumentNullException.ThrowIfNull(command);
        try
        {
            using JsonDocument document = JsonDocument.Parse(command.Payload);
            string logicalId = document.RootElement.GetProperty("logicalId").GetString() ?? string.Empty;
            var scope = new IdentityAdmissionScope(command.TenantId, command.Domain, command.AggregateId,
                command.CommandType, command.MessageId, logicalId, IdentityAdmissionProof.Digest(command.Payload));
            string? proof = command.Extensions?.GetValueOrDefault(IdentityAdmissionProof.ExtensionKey);
            return Result(proofService.Verify(proof, scope));
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            return Result(null);
        }
    }

    /// <inheritdoc/>
    public PartyIdentityAdmissionResult Admit(QueryEnvelope query)
    {
        ArgumentNullException.ThrowIfNull(query);
        var scope = new IdentityAdmissionScope(query.TenantId, query.Domain, query.AggregateId,
            query.QueryType, query.CorrelationId, query.CorrelationId, IdentityAdmissionProof.Digest(query.Payload));
        return Result(proofService.Verify(query.IdentityAdmissionProof, scope));
    }

    private static PartyIdentityAdmissionResult Result(IdentityAdmissionEvidence? evidence)
        => evidence is null ? new(null, "identity-authority-unavailable") : new(evidence, null);
}
