using System.Net.Http.Json;
using System.Text.Json;

using Hexalith.Commons.UniqueIds;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Parties.Client.Abstractions;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Queries;
using Hexalith.Parties.Contracts.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.Parties.Client;

/// <summary>Identity gateway client that never reads or mutates shared ambient tenant options.</summary>
/// <param name="httpClient">The scoped gateway transport.</param>
/// <param name="timeProvider">The clock used to validate evidence at completion.</param>
[method: ActivatorUtilitiesConstructor]
public sealed class HttpPartiesIdentityClient(HttpClient httpClient, TimeProvider? timeProvider = null) : IPartiesIdentityClient
{
    private readonly TimeProvider _completionClock = timeProvider ?? TimeProvider.System;

    /// <summary>Creates a gateway client using the system completion clock.</summary>
    /// <param name="httpClient">The scoped gateway transport.</param>
    public HttpPartiesIdentityClient(HttpClient httpClient) : this(httpClient, TimeProvider.System)
    {
    }

    /// <inheritdoc/>
    public async Task<AgentPartyProvisioningResult> ProvisionAgentPartyAsync(string tenantId, ProvisionAgentParty command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateScope(tenantId, command.Identity.TenantId, command.Identity.PartyId);
        if (command.Identity.ContractVersion != 1
            || AgentPartyIdMapping.Create(tenantId, command.Identity.AgentId) != command.Identity.PartyId)
        {
            throw new ArgumentException("Provisioning intent is invalid.", nameof(command));
        }

        SubmitCommandResponse response = await SubmitAsync(tenantId, command.Identity.PartyId, command.LogicalId, command, cancellationToken).ConfigureAwait(false);
        AgentPartyProvisioningResult? result;
        try
        {
            result = response.ResultPayload?.Deserialize<AgentPartyProvisioningResult>(PartiesJsonOptions.Default);
        }
        catch (JsonException)
        {
            throw Unavailable();
        }
        if (result is null || result.Identity != command.Identity || result.ProvisioningRevision <= 0 || string.IsNullOrWhiteSpace(result.SourceId))
        {
            throw Unavailable();
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<PartyIdentityResult> ResolvePartyIdentityAsync(string tenantId, ResolvePartyIdentity query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ValidateScope(tenantId, query.TenantId, query.PartyId);
        PartyIdentityResult result = await QueryAsync<ResolvePartyIdentity, PartyIdentityResult>(tenantId, query.PartyId, query, cancellationToken).ConfigureAwait(false);
        DateTimeOffset completedAt = _completionClock.GetUtcNow();
        if (!Enum.IsDefined(result.Outcome) || result.Outcome != PartyIdentityOutcome.Resolved && result.Evidence?.HumanBinding is not null
            || result.Evidence is { } evidence && (evidence.ContractVersion != 1 || evidence.TenantId != tenantId
            || evidence.PartyId != query.PartyId || evidence.SourcePosition <= 0 || string.IsNullOrWhiteSpace(evidence.ObservationId)
            || evidence.ObservedAt == default || evidence.ObservedAt > completedAt || evidence.Classification is not (PartyIdentityClassification.Human
                or PartyIdentityClassification.Organization)
            || evidence.HumanBinding is { } binding && (evidence.Classification != PartyIdentityClassification.Human
                || binding.ActorId != query.ExpectedActorId || !Matches(binding, tenantId, query.PartyId)
                || evidence.ObservedAt < binding.ValidFrom || evidence.ObservedAt >= binding.ValidUntil
                || completedAt < binding.ValidFrom || completedAt >= binding.ValidUntil || completedAt >= binding.Custody.ExpiresAt))
            || result.Outcome == PartyIdentityOutcome.Resolved && (result.Evidence is not { } resolved
                || !resolved.IsActive || resolved.IsRestricted || resolved.IsErasingOrErased
                || resolved.Classification == PartyIdentityClassification.Unknown
                || resolved.Classification == PartyIdentityClassification.Human && resolved.HumanBinding is null))
        {
            throw Unavailable();
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<HumanActorBindingResult> ResolveHumanActorBindingAtAsync(string tenantId, ResolveHumanActorBindingAt query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ValidateScope(tenantId, query.TenantId, query.PartyId);
        HumanActorBindingResult result = await QueryAsync<ResolveHumanActorBindingAt, HumanActorBindingResult>(tenantId, query.PartyId, query, cancellationToken).ConfigureAwait(false);
        DateTimeOffset completedAt = _completionClock.GetUtcNow();
        if (!Enum.IsDefined(result.Outcome) || result.Outcome != HumanActorBindingOutcome.Resolved && result.Evidence is not null
            || query.ActionAt > completedAt || result.ContractVersion != 1 || result.TenantId != tenantId || result.PartyId != query.PartyId || result.ActionAt != query.ActionAt
            || result.Evidence is { } binding && (!Matches(binding, tenantId, query.PartyId)
                || binding.ActorId != query.ExpectedActorId || binding.BindingVersion != query.ExpectedBindingVersion
                || completedAt >= binding.Custody.ExpiresAt
                || query.ActionAt < binding.ValidFrom || binding.ValidUntil is { } until && query.ActionAt >= until)
            || result.Outcome == HumanActorBindingOutcome.Resolved && (result.Evidence is null
                || result.SourcePosition <= 0 || result.BindingSourcePosition <= 0
                || result.BindingSourcePosition > result.SourcePosition || string.IsNullOrWhiteSpace(result.ObservationId)))
        {
            throw Unavailable();
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task EstablishHumanActorBindingAsync(string tenantId, EstablishHumanActorBinding command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateScope(tenantId, command.TenantId, command.PartyId);
        _ = await SubmitAsync(tenantId, command.PartyId, command.LogicalId, command, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task RebindHumanActorBindingAsync(string tenantId, RebindHumanActorBinding command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateScope(tenantId, command.TenantId, command.PartyId);
        _ = await SubmitAsync(tenantId, command.PartyId, command.LogicalId, command, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task RevokeHumanActorBindingAsync(string tenantId, RevokeHumanActorBinding command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateScope(tenantId, command.TenantId, command.PartyId);
        _ = await SubmitAsync(tenantId, command.PartyId, command.LogicalId, command, cancellationToken).ConfigureAwait(false);
    }

    private async Task<SubmitCommandResponse> SubmitAsync<T>(string tenantId, string partyId, string logicalId, T payload, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logicalId);
        // Each delivery is distinct; the original logical intent remains in the payload across retries.
        string attemptId = UniqueIdHelper.GenerateSortableUniqueStringId();
        var request = new SubmitCommandRequest(attemptId, tenantId, "party", partyId, typeof(T).FullName!,
            JsonSerializer.SerializeToElement(payload, PartiesJsonOptions.Default), attemptId);
        using HttpResponseMessage response = await httpClient.PostAsJsonAsync("api/v1/commands", request, PartiesJsonOptions.Default, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            await HttpPartiesCommandClient.ThrowOnErrorAsync(response, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(PartiesJsonOptions.Default, cancellationToken).ConfigureAwait(false);
            if (body.ValueKind != JsonValueKind.Object
                || body.TryGetProperty("success", out JsonElement success) && success.ValueKind == JsonValueKind.False
                || body.TryGetProperty("rejected", out JsonElement rejected) && rejected.ValueKind == JsonValueKind.True)
            {
                throw Unavailable();
            }

            return body.Deserialize<SubmitCommandResponse>(PartiesJsonOptions.Default) ?? throw Unavailable();
        }
        catch (JsonException)
        {
            throw Unavailable();
        }
    }

    private async Task<TResult> QueryAsync<TQuery, TResult>(string tenantId, string partyId, TQuery payload, CancellationToken cancellationToken)
    {
        var request = new SubmitQueryRequest(tenantId, "party", partyId, typeof(TQuery).FullName!,
            ProjectionType: "party", Payload: JsonSerializer.SerializeToElement(payload, PartiesJsonOptions.Default), EntityId: partyId);
        using HttpResponseMessage response = await httpClient.PostAsJsonAsync("api/v1/queries", request, PartiesJsonOptions.Default, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            await HttpPartiesCommandClient.ThrowOnErrorAsync(response, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            SubmitQueryResponse envelope = await response.Content.ReadFromJsonAsync<SubmitQueryResponse>(PartiesJsonOptions.Default, cancellationToken).ConfigureAwait(false)
                ?? throw Unavailable();
            if (!envelope.Success || envelope.Payload.ValueKind != JsonValueKind.Object
                || envelope.Metadata?.IsDegraded == true || envelope.Metadata?.IsStale == true)
            {
                throw Unavailable();
            }

            return envelope.Payload.Deserialize<TResult>(PartiesJsonOptions.Default) ?? throw Unavailable();
        }
        catch (JsonException)
        {
            throw Unavailable();
        }
    }

    private static bool Matches(HumanActorBindingEvidence evidence, string tenantId, string partyId)
        => evidence.TenantId == tenantId && evidence.PartyId == partyId && evidence.BindingVersion > 0
            && evidence.ActorRevision > 0 && evidence.ValidUntil > evidence.ValidFrom
            && IsCanonicalActorId(evidence.ActorId)
            && !string.IsNullOrWhiteSpace(evidence.SourceId) && !string.IsNullOrWhiteSpace(evidence.ProvenanceId)
            && evidence.Custody is { Purpose: "party-actor-history-v1", LifecycleRevision: > 0,
                SourceExpiryEnforced: true, RestoreSafe: true, DerivedCopiesCovered: true } custody
            && !string.IsNullOrWhiteSpace(custody.PolicyId) && !string.IsNullOrWhiteSpace(custody.EvidenceId)
            && custody.ExpiresAt >= evidence.ValidUntil;

    private static bool IsCanonicalActorId(string actorId)
        => actorId is { Length: 26 } && actorId[0] <= '7'
            && actorId.All(character => "0123456789ABCDEFGHJKMNPQRSTVWXYZ".Contains(character, StringComparison.Ordinal));

    private static void ValidateScope(string tenantId, string payloadTenant, string partyId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        var identity = new AggregateIdentity(tenantId, "party", partyId);
        if (identity.TenantId != tenantId || tenantId != payloadTenant)
        {
            throw new ArgumentException("Identity call scope differs from its payload.");
        }
    }

    private static PartiesClientException Unavailable()
        => new(503, "Identity unavailable", null, "Authoritative identity evidence is unavailable.", null);
}
