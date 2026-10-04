using System.Reflection;
using System.Net.Http;
using System.Text.Json;

using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.Parties.Authorization;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Queries;
using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Contracts.State;
using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.Queries;

/// <summary>Strict current and action-time identity reads, with no stale fallback or profile result.</summary>
public sealed class PartyIdentityQueryService(IPartyIdentityAuthority authority, TimeProvider timeProvider,
    IAuthoritativeEventStreamReader? reader = null, IIdentityHistoryCustody? custody = null)
{
    /// <summary>Resolves current classification and eligibility from complete source and actor evidence.</summary>
    public async Task<PartyIdentityResult> ResolveAsync(QueryEnvelope envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        try
        {
            ResolvePartyIdentity? query = JsonSerializer.Deserialize<ResolvePartyIdentity>(envelope.Payload, PartiesJsonOptions.Default);
            if (query is null || !Matches(envelope, query.TenantId, query.PartyId))
            {
                return new(PartyIdentityOutcome.Unavailable, null);
            }

            IdentityAdmissionEvidence? admitted = authority.Admit(envelope).Evidence;
            if (admitted is null || reader is null)
            {
                return new(PartyIdentityOutcome.Unavailable, null);
            }

            AuthoritativeStreamReadResult read = await reader.ReadAsync(new(query.TenantId, "party", query.PartyId), cancellationToken).ConfigureAwait(false);
            if (!read.IsAuthoritative || !MatchesSource(envelope, read.Stream))
            {
                return new(PartyIdentityOutcome.Unavailable, null);
            }

            PartyState state = PartyIdentitySourceFold.Fold(read.Stream!);
            if (!state.HasBeenCreated)
            {
                return new(PartyIdentityOutcome.Unavailable, null);
            }

            PartyIdentityClassification classification = state.Type == PartyType.Person ? PartyIdentityClassification.Human
                : state.Type == PartyType.Organization ? state.AgentProvisioning is null
                    ? PartyIdentityClassification.Organization : PartyIdentityClassification.Agent
                : PartyIdentityClassification.Unknown;
            bool eligible = state.IsActive && !state.IsRestricted && state.ErasureStatus == ErasureStatus.Active;
            HumanActorBindingEvidence? binding = null;
            if (classification == PartyIdentityClassification.Human)
            {
                DateTimeOffset now = timeProvider.GetUtcNow();
                HumanActorBindingEvidence[] candidates = [.. state.HumanActorBindings.Select(item => item.Evidence)
                    .Where(item => item.ValidFrom <= now && (item.ValidUntil is null || now < item.ValidUntil))];
                if (candidates.Length == 1 && candidates[0].ActorId == query.ExpectedActorId
                    && admitted.TargetActorId == candidates[0].ActorId && admitted.ActorActive
                    && admitted.ActorRevision >= candidates[0].ActorRevision && custody is not null
                    && await custody.CanReadAsync(read.Stream!.Identity, candidates[0].Custody, cancellationToken).ConfigureAwait(false))
                {
                    binding = candidates[0];
                }

                eligible &= binding is not null;
            }

            AuthoritativeEventStream source = read.Stream!;
            var evidence = new PartyIdentityEvidence(1, query.TenantId, query.PartyId, classification,
                state.IsActive, state.IsRestricted, state.ErasureStatus != ErasureStatus.Active,
                source.Head, source.ObservedAt, source.ObservationId, binding);
            return new(eligible && classification != PartyIdentityClassification.Unknown
                ? PartyIdentityOutcome.Resolved : PartyIdentityOutcome.Ineligible, evidence);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return new(PartyIdentityOutcome.Unavailable, null);
        }
    }

    /// <summary>Resolves the unique recorded interval without substituting current actor or binding state.</summary>
    public async Task<HumanActorBindingResult> ResolveAtAsync(QueryEnvelope envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ResolveHumanActorBindingAt? query = null;
        try
        {
            query = JsonSerializer.Deserialize<ResolveHumanActorBindingAt>(envelope.Payload, PartiesJsonOptions.Default);
            if (query is null || !Matches(envelope, query.TenantId, query.PartyId)
                || string.IsNullOrWhiteSpace(query.ExpectedActorId) || query.ExpectedBindingVersion <= 0
                || authority.Admit(envelope).Evidence is not { } admitted
                || admitted.TargetActorId != query.ExpectedActorId || reader is null || custody is null)
            {
                return HistoryFailure(query, HumanActorBindingOutcome.Unavailable);
            }

            AuthoritativeStreamReadResult read = await reader.ReadAsync(new(query.TenantId, "party", query.PartyId), cancellationToken).ConfigureAwait(false);
            if (!read.IsAuthoritative || !MatchesSource(envelope, read.Stream))
            {
                return HistoryFailure(query, HumanActorBindingOutcome.Unavailable);
            }

            if (query.ActionAt > read.Stream!.ObservedAt)
            {
                return HistoryFailure(query, HumanActorBindingOutcome.Unavailable);
            }

            PartyState state = PartyIdentitySourceFold.Fold(read.Stream!);
            HumanActorBindingEvidence[] matches = [.. state.HumanActorBindings.Select(item => item.Evidence)
                .Where(item => item.ValidFrom <= query.ActionAt && (item.ValidUntil is null || query.ActionAt < item.ValidUntil))];
            if (matches.Length != 1)
            {
                return HistoryFailure(query, matches.Length == 0 ? HumanActorBindingOutcome.Gap : HumanActorBindingOutcome.Ambiguous);
            }

            HumanActorBindingEvidence evidence = matches[0];
            if (evidence.ActorId != query.ExpectedActorId || evidence.BindingVersion != query.ExpectedBindingVersion)
            {
                return HistoryFailure(query, HumanActorBindingOutcome.Mismatch);
            }

            if (timeProvider.GetUtcNow() >= evidence.Custody.ExpiresAt
                || !await custody.CanReadAsync(read.Stream!.Identity, evidence.Custody, cancellationToken).ConfigureAwait(false))
            {
                return HistoryFailure(query, HumanActorBindingOutcome.Expired);
            }

            return new(HumanActorBindingOutcome.Resolved, 1, query.TenantId, query.PartyId, query.ActionAt,
                read.Stream.Head, read.Stream.ObservationId, evidence);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return HistoryFailure(query, HumanActorBindingOutcome.Unavailable);
        }
    }

    private static HumanActorBindingResult HistoryFailure(ResolveHumanActorBindingAt? query, HumanActorBindingOutcome outcome)
        => new(outcome, 1, query?.TenantId ?? string.Empty, query?.PartyId ?? string.Empty,
            query?.ActionAt ?? default, 0, null, null);

    private static bool MatchesSource(QueryEnvelope envelope, AuthoritativeEventStream? stream)
        => stream is not null && stream.Identity.TenantId == envelope.TenantId
            && stream.Identity.Domain == envelope.Domain && stream.Identity.AggregateId == envelope.AggregateId;

    private static bool Matches(QueryEnvelope envelope, string tenantId, string partyId)
        => envelope.Domain == "party" && envelope.TenantId == tenantId && envelope.AggregateId == partyId
            && (envelope.EntityId is null || envelope.EntityId == partyId)
            && new AggregateIdentity(tenantId, "party", partyId).TenantId == tenantId;
}
