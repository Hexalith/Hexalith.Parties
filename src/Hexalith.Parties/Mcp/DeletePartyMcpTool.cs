using System.ComponentModel;
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;

using FluentValidation;
using FluentValidation.Results;

using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.Parties.Authorization;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Projections.Abstractions;
using Hexalith.Parties.Projections.Actors;

using ModelContextProtocol.Server;

namespace Hexalith.Parties.Mcp;

[McpServerToolType]
public static class DeletePartyMcpTool
{
    [McpServerTool(Name = "delete_party")]
    [Description("Deactivates a party (soft delete). The party record is preserved but marked as inactive. This operation is idempotent — deleting an already deactivated party succeeds without error.")]
    public static async Task<string> DeletePartyAsync(
        [Description("The party ID to deactivate (UUID)")] string partyId,
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        McpTenantAccessContext access = await McpTenantAuthorization
            .RequireAccessAsync(services, TenantAccessRequirement.Write, cancellationToken)
            .ConfigureAwait(false);
        string tenant = access.TenantId;

        if (!Guid.TryParse(partyId, out _))
        {
            throw new InvalidOperationException("Party ID is required and must be a valid UUID.");
        }

        // Check party exists via projection
        IActorProxyFactory actorProxyFactory = services.GetRequiredService<IActorProxyFactory>();
        var actorId = new ActorId($"{tenant}:party-detail:{partyId}");
        IPartyDetailProjectionActor proxy = actorProxyFactory.CreateActorProxy<IPartyDetailProjectionActor>(
            actorId, nameof(PartyDetailProjectionActor));

        PartyDetail? currentParty = await proxy.GetDetailAsync().ConfigureAwait(false);
        if (currentParty is null)
        {
            throw new InvalidOperationException($"Party not found. No party exists with ID '{partyId}'.");
        }

        // Idempotent: if already deactivated, return current state immediately (AC #7)
        if (!currentParty.IsActive)
        {
            return JsonSerializer.Serialize(currentParty, McpSessionContext.JsonOptions);
        }

        // Construct DeactivateParty command
        var command = new DeactivateParty { PartyId = partyId };

        // Validate using FluentValidation
        IValidator<DeactivateParty> validator = services.GetRequiredService<IValidator<DeactivateParty>>();
        ValidationResult validationResult = await validator.ValidateAsync(command, cancellationToken).ConfigureAwait(false);
        if (!validationResult.IsValid)
        {
            string errors = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
            throw new InvalidOperationException($"Validation failed: {errors}");
        }

        // Dispatch command via ICommandRouter
        ICommandRouter commandRouter = services.GetRequiredService<ICommandRouter>();

        var submitCommand = new SubmitCommand(
            MessageId: Guid.NewGuid().ToString(),
            Tenant: tenant,
            Domain: "party",
            AggregateId: partyId,
            CommandType: nameof(DeactivateParty),
            Payload: JsonSerializer.SerializeToUtf8Bytes(command),
            CorrelationId: Guid.NewGuid().ToString(),
            UserId: access.UserId);

        CommandProcessingResult result = await commandRouter
            .RouteCommandAsync(submitCommand, cancellationToken)
            .ConfigureAwait(false);

        if (!result.Accepted)
        {
            PartyDetail? latestDetail = await proxy.GetDetailAsync().ConfigureAwait(false);
            if (latestDetail is { IsActive: false })
            {
                return JsonSerializer.Serialize(latestDetail, McpSessionContext.JsonOptions);
            }

            throw new InvalidOperationException($"Deactivation failed: {result.ErrorMessage}");
        }

        // Query updated PartyDetail from projection (eventual consistency)
        PartyDetail? updatedDetail = await proxy.GetDetailAsync().ConfigureAwait(false);
        updatedDetail = updatedDetail is { IsActive: false }
            ? updatedDetail
            : currentParty with { IsActive = false };

        return JsonSerializer.Serialize(updatedDetail, McpSessionContext.JsonOptions);
    }
}
