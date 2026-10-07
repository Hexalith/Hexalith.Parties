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
using Microsoft.Extensions.Options;

namespace Hexalith.Parties.Queries;

/// <summary>Strict current and action-time identity reads, with no stale fallback or profile result.</summary>
public sealed class PartyIdentityQueryService(IPartyIdentityAuthority authority, TimeProvider timeProvider,
    IAuthoritativeEventStreamReader? reader = null, IIdentityHistoryCustody? custody = null,
    IRetainedIdentityHistoryReader? historyReader = null, IOptionsMonitor<PartyIdentityOptions>? identityOptions = null)
{
    /// <summary>Resolves current classification and eligibility from complete source and actor evidence.</summary>
    public async Task<PartyIdentityResult> ResolveAsync(QueryEnvelope envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            IdentityHistoryPolicy? policy = identityOptions?.CurrentValue.Policy;
            ResolvePartyIdentity? query = JsonSerializer.Deserialize<ResolvePartyIdentity>(envelope.Payload, PartiesJsonOptions.Default);
            if (query is null || !Matches(envelope, query.TenantId, query.PartyId))
            {
                cancellationToken.ThrowIfCancellationRequested();
                return new(PartyIdentityOutcome.Unavailable, null);
            }

            IdentityAdmissionEvidence? admitted = authority.Admit(envelope).Evidence;
            if (admitted is null || reader is null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return new(PartyIdentityOutcome.Unavailable, null);
            }

            cancellationToken.ThrowIfCancellationRequested();
            AuthoritativeStreamReadResult read = await reader.ReadAsync(new(query.TenantId, "party", query.PartyId), cancellationToken).WaitAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (!read.IsAuthoritative || !MatchesSource(envelope, read.Stream)
                || read.Stream!.ObservedAt > timeProvider.GetUtcNow())
            {
                cancellationToken.ThrowIfCancellationRequested();
                return new(PartyIdentityOutcome.Unavailable, null);
            }

            PartyState state = PartyIdentitySourceFold.Fold(read.Stream!);
            if (!state.HasBeenCreated)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return new(PartyIdentityOutcome.Unavailable, null);
            }

            PartyIdentityClassification classification = state.Type == PartyType.Person ? PartyIdentityClassification.Human
                : state.Type == PartyType.Organization ? PartyIdentityClassification.Organization
                : PartyIdentityClassification.Unknown;
            bool eligible = state.IsActive && !state.IsRestricted && state.ErasureStatus == ErasureStatus.Active;
            bool requiresHumanPolicy = classification == PartyIdentityClassification.Human && eligible;
            HumanActorBindingEvidence? binding = null;
            if (requiresHumanPolicy)
            {
                if (!PolicyIsCurrent(policy))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return new(PartyIdentityOutcome.Unavailable, null);
                }

                DateTimeOffset now = timeProvider.GetUtcNow();
                HumanActorBindingEvidence[] candidates = [.. state.HumanActorBindings.Select(item => item.Evidence)
                    .Where(item => item.ValidFrom <= now && (item.ValidUntil is null || now < item.ValidUntil))];
                if (candidates.Length == 1 && candidates[0].ActorId == query.ExpectedActorId
                    && admitted.TargetActorId == candidates[0].ActorId && admitted.ActorActive
                    && admitted.ActorRevision >= candidates[0].ActorRevision && custody is not null)
                {
                    if (!candidates[0].Custody.Satisfies(policy!, candidates[0].ValidFrom))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        return new(PartyIdentityOutcome.Unavailable, null);
                    }

                    bool canRead = await custody.CanReadAsync(read.Stream!.Identity, candidates[0].Custody, cancellationToken)
                        .WaitAsync(cancellationToken).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!PolicyIsCurrent(policy))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        return new(PartyIdentityOutcome.Unavailable, null);
                    }

                    if (canRead)
                    {
                        binding = candidates[0];
                    }
                }

                eligible &= binding is not null;
            }

            IdentityAdmissionEvidence? currentAuthority = authority.Admit(envelope).Evidence;
            cancellationToken.ThrowIfCancellationRequested();
            DateTimeOffset completedAt = timeProvider.GetUtcNow();
            if (completedAt < read.Stream!.ObservedAt || !SameAuthority(admitted, currentAuthority, completedAt)
                || requiresHumanPolicy && !PolicyIsCurrent(policy)
                || binding is not null && (!currentAuthority!.ActorActive || completedAt < binding.ValidFrom || completedAt >= binding.ValidUntil
                    || completedAt >= binding.Custody.ExpiresAt))
            {
                cancellationToken.ThrowIfCancellationRequested();
                return new(PartyIdentityOutcome.Unavailable, null);
            }

            AuthoritativeEventStream source = read.Stream!;
            var evidence = new PartyIdentityEvidence(1, query.TenantId, query.PartyId, classification,
                state.IsActive, state.IsRestricted, state.ErasureStatus != ErasureStatus.Active,
                source.Head, source.ObservedAt, source.ObservationId, binding);
            cancellationToken.ThrowIfCancellationRequested();
            return new(eligible && classification != PartyIdentityClassification.Unknown
                ? PartyIdentityOutcome.Resolved : PartyIdentityOutcome.Ineligible, evidence);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new(PartyIdentityOutcome.Unavailable, null);
        }
    }

    /// <summary>Resolves the unique recorded interval without substituting current actor or binding state.</summary>
    public async Task<HumanActorBindingResult> ResolveAtAsync(QueryEnvelope envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        cancellationToken.ThrowIfCancellationRequested();
        ResolveHumanActorBindingAt? query = null;
        try
        {
            IdentityHistoryPolicy? policy = identityOptions?.CurrentValue.Policy;
            query = JsonSerializer.Deserialize<ResolveHumanActorBindingAt>(envelope.Payload, PartiesJsonOptions.Default);
            if (query is null || !Matches(envelope, query.TenantId, query.PartyId)
                || string.IsNullOrWhiteSpace(query.ExpectedActorId) || query.ExpectedBindingVersion <= 0
                || authority.Admit(envelope).Evidence is not { } admitted
                || admitted.TargetActorId != query.ExpectedActorId || historyReader is null || custody is null
                || !PolicyIsCurrent(policy))
            {
                cancellationToken.ThrowIfCancellationRequested();
                return HistoryFailure(query, HumanActorBindingOutcome.Unavailable);
            }

            cancellationToken.ThrowIfCancellationRequested();
            RetainedIdentityHistoryReadResult read = await historyReader.ReadAsync(new(query.TenantId, "party", query.PartyId),
                RetainedIdentityHistoryReadRequest.AttributionPurpose, cancellationToken).WaitAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (!PolicyIsCurrent(policy) || !read.IsAuthoritative || read.Stream is not { } retainedSource
                || retainedSource.Identity != new AggregateIdentity(query.TenantId, "party", query.PartyId)
                || !RetainedIdentityHistoryValidator.IsComplete(new(retainedSource.Identity,
                    RetainedIdentityHistoryReadRequest.AttributionPurpose), retainedSource, timeProvider.GetUtcNow()))
            {
                cancellationToken.ThrowIfCancellationRequested();
                return HistoryFailure(query, HumanActorBindingOutcome.Unavailable);
            }

            if (query.ActionAt > read.Stream!.ObservedAt || read.Stream.ObservedAt > timeProvider.GetUtcNow())
            {
                cancellationToken.ThrowIfCancellationRequested();
                return HistoryFailure(query, HumanActorBindingOutcome.Unavailable);
            }

            IReadOnlyList<RetainedHumanActorBinding> history = RetainedHumanActorHistoryFold.Fold(read.Stream!);
            if (!SameAuthority(admitted, authority.Admit(envelope).Evidence, timeProvider.GetUtcNow()) || !PolicyIsCurrent(policy))
            {
                cancellationToken.ThrowIfCancellationRequested();
                return HistoryFailure(query, HumanActorBindingOutcome.Unavailable);
            }

            RetainedHumanActorBinding[] matches = [.. history
                .Where(item => item.Evidence.ValidFrom <= query.ActionAt && (item.Evidence.ValidUntil is null || query.ActionAt < item.Evidence.ValidUntil))];
            if (matches.Length != 1)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return HistoryFailure(query, matches.Length == 0 ? HumanActorBindingOutcome.Gap : HumanActorBindingOutcome.Ambiguous);
            }

            HumanActorBindingEvidence evidence = matches[0].Evidence;
            if (!evidence.Custody.Satisfies(policy!, evidence.ValidFrom))
            {
                cancellationToken.ThrowIfCancellationRequested();
                return HistoryFailure(query, HumanActorBindingOutcome.Unavailable);
            }

            if (evidence.ActorId != query.ExpectedActorId || evidence.BindingVersion != query.ExpectedBindingVersion)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return HistoryFailure(query, HumanActorBindingOutcome.Mismatch);
            }

            if (timeProvider.GetUtcNow() >= evidence.Custody.ExpiresAt)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return HistoryFailure(query, HumanActorBindingOutcome.Expired);
            }

            bool canRead = await custody.CanReadAsync(read.Stream!.Identity, evidence.Custody, cancellationToken).WaitAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            DateTimeOffset completedAt = timeProvider.GetUtcNow();
            if (!PolicyIsCurrent(policy))
            {
                cancellationToken.ThrowIfCancellationRequested();
                return HistoryFailure(query, HumanActorBindingOutcome.Unavailable);
            }

            if (!canRead || completedAt >= evidence.Custody.ExpiresAt)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return HistoryFailure(query, HumanActorBindingOutcome.Expired);
            }

            IdentityAdmissionEvidence? currentAuthority = authority.Admit(envelope).Evidence;
            completedAt = timeProvider.GetUtcNow();
            if (!PolicyIsCurrent(policy))
            {
                cancellationToken.ThrowIfCancellationRequested();
                return HistoryFailure(query, HumanActorBindingOutcome.Unavailable);
            }

            if (completedAt >= evidence.Custody.ExpiresAt)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return HistoryFailure(query, HumanActorBindingOutcome.Expired);
            }

            if (!SameAuthority(admitted, currentAuthority, completedAt)
                || !RetainedIdentityHistoryValidator.IsComplete(new(retainedSource.Identity,
                    RetainedIdentityHistoryReadRequest.AttributionPurpose), retainedSource, completedAt))
            {
                cancellationToken.ThrowIfCancellationRequested();
                return HistoryFailure(query, HumanActorBindingOutcome.Unavailable);
            }

            cancellationToken.ThrowIfCancellationRequested();
            return new(HumanActorBindingOutcome.Resolved, 1, query.TenantId, query.PartyId, query.ActionAt,
                read.Stream.Head, read.Stream.ObservationId, evidence) { BindingSourcePosition = matches[0].SourcePosition };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return HistoryFailure(query, HumanActorBindingOutcome.Unavailable);
        }
    }

    private bool PolicyIsCurrent(IdentityHistoryPolicy? policy)
        => policy is { IsValid: true } && identityOptions?.CurrentValue.Policy == policy;

    private static HumanActorBindingResult HistoryFailure(ResolveHumanActorBindingAt? query, HumanActorBindingOutcome outcome)
        => new(outcome, 1, query?.TenantId ?? string.Empty, query?.PartyId ?? string.Empty,
            query?.ActionAt ?? default, 0, null, null);

    private static bool SameAuthority(IdentityAdmissionEvidence original, IdentityAdmissionEvidence? current, DateTimeOffset now)
        => current is not null && current.Scope == original.Scope && current.SourceId == original.SourceId
            && current.TargetActorId == original.TargetActorId && current.ActorRevision == original.ActorRevision
            && current.AuthorityRevision == original.AuthorityRevision && current.IssuedAt <= now && current.ExpiresAt > now;

    private static bool MatchesSource(QueryEnvelope envelope, AuthoritativeEventStream? stream)
        => stream is not null && stream.Identity.TenantId == envelope.TenantId
            && stream.Identity.Domain == envelope.Domain && stream.Identity.AggregateId == envelope.AggregateId;

    private static bool Matches(QueryEnvelope envelope, string tenantId, string partyId)
        => envelope.Domain == "party" && envelope.TenantId == tenantId && envelope.AggregateId == partyId
            && (envelope.EntityId is null || envelope.EntityId == partyId)
            && new AggregateIdentity(tenantId, "party", partyId).TenantId == tenantId;
}
